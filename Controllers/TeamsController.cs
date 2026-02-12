using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UserRoles.Data;
using UserRoles.Models;

namespace UserRoles.Controllers
{
    [Authorize]
    public class TeamsController : Controller
    {
        private readonly AppDbContext _context;

        public TeamsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Teams/GetAll
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            // Self-heal: If no teams exist, seed them (recovery from failed migration seed or empty DB)
            if (!await _context.Teams.AnyAsync())
            {
                 _context.Teams.AddRange(
                    new Team { Name = "Development", CreatedAt = DateTime.UtcNow },
                    new Team { Name = "Testing", CreatedAt = DateTime.UtcNow },
                    new Team { Name = "Sales", CreatedAt = DateTime.UtcNow }
                 );
                 await _context.SaveChangesAsync();
                 
                 // Also seed columns if missing
                 if (!await _context.TeamColumns.AnyAsync()) 
                 {
                     var defaults = new List<TeamColumn>();
                     foreach(var tName in new[] { "Development", "Testing", "Sales" })
                     {
                         defaults.Add(new TeamColumn { TeamName = tName, ColumnName = "To Do", Order = 1 });
                         defaults.Add(new TeamColumn { TeamName = tName, ColumnName = "Doing", Order = 2 });
                         defaults.Add(new TeamColumn { TeamName = tName, ColumnName = "Done", Order = 3 });
                     }
                     _context.TeamColumns.AddRange(defaults);
                     await _context.SaveChangesAsync();
                 }
            }

            var teams = await _context.Teams
                .OrderBy(t => t.Name)
                .ToListAsync();
            return Ok(teams);
        }

        // POST: /Teams/Create
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateTeamRequest request)
        {
            // Case-insensitive check
            if (string.IsNullOrWhiteSpace(request?.Name))
                return BadRequest("Team name is required");

            var nameToCheck = request.Name.Trim().ToLower();

            if (await _context.Teams.AnyAsync(t => t.Name.ToLower() == nameToCheck))
                return BadRequest("Team already exists");

            var team = new Team
            {
                Name = request.Name.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Teams.Add(team);
            await _context.SaveChangesAsync();

            // Seed default columns
            var defaultColumns = new[]
            {
                new TeamColumn { TeamName = team.Name, ColumnName = "To Do", Order = 1 },
                new TeamColumn { TeamName = team.Name, ColumnName = "Doing", Order = 2 },
                new TeamColumn { TeamName = team.Name, ColumnName = "Done", Order = 3 }
            };

            _context.TeamColumns.AddRange(defaultColumns);
            await _context.SaveChangesAsync();

            return Ok(team);
        }

        // POST: /Teams/Delete
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete([FromBody] int id)
        {
            var team = await _context.Teams.FindAsync(id);
            if (team == null)
                return NotFound("Team not found");

            _context.Teams.Remove(team);
            await _context.SaveChangesAsync();

            return Ok();
        }

        public class CreateTeamRequest
        {
            public string Name { get; set; }
        }
    }
}
