using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Json;
using UserRoles.Data;
using UserRoles.Hubs;
using UserRoles.Models;
using UserRoles.Models.Enums;
using UserRoles.ViewModels;

using TaskStatusEnum = UserRoles.Models.Enums.TaskStatus;

namespace UserRoles.Services
{
    public class TaskService : ITaskService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly ITaskHistoryService _historyService;
        private readonly IHubContext<TaskHub> _hubContext;
        private readonly ITaskPermissionService _permissions;
        private readonly IImageProcessingService _imageService;
        private readonly ILogger<TaskService> _logger;

        public TaskService(
            AppDbContext context,
            UserManager<Users> userManager,
            ITaskHistoryService historyService,
            IHubContext<TaskHub> hubContext,
            ITaskPermissionService permissions,
            IImageProcessingService imageService,
            ILogger<TaskService> logger)
        {
            _context = context;
            _userManager = userManager;
            _historyService = historyService;
            _hubContext = hubContext;
            _permissions = permissions;
            _imageService = imageService;
            _logger = logger;
        }

        // ════════════════════════════════════════════════════════
        //  CREATE
        // ════════════════════════════════════════════════════════
        public async Task<ServiceResult<TaskItem>> CreateTaskAsync(CreateTaskViewModel model, string userId)
        {
            if (model == null) return ServiceResult<TaskItem>.Fail("Invalid payload");
            if (model.ColumnId <= 0) return ServiceResult<TaskItem>.Fail("ColumnId is required");

            var column = await _context.TeamColumns.FirstOrDefaultAsync(c => c.Id == model.ColumnId);
            if (column == null) return ServiceResult<TaskItem>.Fail("Column not found");

            // Generate WorkItemId if project is selected
            string? workItemId = null;
            if (model.ProjectId.HasValue && model.ProjectId.Value > 0)
            {
                var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == model.ProjectId.Value);
                if (project != null)
                {
                    int taskCount = await _context.TaskItems.CountAsync(t => t.ProjectId == model.ProjectId.Value);
                    workItemId = $"P{project.Id}T{taskCount + 1}";
                }
            }

            var task = new TaskItem
            {
                Title = model.Title?.Trim() ?? string.Empty,
                Description = model.Description?.Trim() ?? string.Empty,
                ColumnId = column.Id,
                TeamName = column.TeamName,
                ProjectId = model.ProjectId,
                WorkItemId = workItemId,
                Priority = model.Priority,
                Status = TaskStatusEnum.ToDo,
                CreatedByUserId = userId,
                AssignedToUserId = string.IsNullOrEmpty(model.AssignedToUserId) ? userId : model.AssignedToUserId,
                AssignedByUserId = userId,
                AssignedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CurrentColumnEntryAt = DateTime.UtcNow,
                DueDate = model.DueDate.HasValue
                    ? DateTime.SpecifyKind(model.DueDate.Value, DateTimeKind.Utc)
                    : null
            };

            _context.TaskItems.Add(task);
            await _context.SaveChangesAsync();

            await _historyService.LogTaskCreated(task.Id, userId);
            await _context.SaveChangesAsync();

            // Save custom field values
            if (model.CustomFieldValues != null && model.CustomFieldValues.Any())
            {
                var cfResult = await SaveCustomFieldValuesAsync(task.Id, model.CustomFieldValues);
                if (!cfResult.Success) return ServiceResult<TaskItem>.Fail(cfResult.Message!, cfResult.StatusCode);
            }

            // SignalR notification
            await BroadcastSafe(column.TeamName, "TaskAdded", new { taskId = task.Id, columnId = column.Id, teamName = column.TeamName });

