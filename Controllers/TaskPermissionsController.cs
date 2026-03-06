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

namespace UserRoles.Controllers
{
    [Authorize]
    public class TaskPermissionsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly IHubContext<TaskHub> _hubContext;
        private readonly ILogger<TaskPermissionsController> _logger;

        public TaskPermissionsController(
            AppDbContext context,
            UserManager<Users> userManager,
            IHubContext<TaskHub> hubContext,
            ILogger<TaskPermissionsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
            _logger = logger;
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetBoardPermissions(string team)
        {
            if (string.IsNullOrWhiteSpace(team)) return BadRequest();

            var users = await _userManager.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();
            var perms = await _context.BoardPermissions
                .AsNoTracking()
                .Where(p => EF.Functions.ILike(p.TeamName, team.Trim()))
                .ToListAsync();

            var userRolesData = await (from ur in _context.UserRoles
                                       join r in _context.Roles on ur.RoleId equals r.Id
                                       select new { ur.UserId, RoleName = r.Name })
                                      .AsNoTracking()
                                      .ToListAsync();

            var userRolesMap = userRolesData
                .GroupBy(ur => ur.UserId)
                .ToDictionary(g => g.Key, g => g.Select(ur => ur.RoleName).ToList());

            var teamColumns = await _context.TeamColumns
                .Where(c => EF.Functions.ILike(c.TeamName, team.Trim()))
                .OrderBy(c => c.Order)
                .ToListAsync();

            var columnIds = teamColumns.Select(c => c.Id).ToList();
            var colPerms = await _context.ColumnPermissions
                .Where(cp => columnIds.Contains(cp.ColumnId))
                .ToListAsync();

            var teamUserIds = await _context.UserTeams
                .AsNoTracking()
                .Where(ut => ut.TeamName == team)
                .Select(ut => ut.UserId)
                .ToHashSetAsync();

            var result = new List<BoardPermissionDto>();

            foreach (var u in users)
            {
                var rawRoles = userRolesMap.GetValueOrDefault(u.Id, new List<string>());
                if (rawRoles.Contains("Admin")) continue;

                string primaryRole = rawRoles.FirstOrDefault() ?? "User";
                if (primaryRole == "Manager" && !string.IsNullOrEmpty(u.ParentUserId))
                {
                    primaryRole = "Sub Manager";
                }

                bool isTopLevelManager = primaryRole == "Manager";
                if (!isTopLevelManager && !teamUserIds.Contains(u.Id))
                    continue;

                var p = perms.FirstOrDefault(x => x.UserId == u.Id);
                var userColPerms = colPerms.Where(cp => cp.UserId == u.Id).ToList();

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
                    AllowedTransitions = !string.IsNullOrEmpty(p?.AllowedTransitionsJson)
                        ? JsonSerializer.Deserialize<Dictionary<int, List<int>>>(p.AllowedTransitionsJson) ?? new()
                        : new(),
                    CanViewHistory = p?.CanViewHistory ?? false,
                    ColumnPermissions = teamColumns.Select(tc =>
                    {
                        var ucp = userColPerms.FirstOrDefault(ucp => ucp.ColumnId == tc.Id);
                        return new ColumnPermissionDto
                        {
                            ColumnId = tc.Id,
                            ColumnName = tc.ColumnName,
                            CanRename = ucp?.CanRename ?? false,
                            CanDelete = ucp?.CanDelete ?? false,
                            CanAddTask = ucp?.CanAddTask ?? false,
                            CanAssignTask = ucp?.CanAssignTask ?? false,
                            CanEditTask = ucp?.CanEditTask ?? false,
                            CanDeleteTask = ucp?.CanDeleteTask ?? false,
                            CanViewHistory = ucp?.CanViewHistory ?? false
                        };
                    }).ToList()
                });
            }

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

            var allExisting = await _context.BoardPermissions
                .Where(p => p.UserId == dto.UserId && EF.Functions.ILike(p.TeamName, dto.TeamName.Trim()))
                .ToListAsync();

            var existing = allExisting.OrderByDescending(p => p.Id).FirstOrDefault();

            if (existing == null)
            {
                existing = new BoardPermission
                {
                    UserId = dto.UserId,
                    TeamName = dto.TeamName.Trim()
                };
                _context.BoardPermissions.Add(existing);
            }
            else
            {
                if (allExisting.Count > 1)
                {
                    var duplicates = allExisting.Where(p => p.Id != existing.Id).ToList();
                    _context.BoardPermissions.RemoveRange(duplicates);
                }
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
            existing.CanViewHistory = dto.CanViewHistory;

            if (dto.ColumnPermissions != null && dto.ColumnPermissions.Any())
            {
                var userColPerms = await _context.ColumnPermissions
                    .Where(cp => cp.UserId == dto.UserId)
                    .ToListAsync();

                foreach (var cpDto in dto.ColumnPermissions)
                {
                    var colPerm = userColPerms.FirstOrDefault(x => x.ColumnId == cpDto.ColumnId);
                    if (colPerm == null)
                    {
                        colPerm = new ColumnPermission
                        {
                            UserId = dto.UserId,
                            ColumnId = cpDto.ColumnId
                        };
                        _context.ColumnPermissions.Add(colPerm);
                    }

                    colPerm.CanRename = cpDto.CanRename;
                    colPerm.CanDelete = cpDto.CanDelete;
                    colPerm.CanClearTasks = cpDto.CanClearTasks;
                    colPerm.CanAddTask = cpDto.CanAddTask;
                    colPerm.CanAssignTask = cpDto.CanAssignTask;
                    colPerm.CanEditTask = cpDto.CanEditTask;
                    colPerm.CanDeleteTask = cpDto.CanDeleteTask;
                    colPerm.CanViewHistory = cpDto.CanViewHistory;
                }
            }

            await _context.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("PermissionsUpdated", dto.UserId, dto.TeamName);

            return Ok(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateTeamSettings([FromBody] UpdateTeamSettingsRequest model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.TeamName))
                return BadRequest();

            var team = await _context.Teams.FirstOrDefaultAsync(t => t.Name == model.TeamName);
            if (team == null)
            {
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
    }
}
