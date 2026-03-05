using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using UserRoles.Data;
using UserRoles.Models;
using UserRoles.DTOs;
using UserRoles.Hubs;
using UserRoles.Services;

using TaskStatusEnum = UserRoles.Models.Enums.TaskStatus;

namespace UserRoles.Controllers
{
    [Authorize]
    public class TaskMoveRequestsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly ITaskHistoryService _historyService;
        private readonly IHubContext<TaskHub> _hubContext;

        public TaskMoveRequestsController(
            AppDbContext context,
            UserManager<Users> userManager,
            ITaskHistoryService historyService,
            IHubContext<TaskHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _historyService = historyService;
            _hubContext = hubContext;
        }

        [HttpPost]
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

            var boardPerm = await _context.BoardPermissions
                .FirstOrDefaultAsync(p => p.UserId == user.Id && p.TeamName.ToLower().Trim() == task.TeamName.ToLower().Trim());

            if (boardPerm == null)
            {
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
                        movedBy = user.UserName,
                        columnName = toCol?.ColumnName ?? "Unknown"
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
    }
}