            return ServiceResult<TaskItem>.Ok(task, workItemId);
        }

        // ════════════════════════════════════════════════════════
        //  UPDATE
        // ════════════════════════════════════════════════════════
        public async Task<ServiceResult> UpdateTaskAsync(UpdateTaskRequest model, ClaimsPrincipal principal, string userId)
        {
            if (model == null || model.TaskId <= 0) return ServiceResult.Fail("Invalid request");

            var task = await _context.TaskItems.FindAsync(model.TaskId);
            if (task == null) return ServiceResult.Fail("Task not found", 404);

            // Permission check
            if (!principal.IsInRole("Admin") && task.AssignedToUserId != userId)
            {
                if (!await _permissions.AuthorizeBoardAction(principal, task.TeamName, "EditAllFields"))
                    return ServiceResult.Fail("Forbidden", 403);
            }

            // Track changes
            if (task.Title != model.Title?.Trim())
            {
                await _historyService.LogTaskUpdated(task.Id, userId, "Title", task.Title, model.Title?.Trim() ?? string.Empty);
                task.Title = model.Title?.Trim() ?? string.Empty;
            }

            if (task.Description != model.Description?.Trim())
            {
                await _historyService.LogTaskUpdated(task.Id, userId, "Description", "Old Description", "New Description");
                task.Description = model.Description?.Trim() ?? string.Empty;
            }

            task.UpdatedAt = DateTime.UtcNow;

            if (task.DueDate != model.DueDate)
            {
                await _historyService.LogTaskUpdated(task.Id, userId, "Due Date",
                    task.DueDate?.ToString("dd MMM yyyy, hh:mm tt") ?? "None",
                    model.DueDate?.ToString("dd MMM yyyy, hh:mm tt") ?? "None");
                task.DueDate = model.DueDate.HasValue
                    ? DateTime.SpecifyKind(model.DueDate.Value, DateTimeKind.Utc)
                    : null;
            }

            if (model.Priority.HasValue && task.Priority != model.Priority.Value)
            {
                await _historyService.LogPriorityChange(task.Id, task.Priority, model.Priority.Value, userId);
                task.Priority = model.Priority.Value;
            }

            if (!string.IsNullOrEmpty(model.AssignedToUserId))
            {
                if (task.AssignedToUserId != model.AssignedToUserId)
                {
                    if (principal.IsInRole("Admin") || await _permissions.AuthorizeBoardAction(principal, task.TeamName, "AssignTask"))
                    {
                        await _historyService.LogAssignment(task.Id, model.AssignedToUserId, userId);
                        task.AssignedToUserId = model.AssignedToUserId;
                    }
                }
            }

            // Custom field values
            if (model.CustomFieldValues != null)
            {
                var cfResult = await UpdateCustomFieldValuesAsync(task, model.CustomFieldValues, userId);
                if (!cfResult.Success) return cfResult;
            }

            await _context.SaveChangesAsync();

            // SignalR
            await BroadcastSafe(task.TeamName, "TaskUpdated", new
            {
                taskId = task.Id
            });

            return ServiceResult.Ok();
        }

        // ════════════════════════════════════════════════════════
        //  DELETE
        // ════════════════════════════════════════════════════════
        public async Task<ServiceResult> DeleteTaskAsync(int taskId, ClaimsPrincipal principal)
        {
            var task = await _context.TaskItems.FindAsync(taskId);
            if (task == null) return ServiceResult.Fail("Task not found", 404);

            if (!await _permissions.AuthorizeBoardAction(principal, task.TeamName, "DeleteTask"))
                return ServiceResult.Fail("Forbidden", 403);

            _context.TaskItems.Remove(task);
            await _context.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        // ════════════════════════════════════════════════════════
        //  GET
        // ════════════════════════════════════════════════════════
        public async Task<TaskItem?> GetTaskByIdAsync(int taskId, bool includeRelated = false)
        {
            var query = _context.TaskItems.AsNoTracking();

            if (includeRelated)
            {
                query = query
                    .Include(t => t.CreatedByUser)
                    .Include(t => t.AssignedToUser)
                    .Include(t => t.AssignedByUser)
                    .Include(t => t.ReviewedByUser)
                    .Include(t => t.CompletedByUser)
                    .Include(t => t.Column)
                    .Include(t => t.CustomFieldValues)
                        .ThenInclude(v => v.Field);
            }
            else
            {
                query = query.Include(t => t.CustomFieldValues);
            }

            return await query.FirstOrDefaultAsync(t => t.Id == taskId);
        }

        // ════════════════════════════════════════════════════════
        //  ASSIGN
        // ════════════════════════════════════════════════════════
        public async Task<ServiceResult<AssignResult>> AssignTaskAsync(int taskId, string userId, ClaimsPrincipal principal, string currentUserId)
        {
            if (string.IsNullOrEmpty(userId)) return ServiceResult<AssignResult>.Fail("UserId is required");
            if (userId.StartsWith("user:")) userId = userId.Substring(5);

            var taskToUpdate = await _context.TaskItems.FindAsync(taskId);
            if (taskToUpdate == null) return ServiceResult<AssignResult>.Fail("Task not found", 404);

            if (!principal.IsInRole("Admin") && !await _permissions.AuthorizeBoardAction(principal, taskToUpdate.TeamName, "AssignTask"))
                return ServiceResult<AssignResult>.Fail("Forbidden", 403);

            var assignToUser = await _userManager.FindByIdAsync(userId);
            if (assignToUser == null) return ServiceResult<AssignResult>.Fail("Invalid user");

            if (taskToUpdate.AssignedToUserId == assignToUser.Id)
            {
                return ServiceResult<AssignResult>.Fail($"The task is already assigned to {assignToUser.Name ?? assignToUser.UserName}.");
            }

            var currentUser = await _userManager.FindByIdAsync(currentUserId);
            if (currentUser == null) return ServiceResult<AssignResult>.Fail("Unauthorized", 401);

            var oldTeam = taskToUpdate.TeamName;
            bool teamChanged = false;

            // Check cross-team assignment
            var targetUserTeam = await _context.UserTeams.Where(ut => ut.UserId == userId).FirstOrDefaultAsync();
            if (targetUserTeam != null && targetUserTeam.TeamName != taskToUpdate.TeamName)
            {
                var targetColumn = await GetFirstColumnForTeam(targetUserTeam.TeamName);
                if (targetColumn != null)
                {
                    taskToUpdate.TeamName = targetUserTeam.TeamName;
                    taskToUpdate.ColumnId = targetColumn.Id;
                    teamChanged = true;
                }
            }

            await _historyService.LogAssignment(taskToUpdate.Id, assignToUser.Id, currentUser.Id);

            taskToUpdate.AssignedToUserId = assignToUser.Id;
            taskToUpdate.AssignedByUserId = currentUser.Id;
            taskToUpdate.AssignedAt = DateTime.UtcNow;
            taskToUpdate.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // SignalR
            if (teamChanged)
            {
                await BroadcastSafe(oldTeam, "TaskRemoved", new { taskId, teamName = oldTeam });
                await BroadcastSafe(taskToUpdate.TeamName, "TaskAdded", new { taskId, teamName = taskToUpdate.TeamName, columnId = taskToUpdate.ColumnId });
            }
            else
            {
                var assigneeTeam = targetUserTeam?.TeamName ?? (await _context.UserTeams.FirstOrDefaultAsync(ut => ut.UserId == userId))?.TeamName ?? "N/A";
                var assigneeRole = await GetPrimaryRole(assignToUser);

                await BroadcastSafe(taskToUpdate.TeamName, "TaskAssigned", new
                {
                    taskId,
                    assignedTo = assignToUser.Name ?? assignToUser.UserName,
                    assignedBy = currentUser.Email,
                    assignedAt = taskToUpdate.AssignedAt!.Value.ToString("dd MMM yyyy, hh:mm tt"),
                    team = assigneeTeam,
                    role = assigneeRole
                });
            }

            return ServiceResult<AssignResult>.Ok(new AssignResult
            {
                AssignedTo = assignToUser.UserName,
                AssignedBy = currentUser.UserName,
                AssignedAt = taskToUpdate.AssignedAt!.Value.ToString("dd MMM yyyy, hh:mm tt"),
                TeamMoved = teamChanged,
                NewTeam = taskToUpdate.TeamName
            });
        }

        // ════════════════════════════════════════════════════════
        //  BULK ASSIGN
        // ════════════════════════════════════════════════════════
        public async Task<ServiceResult<int>> BulkAssignTasksAsync(List<int> taskIds, string userId, ClaimsPrincipal principal, string currentUserId)
        {
            var assignToUser = await _userManager.FindByIdAsync(userId);
            if (assignToUser == null) return ServiceResult<int>.Fail("User not found");

            var currentUser = await _userManager.FindByIdAsync(currentUserId);
            if (currentUser == null) return ServiceResult<int>.Fail("Unauthorized", 401);

            var tasks = await _context.TaskItems.Where(t => taskIds.Contains(t.Id)).ToListAsync();
            if (!tasks.Any()) return ServiceResult<int>.Fail("No tasks found", 404);

            // Pre-fetch assignee info once (avoid N+1)
            var assigneeTeam = (await _context.UserTeams.FirstOrDefaultAsync(ut => ut.UserId == userId))?.TeamName ?? "N/A";
            var assigneeRole = await GetPrimaryRole(assignToUser);

            int updatedCount = 0;
            foreach (var task in tasks)
            {
                if (!principal.IsInRole("Admin") && !await _permissions.AuthorizeBoardAction(principal, task.TeamName, "AssignTask"))
                    continue;

                if (task.AssignedToUserId == assignToUser.Id)
                    continue;

                task.AssignedToUserId = assignToUser.Id;
                task.AssignedByUserId = currentUser.Id;
                task.AssignedAt = DateTime.UtcNow;
                task.UpdatedAt = DateTime.UtcNow;

                await _historyService.LogAssignment(task.Id, assignToUser.Id, currentUser.Id);
                updatedCount++;

                await BroadcastSafe(task.TeamName, "TaskAssigned", new
                {
                    taskId = task.Id,
                    assignedTo = assignToUser.Name ?? assignToUser.UserName,
                    assignedBy = currentUser.Email,
                    assignedAt = task.AssignedAt!.Value.ToString("dd MMM yyyy, hh:mm tt"),
                    team = assigneeTeam,
                    role = assigneeRole
                });
            }

            await _context.SaveChangesAsync();
            return ServiceResult<int>.Ok(updatedCount);
        }

        // ════════════════════════════════════════════════════════
        //  MOVE
        // ════════════════════════════════════════════════════════
        public async Task<ServiceResult> MoveTaskAsync(int taskId, int columnId, ClaimsPrincipal principal, string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return ServiceResult.Fail("Unauthorized", 401);

            var task = await _context.TaskItems.Include(t => t.Column).FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null) return ServiceResult.Fail("Task not found", 404);

            var targetColumn = await _context.TeamColumns.FirstOrDefaultAsync(c => c.Id == columnId);
            if (targetColumn == null) return ServiceResult.Fail("Target column not found", 404);

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var targetColName = targetColumn.ColumnName?.Trim().ToLower();
            var sourceColName = task.Column?.ColumnName?.Trim().ToLower();
            int oldColId = task.ColumnId;

            // Permission check
            if (!await CanUserMoveTaskInternal(task, user, targetColumn.Id))
                return ServiceResult.Fail("You do not have access to move this task", 403);

            // Completed column restrictions
            if (targetColName == "completed")
            {
                if (!isAdmin && !await _permissions.AuthorizeBoardAction(principal, task.TeamName, "ReviewTask"))
                    return ServiceResult.Fail("Only Admin or authorized reviewers can move tasks to Completed.", 403);
                if (task.ReviewStatus != ReviewStatus.Passed)
                    return ServiceResult.Fail("Task must pass review before moving to Completed.", 400);
            }

            // Review column rework logic
            if (sourceColName == "review" && targetColName != "completed" && !isAdmin)
            {
                // Non-admin can only move back if review was failed or none
            }

            // Apply move
            bool wasArchived = task.IsArchived;
            task.PreviousColumnId = task.ColumnId;
            task.ColumnId = targetColumn.Id;

            task.Status = targetColName switch
            {
                "todo" => TaskStatusEnum.ToDo,
                "doing" => TaskStatusEnum.Doing,
                "review" => TaskStatusEnum.Review,
                "completed" => TaskStatusEnum.Complete,
                _ => task.Status
            };

            // DEFERRED Auto-Archive: Stay in Completed if moved today
            if (targetColName == "completed")
            {
                task.ReviewStatus = ReviewStatus.Passed;
                task.IsArchived = false; 
                task.CompletedByUserId = task.AssignedToUserId;
                task.CompletedAt = DateTime.UtcNow;
                // No LogArchivedToHistory here, it happens during actual archive
            }
            else
            {
                task.IsArchived = false;
                task.ArchivedAt = null;
            }

            if (targetColName == "review")
            {
                task.ReviewStatus = ReviewStatus.Pending;
                task.ReviewNote = null;
                await _historyService.LogReviewSubmitted(task.Id, userId);
            }

            task.UpdatedAt = DateTime.UtcNow;
            task.CurrentColumnEntryAt = DateTime.UtcNow;

            _context.TaskItems.Update(task);
            await _context.SaveChangesAsync();

            await _historyService.LogColumnMove(task.Id, oldColId, targetColumn.Id, user.Id);

            if (wasArchived && !task.IsArchived)
            {
                // Restored from history: must broadcast as Added because it didn't exist in active UI
                await BroadcastSafe(task.TeamName, "TaskAdded", new
                {
                    taskId = task.Id,
                    teamName = task.TeamName,
                    columnId = targetColumn.Id
                });
            }
            else
            {
                await BroadcastSafe(task.TeamName, "TaskMoved", new
                {
                    taskId = task.Id,
                    newColumnId = targetColumn.Id,
                    oldColumnId = oldColId,
                    movedBy = user.UserName,
                    columnName = targetColumn.ColumnName
                });
            }

            // Also broadcast directly to the person who assigned this task, if they are not in the team group
            if (!string.IsNullOrEmpty(task.AssignedByUserId))
            {
                try
                {
                    await _hubContext.Clients.User(task.AssignedByUserId).SendAsync("TaskMoved", new
                    {
                        taskId = task.Id,
                        newColumnId = targetColumn.Id,
                        oldColumnId = oldColId,
                        movedBy = user.UserName,
                        columnName = targetColumn.ColumnName
                    });
                }
                catch { /* Ignore SignalR broadcast error */ }
            }

            return ServiceResult.Ok("Task moved successfully");
        }

        // ════════════════════════════════════════════════════════
        //  TEAM BOARD
        // ════════════════════════════════════════════════════════
        public async Task<TeamBoardViewModel> BuildTeamBoardAsync(string team, string userId, IList<string> viewerRoles)
        {
            // ✅ Cleanup: Auto-Archive tasks from previous days that are still in Completed
            await AutoArchiveOldCompletedTasksAsync(team, userId);

            // Load columns with enforced order
            var columnsRaw = await _context.TeamColumns
                .Where(c => c.TeamName == team)
                .OrderBy(c => c.Order)
                .ToListAsync();

            var columns = columnsRaw.OrderBy(c =>
            {
                var name = c.ColumnName?.Trim().ToLower();
                if (name == "review") return 1000;
                if (name == "completed") return 1001;
                return c.Order;
            }).ToList();

            // Load tasks (AsNoTracking for performance)
            var allTasks = await _context.TaskItems
                .AsNoTracking()
                .Where(t => t.TeamName == team && !t.IsArchived)
                .Include(t => t.CreatedByUser)
                .Include(t => t.AssignedToUser)
                .Include(t => t.AssignedByUser)
                .Include(t => t.ReviewedByUser)
                .Include(t => t.CompletedByUser)
                .Include(t => t.Column)
                .Include(t => t.CustomFieldValues)
                    .ThenInclude(v => v.Field)
                .ToListAsync();

            // Bulk-fetch roles
            var userRolesMap = await GetUserRolesMapAsync();

            // Bulk-fetch teams
            var userTeamsMap = await GetUserTeamsMapAsync();

            // Pre-fetch hierarchy
            var hierarchyMap = await _userManager.Users
                .AsNoTracking()
                .Select(u => new { u.Id, u.ManagerId })
                .ToDictionaryAsync(u => u.Id, u => u.ManagerId);

            // Filter visible tasks
            var visibleTasks = allTasks
                .Where(t => CanUserSeeTask(t, userId, viewerRoles, userRolesMap, hierarchyMap))
                .ToList();

            foreach (var col in columns)
            {
                col.Tasks = visibleTasks.Where(t => t.ColumnId == col.Id).ToList();
            }

            // Build assignable users
            var allUsers = await _userManager.Users.AsNoTracking().OrderBy(u => u.Name).ToListAsync();

            var teamUserIds = await _context.UserTeams
                .AsNoTracking()
                .Where(ut => ut.TeamName == team)
                .Select(ut => ut.UserId)
                .ToListAsync();

            var feUserRolesMap = BuildFeUserRolesMap(allUsers, userRolesMap);
            var filteredAssignees = BuildFilteredAssignees(allUsers, teamUserIds, userId, viewerRoles, feUserRolesMap);

            // Build grouped user list
            var (teamGroups, managementUsers) = await BuildTeamGroupsAsync(allUsers, feUserRolesMap, team, userId, viewerRoles);

            // Custom fields
            var customFields = await _context.TaskCustomFields
                .AsNoTracking()
                .Where(f => f.IsActive && f.TeamName == team)
                .OrderBy(f => f.Order)
                .ToListAsync();

            var teamSettings = await _context.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Name == team);

            var userPerms = await _context.BoardPermissions
                .AsNoTracking()
                .Where(p => p.UserId == userId && p.TeamName != null && EF.Functions.ILike(p.TeamName, team.Trim()))
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            var colPerms = await _context.ColumnPermissions
                .Include(p => p.Column)
                .Where(p => p.UserId == userId && p.Column != null && p.Column.TeamName != null && EF.Functions.ILike(p.Column.TeamName, team.Trim()))
                .ToListAsync();

            return new TeamBoardViewModel
            {
                TeamName = team,
                Columns = columns,
                AllTeamNames = await _context.TeamColumns.AsNoTracking().Select(c => c.TeamName).Distinct().ToListAsync(),
                CustomFields = customFields,
                UserPermissions = userPerms,
                ColumnPermissions = colPerms,
                TeamSettings = teamSettings,
                UserRolesMap = feUserRolesMap,
                UserTeamsMap = userTeamsMap,
                TeamGroups = teamGroups,
                ManagementUsers = managementUsers
            };
        }

        public async Task AutoArchiveOldCompletedTasksAsync(string team, string userId)
        {
            try
            {
                // We use local Indian time for "Today" as per project context if possible, 
                // but for now let's use Utc + 5.5 or just Date comparison.
                var today = DateTime.UtcNow.AddHours(5.5).Date;

                var staleTasks = await _context.TaskItems
                    .Where(t => t.TeamName == team
                             && !t.IsArchived
                             && t.ReviewStatus == ReviewStatus.Passed
                             && t.CompletedAt != null
                             && t.CompletedAt.Value.AddHours(5.5).Date < today)
                    .ToListAsync();

                if (!staleTasks.Any()) return;

                foreach (var task in staleTasks)
                {
                    task.IsArchived = true;
                    task.ArchivedAt = DateTime.UtcNow;
                    await _historyService.LogArchivedToHistory(task.Id, userId);
                }

                await _context.SaveChangesAsync();

                // Broadcast to update UIs real-time
                foreach (var task in staleTasks)
                {
                    await BroadcastSafe(team, "TaskArchived", new { taskId = task.Id });
                }
            }
            catch (Exception ex)
            {
                // Silently fail to not block board loading
                Console.WriteLine($"Auto-Archive Error: {ex.Message}");
            }
        }

        public async Task<TeamBoardViewModel> GetQuickAssignModelAsync(string userId, IList<string> viewerRoles)
        {
            var allUsers = await _userManager.Users.AsNoTracking().OrderBy(u => u.Name).ToListAsync();
            var userRolesMap = await GetUserRolesMapAsync();
            var feUserRolesMap = BuildFeUserRolesMap(allUsers, userRolesMap);

            // Use an empty team name or "Global" for initial grouping
            var (teamGroups, managementUsers) = await BuildTeamGroupsAsync(allUsers, feUserRolesMap, string.Empty, userId, viewerRoles);

            return new TeamBoardViewModel
            {
                ManagementUsers = managementUsers,
                TeamGroups = teamGroups,
                UserRolesMap = feUserRolesMap
            };
        }

        // ════════════════════════════════════════════════════════
        //  VISIBILITY
        // ════════════════════════════════════════════════════════
        public bool CanUserSeeTask(TaskItem task, string userId, IList<string> viewerRoles,
            Dictionary<string, IList<string>> userRolesMap, Dictionary<string, string?> hierarchyMap)
        {
            if (viewerRoles.Contains("Admin")) return true;
            if (task.CreatedByUserId == userId || task.AssignedToUserId == userId || task.AssignedByUserId == userId)
                return true;

            bool isManagerOrSub = viewerRoles.Contains("Manager") || viewerRoles.Contains("Sub-Manager");
            if (isManagerOrSub)
            {
                if (IsUnderManager(task.CreatedByUserId, userId, hierarchyMap) ||
                    IsUnderManager(task.AssignedToUserId, userId, hierarchyMap))
                    return true;
            }

            return false;
        }

        // ════════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ════════════════════════════════════════════════════════

        private bool IsUnderManager(string? userId, string managerId, Dictionary<string, string?> hierarchyMap)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(managerId)) return false;
            string? current = userId;
            int depth = 0;
            while (hierarchyMap.TryGetValue(current!, out var parentId) && !string.IsNullOrEmpty(parentId) && depth < 20)
            {
                if (parentId == managerId) return true;
                current = parentId;
                depth++;
            }
            return false;
        }

        private async Task<bool> CanUserMoveTaskInternal(TaskItem task, Users currentUser, int? targetColumnId = null)
        {
            if (await _userManager.IsInRoleAsync(currentUser, "Admin")) return true;

            var boardPerm = await _context.BoardPermissions
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == currentUser.Id && EF.Functions.ILike(p.TeamName, task.TeamName.Trim()));

            // Granular transitions
            if (boardPerm != null && !string.IsNullOrEmpty(boardPerm.AllowedTransitionsJson) && targetColumnId.HasValue)
            {
                try
                {
                    var transitions = JsonSerializer.Deserialize<Dictionary<int, List<int>>>(boardPerm.AllowedTransitionsJson);
                    if (transitions != null)
                    {
                        if (transitions.TryGetValue(task.ColumnId, out var allowedTargets))
                        {
                            if (allowedTargets.Contains(targetColumnId.Value)) return true;
                        }
                        return false;
                    }
                }
                catch { /* corrupted JSON fallback */ }
            }

            // Default rules
            bool isOwner = task.AssignedToUserId == currentUser.Id || task.CreatedByUserId == currentUser.Id;
            var userRoles = await _userManager.GetRolesAsync(currentUser);

            bool isHierarchyAuthorized = false;
            if (userRoles.Contains("Manager") || userRoles.Contains("Sub-Manager"))
            {
                var assignedUser = await _userManager.FindByIdAsync(task.AssignedToUserId);
                if (assignedUser != null)
                {
                    if (assignedUser.ManagerId == currentUser.Id)
                        isHierarchyAuthorized = true;
                    else
                    {
                        var subordinateIds = await _context.Users
                            .Where(u => u.ManagerId == currentUser.Id)
                            .Select(u => u.Id)
                            .ToListAsync();
                        if (subordinateIds.Contains(task.AssignedToUserId))
                            isHierarchyAuthorized = true;
                    }
                }
            }

            return isOwner || isHierarchyAuthorized;
        }

        private async Task<ServiceResult> SaveCustomFieldValuesAsync(int taskId, Dictionary<int, List<string>> customFieldValues)
        {
            int imageCount = customFieldValues.Values
                .SelectMany(v => v)
                .Count(val => !string.IsNullOrEmpty(val) && (val.StartsWith("data:image/") || val.StartsWith("/Tasks/GetFieldImage")));

            if (imageCount > 2) return ServiceResult.Fail("Maximum of 2 images allowed per task.");

            foreach (var kvp in customFieldValues)
            {
                int fieldId = kvp.Key;
                foreach (var val in kvp.Value)
                {
                    if (string.IsNullOrEmpty(val)) continue;

                    var cfv = new TaskFieldValue
                    {
                        TaskId = taskId,
                        FieldId = fieldId,
                        Value = val,
                        CreatedAt = DateTime.UtcNow
                    };

                    if (val.StartsWith("data:image/"))
                    {
                        var imgResult = _imageService.ProcessBase64Image(val, fieldId);
                        if (!imgResult.Success)
                        {
                            _logger.LogWarning("Image processing failed for field {FieldId}: {Error}", fieldId, imgResult.ErrorMessage);
                            continue;
                        }
                        cfv.ImageData = imgResult.ImageData;
                        cfv.ImageMimeType = imgResult.MimeType;
                        cfv.FileName = imgResult.FileName;
                        _context.TaskFieldValues.Add(cfv);
                        await _context.SaveChangesAsync();
                        cfv.Value = $"/Tasks/GetFieldImageById/{cfv.Id}";
                    }
                    else
                    {
                        _context.TaskFieldValues.Add(cfv);
                    }
                }
            }
            await _context.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        private async Task<ServiceResult> UpdateCustomFieldValuesAsync(TaskItem task, Dictionary<int, List<string>> customFieldValues, string userId)
        {
            int totalImages = customFieldValues.Values
                .SelectMany(v => v)
                .Count(val => !string.IsNullOrEmpty(val) && (val.StartsWith("data:image/") || val.Contains("/Tasks/GetFieldImage")));

            if (totalImages > 2) return ServiceResult.Fail("Maximum of 2 images allowed per task.");

            foreach (var kvp in customFieldValues)
            {
                int fieldId = kvp.Key;
                var newValues = kvp.Value.Where(v => !string.IsNullOrEmpty(v)).ToList();

                var fieldDef = await _context.TaskCustomFields.FindAsync(fieldId);
                if (fieldDef == null) continue;

                var existingRecords = await _context.TaskFieldValues
                    .Where(v => v.TaskId == task.Id && v.FieldId == fieldId)
                    .ToListAsync();

                // Remove old values
                var recordsToRemove = existingRecords.Where(r => !newValues.Contains(r.Value)).ToList();
                if (recordsToRemove.Any()) _context.TaskFieldValues.RemoveRange(recordsToRemove);

                // Add new values
                foreach (var val in newValues)
                {
                    if (existingRecords.Any(r => r.Value == val)) continue;

                    var newVal = new TaskFieldValue
                    {
                        TaskId = task.Id,
                        FieldId = fieldId,
                        Value = val,
                        CreatedAt = DateTime.UtcNow
                    };

                    // Trigger task movement for List fields
                    if (fieldDef.FieldType == "List" && int.TryParse(val, out int targetColumnId))
                    {
                        if (task.ColumnId != targetColumnId)
                        {
                            var moveUser = await _userManager.FindByIdAsync(userId);
                            if (moveUser != null)
                            {
                                var moveTask = await _context.TaskItems.Include(t => t.Column).FirstOrDefaultAsync(t => t.Id == task.Id);
                                if (moveTask != null)
                                {
                                    // Inline move (same logic as MoveTaskInternal)
                                    var targetCol = await _context.TeamColumns.FirstOrDefaultAsync(c => c.Id == targetColumnId);
                                    if (targetCol != null)
                                    {
                                        moveTask.PreviousColumnId = moveTask.ColumnId;
                                        moveTask.ColumnId = targetCol.Id;
                                        moveTask.UpdatedAt = DateTime.UtcNow;
                                        moveTask.CurrentColumnEntryAt = DateTime.UtcNow;
                                        await _historyService.LogColumnMove(moveTask.Id, moveTask.PreviousColumnId ?? 0, targetCol.Id, userId);
                                    }
                                }
                            }
                        }
                    }

                    if (val.StartsWith("data:image/"))
                    {
                        var imgResult = _imageService.ProcessBase64Image(val, fieldId);
                        if (imgResult.Success)
                        {
                            newVal.ImageData = imgResult.ImageData;
                            newVal.ImageMimeType = imgResult.MimeType;
                            newVal.FileName = imgResult.FileName;
                            _context.TaskFieldValues.Add(newVal);
                            await _context.SaveChangesAsync();
                            newVal.Value = $"/Tasks/GetFieldImageById/{newVal.Id}";
                        }
                    }
                    else
                    {
                        _context.TaskFieldValues.Add(newVal);
                    }

                    await _historyService.LogCustomFieldChange(task.Id, fieldDef.FieldName, "(added)", val, userId);
                }
            }

            return ServiceResult.Ok();
        }

        private async Task<Dictionary<string, IList<string>>> GetUserRolesMapAsync()
        {
            var data = await (from ur in _context.UserRoles
                              join r in _context.Roles on ur.RoleId equals r.Id
                              select new { ur.UserId, RoleName = r.Name })
                             .AsNoTracking()
                             .ToListAsync();

            return data.GroupBy(ur => ur.UserId)
                       .ToDictionary(g => g.Key, g => (IList<string>)g.Select(ur => ur.RoleName).ToList());
        }

        private async Task<Dictionary<string, string>> GetUserTeamsMapAsync()
        {
            var data = await _context.UserTeams
                .AsNoTracking()
                .Select(ut => new { ut.UserId, ut.TeamName })
                .ToListAsync();

            return data.GroupBy(ut => ut.UserId)
                       .ToDictionary(g => g.Key, g => g.FirstOrDefault()?.TeamName ?? "N/A");
        }

        private Dictionary<string, string> BuildFeUserRolesMap(List<Users> allUsers, Dictionary<string, IList<string>> userRolesMap)
        {
            var map = new Dictionary<string, string>();
            foreach (var u in allUsers)
            {
                var r = userRolesMap.GetValueOrDefault(u.Id, new List<string>());
                var primaryRole = r.FirstOrDefault() ?? "User";

                if (r.Contains("Sub-Manager") || r.Contains("SubManager") || (primaryRole == "Manager" && !string.IsNullOrEmpty(u.ParentUserId)))
                    map[u.Id] = "Sub-Manager";
                else if (r.Contains("Admin"))
                    map[u.Id] = "Admin";
                else if (primaryRole == "Manager")
                    map[u.Id] = "Manager";
                else
                    map[u.Id] = primaryRole;
            }
            return map;
        }

        private List<Users> BuildFilteredAssignees(List<Users> allUsers, List<string> teamUserIds, string userId,
            IList<string> viewerRoles, Dictionary<string, string> feUserRolesMap)
        {
            var rolePriority = new Dictionary<string, int> { { "Admin", 1 }, { "Manager", 2 }, { "Sub-Manager", 3 }, { "User", 4 } };
            List<Users> filtered;

            if (viewerRoles.Contains("Admin"))
            {
                filtered = allUsers.Where(u => teamUserIds.Contains(u.Id)).ToList();
            }
            else if (viewerRoles.Contains("Manager"))
            {
                var accessibleIds = new HashSet<string> { userId };
                var directSubs = allUsers.Where(u => u.ParentUserId == userId).Select(u => u.Id).ToList();
                foreach (var id in directSubs) accessibleIds.Add(id);
                var indirect = allUsers.Where(u => directSubs.Contains(u.ParentUserId ?? "")).Select(u => u.Id);
                foreach (var id in indirect) accessibleIds.Add(id);
                filtered = allUsers.Where(u => accessibleIds.Contains(u.Id) && teamUserIds.Contains(u.Id)).ToList();
            }
            else if (viewerRoles.Contains("Sub-Manager") || viewerRoles.Contains("SubManager"))
            {
                // Sub-Managers can assign to any user in the team board groups for cross-team support
                filtered = allUsers.Where(u => teamUserIds.Contains(u.Id)).ToList();
            }
            else
            {
                filtered = allUsers.Where(u => u.Id == userId && teamUserIds.Contains(u.Id)).ToList();
            }

            return filtered
                .OrderBy(u => rolePriority.GetValueOrDefault(feUserRolesMap.GetValueOrDefault(u.Id, "User"), 99))
                .ThenBy(u => u.Name ?? u.UserName)
                .ToList();
        }

        private async Task<(List<TeamGroupViewModel>, List<UserHierarchyItem>)> BuildTeamGroupsAsync(
            List<Users> allUsers, Dictionary<string, string> feUserRolesMap, string currentTeam, string userId, IList<string> viewerRoles)
        {
            var managementUsers = new List<UserHierarchyItem>();
            var teamGroups = new List<TeamGroupViewModel>();

            var allTeamNames = await _context.UserTeams.AsNoTracking().Select(ut => ut.TeamName).Distinct().ToListAsync();
            var allUserTeams = await _context.UserTeams.AsNoTracking().ToListAsync();

            // ── RESTRICTION LOGIC ──
            var viewer = allUsers.FirstOrDefault(u => u.Id == userId);
            var isAdmin = viewerRoles.Contains("Admin");
            var isManager = viewerRoles.Contains("Manager");
            // A sub-manager is someone with the role OR a Manager with a ParentUserId
            var isSubManager = viewerRoles.Contains("Sub-Manager") || viewerRoles.Contains("SubManager") || 
                               (isManager && viewer != null && !string.IsNullOrEmpty(viewer.ParentUserId));
            
            var myTeams = allUserTeams.Where(ut => ut.UserId == userId).Select(ut => ut.TeamName).ToHashSet();
            var isTechnicalUser = myTeams.Any(t => IsTechnicalTeam(t));
            var isTechnicalSubManager = isSubManager && isTechnicalUser;

            // TECHNICAL SUB-MANAGERS & REGULAR USERS cannot see management. 
            // Only Admins and true top-level Managers can.
            var canSeeManagement = isAdmin || (isManager && !isSubManager);
            
            Func<string, bool> isTeamAllowed = (tName) =>
            {
                // Admins and Top-level Managers see all
                if (isAdmin || (isManager && !isSubManager)) return true;
                
                // If it's the current team board we're looking at, it should be visible
                if (!string.IsNullOrEmpty(currentTeam) && tName.Trim().Equals(currentTeam.Trim(), StringComparison.OrdinalIgnoreCase)) return true;

                // Sub-Managers from technical teams see THE ENTIRE technical trio only (and hide everything else)
                if (isTechnicalSubManager) return IsTechnicalTeam(tName);

                // Regular users OR non-tech Sub-Managers only see their own teams
                return myTeams.Contains(tName);
            };

            // Global managers - Only visible to Admins and Top-level Managers
            if (canSeeManagement)
            {
                var globalManagers = allUsers
                    .Where(u => { var role = feUserRolesMap.GetValueOrDefault(u.Id, "User"); return role == "Admin" || role == "Manager"; })
                    .OrderByDescending(u => feUserRolesMap.GetValueOrDefault(u.Id, "User"))
                    .ThenBy(u => u.Name ?? u.UserName)
                    .ToList();

                foreach (var m in globalManagers)
                    managementUsers.Add(new UserHierarchyItem { Id = m.Id, Name = m.Name ?? m.UserName, RoleName = feUserRolesMap.GetValueOrDefault(m.Id, "User"), Level = 0 });
            }

            var globalManagerIds = allUsers
                .Where(u => { var role = feUserRolesMap.GetValueOrDefault(u.Id, "User"); return role == "Admin" || role == "Manager"; })
                .Select(m => m.Id).ToHashSet();

            // Team groups
            foreach (var tName in allTeamNames)
            {
                if (!isTeamAllowed(tName)) continue;

                var usersInTeamIds = allUserTeams.Where(ut => ut.TeamName == tName).Select(ut => ut.UserId).ToHashSet();
                var teamUsers = allUsers.Where(u => usersInTeamIds.Contains(u.Id) && !globalManagerIds.Contains(u.Id)).ToList();

                var groupedUsers = new List<UserHierarchyItem>();
                var processedIds = new HashSet<string>();

                var subManagers = teamUsers.Where(u => feUserRolesMap.GetValueOrDefault(u.Id, "User") == "Sub-Manager").OrderBy(u => u.Name).ToList();
                foreach (var sm in subManagers)
                {
                    groupedUsers.Add(new UserHierarchyItem { Id = sm.Id, Name = sm.Name ?? sm.UserName, RoleName = feUserRolesMap[sm.Id], Level = 0 });
                    processedIds.Add(sm.Id);

                    var subs = teamUsers.Where(u => u.ParentUserId == sm.Id && !processedIds.Contains(u.Id)).OrderBy(u => u.Name).ToList();
                    foreach (var s in subs)
                    {
                        groupedUsers.Add(new UserHierarchyItem { Id = s.Id, Name = s.Name ?? s.UserName, RoleName = feUserRolesMap[s.Id], Level = 1 });
                        processedIds.Add(s.Id);
                    }
                }

                var rest = teamUsers.Where(u => !processedIds.Contains(u.Id)).OrderBy(u => u.Name).ToList();
                foreach (var r in rest)
                    groupedUsers.Add(new UserHierarchyItem { Id = r.Id, Name = r.Name ?? r.UserName, RoleName = feUserRolesMap.GetValueOrDefault(r.Id, "User"), Level = 0 });

                if (groupedUsers.Any())
                    teamGroups.Add(new TeamGroupViewModel { TeamName = tName, Users = groupedUsers });
            }

            teamGroups = teamGroups
                .OrderByDescending(g => g.TeamName.Trim().Equals(currentTeam.Trim(), StringComparison.OrdinalIgnoreCase))
                .ThenBy(g => g.TeamName)
                .ToList();

            return (teamGroups, managementUsers);
        }

        private bool IsTechnicalTeam(string teamName)
        {
            if (string.IsNullOrEmpty(teamName)) return false;
            var t = teamName.Trim().ToLower();
            return t.Contains("frontend") || t.Contains("backend") || t.Contains("testing") || t.Contains("tesign");
        }

        private async Task<TeamColumn?> GetFirstColumnForTeam(string teamName)
        {
            var cols = await _context.TeamColumns.Where(c => c.TeamName == teamName).ToListAsync();
            return cols.OrderBy(c =>
            {
                var name = c.ColumnName?.Trim().ToLower();
                if (name == "review") return 1000;
                if (name == "completed") return 1001;
                return c.Order;
            }).FirstOrDefault();
        }

        private async Task<string> GetPrimaryRole(Users user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "User";
            if (roles.Contains("Sub-Manager") || (role == "Manager" && !string.IsNullOrEmpty(user.ParentUserId)))
                role = "Sub-Manager";
            return role;
        }

        private async Task BroadcastSafe(string teamName, string method, object payload)
        {
            try
            {
                await _hubContext.Clients.Group(teamName).SendAsync(method, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR broadcast failed: {Method} to {Team}", method, teamName);
            }
        }
    }
}
