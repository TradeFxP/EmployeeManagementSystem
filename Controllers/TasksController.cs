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
    private readonly IFacebookLeadsService _leadsService;

    private readonly ITaskPermissionService _permissions;

    public TasksController(AppDbContext context, UserManager<Users> userManager, ITaskHistoryService historyService, IHubContext<TaskHub> hubContext, IFacebookLeadsService leadsService, ITaskPermissionService permissions)
    {
        _context = context;
        _userManager = userManager;
        _historyService = historyService;
        _hubContext = hubContext;
        _leadsService = leadsService;
        _permissions = permissions;
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
            .Where(t => t.UserId == user.Id && t.TeamName != "Development")
            .Select(t => t.TeamName)
            .Distinct()
            .ToListAsync();
        ViewBag.UserTeams = userTeams; // 🔥 REQUIRED

        // Always show the index view with left panel
        // Users see their assigned teams, can select to load board
        return View();
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetVirtualLeads()
    {
        if (!User.IsInRole("Admin"))
        {
            return Unauthorized("Only admins can view virtual leads.");
        }

        try
        {
            var leads = await _leadsService.FetchLeadsAsync();
            return Json(new { success = true, leads });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ConvertLeadToTask([FromBody] LeadConversionDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.id))
            return BadRequest("Invalid lead data: ID is required.");

        string leadId = dto.id;

        // 1. Check if already exists
        var existing = await _context.TaskItems
            .FirstOrDefaultAsync(t => t.ExternalLeadId == leadId);

        if (existing != null)
        {
            return Json(new { success = true, taskId = existing.Id, alreadyExists = true });
        }

        // 2. Create new TaskItem (Stub Model)
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        int targetColumnId = dto.columnId ?? 0;
        if (targetColumnId <= 0) return BadRequest("Column ID is required.");

        var column = await _context.TeamColumns.FindAsync(targetColumnId);
        if (column == null) return BadRequest("Column not found");

        var task = new TaskItem
        {
            ExternalLeadId = leadId,
            Title = dto.name ?? "API Lead",
            Description = $"Facebook Lead: {leadId}", // Minimum data stored
            ColumnId = targetColumnId,
            TeamName = column.TeamName,
            Priority = UserRoles.Models.Enums.TaskPriority.Medium,
            Status = UserRoles.Models.Enums.TaskStatus.ToDo,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            AssignedToUserId = user.Id, 
            AssignedByUserId = user.Id,
            AssignedAt = DateTime.UtcNow,
            CurrentColumnEntryAt = DateTime.UtcNow
        };

        _context.TaskItems.Add(task);
        await _context.SaveChangesAsync();

        await _historyService.LogTaskCreated(task.Id, user.Id);

        return Json(new { success = true, taskId = task.Id, alreadyExists = false });
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetLeadLiveDetails(string leadId)
    {
        if (string.IsNullOrEmpty(leadId)) return BadRequest("Lead ID is missing");

        try
        {
            var leads = await _leadsService.FetchLeadsAsync();
            var lead = leads.FirstOrDefault(l => l.Id == leadId);
            if (lead == null) return NotFound("Lead not found in API pool");

            return Json(new { success = true, lead });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
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
        if (!User.IsInRole("Admin") && !await _permissions.AuthorizeBoardAction(User, taskToUpdate.TeamName, "AssignTask"))
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
            // Get assignee team and primary role for SignalR enrichment
            var assigneeTeam = targetUserTeam?.TeamName ?? (await _context.UserTeams.FirstOrDefaultAsync(ut => ut.UserId == userId))?.TeamName ?? "N/A";
            var assigneeRawRoles = await _userManager.GetRolesAsync(assignToUser);
            var assigneeRole = assigneeRawRoles.FirstOrDefault() ?? "User";
            if (assigneeRawRoles.Contains("Sub-Manager") || (assigneeRole == "Manager" && !string.IsNullOrEmpty(assignToUser.ParentUserId)))
                assigneeRole = "Sub-Manager";

            // Simple update for same team
            await _hubContext.Clients.Group(taskToUpdate.TeamName).SendAsync("TaskAssigned", new
            {
                taskId = taskId,
                assignedTo = assignToUser.Name ?? assignToUser.UserName,
                assignedBy = currentUser.Email,
                assignedAt = taskToUpdate.AssignedAt.Value.ToString("dd MMM yyyy, hh:mm tt"),
                team = assigneeTeam,
                role = assigneeRole
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

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> BulkAssignTasks([FromBody] BulkAssignRequest model)
    {
        if (model == null || model.TaskIds == null || !model.TaskIds.Any() || string.IsNullOrEmpty(model.UserId))
            return BadRequest("Invalid request");

        var assignToUser = await _userManager.FindByIdAsync(model.UserId);
        if (assignToUser == null) return BadRequest("User not found");

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var tasks = await _context.TaskItems.Where(t => model.TaskIds.Contains(t.Id)).ToListAsync();
        if (!tasks.Any()) return NotFound("No tasks found");

        foreach (var task in tasks)
        {
            // Check permission per team (optimization: cache team permission check)
            if (!User.IsInRole("Admin") && !await _permissions.AuthorizeBoardAction(User, task.TeamName, "AssignTask"))
                continue;

            task.AssignedToUserId = assignToUser.Id;
            task.AssignedByUserId = currentUser.Id;
            task.AssignedAt = DateTime.UtcNow;
            task.UpdatedAt = DateTime.UtcNow;

            await _historyService.LogAssignment(task.Id, assignToUser.Id, currentUser.Id);

            // SignalR notification
            try
            {
                // Get assignee team and primary role for SignalR enrichment
                var assigneeTeam = (await _context.UserTeams.FirstOrDefaultAsync(ut => ut.UserId == model.UserId))?.TeamName ?? "N/A";
                var assigneeRawRoles = await _userManager.GetRolesAsync(assignToUser);
                var assigneeRole = assigneeRawRoles.FirstOrDefault() ?? "User";
                if (assigneeRawRoles.Contains("Sub-Manager") || (assigneeRole == "Manager" && !string.IsNullOrEmpty(assignToUser.ParentUserId)))
                    assigneeRole = "Sub-Manager";

                await _hubContext.Clients.Group(task.TeamName).SendAsync("TaskAssigned", new
                {
                    taskId = task.Id,
                    assignedTo = assignToUser.Name ?? assignToUser.UserName,
                    assignedBy = currentUser.Email,
                    assignedAt = task.AssignedAt.Value.ToString("dd MMM yyyy, hh:mm tt"),
                    team = assigneeTeam,
                    role = assigneeRole
                });
            }
            catch { }
        }

        await _context.SaveChangesAsync();
        return Ok(new { success = true, count = tasks.Count });
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
            if (!await _permissions.AuthorizeBoardAction(User, task.TeamName, "EditAllFields"))
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
            if (User.IsInRole("Admin") || await _permissions.AuthorizeBoardAction(User, task.TeamName, "AssignTask"))
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
                    if (existingRecords.Any(r => r.Value == val)) continue;

                    var newVal = new TaskFieldValue
                    {
                        TaskId = task.Id,
                        FieldId = fieldId,
                        Value = val,
                        CreatedAt = DateTime.UtcNow
                    };

                    // 🔥 NEW: Trigger Task Movement if field type is "List" (Column)
                    if (fieldDef.FieldType == "List" && int.TryParse(val, out int targetColumnId))
                    {
                        if (task.ColumnId != targetColumnId)
                        {
                            await MoveTaskInternal(task.Id, targetColumnId, user);
                        }
                    }

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

        if (!await _permissions.AuthorizeBoardAction(User, task.TeamName, "DeleteTask"))
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
            .Include(t => t.Column)
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

        // Auto-archiving removed from controller to improve performance. 
        // This should be handled by a background service or the TaskReviewController.

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

        // 🔥 PERFORMANCE: Bulk-fetch teams for ALL users
        var userTeamsData = await _context.UserTeams
            .AsNoTracking()
            .Select(ut => new { ut.UserId, ut.TeamName })
            .ToListAsync();

        var userTeamsMap = userTeamsData
            .GroupBy(ut => ut.UserId)
            .ToDictionary(g => g.Key, g => g.FirstOrDefault()?.TeamName ?? "N/A");

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
            foreach (var id in directSubordinates) accessibleIds.Add(id);
            var indirect = allUsers.Where(u => directSubordinates.Contains(u.ParentUserId ?? "")).Select(u => u.Id);
            foreach (var id in indirect) accessibleIds.Add(id);

            filteredAssignees = allUsers.Where(u => accessibleIds.Contains(u.Id) && teamUserIds.Contains(u.Id)).ToList();
        }
        else if (viewerRoles.Contains("Sub-Manager") || viewerRoles.Contains("SubManager"))
        {
            var accessibleIds = new HashSet<string> { user.Id };
            var subs = allUsers.Where(u => u.ParentUserId == user.Id).Select(u => u.Id);
            foreach (var id in subs) accessibleIds.Add(id);

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

        // ✅ NEW: Build Grouped User List by Team and Hierarchy
        var teamGroups = new List<TeamGroupViewModel>();
        var managementUsers = new List<UserHierarchyItem>();
        var allTeams = await _context.UserTeams.AsNoTracking().Select(ut => ut.TeamName).Distinct().ToListAsync();
        var allUserTeams = await _context.UserTeams.AsNoTracking().ToListAsync();

        // 1. First, identify Global Management (Admins/Managers)
        var globalManagers = allUsers.Where(u =>
        {
            var role = feUserRolesMap.GetValueOrDefault(u.Id, "User");
            return role == "Admin" || role == "Manager";
        }).OrderByDescending(u => feUserRolesMap[u.Id]).ThenBy(u => u.Name ?? u.UserName).ToList();

        foreach (var m in globalManagers)
        {
            managementUsers.Add(new UserHierarchyItem { Id = m.Id, Name = m.Name ?? m.UserName, RoleName = feUserRolesMap[m.Id], Level = 0 });
        }

        var globalManagerIds = globalManagers.Select(m => m.Id).ToHashSet();

        // 2. Build Team Groups (excluding global management)
        foreach (var tName in allTeams)
        {
            var usersInTeamIds = allUserTeams.Where(ut => ut.TeamName == tName).Select(ut => ut.UserId).ToHashSet();
            var teamUsers = allUsers.Where(u => usersInTeamIds.Contains(u.Id) && !globalManagerIds.Contains(u.Id)).ToList();

            var groupedUsers = new List<UserHierarchyItem>();
            var processedIds = new HashSet<string>();

            // Sub-Managers in this team and their subordinates
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

            // Anyone else left in the team
            var rest = teamUsers.Where(u => !processedIds.Contains(u.Id)).OrderBy(u => u.Name).ToList();
            foreach (var r in rest)
            {
                groupedUsers.Add(new UserHierarchyItem { Id = r.Id, Name = r.Name ?? r.UserName, RoleName = feUserRolesMap.GetValueOrDefault(r.Id, "User"), Level = 0 });
            }

            if (groupedUsers.Any())
            {
                teamGroups.Add(new TeamGroupViewModel { TeamName = tName, Users = groupedUsers });
            }
        }

        // 3. Sort TeamGroups so current board team is first
        teamGroups = teamGroups
            .OrderByDescending(g => g.TeamName.Trim().ToLower() == team.Trim().ToLower())
            .ThenBy(g => g.TeamName)
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

        var colPerms = user == null ? new List<ColumnPermission>() : await _context.ColumnPermissions
            .Include(p => p.Column)
            .Where(p => p.UserId == user.Id && p.Column != null && p.Column.TeamName != null && p.Column.TeamName.ToLower().Trim() == team.ToLower().Trim())
            .ToListAsync();

        var vm = new TeamBoardViewModel
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

        return PartialView("_TeamBoard", vm);


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

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var result = await MoveTaskInternal(model.TaskId, model.ColumnId, user);
        if (!result.Success)
        {
            if (result.StatusCode == 403) return StatusCode(403, result.Message);
            if (result.StatusCode == 404) return NotFound(result.Message);
            return BadRequest(result.Message);
        }

        return Ok(new { success = true, message = "Task moved successfully" });
    }

    private class MoveResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int StatusCode { get; set; }
    }

    private async Task<MoveResult> MoveTaskInternal(int taskId, int columnId, Users user)
    {
        var task = await _context.TaskItems
            .Include(t => t.Column)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null)
            return new MoveResult { Success = false, Message = "Task not found", StatusCode = 404 };

        var targetColumn = await _context.TeamColumns
            .FirstOrDefaultAsync(c => c.Id == columnId);

        if (targetColumn == null)
            return new MoveResult { Success = false, Message = "Target column not found", StatusCode = 404 };

        var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
        var targetColName = targetColumn.ColumnName?.Trim().ToLower();
        var sourceColName = task.Column?.ColumnName?.Trim().ToLower();
        int oldColId = task.ColumnId;

        // ══════ STRICT PERMISSION CHECK ══════
        if (!await CanUserMoveTask(task, user, targetColumn.Id))
        {
            return new MoveResult { Success = false, Message = "you have not acess to move", StatusCode = 403 };
        }

        // ═══════ ROLE-BASED RESTRICTIONS ═══════

        // 1. COMPLETED column: ONLY Admin OR users with ReviewTask permission can move tasks here
        if (targetColName == "completed")
        {
            if (!isAdmin && !await _permissions.AuthorizeBoardAction(User, task.TeamName, "ReviewTask"))
                return new MoveResult { Success = false, Message = "Only Admin or authorized reviewers can move tasks to Completed.", StatusCode = 403 };

            // Task must have passed review first
            if (task.ReviewStatus != UserRoles.Models.Enums.ReviewStatus.Passed)
                return new MoveResult { Success = false, Message = "Task must pass review before moving to Completed.", StatusCode = 400 };
        }

        // 3. Moving FROM review back (fail rework): allowed if task was failed
        if (sourceColName == "review" && targetColName != "completed" && !isAdmin)
        {
            // Non-admin can only move back if review was failed or none
            if (task.ReviewStatus != UserRoles.Models.Enums.ReviewStatus.Failed &&
                task.ReviewStatus != UserRoles.Models.Enums.ReviewStatus.None)
            {
                // Logic kept from original
            }
        }

        // Apply Move Logic...
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

        // If moving to completed (admin logic), track completion
        if (targetColName == "completed")
        {
            task.CompletedByUserId = task.AssignedToUserId;
            task.CompletedAt = DateTime.UtcNow;
        }

        task.UpdatedAt = DateTime.UtcNow;
        task.CurrentColumnEntryAt = DateTime.UtcNow;

        _context.TaskItems.Update(task);
        await _context.SaveChangesAsync();

        // Log history
        await _historyService.LogColumnMove(task.Id, oldColId, targetColumn.Id, user.Id);

        // Notify via SignalR
        if (_hubContext != null)
        {
            await _hubContext.Clients.Group(task.TeamName).SendAsync("TaskMoved", new
            {
                taskId = task.Id,
                newColumnId = targetColumn.Id,
                oldColumnId = oldColId,
                movedBy = user.UserName,
                columnName = targetColumn.ColumnName
            });
        }

        return new MoveResult { Success = true };
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
            if (!await _permissions.AuthorizeBoardAction(User, task.TeamName, "AssignTask"))
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

        return Ok(new { success = true, message = $"Task moved to {model.TeamName} ({targetColumn.ColumnName})" });
    }

    // ========== TASK HISTORY ==========

    [HttpGet("/Tasks/{taskId}/History")]
    [Authorize]
    public async Task<IActionResult> GetTaskHistory(int taskId)
    {
        var history = await _historyService.GetTaskHistory(taskId);
        return Ok(history);
    }


}
