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
        private readonly ILogger<TaskReviewController> _logger;
 
        public TaskReviewController(
            AppDbContext context,
            UserManager<Users> userManager,
            ITaskHistoryService historyService,
            IHubContext<TaskHub> hubContext,
            ITaskPermissionService permissions,
            ILogger<TaskReviewController> logger)
        {
            _context = context;
            _userManager = userManager;
            _historyService = historyService;
            _hubContext = hubContext;
            _permissions = permissions;
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
    }
}
