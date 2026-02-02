using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserRoles.Data;
using UserRoles.Models;
using UserRoles.ViewModels;

//[Authorize]
//public class TasksController : Controller
//{
//    private readonly AppDbContext _context;
//    private readonly UserManager<Users> _userManager;

//    public TasksController(AppDbContext context, UserManager<Users> userManager)
//    {
//        _context = context;
//        _userManager = userManager;
//    }

//    // GET: Assign Task
//    [Authorize(Roles = "Admin,Manager,SubManager")]
//    public async Task<IActionResult> Create()
//    {
//        var currentUser = await _userManager.GetUserAsync(User);
//        var allUsers = await _userManager.Users.ToListAsync();

//        var allowedUsers = new List<Users>();

//        foreach (var u in allUsers)
//        {
//            if (u.Id == currentUser.Id)
//                continue;

//            // ADMIN
//            if (User.IsInRole("Admin"))
//            {
//                allowedUsers.Add(u);
//            }
//            // MANAGER
//            else if (User.IsInRole("Manager"))
//            {
//                if (u.ParentUserId == currentUser.Id &&
//                    !await _userManager.IsInRoleAsync(u, "Admin"))
//                {
//                    allowedUsers.Add(u);
//                }
//            }
//            // SUB MANAGER
//            else if (User.IsInRole("SubManager"))
//            {
//                if (u.ParentUserId == currentUser.Id &&
//                    await _userManager.IsInRoleAsync(u, "User"))
//                {
//                    allowedUsers.Add(u);
//                }
//            }
//        }

//        ViewBag.Users = allowedUsers;

//        // 🔽 FETCH TASKS ASSIGNED BY CURRENT USER
//        var assignedTasks = await _context.AssignedTasks
//            .Include(t => t.AssignedTo)
//            .Where(t => t.AssignedById == currentUser.Id)
//            .OrderByDescending(t => t.CreatedAt)
//            .ToListAsync();

//        ViewBag.AssignedTasks = assignedTasks;

//        return View();

//        return View();
//    }



//    // POST: Assign Task
//    [HttpPost]
//    [ValidateAntiForgeryToken]
//    [Authorize(Roles = "Admin,Manager,SubManager")]
//    public async Task<IActionResult> Create(string title, string priority, string assignedToId)
//    {
//        var assignedBy = await _userManager.GetUserAsync(User);

//        if (assignedBy.Id == assignedToId)
//            return Forbid();

//        var targetUser = await _userManager.FindByIdAsync(assignedToId);
//        if (targetUser == null)
//            return NotFound();

//        // 🔒 ROLE ENFORCEMENT
//        if (User.IsInRole("Manager"))
//        {
//            if (targetUser.ParentUserId != assignedBy.Id)
//                return Forbid();

//            if (await _userManager.IsInRoleAsync(targetUser, "Admin"))
//                return Forbid();
//        }

//        if (User.IsInRole("SubManager"))
//        {
//            if (targetUser.ParentUserId != assignedBy.Id)
//                return Forbid();

//            if (!await _userManager.IsInRoleAsync(targetUser, "User"))
//                return Forbid();
//        }

//        var task = new AssignedTask
//        {
//            Title = title,
//            Priority = priority,
//            AssignedById = assignedBy.Id,
//            AssignedToId = assignedToId
//        };

//        _context.AssignedTasks.Add(task);
//        await _context.SaveChangesAsync();

//        TempData["Success"] = "Task assigned successfully";
//        return RedirectToAction("Create");
//    }



//    [Authorize(Roles = "User")]
//    public async Task<IActionResult> AssignedToMe()
//    {
//        var user = await _userManager.GetUserAsync(User);

//        var tasks = await _context.AssignedTasks
//            .Include(t => t.AssignedBy)
//            .Where(t => t.AssignedToId == user.Id)
//            .OrderByDescending(t => t.CreatedAt)
//            .ToListAsync();

//        return View(tasks);
//    }

//    // ✅ VIEW TASKS – ALL LOGGED-IN USERS
//    public async Task<IActionResult> Index()
//    {
//        var currentUser = await _userManager.GetUserAsync(User);

//        var tasks = await _context.AssignedTasks
//            .Include(t => t.AssignedBy)
//            .Where(t => t.AssignedToId == currentUser.Id)
//            .OrderByDescending(t => t.CreatedAt)
//            .ToListAsync();

//        return View(tasks);
//    }





