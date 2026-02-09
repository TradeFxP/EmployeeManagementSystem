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

using TaskStatusEnum = UserRoles.Models.Enums.TaskStatus;


[Authorize]
public class TasksController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<Users> _userManager;

    public TasksController(AppDbContext context, UserManager<Users> userManager)
    {
        _context = context;
        _userManager = userManager;
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

        if (string.IsNullOrWhiteSpace(model.Title))
            return BadRequest("Title is required");

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

            Status = TaskStatusEnum.ToDo,

            CreatedByUserId = user.Id,
            AssignedToUserId = user.Id,      // creator owns initially
            AssignedByUserId = user.Id,      // 🔥 IMPORTANT
            AssignedAt = DateTime.UtcNow,    // 🔥 IMPORTANT
            CreatedAt = DateTime.UtcNow
        };


        _context.TaskItems.Add(task);
        await _context.SaveChangesAsync();


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

        task.Title = model.Title?.Trim();
        task.Description = model.Description?.Trim();
        task.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(model.AssignedToUserId))
        {
            if (User.IsInRole("Admin") ||
                User.IsInRole("Manager") ||
                User.IsInRole("SubManager"))
            {
                task.AssignedToUserId = model.AssignedToUserId;
            }
        }

        await _context.SaveChangesAsync();
        // ✅ RETURN JSON (IMPORTANT)
        return Json(new { success = true });
    }





    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteTask([FromBody] int taskId)

    {
        var task = await _context.TaskItems.FindAsync(taskId);
        if (task == null)
            return NotFound();

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

        // Load columns
        var columns = await _context.TeamColumns
            .Where(c => c.TeamName == team)
            .OrderBy(c => c.Order)
            .ToListAsync();

        // Load all tasks for team (with users)
        var allTasks = await _context.TaskItems
     .Where(t => t.TeamName == team)
     .Include(t => t.CreatedByUser)
     .Include(t => t.AssignedToUser)
     .Include(t => t.AssignedByUser)   // ✅ REQUIRED
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

        // ✅ 2. Build ViewModel AFTER data exists
        var vm = new TeamBoardViewModel
        {
            TeamName = team,
            Columns = columns,
            AssignableUsers = assignableUsers
        };

        // ✅ 3. Return partial view
        return PartialView("_TeamBoard", vm);


    }



    [HttpPost]
    [Authorize(Roles = "Admin")]
    public IActionResult AddColumn([FromBody] AddColumnRequest model)
    {
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
    [Authorize(Roles = "Admin")]
    public IActionResult ReorderColumns([FromBody] List<int> columnIds)
    {
        // 🔒 Safety check
        if (columnIds == null || columnIds.Count == 0)
            return BadRequest("No columns received");

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
    [Authorize(Roles = "Admin")]
    public IActionResult RenameColumn([FromBody] RenameColumnRequest model)
    {
        if (model == null || model.ColumnId <= 0 || string.IsNullOrWhiteSpace(model.Name))
            return BadRequest("Invalid request");

        var col = _context.TeamColumns.Find(model.ColumnId);

        if (col == null)
            return NotFound("Column not found");

        col.ColumnName = model.Name.Trim();
        _context.SaveChanges();

        return Ok();
    }

    public class DeleteColumnRequest
    {
        public int ColumnId { get; set; }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public IActionResult DeleteColumn([FromBody] DeleteColumnRequest model)
    {
        if (model == null || model.ColumnId <= 0)
            return BadRequest("Invalid request");

        var hasTasks = _context.TaskItems.Any(t => t.ColumnId == model.ColumnId);
        if (hasTasks)
            return BadRequest("Move tasks before deleting column");

        var col = _context.TeamColumns.Find(model.ColumnId);
        if (col == null)
            return NotFound();

        _context.TeamColumns.Remove(col);
        _context.SaveChanges();

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

        // 🔹 Get task
        var task = await _context.TaskItems
            .FirstOrDefaultAsync(t => t.Id == model.TaskId);

        if (task == null)
            return NotFound("Task not found");

        // 🔹 Get target column
        var targetColumn = await _context.TeamColumns
            .FirstOrDefaultAsync(c => c.Id == model.ColumnId);

        if (targetColumn == null)
            return NotFound("Target column not found");

        // ✅ Move task to new column
        task.ColumnId = targetColumn.Id;

        // ✅ Sync status with column name
        task.Status = targetColumn.ColumnName switch
        {
            "ToDo" => TaskStatusEnum.ToDo,
            "Doing" => TaskStatusEnum.Doing,
            "Review" => TaskStatusEnum.Review,
            //"Done" => TaskStatusEnum.Done,
            _ => task.Status
        };

        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok();
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
        task.AssignedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new 
        { 
            success = true, 
            message = $"Task moved to {model.TeamName} ({targetColumn.ColumnName})" 
        });
    }
}


