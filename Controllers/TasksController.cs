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
using Microsoft.AspNetCore.SignalR;
using UserRoles.Hubs;
using System.Text.Json;

using TaskStatusEnum = UserRoles.Models.Enums.TaskStatus;


[Authorize]
public class TasksController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<Users> _userManager;
    private readonly ITaskHistoryService _historyService;
    private readonly IHubContext<TaskHub> _hubContext;

    public TasksController(AppDbContext context, UserManager<Users> userManager, ITaskHistoryService historyService, IHubContext<TaskHub> hubContext)
    {
        _context = context;
        _userManager = userManager;
        _historyService = historyService;
        _hubContext = hubContext;
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
            Title = model.Title?.Trim() ?? string.Empty,
            Description = model.Description?.Trim() ?? string.Empty,

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
            CreatedAt = DateTime.UtcNow,
            CurrentColumnEntryAt = DateTime.UtcNow, // Moved here!
            DueDate = model.DueDate.HasValue ? DateTime.SpecifyKind(model.DueDate.Value, DateTimeKind.Utc) : null
        };


        _context.TaskItems.Add(task);
        await _context.SaveChangesAsync();

        // Log task creation
        await _historyService.LogTaskCreated(task.Id, user.Id);
        await _context.SaveChangesAsync();

        // Save custom field values if provided
        if (model.CustomFieldValues != null && model.CustomFieldValues.Any())
        {
            int imageCount = model.CustomFieldValues.Values
                .SelectMany(v => v)
                .Count(val => !string.IsNullOrEmpty(val) && (val.StartsWith("data:image/") || val.StartsWith("/Tasks/GetFieldImage")));
            
            if (imageCount > 2) return BadRequest("Maximum of 2 images allowed per task.");

            foreach (var kvp in model.CustomFieldValues)
            {
                int fieldId = kvp.Key;
                foreach (var val in kvp.Value)
                {
                    if (string.IsNullOrEmpty(val)) continue;

                    var customFieldValue = new TaskFieldValue
                    {
                        TaskId = task.Id,
                        FieldId = fieldId,
                        Value = val,
                        CreatedAt = DateTime.UtcNow
                    };

                    // Handle Image Data if it's a Base64 string
                    if (val.StartsWith("data:image/"))
                    {
                        try
                        {
                            var parts = val.Split(',');
                            if (parts.Length > 1)
                            {
                                var header = parts[0];
                                var base64Data = parts[1];
                                var mimeType = header.Split(':')[1].Split(';')[0];
                                customFieldValue.ImageData = Convert.FromBase64String(base64Data);
                                customFieldValue.ImageMimeType = mimeType;
                                customFieldValue.FileName = $"upload_{fieldId}_{DateTime.UtcNow.Ticks}";
                                _context.TaskFieldValues.Add(customFieldValue);
                                await _context.SaveChangesAsync(); // Need ID for URL
                                customFieldValue.Value = $"/Tasks/GetFieldImageById/{customFieldValue.Id}";
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing image for field {fieldId}: {ex.Message}");
                        }
                    }
                    else
                    {
                        _context.TaskFieldValues.Add(customFieldValue);
                    }
                }
            }
            await _context.SaveChangesAsync();
        }

        // SignalR: Notify team that a new task was added
        try
        {
            await _hubContext.Clients.Group(column.TeamName).SendAsync("TaskAdded", new
            {
                taskId = task.Id,
                columnId = column.Id,
                teamName = column.TeamName
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SignalR Error (TaskAdded): {ex.Message}");
        }


        return Ok(new
        {
            success = true,
            message = "Task created successfully",
            workItemId = workItemId
        });

    }


    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetTaskCardPartial(int taskId)
    {
        var task = await _context.TaskItems
            .AsNoTracking()
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .Include(t => t.ReviewedByUser)
            .Include(t => t.CompletedByUser)
            .Include(t => t.CustomFieldValues)
                .ThenInclude(cfv => cfv.Field)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null) return NotFound();

        var team = await _context.Teams.FirstOrDefaultAsync(t => t.Name == task.TeamName);
        var users = await _userManager.Users.ToListAsync();

        ViewData["TeamSettings"] = team;
        ViewData["ColumnName"] = (await _context.TeamColumns.FindAsync(task.ColumnId))?.ColumnName;
        ViewData["AssignableUsers"] = users;

        // Load permissions for this user on this team
        var currentUser = await _userManager.GetUserAsync(User);
        BoardPermission? permissions = null;
        if (currentUser != null)
        {
            permissions = await _context.BoardPermissions
                .Where(p => p.UserId == currentUser.Id && p.TeamName.ToLower().Trim() == task.TeamName.ToLower().Trim())
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();
        }
        ViewData["UserPermissions"] = permissions;

        return PartialView("_TaskCard", task);
    }


    [HttpPost]
    [Authorize]
    public async Task<IActionResult> AssignTask(int taskId, string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("UserId is required");

        // userId format might be "user:ID" or just "ID"
        if (userId.StartsWith("user:")) userId = userId.Substring(5);

        var taskToUpdate = await _context.TaskItems.FindAsync(taskId);
        if (taskToUpdate == null) return NotFound();

        // Check permission: Admin or granted CanAssignTask
        if (!User.IsInRole("Admin") && !await AuthorizeBoardAction(taskToUpdate.TeamName, "AssignTask"))
        {
            return Forbid();
        }

        var assignToUser = await _userManager.FindByIdAsync(userId);
        if (assignToUser == null)
            return BadRequest("Invalid user");

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized();

        var oldTeam = taskToUpdate.TeamName;
        var oldColumnId = taskToUpdate.ColumnId;

        // ✅ Check if target user belongs to a different team
        var targetUserTeam = await _context.UserTeams
            .Where(ut => ut.UserId == userId)
            .FirstOrDefaultAsync();

        bool teamChanged = false;
        if (targetUserTeam != null && targetUserTeam.TeamName != taskToUpdate.TeamName)
        {
            // Cross-team assignment: move task to target user's team
            var newTeam = targetUserTeam.TeamName;
            var targetColumn = (await _context.TeamColumns
                .Where(c => c.TeamName == newTeam)
                .ToListAsync())
                .OrderBy(c =>
                {
                    var name = c.ColumnName?.Trim().ToLower();
                    if (name == "review") return 1000;
                    if (name == "completed") return 1001;
                    return c.Order;
                })
                .FirstOrDefault();

            if (targetColumn != null)
            {
                taskToUpdate.TeamName = newTeam;
                taskToUpdate.ColumnId = targetColumn.Id;
                teamChanged = true;
            }
        }

        // ✅ LOG ASSIGNMENT
        await _historyService.LogAssignment(taskToUpdate.Id, assignToUser.Id, currentUser.Id);

        taskToUpdate.AssignedToUserId = assignToUser.Id;
        taskToUpdate.AssignedByUserId = currentUser.Id;
        taskToUpdate.AssignedAt = DateTime.UtcNow;
        taskToUpdate.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // 🚀 BROADCAST SIGNALR
        if (teamChanged)
        {
            // Remove from old team board
            await _hubContext.Clients.Group(oldTeam).SendAsync("TaskRemoved", new { taskId = taskId, teamName = oldTeam });
            // Add to new team board
            await _hubContext.Clients.Group(taskToUpdate.TeamName).SendAsync("TaskAdded", new { taskId = taskId, teamName = taskToUpdate.TeamName, columnId = taskToUpdate.ColumnId });
        }
        else
        {
            // Simple update for same team
            await _hubContext.Clients.Group(taskToUpdate.TeamName).SendAsync("TaskAssigned", new
            {
                taskId = taskId,
                assignedTo = assignToUser.UserName,
                assignedBy = currentUser.Email,
                assignedAt = taskToUpdate.AssignedAt.Value.ToString("dd MMM yyyy, hh:mm tt")
            });
        }

        return Ok(new
        {
            success = true,
            assignedTo = assignToUser.UserName,
            assignedBy = currentUser.UserName,
            assignedAt = taskToUpdate.AssignedAt.Value.ToString("dd MMM yyyy, hh:mm tt"),
            teamMoved = teamChanged,
            newTeam = taskToUpdate.TeamName
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
            await _historyService.LogTaskUpdated(task.Id, user.Id, "Title", task.Title, model.Title?.Trim() ?? string.Empty);
            task.Title = model.Title?.Trim() ?? string.Empty;
        }

        // 2. Description
        if (task.Description != model.Description?.Trim())
        {
            // Just logging that it changed, or the full value if needed. 
            // _historyService.LogTaskUpdated(task.Id, user.Id, "Description", task.Description, model.Description?.Trim());
            await _historyService.LogTaskUpdated(task.Id, user.Id, "Description", "Old Description", "New Description");
            task.Description = model.Description?.Trim() ?? string.Empty;
        }

        task.UpdatedAt = DateTime.UtcNow;

        if (task.DueDate != model.DueDate)
        {
            await _historyService.LogTaskUpdated(task.Id, user.Id, "Due Date",
                task.DueDate?.ToString("dd MMM yyyy, hh:mm tt") ?? "None",
                model.DueDate?.ToString("dd MMM yyyy, hh:mm tt") ?? "None");

            task.DueDate = model.DueDate.HasValue
                ? DateTime.SpecifyKind(model.DueDate.Value, DateTimeKind.Utc)
                : null;
        }

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
            if (User.IsInRole("Admin") || await AuthorizeBoardAction(task.TeamName, "AssignTask"))
            {
                if (task.AssignedToUserId != model.AssignedToUserId)
                {
                    await _historyService.LogAssignment(task.Id, model.AssignedToUserId, user.Id);
                    task.AssignedToUserId = model.AssignedToUserId;
                }
            }
        }

        // 4. Update custom field values
        if (model.CustomFieldValues != null)
        {
            // Get existing values for this task
            var existingFieldValues = await _context.TaskFieldValues
                .Where(v => v.TaskId == task.Id)
                .ToListAsync();

            var providedFieldIds = model.CustomFieldValues.Keys.ToList();

            // Check total image count limit (2)
            int totalImages = model.CustomFieldValues.Values
                .SelectMany(v => v)
                .Count(val => !string.IsNullOrEmpty(val) && (val.StartsWith("data:image/") || val.Contains("/Tasks/GetFieldImage")));
            
            if (totalImages > 2) return BadRequest("Maximum of 2 images allowed per task.");

            // 1. Update or Insert
            foreach (var kvp in model.CustomFieldValues)
            {
                int fieldId = kvp.Key;
                var newValues = kvp.Value.Where(v => !string.IsNullOrEmpty(v)).ToList();

                // Check if field exists
                var fieldDef = await _context.TaskCustomFields.FindAsync(fieldId);
                if (fieldDef == null) continue;

                var existingRecords = await _context.TaskFieldValues
                    .Where(v => v.TaskId == task.Id && v.FieldId == fieldId)
                    .ToListAsync();

                // Identify values to REMOVE
                var recordsToRemove = existingRecords.Where(r => !newValues.Contains(r.Value)).ToList();
                if (recordsToRemove.Any())
                {
                    _context.TaskFieldValues.RemoveRange(recordsToRemove);
                }

                // Identify values to ADD
                foreach (var val in newValues)
                {
                    // Check if this value already exists in records
                    bool alreadyExists = existingRecords.Any(r => r.Value == val);
                    if (alreadyExists) continue;

                    // It's a NEW value
                    var newVal = new TaskFieldValue
                    {
                        TaskId = task.Id,
                        FieldId = fieldId,
                        Value = val,
                        CreatedAt = DateTime.UtcNow
                    };

                    // Handle Image Data if it's a Base64 string
                    if (val.StartsWith("data:image/"))
                    {
                        try
                        {
                            var parts = val.Split(',');
                            if (parts.Length > 1)
                            {
                                var header = parts[0];
                                var base64Data = parts[1];
                                var mimeType = header.Split(':')[1].Split(';')[0];
                                newVal.ImageData = Convert.FromBase64String(base64Data);
                                newVal.ImageMimeType = mimeType;
                                newVal.FileName = $"upload_{fieldId}_{DateTime.UtcNow.Ticks}";
                                _context.TaskFieldValues.Add(newVal);
                                await _context.SaveChangesAsync(); // Need ID for URL
                                newVal.Value = $"/Tasks/GetFieldImageById/{newVal.Id}";
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing image for field {fieldId}: {ex.Message}");
                        }
                    }
                    else
                    {
                        _context.TaskFieldValues.Add(newVal);
                    }
                    
                    await _historyService.LogCustomFieldChange(task.Id, fieldDef.FieldName, "(added)", val, user.Id);
                }
            }
        }

        await _context.SaveChangesAsync();

        // SignalR: Notify team that the task was updated (refresh card)
        try
        {
            await _hubContext.Clients.Group(task.TeamName).SendAsync("TaskAssigned", new
            {
                taskId = task.Id,
                assignedTo = task.AssignedToUser?.Name ?? task.AssignedToUser?.UserName ?? "Unassigned"
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SignalR Error (TaskUpdate): {ex.Message}");
        }

        // ✅ RETURN JSON (IMPORTANT)
        return Json(new { success = true });
    }


    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetTask(int id)
    {
        var task = await _context.TaskItems
            .AsNoTracking()
            .Include(t => t.CustomFieldValues)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null) return NotFound();

        // Safety: Group by FieldId to support multiple values (like multiple images)
        var fieldValuesMap = task.CustomFieldValues
            .GroupBy(v => v.FieldId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(v => v.Value ?? "").ToList()
            );

        // 1. Get basic info
        var result = new
        {
            id = task.Id,
            title = task.Title,
            description = task.Description,
            priority = (int)task.Priority,
            assignedToUserId = task.AssignedToUserId,
            dueDate = task.DueDate?.ToString("yyyy-MM-ddTHH:mm"), // for datetime-local input
            // 2. Convert custom fields to dictionary of LISTS for easy JS consumption
            customFieldValues = fieldValuesMap
        };

        return Ok(result);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetTaskDetail(int id)
    {
        var task = await _context.TaskItems
            .AsNoTracking()
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
            CreatedAtFormatted = task.CreatedAt.ToString("dd MMM yyyy, hh:mm tt"),
            AssignedAtFormatted = task.AssignedAt?.ToString("dd MMM yyyy, hh:mm tt"),
            ReviewedAtFormatted = task.ReviewedAt?.ToString("dd MMM yyyy, hh:mm tt"),
            CompletedAtFormatted = task.CompletedAt?.ToString("dd MMM yyyy, hh:mm tt"),
            DueDateFormatted = task.DueDate?.ToString("dd MMM, hh:mm tt"),
            CustomFields = task.CustomFieldValues?.Select(fv => new
            {
                FieldName = fv.Field?.FieldName,
                FieldType = fv.Field?.FieldType,
                Value = fv.Field?.FieldType == "DateTime" && DateTime.TryParse(fv.Value, out var dt) 
                        ? dt.ToString("dd MMM yyyy, hh:mm tt") 
                        : fv.Value
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




    [HttpGet]
    public async Task<IActionResult> AssignedTasksOverview(string team)
    {
        if (string.IsNullOrEmpty(team))
            return BadRequest("Team name is required");

        // Fetch tasks
        var tasks = await _context.TaskItems
            .AsNoTracking()
            .Include(t => t.AssignedByUser)
            .Include(t => t.AssignedToUser)
            .Where(t => t.TeamName == team && !t.IsArchived && t.AssignedToUserId != null)
            .OrderByDescending(t => t.AssignedAt)
            .ToListAsync();

        // 🔥 PERFORMANCE: Bulk-fetch ALL users, teams, and roles once
        var allUsers = await _userManager.Users.AsNoTracking().ToListAsync();
        var allTeams = await _context.Teams.AsNoTracking().ToListAsync();
        var allUserTeams = await _context.UserTeams.AsNoTracking().ToListAsync();
        
        var userRolesData = await (from ur in _context.UserRoles
                                   join r in _context.Roles on ur.RoleId equals r.Id
                                   select new { ur.UserId, RoleName = r.Name })
                                  .AsNoTracking()
                                  .ToListAsync();

        var userRolesMap = userRolesData
            .GroupBy(ur => ur.UserId)
            .ToDictionary(g => g.Key, g => g.Select(ur => ur.RoleName).ToList());

        var groupedMembers = new List<dynamic>();
        var userTeamsLookup = allUserTeams.ToLookup(ut => ut.TeamName);

        foreach (var t in allTeams)
        {
            var userIdsInTeam = userTeamsLookup[t.Name].Select(ut => ut.UserId).ToList();
            var teamUsers = allUsers.Where(u => userIdsInTeam.Contains(u.Id)).ToList();

            foreach (var u in teamUsers)
            {
                var roles = userRolesMap.GetValueOrDefault(u.Id, new List<string>());
                var primaryRole = roles.FirstOrDefault() ?? "User";
                bool isManagement = roles.Contains("Admin") || roles.Contains("Manager") || roles.Contains("Sub-Manager") || roles.Contains("SubManager");

                if (roles.Contains("Sub-Manager") || roles.Contains("SubManager") || (primaryRole == "Manager" && !string.IsNullOrEmpty(u.ParentUserId)))
                    primaryRole = "Sub-Manager";
                else if (roles.Contains("Admin"))
                    primaryRole = "Admin";
                else if (roles.Contains("Manager"))
                    primaryRole = "Manager";

                groupedMembers.Add(new
                {
                    TeamName = t.Name,
                    u.Id,
                    u.UserName,
                    Name = u.Name ?? u.UserName,
                    Role = primaryRole,
                    Category = isManagement ? "Management" : "Team Members"
                });
            }
        }

        // Sort: Team Name, then Category (Management first), then Role Priority, then Name
        var rolePriority = new Dictionary<string, int> { { "Admin", 1 }, { "Manager", 2 }, { "Sub-Manager", 3 }, { "User", 4 } };
        var finalMemberList = groupedMembers
            .OrderBy(m => (string)m.TeamName)
            .ThenBy(m => (string)m.Category == "Management" ? 1 : 2)
            .ThenBy(m => rolePriority.ContainsKey((string)m.Role) ? rolePriority[(string)m.Role] : 99)
            .ThenBy(m => (string)m.Name)
            .ToList();

        ViewBag.TeamName = team;
        ViewBag.Members = finalMemberList;
        return PartialView("_AssignedTasksOverview", tasks);
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

        // Load all tasks for this team strictly (AsNoTracking for performance)
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

        // 🔥 PERFORMANCE: Bulk-fetch roles for ALL users in one query
        var userRolesData = await (from ur in _context.UserRoles
                                   join r in _context.Roles on ur.RoleId equals r.Id
                                   select new { ur.UserId, RoleName = r.Name })
                                  .AsNoTracking()
                                  .ToListAsync();

        var userRolesMap = userRolesData
            .GroupBy(ur => ur.UserId)
            .ToDictionary(g => g.Key, g => (IList<string>)g.Select(ur => ur.RoleName).ToList());

        // Pre-fetch hierarchy for visibility check (AsNoTracking)
        var userHierarchy = await _userManager.Users
            .AsNoTracking()
            .Select(u => new { u.Id, u.ManagerId })
            .ToDictionaryAsync(u => u.Id, u => u.ManagerId);

        var visibleTasks = new List<TaskItem>();
        var viewerRoles = userRolesMap.GetValueOrDefault(user.Id, new List<string>());

        foreach (var task in allTasks)
        {
            if (CanUserSeeTaskOptimized(task, user.Id, viewerRoles, userRolesMap, userHierarchy))
                visibleTasks.Add(task);
        }

        // Attach filtered tasks to columns
        foreach (var col in columns)
        {
            col.Tasks = visibleTasks
                .Where(t => t.ColumnId == col.Id)
                .ToList();
        }

        // ✅ 1. Load assignable users (AsNoTracking)
        var allUsers = await _userManager.Users.AsNoTracking().OrderBy(u => u.Name).ToListAsync();

        // Fetch UserIds belonging to the current team
        var teamUserIds = await _context.UserTeams
            .AsNoTracking()
            .Where(ut => ut.TeamName == team)
            .Select(ut => ut.UserId)
            .ToListAsync();

        // Pre-calculate primary roles for all users for filtering/FE
        var feUserRolesMap = new Dictionary<string, string>();
        foreach (var u in allUsers)
        {
            var r = userRolesMap.GetValueOrDefault(u.Id, new List<string>());
            var primaryRole = r.FirstOrDefault() ?? "User";

            if (r.Contains("Sub-Manager") || r.Contains("SubManager") || (primaryRole == "Manager" && !string.IsNullOrEmpty(u.ParentUserId)))
                feUserRolesMap[u.Id] = "Sub-Manager";
            else if (r.Contains("Admin"))
                feUserRolesMap[u.Id] = "Admin";
            else if (primaryRole == "Manager")
                feUserRolesMap[u.Id] = "Manager";
            else
                feUserRolesMap[u.Id] = primaryRole;
        }

        // Populate FilteredAssignees (Hierarchical + Team restricted)
        var filteredAssignees = new List<Users>();
        if (viewerRoles.Contains("Admin"))
        {
            filteredAssignees = allUsers.Where(u => teamUserIds.Contains(u.Id)).ToList();
        }
        else if (viewerRoles.Contains("Manager"))
        {
            var accessibleIds = new HashSet<string> { user.Id };
            var directSubordinates = allUsers.Where(u => u.ParentUserId == user.Id).Select(u => u.Id).ToList();
            foreach(var id in directSubordinates) accessibleIds.Add(id);
            var indirect = allUsers.Where(u => directSubordinates.Contains(u.ParentUserId ?? "")).Select(u => u.Id);
            foreach(var id in indirect) accessibleIds.Add(id);

            filteredAssignees = allUsers.Where(u => accessibleIds.Contains(u.Id) && teamUserIds.Contains(u.Id)).ToList();
        }
        else if (viewerRoles.Contains("Sub-Manager") || viewerRoles.Contains("SubManager"))
        {
            var accessibleIds = new HashSet<string> { user.Id };
            var subs = allUsers.Where(u => u.ParentUserId == user.Id).Select(u => u.Id);
            foreach(var id in subs) accessibleIds.Add(id);

            filteredAssignees = allUsers.Where(u => accessibleIds.Contains(u.Id) && teamUserIds.Contains(u.Id)).ToList();
        }
        else
        {
            filteredAssignees = allUsers.Where(u => u.Id == user.Id && teamUserIds.Contains(u.Id)).ToList();
        }

        // Role priority for sorting
        var rolePriority = new Dictionary<string, int> { { "Admin", 1 }, { "Manager", 2 }, { "Sub-Manager", 3 }, { "User", 4 } };

        filteredAssignees = filteredAssignees
            .OrderBy(u => rolePriority.GetValueOrDefault(feUserRolesMap.GetValueOrDefault(u.Id, "User"), 99))
            .ThenBy(u => u.Name ?? u.UserName)
            .ToList();

        // Assignors: Anyone with Management role
        var assignors = allUsers
            .Where(u => feUserRolesMap.ContainsKey(u.Id) &&
                (feUserRolesMap[u.Id] == "Admin" || feUserRolesMap[u.Id] == "Manager" || feUserRolesMap[u.Id] == "Sub-Manager"))
            .OrderBy(u => rolePriority.GetValueOrDefault(feUserRolesMap[u.Id], 99))
            .ThenBy(u => u.Name ?? u.UserName)
            .ToList();

        // Load active custom fields
        var customFields = await _context.TaskCustomFields
            .AsNoTracking()
            .Where(f => f.IsActive && f.TeamName == team)
            .OrderBy(f => f.Order)
            .ToListAsync();

        var teamSettings = await _context.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Name == team);

        var userPerms = await _context.BoardPermissions
            .AsNoTracking()
            .Where(p => p.UserId == user.Id && p.TeamName != null && p.TeamName.ToLower().Trim() == team.ToLower().Trim())
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync();

        var vm = new TeamBoardViewModel
        {
            TeamName = team,
            Columns = columns,
            AssignableUsers = allUsers, // Industry standard: can assign to anyone (cross-team)
            FilteredAssignees = filteredAssignees,
            Assignors = assignors,
            AllTeamNames = await _context.TeamColumns.AsNoTracking().Select(c => c.TeamName).Distinct().ToListAsync(),
            CustomFields = customFields,
            UserPermissions = userPerms,
            TeamSettings = teamSettings,
            UserRolesMap = feUserRolesMap
        };

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

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> DeleteAllTasksInColumn([FromBody] int columnId)
    {
        if (columnId <= 0) return BadRequest();

        var column = await _context.TeamColumns.FindAsync(columnId);
        if (column == null) return NotFound();

        // Check permission
        if (!User.IsInRole("Admin"))
        {
            if (!await AuthorizeBoardAction(column.TeamName, "DeleteColumn")) // Reusing delete column permission for bulk delete
                return Forbid();
        }

        var tasks = await _context.TaskItems
            .Where(t => t.ColumnId == columnId)
            .ToListAsync();

        if (tasks.Any())
        {
            _context.TaskItems.RemoveRange(tasks);
            await _context.SaveChangesAsync();
        }

        return Ok(new { success = true, count = tasks.Count });
    }

    private bool CanUserSeeTaskOptimized(TaskItem task, string currentUserId, IList<string> viewerRoles, Dictionary<string, IList<string>> userRolesMap, Dictionary<string, string?> hierarchyMap)
    {
        if (viewerRoles.Contains("Admin"))
            return true;

        // Creator, Assignee, and Assignor always see their tasks
        if (task.CreatedByUserId == currentUserId || task.AssignedToUserId == currentUserId || task.AssignedByUserId == currentUserId)
            return true;

        // Managerial Role Check (Hierarchy based)
        bool isManagerOrSub = viewerRoles.Contains("Manager") || viewerRoles.Contains("Sub-Manager");
        if (isManagerOrSub)
        {
            // Does the creator or assignee report to the current manager?
            if (IsUnderManager(task.CreatedByUserId, currentUserId, hierarchyMap) ||
                IsUnderManager(task.AssignedToUserId, currentUserId, hierarchyMap))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsUnderManager(string? userId, string managerId, Dictionary<string, string?> hierarchyMap)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(managerId)) return false;

        string? currentUserIdToCheck = userId;
        int maxDepth = 20; // Safety against cycles
        int depth = 0;

        while (hierarchyMap.TryGetValue(currentUserIdToCheck, out var parentId) && !string.IsNullOrEmpty(parentId) && depth < maxDepth)
        {
            if (parentId == managerId) return true;
            currentUserIdToCheck = parentId;
            depth++;
        }
        return false;
    }

    // Keep original for back-compat if needed elsewhere, but mark as obsolete or refactor
    private async Task<bool> CanUserSeeTask(TaskItem task, Users currentUser)
    {
        if (task.AssignedToUserId == currentUser.Id) return true;
        if (task.CreatedByUserId == currentUser.Id) return true;

        var roles = await _userManager.GetRolesAsync(currentUser);
        if (roles.Contains("Admin")) return true;

        var userHierarchy = await _userManager.Users
            .Select(u => new { u.Id, u.ManagerId })
            .ToDictionaryAsync(u => u.Id, u => u.ManagerId);

        var map = new Dictionary<string, IList<string>> { { currentUser.Id, roles } };
        // We don't really need the userRolesMap for the new hierarchical logic unless we want to keep the old role logic
        // but the requirement is "only to user and admin and their manager".

        return CanUserSeeTaskOptimized(task, currentUser.Id, roles, map, userHierarchy);
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
        int oldColId = task.ColumnId;

        // ══════ STRICT PERMISSION CHECK ══════
        if (!await CanUserMoveTask(task, user, targetColumn.Id))
        {
            return StatusCode(403, "you have not acess to move");
        }

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

        // 2. REVIEW column: Check hierarchy
        if (targetColName == "review")
        {
            // Already checked by global CanUserMoveTask above, but kept for clarity/legacy
        }

        // 3. Moving FROM review back (fail rework): allowed if task was failed
        if (sourceColName == "review" && targetColName != "completed" && !isAdmin)
        {
            // Non-admin can only move back if review was failed
            if (task.ReviewStatus != UserRoles.Models.Enums.ReviewStatus.Failed &&
                task.ReviewStatus != UserRoles.Models.Enums.ReviewStatus.None)
            {
                // Already checked globally, but can override with specific message if needed
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

        // 🚀 BROADCAST UPDATE
        await _hubContext.Clients.Group(task.TeamName).SendAsync("TaskMoved", new
        {
            taskId = task.Id,
            oldColumnId = oldColId,
            newColumnId = task.ColumnId,
            columnName = targetColumn.ColumnName
        });

        return Ok(new { success = true, message = "Task moved successfully" });
    }

    // ═══════ HIERARCHY-BASED MOVE PERMISSION CHECK ═══════
    private async Task<bool> CanUserMoveTask(TaskItem task, Users currentUser, int? targetColumnId = null)
    {
        // Admin can do anything
        if (await _userManager.IsInRoleAsync(currentUser, "Admin"))
            return true;

        // Fetch granular permissions specifically for this user and team
        var boardPerm = await _context.BoardPermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == currentUser.Id && p.TeamName.ToLower().Trim() == task.TeamName.ToLower().Trim());

        // ══════ Specific Granular Transitions ══════
        // If the admin has defined ANY transitions for this user, we follow them STRICTLY.
        if (boardPerm != null && !string.IsNullOrEmpty(boardPerm.AllowedTransitionsJson) && targetColumnId.HasValue)
        {
            try
            {
                var transitions = JsonSerializer.Deserialize<Dictionary<int, List<int>>>(boardPerm.AllowedTransitionsJson);
                if (transitions != null)
                {
                    // Check if current column has ANY allowed targets
                    if (transitions.TryGetValue(task.ColumnId, out var allowedTargets))
                    {
                        if (allowedTargets.Contains(targetColumnId.Value))
                            return true;
                    }
                    
                    // If a transition map exists (even empty), and this move isn't allowed, return FALSE.
                    // This creates a "restricted" mode for the user.
                    return false;
                }
            }
            catch { /* corrupted JSON fallback to default rules below */ }
        }

        // ══════ Default Rules (Owner/Hierarchy) ══════
        // User can move their own assigned tasks
        bool isOwner = task.AssignedToUserId == currentUser.Id || task.CreatedByUserId == currentUser.Id;

        var userRoles = await _userManager.GetRolesAsync(currentUser);

        // Manager: can move tasks of users under them
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

                // 🚀 BROADCAST UPDATE (Review Result)
                await _hubContext.Clients.Group(task.TeamName).SendAsync("TaskReviewed", new
                {
                    taskId = task.Id,
                    passed = true,
                    newColumnId = task.ColumnId,
                    columnName = "Completed",
                    reviewNote = task.ReviewNote,
                    reviewedBy = user.UserName
                });

                return Ok(new { success = true, passed = true, message = "Review passed! Task automatically moved to Completed." });
            }

            // Normal pass without auto-move (edge case)
            await _context.SaveChangesAsync();
            await _hubContext.Clients.Group(task.TeamName).SendAsync("TaskReviewed", new
            {
                taskId = task.Id,
                passed = true,
                newColumnId = task.ColumnId,
                columnName = task.Column?.ColumnName,
                reviewNote = task.ReviewNote,
                reviewedBy = user.UserName
            });

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

            // 🚀 BROADCAST UPDATE (Review Result)
            await _hubContext.Clients.Group(task.TeamName).SendAsync("TaskReviewed", new
            {
                taskId = task.Id,
                passed = model.Passed,
                newColumnId = task.ColumnId,
                columnName = task.Column?.ColumnName ?? "Completed",
                reviewNote = task.ReviewNote,
                reviewedBy = user.UserName
            });

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

        // Check permission if not Admin
        if (!User.IsInRole("Admin"))
        {
            if (!await AuthorizeBoardAction(task.TeamName, "AssignTask"))
                return Forbid();
        }

        // 1. Find the FIRST column for the target team
        var targetColumn = (await _context.TeamColumns
            .Where(c => c.TeamName == model.TeamName)
            .ToListAsync())
            .OrderBy(c =>
            {
                var name = c.ColumnName?.Trim().ToLower();
                if (name == "review") return 1000;
                if (name == "completed") return 1001;
                return c.Order;
            })
            .FirstOrDefault();

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
    public async Task<IActionResult> GetCustomFields(string? team)
    {
        var fields = await _context.TaskCustomFields
            .Where(f => f.IsActive && f.TeamName == team)
            .OrderBy(f => f.Order)
            .Select(f => new
            {
                f.Id,
                f.FieldName,
                f.FieldType,
                f.IsRequired,
                f.DropdownOptions,
                f.Order,
                f.TeamName
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
        public string? TeamName { get; set; }
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
            CreatedAt = DateTime.UtcNow,
            TeamName = model.TeamName
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

        var users = await _userManager.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();
        var perms = await _context.BoardPermissions
            .AsNoTracking()
            .Where(p => p.TeamName.ToLower().Trim() == team.ToLower().Trim())
            .ToListAsync();

        // 🔥 PERFORMANCE: Bulk-fetch roles
        var userRolesData = await (from ur in _context.UserRoles
                                   join r in _context.Roles on ur.RoleId equals r.Id
                                   select new { ur.UserId, RoleName = r.Name })
                                  .AsNoTracking()
                                  .ToListAsync();

        var userRolesMap = userRolesData
            .GroupBy(ur => ur.UserId)
            .ToDictionary(g => g.Key, g => g.Select(ur => ur.RoleName).ToList());

        // Get the set of user IDs that belong to this specific team
        var teamUserIds = await _context.UserTeams
            .AsNoTracking()
            .Where(ut => ut.TeamName == team)
            .Select(ut => ut.UserId)
            .ToHashSetAsync();

        var result = new List<BoardPermissionDto>();

        foreach (var u in users)
        {
            var rawRoles = userRolesMap.GetValueOrDefault(u.Id, new List<string>());
            if (rawRoles.Contains("Admin")) continue; // 🚫 Hide Admins from permissions dashboard

            string primaryRole = rawRoles.FirstOrDefault() ?? "User";
            // Distinguish Sub Manager: role is "Manager" + has ParentUserId
            if (primaryRole == "Manager" && !string.IsNullOrEmpty(u.ParentUserId))
            {
                primaryRole = "Sub Manager";
            }

            // 🔒 Scoping rule:
            // - Manager (no ParentUserId): always show — they oversee all teams
            // - Sub Manager / User: only show if they belong to this team
            bool isTopLevelManager = primaryRole == "Manager";
            if (!isTopLevelManager && !teamUserIds.Contains(u.Id))
                continue;

            var p = perms.FirstOrDefault(x => x.UserId == u.Id);

            result.Add(new BoardPermissionDto
            {
                UserId = u.Id,
                UserName = u.UserName ?? "Unknown",
                Role = primaryRole,
                TeamName = team,
                CanAddColumn = p?.CanAddColumn ?? false,
                CanRenameColumn = p?.CanRenameColumn ?? false,
                CanReorderColumns = p?.CanReorderColumns ?? false,
                CanDeleteColumn = p?.CanDeleteColumn ?? false,
                CanEditAllFields = p?.CanEditAllFields ?? false,
                CanDeleteTask = p?.CanDeleteTask ?? false,
                CanReviewTask = p?.CanReviewTask ?? false,
                CanImportExcel = p?.CanImportExcel ?? false,
                CanAssignTask = p?.CanAssignTask ?? false,
                AllowedTransitions = !string.IsNullOrEmpty(p?.AllowedTransitionsJson) 
                    ? JsonSerializer.Deserialize<Dictionary<int, List<int>>>(p.AllowedTransitionsJson) ?? new()
                    : new()
            });
        }

        // Sort: Manager (1) -> Sub Manager (2) -> User (3)
        var rolePriority = new Dictionary<string, int>
        {
            { "Manager", 1 },
            { "Sub Manager", 2 },
            { "User", 3 }
        };

        var sortedResult = result
            .OrderBy(r => rolePriority.GetValueOrDefault(r.Role, 99))
            .ThenBy(r => r.UserName)
            .ToList();

        return Ok(sortedResult);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> UpdateBoardPermission([FromBody] BoardPermissionDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.UserId) || string.IsNullOrWhiteSpace(dto.TeamName))
            return BadRequest();

        // Get all records for this user/team robustly
        var allExisting = await _context.BoardPermissions
            .Where(p => p.UserId == dto.UserId && p.TeamName.ToLower().Trim() == dto.TeamName.ToLower().Trim())
            .ToListAsync();

        var existing = allExisting.OrderByDescending(p => p.Id).FirstOrDefault();

        if (existing == null)
        {
            existing = new BoardPermission
            {
                UserId = dto.UserId,
                TeamName = dto.TeamName.Trim() // Sanitize on save
            };
            _context.BoardPermissions.Add(existing);
        }
        else
        {
            // Cleanup duplicates if they exist
            if (allExisting.Count > 1)
            {
                var duplicates = allExisting.Where(p => p.Id != existing.Id).ToList();
                _context.BoardPermissions.RemoveRange(duplicates);
            }

            // Ensure team name is sanitized/standardized on the one we keep
            existing.TeamName = dto.TeamName.Trim();
        }

        existing.CanAddColumn = dto.CanAddColumn;
        existing.CanRenameColumn = dto.CanRenameColumn;
        existing.CanReorderColumns = dto.CanReorderColumns;
        existing.CanDeleteColumn = dto.CanDeleteColumn;
        existing.CanEditAllFields = dto.CanEditAllFields;
        existing.CanDeleteTask = dto.CanDeleteTask;
        existing.CanReviewTask = dto.CanReviewTask;
        existing.CanImportExcel = dto.CanImportExcel;
        existing.CanAssignTask = dto.CanAssignTask;
        existing.AllowedTransitionsJson = JsonSerializer.Serialize(dto.AllowedTransitions ?? new());

        await _context.SaveChangesAsync();

        // Broadcast permission update to the user
        await _hubContext.Clients.All.SendAsync("PermissionsUpdated", dto.UserId, dto.TeamName);

        return Ok(new { success = true });
    }

    private async Task<bool> AuthorizeBoardAction(string teamName, string action)
    {
        if (User.IsInRole("Admin")) return true;

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return false;

        var perms = await _context.BoardPermissions
            .Where(p => p.UserId == user.Id && p.TeamName.ToLower().Trim() == teamName.ToLower().Trim())
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync();

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
            "ImportExcel" => perms.CanImportExcel,
            "AssignTask" => perms.CanAssignTask,
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

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> UpdateTeamSettings([FromBody] UpdateTeamSettingsRequest model)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.TeamName))
            return BadRequest();

        var team = await _context.Teams.FirstOrDefaultAsync(t => t.Name == model.TeamName);
        if (team == null)
        {
            // Create if missing (lazy initialization)
            team = new Team { Name = model.TeamName };
            _context.Teams.Add(team);
        }

        team.IsPriorityVisible = model.IsPriorityVisible;
        team.IsDueDateVisible = model.IsDueDateVisible;
        team.IsTitleVisible = model.IsTitleVisible;
        team.IsDescriptionVisible = model.IsDescriptionVisible;

        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> UploadCustomFieldImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        // Enforce 2MB limit (user request)
        if (file.Length > 2 * 1024 * 1024)
            return BadRequest("File size exceeds 2MB limit");

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLower();
        if (!allowedExtensions.Contains(extension))
            return BadRequest("Invalid file type");

        // Instead of saving to disk, convert to Base64 for the frontend to send back during Task creation/update
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            var base64String = Convert.ToBase64String(fileBytes);
            var contentType = file.ContentType;
            var dataUrl = $"data:{contentType};base64,{base64String}";

            return Ok(new { success = true, url = dataUrl });
        }
    }

    [HttpGet]
    [Route("Tasks/GetFieldImage/{taskId}/{fieldId}")]
    [Route("Tasks/GetFieldImageById/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFieldImage(int? taskId, int? fieldId, int? id)
    {
        TaskFieldValue fieldValue = null;

        if (id.HasValue)
        {
            fieldValue = await _context.TaskFieldValues.FindAsync(id.Value);
        }
        else if (taskId.HasValue && fieldId.HasValue)
        {
            fieldValue = await _context.TaskFieldValues
                .OrderByDescending(v => v.Id)
                .FirstOrDefaultAsync(v => v.TaskId == taskId.Value && v.FieldId == fieldId.Value);
        }

        if (fieldValue == null || fieldValue.ImageData == null)
            return NotFound();

        return File(fieldValue.ImageData, fieldValue.ImageMimeType ?? "image/jpeg", fieldValue.FileName ?? "image.jpg");
    }

    public class UpdateTeamSettingsRequest
    {
        public string TeamName { get; set; }
        public bool IsPriorityVisible { get; set; }
        public bool IsDueDateVisible { get; set; }
        public bool IsTitleVisible { get; set; }
        public bool IsDescriptionVisible { get; set; }
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> SubmitMoveRequest([FromBody] MoveRequestSubmitModel model)
    {
        if (model == null || model.TaskId <= 0 || model.ToColumnId <= 0)
            return BadRequest("Invalid request data.");

        var task = await _context.TaskItems.FindAsync(model.TaskId);
        if (task == null) return NotFound("Task not found.");

        var toColumn = await _context.TeamColumns.FindAsync(model.ToColumnId);
        if (toColumn == null) return NotFound("Target column not found.");

        var fromColumn = await _context.TeamColumns.FindAsync(task.ColumnId);

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        // Get board permissions for this user to save the request
        var boardPerm = await _context.BoardPermissions
            .FirstOrDefaultAsync(p => p.UserId == user.Id && p.TeamName.ToLower().Trim() == task.TeamName.ToLower().Trim());

        if (boardPerm == null)
        {
            // Create a basic permission record if it doesn't exist
            boardPerm = new BoardPermission { UserId = user.Id, TeamName = task.TeamName };
            _context.BoardPermissions.Add(boardPerm);
        }

        var requests = string.IsNullOrEmpty(boardPerm.MoveRequestsJson)
            ? new List<MoveRequest>()
            : JsonSerializer.Deserialize<List<MoveRequest>>(boardPerm.MoveRequestsJson) ?? new List<MoveRequest>();

        var newRequest = new MoveRequest
        {
            TaskId = task.Id,
            TaskTitle = task.Title,
            FromColumnId = task.ColumnId,
            FromColumnName = fromColumn?.ColumnName ?? "Unknown",
            ToColumnId = toColumn.Id,
            ToColumnName = toColumn.ColumnName,
            RequestedByUserId = user.Id,
            RequestedByUserName = user.UserName ?? "User",
            RequestedAt = DateTime.UtcNow,
            Status = "Pending"
        };

        requests.Insert(0, newRequest);
        boardPerm.MoveRequestsJson = JsonSerializer.Serialize(requests);
        await _context.SaveChangesAsync();

        await _hubContext.Clients.Group(task.TeamName).SendAsync("NewMoveRequest", new
        {
            teamName = task.TeamName,
            requestedBy = newRequest.RequestedByUserName,
            taskTitle = newRequest.TaskTitle
        });

        return Ok(new { success = true, message = "Move request submitted successfully." });
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetBoardMoveRequests(string teamName)
    {
        if (string.IsNullOrEmpty(teamName)) return BadRequest();

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
        bool isManager = await _userManager.IsInRoleAsync(user, "Manager") || await _userManager.IsInRoleAsync(user, "Sub-Manager");

        if (!isAdmin && !isManager) return Forbid();

        var allPerms = await _context.BoardPermissions
            .Where(p => p.TeamName.ToLower().Trim() == teamName.ToLower().Trim() && !string.IsNullOrEmpty(p.MoveRequestsJson))
            .ToListAsync();

        var allRequests = new List<object>();
        foreach (var p in allPerms)
        {
            var reqs = JsonSerializer.Deserialize<List<MoveRequest>>(p.MoveRequestsJson);
            if (reqs != null) 
            {
                foreach (var r in reqs)
                {
                    allRequests.Add(new
                    {
                        r.Id,
                        r.TaskId,
                        r.TaskTitle,
                        r.FromColumnId,
                        r.FromColumnName,
                        r.ToColumnId,
                        r.ToColumnName,
                        r.RequestedByUserId,
                        r.RequestedByUserName,
                        r.RequestedAt,
                        RequestedAtFormatted = r.RequestedAt.ToString("dd MMM, hh:mm tt"),
                        r.Status,
                        r.AdminReply,
                        r.HandledAt,
                        HandledAtFormatted = r.HandledAt?.ToString("dd MMM, hh:mm tt"),
                        r.HandledByUserName,
                        r.IsNew
                    });
                }
            }
        }

        return Ok(allRequests.OrderByDescending(r => ((dynamic)r).RequestedAt).ToList());
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> HandleMoveRequest([FromBody] HandleMoveRequestModel model)
    {
        if (model == null || string.IsNullOrEmpty(model.RequestId))
            return BadRequest();

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var boardPerm = await _context.BoardPermissions
            .FirstOrDefaultAsync(p => p.MoveRequestsJson != null && p.MoveRequestsJson.Contains(model.RequestId));

        if (boardPerm == null) return NotFound("Request not found.");

        var requests = JsonSerializer.Deserialize<List<MoveRequest>>(boardPerm.MoveRequestsJson);
        var request = requests?.FirstOrDefault(r => r.Id.ToString() == model.RequestId);
        if (request == null) return NotFound("Request data not found.");

        if (request.Status != "Pending") return BadRequest("Request already handled.");

        request.Status = model.Approved ? "Approved" : "Rejected";
        request.AdminReply = model.AdminReply;
        request.HandledAt = DateTime.UtcNow;
        request.HandledByUserName = user.UserName;
        request.IsNew = false;

        if (model.Approved)
        {
            var task = await _context.TaskItems.FindAsync(request.TaskId);
            if (task != null)
            {
                await _historyService.LogColumnMove(task.Id, task.ColumnId, request.ToColumnId, user.Id);
                task.ColumnId = request.ToColumnId;
                task.CurrentColumnEntryAt = DateTime.UtcNow;
                
                var toCol = await _context.TeamColumns.FindAsync(request.ToColumnId);
                if (toCol != null && (toCol.ColumnName.ToLower().Trim() == "completed" || toCol.ColumnName.ToLower().Trim() == "done"))
                {
                    task.Status = TaskStatusEnum.Complete;
                    task.CompletedByUserId = request.RequestedByUserId;
                    task.CompletedAt = DateTime.UtcNow;
                }
                
                await _context.SaveChangesAsync();

                await _hubContext.Clients.Group(boardPerm.TeamName).SendAsync("TaskMoved", new
                {
                    taskId = task.Id,
                    newColumnId = task.ColumnId,
                    movedBy = user.UserName
                });
            }
        }

        boardPerm.MoveRequestsJson = JsonSerializer.Serialize(requests);
        await _context.SaveChangesAsync();

        await _hubContext.Clients.User(request.RequestedByUserId).SendAsync("MoveRequestHandled", new
        {
            taskId = request.TaskId,
            approved = model.Approved,
            reply = model.AdminReply
        });

        return Ok(new { success = true });
    }

    public class MoveRequestSubmitModel
    {
        public int TaskId { get; set; }
        public int ToColumnId { get; set; }
    }

    public class HandleMoveRequestModel
    {
        public string RequestId { get; set; } = string.Empty;
        public bool Approved { get; set; }
        public string? AdminReply { get; set; }
    }
}



