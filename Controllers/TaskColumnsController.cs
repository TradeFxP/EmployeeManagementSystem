using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserRoles.Data;
using UserRoles.Models;
using UserRoles.ViewModels;
using UserRoles.DTOs;
using UserRoles.Services;

namespace UserRoles.Controllers
{
    [Authorize]
    public class TaskColumnsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ITaskPermissionService _permissions;

        public TaskColumnsController(AppDbContext context, ITaskPermissionService permissions)
        {
            _context = context;
            _permissions = permissions;
        }

        [HttpPost]
        public async Task<IActionResult> AddColumn([FromBody] AddColumnRequest model)
        {
            if (!await _permissions.AuthorizeBoardAction(User, model.Team, "AddColumn"))
                return Forbid();

            if (string.IsNullOrWhiteSpace(model.ColumnName))
                return BadRequest("Column name is required");

            var maxOrder = _context.TeamColumns
                .Where(c => c.TeamName == model.Team)
                .Max(c => (int?)c.Order) ?? 0;

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
        public async Task<IActionResult> ReorderColumns([FromBody] List<int> columnIds)
        {
            if (columnIds == null || columnIds.Count == 0)
                return BadRequest("No columns received");

            var firstCol = await _context.TeamColumns.FindAsync(columnIds[0]);
            if (firstCol == null || !await _permissions.AuthorizeBoardAction(User, firstCol.TeamName, "ReorderColumns"))
                return Forbid();

            var columns = _context.TeamColumns
                .Where(c => columnIds.Contains(c.Id))
                .ToList();

            for (int i = 0; i < columnIds.Count; i++)
            {
                var column = columns.FirstOrDefault(c => c.Id == columnIds[i]);
                if (column != null)
                {
                    column.Order = i + 1;
                }
            }

            _context.SaveChanges();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> RenameColumn([FromBody] RenameColumnRequest model)
        {
            if (model == null || model.ColumnId <= 0 || string.IsNullOrWhiteSpace(model.Name))
                return BadRequest("Invalid request");

            var col = await _context.TeamColumns.FindAsync(model.ColumnId);
            if (col == null) return NotFound("Column not found");

            if (!await _permissions.AuthorizeBoardAction(User, col.TeamName, "RenameColumn"))
                return Forbid();

            col.ColumnName = model.Name.Trim();
            _context.SaveChanges();

            return Ok();
        }

        [HttpPost]
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
        public async Task<IActionResult> DeleteAllTasksInColumn([FromBody] int columnId)
        {
            if (columnId <= 0) return BadRequest();

            var column = await _context.TeamColumns.FindAsync(columnId);
            if (column == null) return NotFound();

            if (!User.IsInRole("Admin"))
            {
                if (!await _permissions.AuthorizeBoardAction(User, column.TeamName, "DeleteColumn"))
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

        [HttpGet]
        public async Task<IActionResult> GetColumns(string team)
        {
            if (string.IsNullOrEmpty(team))
                return BadRequest("Team name is required");

            var columns = await _context.TeamColumns
                .Where(c => c.TeamName == team)
                .OrderBy(c => c.Order)
                .Select(c => new { c.Id, c.ColumnName })
                .ToListAsync();

            return Ok(columns);
        }
    }
}
