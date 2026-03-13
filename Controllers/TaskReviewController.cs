using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using UserRoles.Data;
using UserRoles.Models;
using UserRoles.DTOs;
using UserRoles.Hubs;
using UserRoles.Services;

using TaskStatusEnum = UserRoles.Models.Enums.TaskStatus;

namespace UserRoles.Controllers
{
    [Authorize]
    public class TaskReviewController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly ITaskHistoryService _historyService;
        private readonly IHubContext<TaskHub> _hubContext;
        private readonly ITaskPermissionService _permissions;
        private readonly ITaskService _taskService;
        private readonly ILogger<TaskReviewController> _logger;
 
        public TaskReviewController(
            AppDbContext context,
            UserManager<Users> userManager,
            ITaskHistoryService historyService,
            IHubContext<TaskHub> hubContext,
            ITaskPermissionService permissions,
            ITaskService taskService,
            ILogger<TaskReviewController> logger)
        {
            _context = context;
            _userManager = userManager;
            _historyService = historyService;
            _hubContext = hubContext;
            _permissions = permissions;
            _taskService = taskService;
            _logger = logger;
        }

        [HttpPost]
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

            if (!await _permissions.AuthorizeBoardAction(User, task.TeamName, "ReviewTask"))
                return Forbid();

            task.ReviewedByUserId = user.Id;
            task.ReviewedAt = DateTime.UtcNow;
            task.ReviewNote = model.ReviewNote;

            if (model.Passed)
            {
                task.ReviewStatus = UserRoles.Models.Enums.ReviewStatus.Passed;
                await _historyService.LogReviewPassed(task.Id, user.Id, model.ReviewNote);

                var completedCol = await _context.TeamColumns
                    .FirstOrDefaultAsync(c => c.TeamName == task.TeamName && EF.Functions.ILike(c.ColumnName, "completed"));

                if (completedCol != null)
                {
                    await _historyService.LogColumnMove(task.Id, task.ColumnId, completedCol.Id, user.Id);
                    task.ColumnId = completedCol.Id;
                    task.Status = TaskStatusEnum.Complete;
                    task.CompletedByUserId = task.AssignedToUserId;
                    task.CompletedAt = DateTime.UtcNow;
                    task.CurrentColumnEntryAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

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
                task.ReviewStatus = UserRoles.Models.Enums.ReviewStatus.Failed;
                await _historyService.LogReviewFailed(task.Id, user.Id, model.ReviewNote);

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
        public async Task<IActionResult> GetArchivedTasks(string team)
        {
            if (string.IsNullOrWhiteSpace(team))
                return BadRequest();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var archivedTasksQuery = _context.TaskItems
                .Where(t => t.TeamName == team && t.IsArchived)
                .Include(t => t.AssignedToUser)
                .Include(t => t.CompletedByUser)
                .OrderByDescending(t => t.ArchivedAt);

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Admin"))
            {
            var allTasksForAdmin = await archivedTasksQuery.ToListAsync();
            return Ok(allTasksForAdmin.Select(t => new
            {
                t.Id,
                t.Title,
                t.Description,
                CompletedBy = t.CompletedByUser?.UserName ?? t.AssignedToUser?.UserName ?? "Unknown",
                t.CompletedAt,
                t.ArchivedAt,
                Priority = (int)t.Priority,
                ReviewStatus = t.ReviewStatus.ToString()
            }).ToList());
            }

            // For non-admins, we need to check visibility
            var allTasks = await archivedTasksQuery.ToListAsync();
            
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

            var visibleTasks = allTasks
                .Where(t => _taskService.CanUserSeeTask(t, user.Id, roles, rolesMap, hierarchyMap))
                .Select(t => new
                {
                    t.Id,
                    t.Title,
                    t.Description,
                    CompletedBy = t.CompletedByUser?.UserName ?? t.AssignedToUser?.UserName ?? "Unknown",
                    t.CompletedAt,
                    t.ArchivedAt,
                    Priority = (int)t.Priority,
                    ReviewStatus = t.ReviewStatus.ToString()
                })
                .ToList();

            return Ok(visibleTasks);
        }

        [HttpGet]
        public async Task<IActionResult> GetArchivedTaskDetail(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

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
    }
}