//}
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
    public IActionResult Index()
    {
        return View();
    }

    // ================= BOARDS =================
    // Admin → Team tasks
    // Others → Self tasks
    [HttpGet]
    public async Task<IActionResult> Boards()
    {
        var user = await _userManager.GetUserAsync(User);

        if (User.IsInRole("Admin"))
        {
            // 🔴 IMPORTANT: NO FILTER HERE
            var teamTasks = _context.TaskItems
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .OrderBy(t => t.Status)
            .ToList();


            return PartialView("_TaskBoard", teamTasks);
        }

        var selfTasks = _context.TaskItems
             .Include(t => t.CreatedByUser)
             .Include(t => t.AssignedToUser)
             .Where(t => t.AssignedToUserId == user.Id)
             .OrderBy(t => t.Status)
             .ToList();


        return PartialView("_TaskBoard", selfTasks);
    }


    // ================= TEAM TASKS =================
    // Admin / Manager / Sub-Manager
    [Authorize(Roles = "Admin,Manager,Sub-Manager")]
    [HttpGet]
    public IActionResult TeamTasks()
    {
        // IMPORTANT: No filtering by user
        var tasks = _context.TaskItems
          .Include(t => t.CreatedByUser)
          .Include(t => t.AssignedToUser)
          .OrderBy(t => t.Status)
          .ToList();
        ViewBag.Users = _userManager.Users.ToList();

        return PartialView("_TaskBoard", tasks);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateTask(string title, string description)
    {
        // 🔒 Validate FIRST
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            return BadRequest("Title and Description are required.");

        var user = await _userManager.GetUserAsync(User);

        var task = new TaskItem
        {
            Title = title,
            Description = description,

            // Always start in ToDo
            Status = UserRoles.Models.Enums.TaskStatus.ToDo,

            // Assigned to creator by default
            AssignedToUserId = user.Id,

            // Audit fields
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };

        _context.TaskItems.Add(task);
        await _context.SaveChangesAsync();

        return Ok();
    }


    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        var task = await _context.TaskItems.FindAsync(id);

        if (task == null)
            return NotFound();

        // Safety: allow delete only for ToDo
        if (task.Status != UserRoles.Models.Enums.TaskStatus.ToDo)
            return BadRequest();

        _context.TaskItems.Remove(task);
        await _context.SaveChangesAsync();

        return Ok();
    }


    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignTask(int taskId, string userId)
    {
        var task = await _context.TaskItems.FindAsync(taskId);
        if (task == null) return NotFound();

        task.AssignedToUserId = userId;
        await _context.SaveChangesAsync();

        return Ok();
    }


    //edittask
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditTask(int id, string title, string description)
    {
        var task = await _context.TaskItems.FindAsync(id);
        if (task == null) return NotFound();

        task.Title = title;
        task.Description = description;

        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> UpdateStatus(int taskId, UserRoles.Models.Enums.TaskStatus newStatus)
    {
        var task = await _context.TaskItems.FindAsync(taskId);
        if (task == null)
            return NotFound();

        // 🔐 ROLE-BASED SECURITY CHECK
        var isAdmin = User.IsInRole("Admin");
        var isManager = User.IsInRole("Manager") || User.IsInRole("Sub-Manager");

        // USER RULES
        if (!isAdmin && !isManager)
        {
            if (
                (task.Status == UserRoles.Models.Enums.TaskStatus.ToDo && newStatus != UserRoles.Models.Enums.TaskStatus.Doing) ||
                (task.Status == UserRoles.Models.Enums.TaskStatus.Doing && newStatus != UserRoles.Models.Enums.TaskStatus.Review)
            )
            {
                return Forbid();
            }
        }

        // MANAGER / ADMIN RULES
        if (isManager || isAdmin)
        {
            bool valid =
                (task.Status == UserRoles.Models.Enums.TaskStatus.ToDo && newStatus == UserRoles.Models.Enums.TaskStatus.Doing) ||
                (task.Status == UserRoles.Models.Enums.TaskStatus.Doing && newStatus == UserRoles.Models.Enums.TaskStatus.Review) ||
                (task.Status == UserRoles.Models.Enums.TaskStatus.Review && newStatus == UserRoles.Models.Enums.TaskStatus.Complete);

            if (!valid)
                return Forbid();
        }

        task.Status = newStatus;
        await _context.SaveChangesAsync();

        return Ok();
    }

    public IActionResult TeamBoard(string team)
    {
        // 1️⃣ Load columns for the selected team (ordered)
        var columns = _context.TeamColumns
            .Where(c => c.TeamName == team)
            .OrderBy(c => c.Order)
            .ToList();

        // 2️⃣ Load tasks that belong to this team
        var tasks = _context.TaskItems
            .Where(t => t.TeamName == team)
            .ToList();

        // 3️⃣ Pass both columns + tasks to the view
        var vm = new TeamBoardViewModel
        {
            TeamName = team,
            Columns = columns,
            Tasks = tasks
        };

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
        for (int i = 0; i < columnIds.Count; i++)
        {
            var col = _context.TeamColumns.Find(columnIds[i]);
            if (col != null)
                col.Order = i + 1;
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



}

