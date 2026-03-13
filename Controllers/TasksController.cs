using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserRoles.Data;
using UserRoles.Models;
using UserRoles.ViewModels;
using UserRoles.DTOs;
using UserRoles.Services;
using Microsoft.AspNetCore.SignalR;
using UserRoles.Hubs;

namespace UserRoles.Controllers
{
    [Authorize]
    public class TasksController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly ITaskService _taskService;
        private readonly ITaskHistoryService _historyService;
        private readonly IHubContext<TaskHub> _hubContext;
        private readonly IFacebookLeadsService _leadsService;
        private readonly ITaskPermissionService _permissions;
        private readonly ILogger<TasksController> _logger;

        public TasksController(
            AppDbContext context,
            UserManager<Users> userManager,
            ITaskService taskService,
            ITaskHistoryService historyService,
            IHubContext<TaskHub> hubContext,
            IFacebookLeadsService leadsService,
            ITaskPermissionService permissions,
            ILogger<TasksController> logger)
        {
            _context = context;
            _userManager = userManager;
            _taskService = taskService;
            _historyService = historyService;
            _hubContext = hubContext;
            _leadsService = leadsService;
            _permissions = permissions;
            _logger = logger;
        }

        // ═══════════════════════════════════════════════════════
        //  INDEX
        // ═══════════════════════════════════════════════════════

        [Authorize]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var roles = await _userManager.GetRolesAsync(user);
            var model = await _taskService.GetQuickAssignModelAsync(user.Id, roles);

            if (!User.IsInRole("Admin"))
            {
                var userTeams = await _context.UserTeams
                    .AsNoTracking()
                    .Where(t => t.UserId == user.Id && t.TeamName != "Development")
                    .Select(t => t.TeamName)
                    .Distinct()
                    .ToListAsync();
                ViewBag.UserTeams = userTeams;
                model.AvailableTeams = userTeams;
            }

