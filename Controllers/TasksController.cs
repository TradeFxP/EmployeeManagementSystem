using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using UserRoles.Data;
using UserRoles.Models;
using UserRoles.ViewModels;
using UserRoles.Models.Enums;
using UserRoles.DTOs;
using UserRoles.Services; // Added for ITaskHistoryService

using TaskStatusEnum = UserRoles.Models.Enums.TaskStatus;


[Authorize]
public class TasksController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<Users> _userManager;
    private readonly ITaskHistoryService _historyService;

    public TasksController(AppDbContext context, UserManager<Users> userManager, ITaskHistoryService historyService)
    {
        _context = context;
        _userManager = userManager;
        _historyService = historyService;
    }

    // Loads page
    [Authorize]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login", "Account");

        // ================= ADMIN =================
        if (User.IsInRole("Admin"))
        {
            // Admin always sees board chooser
            return View();
        }

        // ================= USER / MANAGER =================
        var userTeams = await _context.UserTeams
            .Where(t => t.UserId == user.Id)
            .Select(t => t.TeamName)
            .Distinct()
            .ToListAsync();
        ViewBag.UserTeams = userTeams; // 🔥 REQUIRED

        // Always show the index view with left panel
        // Users see their assigned teams, can select to load board
        return View();
    }




    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskViewModel model)
    {
        if (model == null)
            return BadRequest("Invalid payload");

        if (model.ColumnId <= 0)
            return BadRequest("ColumnId is required");

        //if (string.IsNullOrWhiteSpace(model.Title))
        //    return BadRequest("Title is required");

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        // 🔑 LOAD COLUMN (THIS WAS MISSING)
        var column = await _context.TeamColumns
            .FirstOrDefaultAsync(c => c.Id == model.ColumnId);

        if (column == null)
            return BadRequest("Column not found");

        // Generate WorkItemId if project is selected
        string? workItemId = null;
        if (model.ProjectId.HasValue && model.ProjectId.Value > 0)
        {
            var project = await _context.Projects
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == model.ProjectId.Value);

            if (project != null)
            {
                // Count existing tasks linked to this project
                int taskCount = await _context.TaskItems
                    .CountAsync(t => t.ProjectId == model.ProjectId.Value);

                // Generate ID: P{projectId}T{taskNumber}
                workItemId = $"P{project.Id}T{taskCount + 1}";
            }
        }

        var task = new TaskItem
        {
            Title = model.Title.Trim(),
            Description = model.Description?.Trim(),

            ColumnId = column.Id,
            TeamName = column.TeamName,

            // Project linkage
            ProjectId = model.ProjectId,
            WorkItemId = workItemId,

            // Priority
            Priority = model.Priority,

            Status = TaskStatusEnum.ToDo,

            CreatedByUserId = user.Id,
            AssignedToUserId = user.Id,      // creator owns initially
            AssignedByUserId = user.Id,      // 🔥 IMPORTANT
            AssignedAt = DateTime.UtcNow,    // 🔥 IMPORTANT
            CreatedAt = DateTime.UtcNow
        };


        _context.TaskItems.Add(task);
        await _context.SaveChangesAsync();

        // Set column entry timestamp for time tracking
        task.CurrentColumnEntryAt = DateTime.UtcNow;

        // Log task creation
        await _historyService.LogTaskCreated(task.Id, user.Id);
        await _context.SaveChangesAsync();

        // Save custom field values if provided
        if (model.CustomFieldValues != null && model.CustomFieldValues.Any())
        {
            foreach (var fieldValue in model.CustomFieldValues)
            {
                var customFieldValue = new TaskFieldValue
                {
                    TaskId = task.Id,
                    FieldId = fieldValue.Key,
                    Value = fieldValue.Value,
                    CreatedAt = DateTime.UtcNow
                };
                _context.TaskFieldValues.Add(customFieldValue);
            }
            await _context.SaveChangesAsync();
        }


        return Ok(new
        {
            success = true,
            message = "Task created successfully",
            workItemId = workItemId
        });

    }






    [HttpPost]
    [Authorize(Roles = "Admin,Manager,Sub-Manager")]
    public async Task<IActionResult> AssignTask(int taskId, string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("UserId is required");

        var task = await _context.TaskItems.FindAsync(taskId);
        if (task == null)
            return NotFound();

        var assignToUser = await _userManager.FindByIdAsync(userId);
        if (assignToUser == null)
            return BadRequest("Invalid user");

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized();

        // ✅ LOG ASSIGNMENT
        await _historyService.LogAssignment(task.Id, assignToUser.Id, currentUser.Id);

        task.AssignedToUserId = assignToUser.Id;
        task.AssignedByUserId = currentUser.Id;
        task.AssignedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            assignedTo = assignToUser.UserName,
            assignedBy = currentUser.UserName,
            assignedAt = task.AssignedAt.Value.ToString("dd MMM yyyy, hh:mm tt")
        });
    }



    ////edittask
    //[HttpPost]
    //[Authorize(Roles = "Admin")]
    //public async Task<IActionResult> EditTask(int id, string title, string description)
    //{
    //    var task = await _context.TaskItems.FindAsync(id);
    //    if (task == null) return NotFound();

    //    task.Title = title;
    //    task.Description = description;

    //    await _context.SaveChangesAsync();
    //    return Ok();
    //}




    [HttpPost]
    [Authorize]
    public async Task<IActionResult> UpdateTask([FromBody] UpdateTaskRequest model)
    {
        if (model == null || model.TaskId <= 0)
            return BadRequest();

        var task = await _context.TaskItems.FindAsync(model.TaskId);
        if (task == null)
            return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        // Check permission if not Admin
        if (!User.IsInRole("Admin") && task.AssignedToUserId != user.Id)
        {
            if (!await AuthorizeBoardAction(task.TeamName, "EditAllFields"))
                return Forbid();
        }

        // 1. Title
        if (task.Title != model.Title?.Trim())
        {
            await _historyService.LogTaskUpdated(task.Id, user.Id, "Title", task.Title, model.Title?.Trim());
            task.Title = model.Title?.Trim();
        }

        // 2. Description
        if (task.Description != model.Description?.Trim())
        {
            // Just logging that it changed, or the full value if needed. 
            // _historyService.LogTaskUpdated(task.Id, user.Id, "Description", task.Description, model.Description?.Trim());
            await _historyService.LogTaskUpdated(task.Id, user.Id, "Description", "Old Description", "New Description");
            task.Description = model.Description?.Trim();
        }

        task.UpdatedAt = DateTime.UtcNow;

        // 3. Update priority if provided
        if (model.Priority.HasValue)
        {
            if (task.Priority != model.Priority.Value)
            {
                await _historyService.LogPriorityChange(task.Id, task.Priority, model.Priority.Value, user.Id);
                task.Priority = model.Priority.Value;
            }
        }

        if (!string.IsNullOrEmpty(model.AssignedToUserId))
        {
            if (User.IsInRole("Admin") ||
                User.IsInRole("Manager") ||
                User.IsInRole("SubManager"))
            {
                if (task.AssignedToUserId != model.AssignedToUserId)
                {
                    await _historyService.LogAssignment(task.Id, model.AssignedToUserId, user.Id);
                    task.AssignedToUserId = model.AssignedToUserId;
                }
            }
        }

        // 4. Update custom field values
        if (model.CustomFieldValues != null && model.CustomFieldValues.Any())
        {
            // Remove existing field values for this task
            var existingValues = await _context.TaskFieldValues
                .Include(v => v.Field)
                .Where(v => v.TaskId == task.Id)
                .ToListAsync();

            var existingDict = existingValues.ToDictionary(v => v.FieldId, v => v);

            foreach (var fieldValue in model.CustomFieldValues)
            {
                int fieldId = fieldValue.Key;
                string newValue = fieldValue.Value;

                if (existingDict.TryGetValue(fieldId, out var existingVal))
                {
                    if (existingVal.Value != newValue)
                    {
                        await _historyService.LogCustomFieldChange(task.Id, existingVal.Field?.FieldName ?? $"Field #{fieldId}", existingVal.Value, newValue, user.Id);
                        existingVal.Value = newValue;
                    }
                }
                else
                {
                    // New value
                    var fieldDef = await _context.TaskCustomFields.FindAsync(fieldId);
                    if (fieldDef != null)
                    {
                        await _historyService.LogCustomFieldChange(task.Id, fieldDef.FieldName, "(empty)", newValue, user.Id);
                        var customFieldValue = new TaskFieldValue
                        {
                            TaskId = task.Id,
                            FieldId = fieldValue.Key,
                            Value = fieldValue.Value,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.TaskFieldValues.Add(customFieldValue);
                    }
                }
            }

            // Clean up old method: We used to remove all and re-add. Now we update in place? 
            // The Original code did: _context.TaskFieldValues.RemoveRange(existingValues);
            // If I change to update-in-place, I must ensure I don't break anything.
            // AND I need to handle fields that are NOT in the payload (if any).
            // Typically UpdateTask payload sends ALL fields.
            // But to be safe and match original "replace all" behavior while logging diffs:

            // Re-fetch or stick to Original Logic but with logging?
            // Original logic:
            // RemoveRange(existingValues) -> Add(newValues).
            // This is easier but we lose "OldValue" reference if we delete first.
            // So:
            // 1. Log diffs (done above)
            // 2. Remove all existing (except maybe ones we just updated? effectively same result)
            // 3. Add all new.

            // Let's stick to "Log then Replace" pattern to minimize side effects, 
            // BUT we must be careful not to hold onto entities we are about to delete?
            // Actually, if I logged changes, I don't need the entities anymore.

            _context.TaskFieldValues.RemoveRange(existingValues);

            foreach (var fieldValue in model.CustomFieldValues)
            {
                var customFieldValue = new TaskFieldValue
                {
                    TaskId = task.Id,
                    FieldId = fieldValue.Key,
                    Value = fieldValue.Value,
                    CreatedAt = DateTime.UtcNow
                };
                _context.TaskFieldValues.Add(customFieldValue);
            }
        }

        await _context.SaveChangesAsync();
        // ✅ RETURN JSON (IMPORTANT)
        return Json(new { success = true });
    }


    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetTask(int id)
    {
        var task = await _context.TaskItems
            .Include(t => t.CustomFieldValues)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null) return NotFound();

        // 1. Get basic info
        var result = new
        {
            id = task.Id,
            title = task.Title,
            description = task.Description,
            priority = (int)task.Priority,
            assignedToUserId = task.AssignedToUserId,
            // 2. Convert custom fields to dictionary for easy JS consumption
            customFieldValues = task.CustomFieldValues.ToDictionary(k => k.FieldId, v => v.Value)
        };

        return Ok(result);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetTaskDetail(int id)
    {
        var task = await _context.TaskItems
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .Include(t => t.ReviewedByUser)
            .Include(t => t.CompletedByUser)
            .Include(t => t.Column)
            .Include(t => t.CustomFieldValues)
                .ThenInclude(v => v.Field)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null) return NotFound();

        return Ok(new
        {
            task.Id,
            task.Title,
            task.Description,
            Priority = task.Priority.ToString(),
            Status = task.Status.ToString(),
            ReviewStatus = task.ReviewStatus.ToString(),
            task.ReviewNote,
            Column = task.Column?.ColumnName,
            CreatedBy = task.CreatedByUser?.UserName,
            AssignedTo = task.AssignedToUser?.UserName,
            AssignedBy = task.AssignedByUser?.UserName,
            ReviewedBy = task.ReviewedByUser?.UserName,
            CompletedBy = task.CompletedByUser?.UserName,
            task.CreatedAt,
            task.AssignedAt,
            task.ReviewedAt,
            task.CompletedAt,
            CustomFields = task.CustomFieldValues?.Select(fv => new
            {
                FieldName = fv.Field?.FieldName,
                fv.Value
            }).ToList()
        });
    }






    [HttpPost]
    [Authorize]
    public async Task<IActionResult> DeleteTask([FromBody] int taskId)
    {
        var task = await _context.TaskItems.FindAsync(taskId);
        if (task == null)
            return NotFound();

        if (!await AuthorizeBoardAction(task.TeamName, "DeleteTask"))
            return Forbid();

        _context.TaskItems.Remove(task);
        await _context.SaveChangesAsync();
        return Ok();
    }




    [Authorize]
    public async Task<IActionResult> TeamBoard(string team)
    {
        if (string.IsNullOrWhiteSpace(team))
            return BadRequest();

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        // Security: team access
        if (!User.IsInRole("Admin"))
        {
            bool hasAccess = await _context.UserTeams
                .AnyAsync(t => t.UserId == user.Id && t.TeamName == team);

            if (!hasAccess)
                return Forbid();
        }

        // ✅ AUTO-ARCHIVE logic: Move tasks from "Completed" to "History" if they were finished before today
        await AutoArchiveOldTasks(team);

        // Load columns — enforce order: normal columns → Review → Completed
        var columnsRaw = await _context.TeamColumns
            .Where(c => c.TeamName == team)
            .OrderBy(c => c.Order)
            .ToListAsync();

        // Sort: normal columns first, then Review, then Completed
        var columns = columnsRaw
            .OrderBy(c =>
            {
                var name = c.ColumnName?.Trim().ToLower();
                if (name == "review") return 1000;
                if (name == "completed") return 1001;
                return c.Order;
            })
            .ToList();

        // Load all tasks for team (with users and custom field values) — exclude archived
        var allTasks = await _context.TaskItems
     .Where(t => t.TeamName == team && !t.IsArchived)
     .Include(t => t.CreatedByUser)
     .Include(t => t.AssignedToUser)
     .Include(t => t.AssignedByUser)
     .Include(t => t.ReviewedByUser)
     .Include(t => t.CompletedByUser)
     .Include(t => t.CustomFieldValues)
        .ThenInclude(v => v.Field)
     .ToListAsync();


        // 🔥 FILTER TASKS BASED ON ROLE RULES
        var visibleTasks = new List<TaskItem>();

        foreach (var task in allTasks)
        {
            if (await CanUserSeeTask(task, user))
                visibleTasks.Add(task);
        }

        // Attach filtered tasks to columns
        foreach (var col in columns)
        {
            col.Tasks = visibleTasks
                .Where(t => t.ColumnId == col.Id)
                .ToList();
        }

        // ✅ 1. Load assignable users FIRST
        var assignableUsers = await _userManager.Users
            .OrderBy(u => u.UserName)
            .ToListAsync();

        // Load active custom fields
        var customFields = await _context.TaskCustomFields
            .Where(f => f.IsActive)
            .OrderBy(f => f.Order)
            .ToListAsync();

        // ✅ 2. Build ViewModel AFTER data exists
        var userPerms = await _context.BoardPermissions
            .FirstOrDefaultAsync(p => p.UserId == user.Id && p.TeamName == team);

        var vm = new TeamBoardViewModel
        {
            TeamName = team,
            Columns = columns,
            AssignableUsers = assignableUsers,
            CustomFields = customFields,
            UserPermissions = userPerms
        };

        // ✅ 3. Return partial view
        return PartialView("_TeamBoard", vm);


    }



    [HttpPost]
    [Authorize]
    public async Task<IActionResult> AddColumn([FromBody] AddColumnRequest model)
    {
        if (!await AuthorizeBoardAction(model.Team, "AddColumn"))
            return Forbid();

        // Validate input
        if (string.IsNullOrWhiteSpace(model.ColumnName))
            return BadRequest("Column name is required");

        // Get next column order for the team
        var maxOrder = _context.TeamColumns
            .Where(c => c.TeamName == model.Team)
            .Max(c => (int?)c.Order) ?? 0;

        // Create new column
        var column = new TeamColumn
        {
            TeamName = model.Team,
            ColumnName = model.ColumnName,
            Order = maxOrder + 1
        };

        _context.TeamColumns.Add(column);
        _context.SaveChanges();

        return Ok();
    }
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ReorderColumns([FromBody] List<int> columnIds)
    {
        // 🔒 Safety check
        if (columnIds == null || columnIds.Count == 0)
            return BadRequest("No columns received");

        var firstCol = await _context.TeamColumns.FindAsync(columnIds[0]);
        if (firstCol == null || !await AuthorizeBoardAction(firstCol.TeamName, "ReorderColumns"))
            return Forbid();

        // Load only affected columns
        var columns = _context.TeamColumns
            .Where(c => columnIds.Contains(c.Id))
            .ToList();

        // Save order exactly as UI order
        for (int i = 0; i < columnIds.Count; i++)
        {
            var column = columns.FirstOrDefault(c => c.Id == columnIds[i]);
            if (column != null)
            {
                column.Order = i + 1; // 1-based order
            }
        }

        _context.SaveChanges();
        return Ok();
    }


    [HttpPost]
    [Authorize]
    public async Task<IActionResult> RenameColumn([FromBody] RenameColumnRequest model)
    {
        if (model == null || model.ColumnId <= 0 || string.IsNullOrWhiteSpace(model.Name))
            return BadRequest("Invalid request");

        var col = await _context.TeamColumns.FindAsync(model.ColumnId);
        if (col == null) return NotFound("Column not found");

        if (!await AuthorizeBoardAction(col.TeamName, "RenameColumn"))
            return Forbid();

        if (col == null)
            return NotFound("Column not found");

        col.ColumnName = model.Name.Trim();
        _context.SaveChanges();

        return Ok();
    }

    public class DeleteColumnRequest
    {
        public int columnId { get; set; } // Match JS casing precisely or use [JsonPropertyName]
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> DeleteColumn([FromBody] DeleteColumnRequest model)
    {
        if (model == null || model.columnId <= 0)
            return BadRequest("Invalid request");

        var hasAnyTasks = await _context.TaskItems.AnyAsync(t => t.ColumnId == model.columnId);
        if (hasAnyTasks)
            return BadRequest("Move all tasks (including archived) before deleting column");

        var col = await _context.TeamColumns.FindAsync(model.columnId);
        if (col == null)
            return NotFound();

        _context.TeamColumns.Remove(col);
        await _context.SaveChangesAsync();

        return Ok();
    }




    private async Task<bool> CanUserSeeTask(TaskItem task, Users currentUser)
    {
        // Assigned user always sees
        if (task.AssignedToUserId == currentUser.Id)
            return true;

        var viewerRoles = await _userManager.GetRolesAsync(currentUser);

        if (viewerRoles.Contains("Admin"))
            return true;

        var creator = await _userManager.FindByIdAsync(task.CreatedByUserId);
        if (creator == null)
            return false;

        var creatorRoles = await _userManager.GetRolesAsync(creator);

        bool isManager = viewerRoles.Contains("Manager");
        bool isSubManager = viewerRoles.Contains("Sub-Manager");

        if (creatorRoles.Contains("User"))
            return isSubManager || isManager;

        if (creatorRoles.Contains("Sub-Manager"))
            return isManager;

        if (creatorRoles.Contains("Manager"))
            return isManager;

        return false;
    }


    [HttpPost]
    [Authorize]
    public async Task<IActionResult> MoveTask([FromBody] MoveTaskDto model)
    {
        if (model == null)
            return BadRequest("Invalid payload");

        var task = await _context.TaskItems
            .Include(t => t.Column)
            .FirstOrDefaultAsync(t => t.Id == model.TaskId);

        if (task == null)
            return NotFound("Task not found");

        var targetColumn = await _context.TeamColumns
            .FirstOrDefaultAsync(c => c.Id == model.ColumnId);

        if (targetColumn == null)
            return NotFound("Target column not found");

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var isAdmin = User.IsInRole("Admin");
        var targetColName = targetColumn.ColumnName?.Trim().ToLower();
        var sourceColName = task.Column?.ColumnName?.Trim().ToLower();

        // ═══════ ROLE-BASED RESTRICTIONS ═══════
        
        // 1. COMPLETED column: ONLY Admin OR users with ReviewTask permission can move tasks here
        if (targetColName == "completed")
        {
            if (!isAdmin && !await AuthorizeBoardAction(task.TeamName, "ReviewTask"))
                return StatusCode(403, "Only Admin or authorized reviewers can move tasks to Completed.");

            // Task must have passed review first
            if (task.ReviewStatus != UserRoles.Models.Enums.ReviewStatus.Passed)
                return BadRequest("Task must pass review before moving to Completed.");
        }

        // 2. REVIEW column: Check hierarchy - user can only move tasks they or subordinates own
        if (targetColName == "review")
        {
            if (!await CanUserMoveTask(task, user))
                return StatusCode(403, "You can only move tasks assigned to you or your subordinates to Review.");
        }

        // 3. Moving FROM review back (fail rework): allowed if task was failed
        if (sourceColName == "review" && targetColName != "completed" && !isAdmin)
        {
            // Non-admin can only move back if review was failed
            if (task.ReviewStatus != UserRoles.Models.Enums.ReviewStatus.Failed &&
                task.ReviewStatus != UserRoles.Models.Enums.ReviewStatus.None)
            {
                if (!await CanUserMoveTask(task, user))
                    return StatusCode(403, "You cannot move this task.");
            }
        }

        // ═══════ LOG & MOVE ═══════
        await _historyService.LogColumnMove(task.Id, task.ColumnId, targetColumn.Id, user.Id);

        // Save previous column for fail-return
        task.PreviousColumnId = task.ColumnId;
        task.ColumnId = targetColumn.Id;

        // Sync status
        task.Status = targetColumn.ColumnName switch
        {
            "ToDo" => TaskStatusEnum.ToDo,
            "Doing" => TaskStatusEnum.Doing,
            "Review" => TaskStatusEnum.Review,
            "completed" => TaskStatusEnum.Complete,
            _ => task.Status
        };

        // If moving to review, set review status to pending
        if (targetColName == "review")
        {
            task.ReviewStatus = UserRoles.Models.Enums.ReviewStatus.Pending;
            task.ReviewNote = null; // Clear old notes
            await _historyService.LogReviewSubmitted(task.Id, user.Id);
        }

        // If moving to completed (admin only), track completion
        if (targetColName == "completed")
        {
            task.CompletedByUserId = task.AssignedToUserId;
            task.CompletedAt = DateTime.UtcNow;
        }

        task.UpdatedAt = DateTime.UtcNow;
        task.CurrentColumnEntryAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "Task moved successfully" });
    }

    // ═══════ HIERARCHY-BASED MOVE PERMISSION CHECK ═══════
    private async Task<bool> CanUserMoveTask(TaskItem task, Users currentUser)
    {
        // Admin can do anything
        if (await _userManager.IsInRoleAsync(currentUser, "Admin"))
            return true;

        // User can move their own assigned tasks
        if (task.AssignedToUserId == currentUser.Id)
            return true;

        // User can move tasks they created
        if (task.CreatedByUserId == currentUser.Id)
            return true;

        var userRoles = await _userManager.GetRolesAsync(currentUser);

        // Manager: can move tasks of users under them
        if (userRoles.Contains("Manager") || userRoles.Contains("Sub-Manager"))
        {
            // Check if the assigned user is a subordinate
            var assignedUser = await _userManager.FindByIdAsync(task.AssignedToUserId);
            if (assignedUser != null)
            {
                // Direct report check
                if (assignedUser.ManagerId == currentUser.Id)
                    return true;

                // Indirect: sub-manager's reports
                var subordinateIds = await _context.Users
                    .Where(u => u.ManagerId == currentUser.Id)
                    .Select(u => u.Id)
                    .ToListAsync();

                if (subordinateIds.Contains(task.AssignedToUserId))
                    return true;
            }
        }

        return false;
    }

    // ═══════ ADMIN REVIEW ENDPOINT ═══════
    public class ReviewTaskRequest
    {
        public int TaskId { get; set; }
        public bool Passed { get; set; }
        public string? ReviewNote { get; set; }
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ReviewTask([FromBody] ReviewTaskRequest model)
    {
        if (model == null) return BadRequest();

        var task = await _context.TaskItems
            .Include(t => t.Column)
            .Include(t => t.PreviousColumn)
            .FirstOrDefaultAsync(t => t.Id == model.TaskId);

        if (task == null) return NotFound("Task not found");

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (!await AuthorizeBoardAction(task.TeamName, "ReviewTask"))
            return Forbid();

        task.ReviewedByUserId = user.Id;
        task.ReviewedAt = DateTime.UtcNow;
        task.ReviewNote = model.ReviewNote;

        if (model.Passed)
        {
            // ✅ PASSED
            task.ReviewStatus = UserRoles.Models.Enums.ReviewStatus.Passed;
            await _historyService.LogReviewPassed(task.Id, user.Id, model.ReviewNote);

            // ✅ AUTO-MOVE TO COMPLETED: Find the designated "Completed" column for this team
            var completedCol = await _context.TeamColumns
                .FirstOrDefaultAsync(c => c.TeamName == task.TeamName && c.ColumnName.ToLower().Trim() == "completed");

            if (completedCol != null)
            {
                await _historyService.LogColumnMove(task.Id, task.ColumnId, completedCol.Id, user.Id);
                task.ColumnId = completedCol.Id;
                task.Status = TaskStatusEnum.Complete;
                task.CompletedByUserId = task.AssignedToUserId;
                task.CompletedAt = DateTime.UtcNow;
                task.CurrentColumnEntryAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                return Ok(new { success = true, passed = true, message = "Review passed! Task automatically moved to Completed." });
            }

            return Ok(new { success = true, passed = true, message = "Review passed. Task can now be moved to Completed." });
        }
        else
        {
            // ❌ FAILED — move back to previous column
            task.ReviewStatus = UserRoles.Models.Enums.ReviewStatus.Failed;
            await _historyService.LogReviewFailed(task.Id, user.Id, model.ReviewNote);

            // Move back to previous column
            if (task.PreviousColumnId.HasValue)
            {
                var prevCol = task.PreviousColumn ?? await _context.TeamColumns.FindAsync(task.PreviousColumnId);
                if (prevCol != null)
                {
                    await _historyService.LogColumnMove(task.Id, task.ColumnId, prevCol.Id, user.Id);
                    task.ColumnId = prevCol.Id;
                    task.CurrentColumnEntryAt = DateTime.UtcNow;
                }
            }

            task.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, passed = false, message = "Review failed. Task returned to previous column." });
        }
    }

    // ═══════ ARCHIVE TO HISTORY ═══════
    public class ArchiveRequest
    {
        public string TeamName { get; set; } = string.Empty;
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ArchiveCompletedTasks([FromBody] ArchiveRequest model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var completedTasks = await _context.TaskItems
            .Where(t => t.TeamName == model.TeamName
                     && !t.IsArchived
                     && t.ReviewStatus == UserRoles.Models.Enums.ReviewStatus.Passed
                     && t.CompletedAt != null)
            .ToListAsync();

        foreach (var task in completedTasks)
        {
            task.IsArchived = true;
            task.ArchivedAt = DateTime.UtcNow;
            await _historyService.LogArchivedToHistory(task.Id, user.Id);
        }

        await _context.SaveChangesAsync();

        return Ok(new { success = true, archivedCount = completedTasks.Count });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ArchiveSingleTask([FromBody] int taskId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var task = await _context.TaskItems.FindAsync(taskId);
        if (task == null) return NotFound();

        task.IsArchived = true;
        task.ArchivedAt = DateTime.UtcNow;
        await _historyService.LogArchivedToHistory(task.Id, user.Id);
        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetArchivedTasks(string team)
    {
        if (string.IsNullOrWhiteSpace(team))
            return BadRequest();

        var archivedTasks = await _context.TaskItems
            .Where(t => t.TeamName == team && t.IsArchived)
            .Include(t => t.AssignedToUser)
            .Include(t => t.CompletedByUser)
            .OrderByDescending(t => t.ArchivedAt)
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.Description,
                CompletedBy = t.CompletedByUser != null ? t.CompletedByUser.UserName : (t.AssignedToUser != null ? t.AssignedToUser.UserName : "Unknown"),
                CompletedAt = t.CompletedAt,
                ArchivedAt = t.ArchivedAt,
                Priority = (int)t.Priority,
                ReviewStatus = t.ReviewStatus.ToString()
            })
            .ToListAsync();

        return Ok(archivedTasks);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetArchivedTaskDetail(int id)
    {
        var task = await _context.TaskItems
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .Include(t => t.ReviewedByUser)
            .Include(t => t.CompletedByUser)
            .Include(t => t.CustomFieldValues.Where(cv => cv.Field.IsActive))
                .ThenInclude(v => v.Field)
            .FirstOrDefaultAsync(t => t.Id == id && t.IsArchived);

        if (task == null) return NotFound();

        return Ok(new
        {
            task.Id,
            task.Title,
            task.Description,
            Priority = task.Priority.ToString(),
            Status = task.Status.ToString(),
            ReviewStatus = task.ReviewStatus.ToString(),
            task.ReviewNote,
            CreatedBy = task.CreatedByUser?.UserName,
            AssignedTo = task.AssignedToUser?.UserName,
            AssignedBy = task.AssignedByUser?.UserName,
            ReviewedBy = task.ReviewedByUser?.UserName,
            CompletedBy = task.CompletedByUser?.UserName ?? task.AssignedToUser?.UserName,
            task.CreatedAt,
            task.AssignedAt,
            task.ReviewedAt,
            task.CompletedAt,
            task.ArchivedAt,
            CustomFields = task.CustomFieldValues?.Select(fv => new
            {
                FieldName = fv.Field?.FieldName,
                fv.Value
            }).ToList()
        });
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAllTeams()
    {
        // Get all unique team names from TeamColumns
        var teams = await _context.TeamColumns
            .Select(c => c.TeamName)
            .Distinct()
            .ToListAsync();

        return Ok(teams);
    }

    public class AssignTaskToTeamRequest
    {
        public int TaskId { get; set; }
        public string TeamName { get; set; }
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> AssignTaskToTeam([FromBody] AssignTaskToTeamRequest model)
    {
        if (model == null || model.TaskId <= 0 || string.IsNullOrWhiteSpace(model.TeamName))
            return BadRequest("Invalid request");

        var task = await _context.TaskItems.FindAsync(model.TaskId);
        if (task == null)
            return NotFound("Task not found");

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        // 1. Find the FIRST column for the target team
        var targetColumn = await _context.TeamColumns
            .Where(c => c.TeamName == model.TeamName)
            .OrderBy(c => c.Order)
            .FirstOrDefaultAsync();

        if (targetColumn == null)
            return BadRequest($"No columns found for team '{model.TeamName}'. Create columns first.");

        // ✅ LOG HISTORY (Column Move + Assignment)
        await _historyService.LogColumnMove(task.Id, task.ColumnId, targetColumn.Id, user.Id);
        await _historyService.LogAssignment(task.Id, user.Id, user.Id); // Self-assigned by mover

        // 2. Update Task
        task.TeamName = model.TeamName;
        task.ColumnId = targetColumn.Id;

        // 3. Update Status based on the new column name (optional but good for consistency)
        // If the column name matches a known status, update it. Otherwise, keep it or set to generic.
        // For now, let's try to map it if possible, or default to ToDo if it's the first column
        if (targetColumn.ColumnName.Contains("Todo", StringComparison.OrdinalIgnoreCase) ||
            targetColumn.ColumnName.Contains("To Do", StringComparison.OrdinalIgnoreCase))
        {
            task.Status = TaskStatusEnum.ToDo;
        }
        else if (targetColumn.Order == 1)
        {
            // If it's the first column, it's likely ToDo
            task.Status = TaskStatusEnum.ToDo;
        }

        task.UpdatedAt = DateTime.UtcNow;
        task.AssignedByUserId = user.Id; // The one who moved it
                                         // task.AssignedToUserId = user.Id; // Keep original assignment or assign to mover? 
                                         // Logic says "AssignTaskToTeam" might imply unassigning from individual? 
                                         // But the previous code didn't change AssignedToUserId, only AssignedBy. 
                                         // So I will stick to previous logic BUT log the "AssignedBy" change?
                                         // Actually, let's assume it keeps the assignee or assigns to self as it's a team move.
                                         // The previous code ONLY updated AssignedByUserId.
                                         // I will keep it consistent.

        task.AssignedAt = DateTime.UtcNow;
        task.CurrentColumnEntryAt = DateTime.UtcNow; // Reset timer

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = $"Task moved to {model.TeamName} ({targetColumn.ColumnName})"
        });
    }

    // ========== CUSTOM FIELD MANAGEMENT ENDPOINTS ==========

    [HttpGet]
    [Authorize]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> GetCustomFields()
    {
        var fields = await _context.TaskCustomFields
            .Where(f => f.IsActive)
            .OrderBy(f => f.Order)
            .Select(f => new
            {
                f.Id,
                f.FieldName,
                f.FieldType,
                f.IsRequired,
                f.DropdownOptions,
                f.Order
            })
            .ToListAsync();

        return Ok(fields);
    }

    public class CreateFieldRequest
    {
        public string FieldName { get; set; } = string.Empty;
        public string FieldType { get; set; } = "Text";
        public bool IsRequired { get; set; } = false;
        public string? DropdownOptions { get; set; }
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateCustomField([FromBody] CreateFieldRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.FieldName))
            return BadRequest("Field name is required");

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        // Get max order
        var maxOrder = await _context.TaskCustomFields
            .MaxAsync(f => (int?)f.Order) ?? 0;

        var field = new TaskCustomField
        {
            FieldName = model.FieldName.Trim(),
            FieldType = model.FieldType,
            IsRequired = model.IsRequired,
            DropdownOptions = model.DropdownOptions,
            IsActive = true,
            Order = maxOrder + 1,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };

        _context.TaskCustomFields.Add(field);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, fieldId = field.Id });
    }

    public class UpdateFieldRequest
    {
        public int FieldId { get; set; }
        public string? FieldName { get; set; }
        public string? FieldType { get; set; }
        public bool? IsRequired { get; set; }
        public string? DropdownOptions { get; set; }
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> UpdateCustomField([FromBody] UpdateFieldRequest model)
    {
        var field = await _context.TaskCustomFields.FindAsync(model.FieldId);
        if (field == null)
            return NotFound("Field not found");

        if (!string.IsNullOrWhiteSpace(model.FieldName))
            field.FieldName = model.FieldName.Trim();

        if (!string.IsNullOrWhiteSpace(model.FieldType))
            field.FieldType = model.FieldType;

        if (model.IsRequired.HasValue)
            field.IsRequired = model.IsRequired.Value;

        if (model.DropdownOptions != null)
            field.DropdownOptions = model.DropdownOptions;

        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> DeleteCustomField([FromBody] int fieldId)
    {
        var field = await _context.TaskCustomFields.FindAsync(fieldId);
        if (field == null)
            return NotFound("Field not found");

        // Soft delete - set IsActive to false
        field.IsActive = false;
        await _context.SaveChangesAsync();

        // Or hard delete (this will cascade delete all field values)
        // _context.TaskCustomFields.Remove(field);
        // await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ReorderCustomFields([FromBody] List<int> fieldIds)
    {
        if (fieldIds == null || !fieldIds.Any())
            return BadRequest("No field IDs provided");

        var fields = await _context.TaskCustomFields
            .Where(f => fieldIds.Contains(f.Id))
            .ToListAsync();

        for (int i = 0; i < fieldIds.Count; i++)
        {
            var field = fields.FirstOrDefault(f => f.Id == fieldIds[i]);
            if (field != null)
            {
                field.Order = i + 1;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }

    // ========== TASK HISTORY ==========

    [HttpGet("/Tasks/{taskId}/History")]
    [Authorize]
    public async Task<IActionResult> GetTaskHistory(int taskId)
    {
        var history = await _historyService.GetTaskHistory(taskId);
        return Ok(history);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetTaskCustomFields(int taskId)
    {
        if (taskId <= 0) return BadRequest("Invalid task id");

        var values = await _context.TaskFieldValues
            .Where(v => v.TaskId == taskId && v.Field.IsActive)
            .Select(v => new { v.FieldId, v.Value })
            .ToListAsync();

        var dict = values.ToDictionary(v => v.FieldId, v => v.Value ?? string.Empty);

        return Ok(dict);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> GetBoardPermissions(string team)
    {
        if (string.IsNullOrWhiteSpace(team)) return BadRequest();

        var users = await _userManager.Users.OrderBy(u => u.UserName).ToListAsync();
        var perms = await _context.BoardPermissions
            .Where(p => p.TeamName == team)
            .ToListAsync();

        var result = new List<BoardPermissionDto>();

        foreach (var u in users)
        {
            var p = perms.FirstOrDefault(x => x.UserId == u.Id);
            var roles = await _userManager.GetRolesAsync(u);
            
            result.Add(new BoardPermissionDto
            {
                UserId = u.Id,
                UserName = u.UserName ?? "Unknown",
                Role = roles.FirstOrDefault() ?? "No Role",
                TeamName = team,
                CanAddColumn = p?.CanAddColumn ?? false,
                CanRenameColumn = p?.CanRenameColumn ?? false,
                CanReorderColumns = p?.CanReorderColumns ?? false,
                CanDeleteColumn = p?.CanDeleteColumn ?? false,
                CanEditAllFields = p?.CanEditAllFields ?? false,
                CanDeleteTask = p?.CanDeleteTask ?? false,
                CanReviewTask = p?.CanReviewTask ?? false
            });
        }

        return Ok(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> UpdateBoardPermission([FromBody] BoardPermissionDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.UserId) || string.IsNullOrWhiteSpace(dto.TeamName))
            return BadRequest();

        var existing = await _context.BoardPermissions
            .FirstOrDefaultAsync(p => p.UserId == dto.UserId && p.TeamName == dto.TeamName);

        if (existing == null)
        {
            existing = new BoardPermission
            {
                UserId = dto.UserId,
                TeamName = dto.TeamName
            };
            _context.BoardPermissions.Add(existing);
        }

        existing.CanAddColumn = dto.CanAddColumn;
        existing.CanRenameColumn = dto.CanRenameColumn;
        existing.CanReorderColumns = dto.CanReorderColumns;
        existing.CanDeleteColumn = dto.CanDeleteColumn;
        existing.CanEditAllFields = dto.CanEditAllFields;
        existing.CanDeleteTask = dto.CanDeleteTask;
        existing.CanReviewTask = dto.CanReviewTask;

        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    private async Task<bool> AuthorizeBoardAction(string teamName, string action)
    {
        if (User.IsInRole("Admin")) return true;

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return false;

        var perms = await _context.BoardPermissions
            .FirstOrDefaultAsync(p => p.UserId == user.Id && p.TeamName.ToLower().Trim() == teamName.ToLower().Trim());

        if (perms == null) return false;

        return action switch
        {
            "AddColumn" => perms.CanAddColumn,
            "RenameColumn" => perms.CanRenameColumn,
            "ReorderColumns" => perms.CanReorderColumns,
            "DeleteColumn" => perms.CanDeleteColumn,
            "EditAllFields" => perms.CanEditAllFields,
            "DeleteTask" => perms.CanDeleteTask,
            "ReviewTask" => perms.CanReviewTask,
            _ => false
        };
    }

    private async Task AutoArchiveOldTasks(string teamName)
    {
        // Find tasks in "Completed" column that were completed before today (UTC)
        var today = DateTime.UtcNow.Date;

        var oldTasks = await _context.TaskItems
            .Include(t => t.Column)
            .Where(t => t.TeamName == teamName 
                     && !t.IsArchived 
                     && t.Column.ColumnName.ToLower().Trim() == "completed" 
                     && t.CompletedAt.HasValue 
                     && t.CompletedAt.Value < today)
            .ToListAsync();

        if (oldTasks.Any())
        {
            foreach (var task in oldTasks)
            {
                task.IsArchived = true;
                task.ArchivedAt = DateTime.UtcNow;
                // Log archival (system user or null if we don't have a specific trigger user context here)
                await _historyService.LogArchivedToHistory(task.Id, "System");
            }
            await _context.SaveChangesAsync();
        }
    }
}