            return View(model);
        }

        // ═══════════════════════════════════════════════════════
        //  FACEBOOK LEADS
        // ═══════════════════════════════════════════════════════
        // Facebook leads are now ingested automatically by FacebookLeadIngestionService
        // and stored as persistent TaskItems.

        [HttpGet, Authorize]
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
                _logger.LogError(ex, "Failed to get lead details for {LeadId}", leadId);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════
        //  TASK CRUD  (delegated to ITaskService)
        // ═══════════════════════════════════════════════════════

        [HttpPost, Authorize]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var result = await _taskService.CreateTaskAsync(model, user.Id);
            if (!result.Success) return StatusCode(result.StatusCode, result.Message);

            return Ok(new { success = true, message = "Task created successfully", workItemId = result.Message });
        }

        [HttpPost, Authorize]
        public async Task<IActionResult> UpdateTask([FromBody] UpdateTaskRequest model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var result = await _taskService.UpdateTaskAsync(model, User, user.Id);
            if (!result.Success)
            {
                if (result.StatusCode == 403) return Forbid();
                if (result.StatusCode == 404) return NotFound(result.Message);
                return BadRequest(result.Message);
            }
            return Json(new { success = true });
        }

        [HttpPost, Authorize]
        public async Task<IActionResult> DeleteTask([FromBody] int taskId)
        {
            var result = await _taskService.DeleteTaskAsync(taskId, User);
            if (!result.Success)
            {
                if (result.StatusCode == 403) return Forbid();
                if (result.StatusCode == 404) return NotFound();
                return BadRequest(result.Message);
            }
            return Ok();
        }

        [HttpGet, Authorize]
        public async Task<IActionResult> GetTask(int id)
        {
            var task = await _taskService.GetTaskByIdAsync(id);
            if (task == null) return NotFound();

            var fieldValuesMap = task.CustomFieldValues
                .GroupBy(v => v.FieldId)
                .ToDictionary(g => g.Key, g => g.Select(v => v.Value ?? "").ToList());

            return Ok(new
            {
                id = task.Id,
                title = task.Title,
                description = task.Description,
                priority = (int)task.Priority,
                assignedToUserId = task.AssignedToUserId,
                dueDate = task.DueDate?.ToString("yyyy-MM-ddTHH:mm"),
                customFieldValues = fieldValuesMap
            });
        }

        [HttpGet, Authorize]
        public async Task<IActionResult> GetTaskDetail(int id)
        {
            var task = await _taskService.GetTaskByIdAsync(id, includeRelated: true);
            if (task == null) return NotFound();

            var customFields = task.CustomFieldValues?.Select(fv => new
            {
                FieldName = fv.Field?.FieldName,
                FieldType = fv.Field?.FieldType,
                Value = fv.Field?.FieldType == "DateTime" && DateTime.TryParse(fv.Value, out var dt)
                        ? dt.ToString("dd-MM-yyyy, HH:mm:ss")
                        : fv.Value
            }).ToList();

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
                CreatedAtFormatted = task.CreatedAt.ToString("dd-MM-yyyy, HH:mm:ss"),
                AssignedAtFormatted = task.AssignedAt?.ToString("dd-MM-yyyy, HH:mm:ss"),
                ReviewedAtFormatted = task.ReviewedAt?.ToString("dd-MM-yyyy, HH:mm:ss"),
                CompletedAtFormatted = task.CompletedAt?.ToString("dd-MM-yyyy, HH:mm:ss"),
                DueDateFormatted = task.DueDate?.ToString("dd-MM-yyyy, HH:mm:ss"),
                CustomFields = customFields
            });
        }

        [HttpGet, Authorize]
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

            var currentUser = await _userManager.GetUserAsync(User);
            BoardPermission? permissions = null;
            if (currentUser != null)
            {
                permissions = await _context.BoardPermissions
                    .Where(p => p.UserId == currentUser.Id && EF.Functions.ILike(p.TeamName, task.TeamName.Trim()))
                    .OrderByDescending(p => p.Id)
                    .FirstOrDefaultAsync();
            }
            ViewData["UserPermissions"] = permissions;
            return PartialView("_TaskCard", task);
        }

        // ═══════════════════════════════════════════════════════
        //  ASSIGNMENT  (delegated to ITaskService)
        // ═══════════════════════════════════════════════════════

        [HttpPost, Authorize]
        public async Task<IActionResult> AssignTask(int taskId, string userId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var result = await _taskService.AssignTaskAsync(taskId, userId, User, currentUser.Id);
            if (!result.Success)
            {
                if (result.StatusCode == 403) return Forbid();
                if (result.StatusCode == 404) return NotFound();
                return BadRequest(result.Message);
            }

            return Ok(new
            {
                success = true,
                assignedTo = result.Data!.AssignedTo,
                assignedBy = result.Data.AssignedBy,
                assignedAt = result.Data.AssignedAt,
                teamMoved = result.Data.TeamMoved,
                newTeam = result.Data.NewTeam
            });
        }

        [HttpPost, Authorize]
        public async Task<IActionResult> BulkAssignTasks([FromBody] BulkAssignRequest model)
        {
            if (model == null || model.TaskIds == null || !model.TaskIds.Any() || string.IsNullOrEmpty(model.UserId))
                return BadRequest("Invalid request");

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var result = await _taskService.BulkAssignTasksAsync(model.TaskIds, model.UserId, User, currentUser.Id);
            if (!result.Success) return BadRequest(result.Message);

            return Ok(new { success = true, count = result.Data });
        }

        // ═══════════════════════════════════════════════════════
        //  MOVEMENT  (delegated to ITaskService)
        // ═══════════════════════════════════════════════════════

        [HttpPost, Authorize]
        public async Task<IActionResult> MoveTask([FromBody] MoveTaskDto model)
        {
            if (model == null) return BadRequest("Invalid payload");

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var result = await _taskService.MoveTaskAsync(model.TaskId, model.ColumnId, User, user.Id);
            if (!result.Success)
            {
                if (result.StatusCode == 403) return StatusCode(403, result.Message);
                if (result.StatusCode == 404) return NotFound(result.Message);
                return BadRequest(result.Message);
            }

            return Ok(new { success = true, message = "Task moved successfully" });
        }

        // ═══════════════════════════════════════════════════════
        //  TEAM BOARD  (delegated to ITaskService)
        // ═══════════════════════════════════════════════════════

        [Authorize]
        public async Task<IActionResult> TeamBoard(string team)
        {
            if (string.IsNullOrWhiteSpace(team)) return BadRequest();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Security: team access
            if (!User.IsInRole("Admin"))
            {
                bool hasAccess = await _context.UserTeams.AnyAsync(t => t.UserId == user.Id && t.TeamName == team);
                if (!hasAccess) return Forbid();
            }

            // Resolve viewer roles
            var userRolesData = await (from ur in _context.UserRoles
                                       join r in _context.Roles on ur.RoleId equals r.Id
                                       where ur.UserId == user.Id
                                       select r.Name)
                                      .AsNoTracking()
                                      .ToListAsync();

            var vm = await _taskService.BuildTeamBoardAsync(team, user.Id, userRolesData);
            return PartialView("_TeamBoard", vm);
        }

        // ═══════════════════════════════════════════════════════
        //  ASSIGNED TASKS OVERVIEW
        // ═══════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> AssignedTasksOverview(string team)
        {
            if (string.IsNullOrEmpty(team)) return BadRequest("Team name is required");

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var tasks = await _context.TaskItems
                .AsNoTracking()
                .Include(t => t.AssignedByUser)
                .Include(t => t.AssignedToUser)
                .Include(t => t.Column)
                .Where(t => (t.TeamName == team || t.AssignedByUserId == currentUser.Id) && !t.IsArchived && t.AssignedToUserId != null)
                .OrderByDescending(t => t.AssignedAt)
                .ToListAsync();

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

        // ═══════════════════════════════════════════════════════
        //  TEAMS
        // ═══════════════════════════════════════════════════════

        [HttpGet, Authorize]
        public async Task<IActionResult> GetAllTeams()
        {
            var teams = await _context.TeamColumns.Select(c => c.TeamName).Distinct().ToListAsync();
            return Ok(teams);
        }

        [HttpPost, Authorize]
        public async Task<IActionResult> AssignTaskToTeam([FromBody] AssignTaskToTeamRequest model)
        {
            if (model == null || model.TaskId <= 0 || string.IsNullOrWhiteSpace(model.TeamName))
                return BadRequest("Invalid request");

            var task = await _context.TaskItems.FindAsync(model.TaskId);
            if (task == null) return NotFound("Task not found");

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (!User.IsInRole("Admin"))
            {
                if (!await _permissions.AuthorizeBoardAction(User, task.TeamName, "AssignTask"))
                    return Forbid();
            }

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

            if (targetColumn == null) return BadRequest($"No columns found for team '{model.TeamName}'. Create columns first.");

            await _historyService.LogColumnMove(task.Id, task.ColumnId, targetColumn.Id, user.Id);
            await _historyService.LogAssignment(task.Id, user.Id, user.Id);

            task.TeamName = model.TeamName;
            task.ColumnId = targetColumn.Id;

            if (targetColumn.ColumnName.Contains("Todo", StringComparison.OrdinalIgnoreCase) ||
                targetColumn.ColumnName.Contains("To Do", StringComparison.OrdinalIgnoreCase))
            {
                task.Status = Models.Enums.TaskStatus.ToDo;
            }
            else if (targetColumn.Order == 1)
            {
                task.Status = Models.Enums.TaskStatus.ToDo;
            }

            task.UpdatedAt = DateTime.UtcNow;
            task.AssignedByUserId = user.Id;
            task.AssignedAt = DateTime.UtcNow;
            task.CurrentColumnEntryAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = $"Task moved to {model.TeamName} ({targetColumn.ColumnName})" });
        }

        // ═══════════════════════════════════════════════════════
        //  TASK HISTORY
        // ═══════════════════════════════════════════════════════

        [HttpGet("/Tasks/{taskId}/History"), Authorize]
        public async Task<IActionResult> GetTaskHistory(int taskId)
        {
            var task = await _context.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Contains("Admin"))
            {
                var userRolesMap = await (from ur in _context.UserRoles
                                          join r in _context.Roles on ur.RoleId equals r.Id
                                          select new { ur.UserId, RoleName = r.Name })
                                         .AsNoTracking()
                                         .ToListAsync();

                var rolesMap = userRolesMap.GroupBy(ur => ur.UserId)
                                           .ToDictionary(g => g.Key, g => (IList<string>)g.Select(ur => ur.RoleName).ToList());

                var hierarchyMap = await _userManager.Users
                    .AsNoTracking()
                    .Select(u => new { u.Id, u.ManagerId })
                    .ToDictionaryAsync(u => u.Id, u => u.ManagerId);

                if (!_taskService.CanUserSeeTask(task, user.Id, roles, rolesMap, hierarchyMap))
                    return Forbid();
            }

            var history = await _historyService.GetTaskHistory(taskId);
            return Ok(history);
        }
        // ═══════════════════════════════════════════════════════
        //  BACKFILL DIGI LEADS CUSTOM FIELDS
        // ═══════════════════════════════════════════════════════

        [HttpPost("/api/Tasks/BackfillDigiLeads"), Authorize]
        public async Task<IActionResult> BackfillDigiLeads()
        {
            // 1. Get all Digi Leads tasks
            var tasks = await _context.TaskItems
                .Where(t => t.TeamName == "Digi Leads" && !t.IsArchived)
                .Include(t => t.CustomFieldValues)
                .ToListAsync();

            // 2. Ensure custom fields exist
            var fieldDefs = new (string Name, int Order)[]
            {
                ("Full Name", 1), ("Phone", 2), ("Email", 3),
                ("Company Name", 4), ("Country", 5),
                ("What Best Describes Your Business", 6),
                ("What Are You Looking To Launch", 7)
            };

            var dbFields = new Dictionary<string, TaskCustomField>();
            foreach (var (name, order) in fieldDefs)
            {
                var field = await _context.TaskCustomFields
                    .FirstOrDefaultAsync(f => f.TeamName == "Digi Leads" && f.FieldName == name);
                if (field == null)
                {
                    field = new TaskCustomField
                    {
                        TeamName = "Digi Leads", FieldName = name,
                        FieldType = "String", Order = order, IsActive = true
                    };
                    _context.TaskCustomFields.Add(field);
                    await _context.SaveChangesAsync();
                }
                dbFields[name] = field;
            }

            // 3. Backfill values for each task
            int updated = 0;
            foreach (var task in tasks)
            {
                if (string.IsNullOrEmpty(task.Description)) continue;

                var existingFieldIds = task.CustomFieldValues?
                    .Where(v => !string.IsNullOrWhiteSpace(v.Value))
                    .Select(v => v.FieldId)
                    .ToHashSet() ?? new HashSet<int>();

                // Build extraction map: field name → aliases to search in description
                var extractionMap = new Dictionary<string, string[]>
                {
                    ["Full Name"]  = new[] { "Full Name", "Name" },
                    ["Phone"]      = new[] { "Phone" },
                    ["Email"]      = new[] { "Email" },
                    ["Company Name"] = new[] { "Company Name" },
                    ["Country"]    = new[] { "Country" },
                    ["What Best Describes Your Business"] = new[] { "What Best Describes Your Business" },
                    ["What Are You Looking To Launch"]    = new[] { "What Are You Looking To Launch" }
                };

                bool taskModified = false;
                foreach (var kvp in extractionMap)
                {
                    var field = dbFields[kvp.Key];
                    if (existingFieldIds.Contains(field.Id)) continue; // Already has data

                    string? value = null;
                    foreach (var alias in kvp.Value)
                    {
                        value = ExtractFieldFromDescription(task.Description, alias);
                        if (value != null) break;
                    }

                    // Fallback: for Full Name use Title
                    if (value == null && kvp.Key == "Full Name")
                        value = task.Title;

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        _context.TaskFieldValues.Add(new TaskFieldValue
                        {
                            TaskId = task.Id,
                            FieldId = field.Id,
                            Value = value
                        });
                        taskModified = true;
                    }
                }
                if (taskModified) updated++;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = $"Backfilled {updated} of {tasks.Count} Digi Leads tasks." });
        }

        /// <summary>Extracts a value from the lead description markdown using the pattern **Key:** Value</summary>
        private string? ExtractFieldFromDescription(string description, string fieldName)
        {
            // Match: **FieldName:** Value  (with optional leading "- ")
            var pattern = $@"\*\*{System.Text.RegularExpressions.Regex.Escape(fieldName)}:\*\*\s*(.+)";
            var match = System.Text.RegularExpressions.Regex.Match(
                description, pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);

            if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                return match.Groups[1].Value.Trim();

            return null;
        }
    }
}
