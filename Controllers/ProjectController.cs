using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using UserRoles.Hubs;
using UserRoles.Data;
using UserRoles.Models;
using UserRoles.ViewModels;
using UserRoles.Services;

namespace UserRoles.Controllers
{
    [Authorize]
    public class ProjectController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly IHubContext<TaskHub> _hubContext;
        private readonly ITaskHistoryService _historyService;

        public ProjectController(AppDbContext context, 
            UserManager<Users> userManager, 
            IHubContext<TaskHub> hubContext,
            ITaskHistoryService historyService)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
            _historyService = historyService;
        }

        // ==================== INDEX ====================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            IQueryable<Project> query = _context.Projects
                .AsNoTracking()
                .Include(p => p.CreatedByUser);

            if (!isAdmin)
            {
                // Only show projects where user is a member or creator
                query = query.Where(p => p.CreatedByUserId == user.Id || p.Members.Any(m => m.UserId == user.Id));
            }

            var projects = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

            return View(projects);
        }

        // ==================== BOARD VIEW ====================
        [HttpGet]
        public async Task<IActionResult> Board(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            var project = await _context.Projects
                .AsNoTracking()
                .Include(p => p.Members).ThenInclude(m => m.User)
                .Include(p => p.Epics.OrderBy(e => e.Order))
                    .ThenInclude(e => e.AssignedToUser)
                .Include(p => p.Epics.OrderBy(e => e.Order))
                    .ThenInclude(e => e.CreatedByUser)
                .Include(p => p.Epics.OrderBy(e => e.Order))
                    .ThenInclude(e => e.Features.OrderBy(f => f.Order))
                        .ThenInclude(f => f.AssignedToUser)
                .Include(p => p.Epics.OrderBy(e => e.Order))
                    .ThenInclude(e => e.Features.OrderBy(f => f.Order))
                        .ThenInclude(f => f.Stories.OrderBy(s => s.Order))
                            .ThenInclude(s => s.AssignedToUser)
                .Include(p => p.Epics.OrderBy(e => e.Order))
                    .ThenInclude(e => e.Features.OrderBy(f => f.Order))
                        .ThenInclude(f => f.Stories.OrderBy(s => s.Order))
                            .ThenInclude(s => s.Tasks)
                                .ThenInclude(t => t.AssignedToUser)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            // Check access
            if (!isAdmin && project.CreatedByUserId != user.Id && !project.Members.Any(m => m.UserId == user.Id))
            {
                return Forbid();
            }

            // 🔥 PERFORMANCE: Bulk-fetch roles
            var userRolesData = await (from ur in _context.UserRoles
                                       join r in _context.Roles on ur.RoleId equals r.Id
                                       select new { ur.UserId, RoleName = r.Name })
                                      .AsNoTracking()
                                      .ToListAsync();

            var userRolesMap = userRolesData
                .GroupBy(ur => ur.UserId)
                .ToDictionary(g => g.Key, g => g.Select(ur => ur.RoleName).FirstOrDefault() ?? "User");

            var vm = new ProjectBoardViewModel
            {
                Project = project,
                Epics = project.Epics.ToList(),
                AssignableUsers = await _userManager.Users.AsNoTracking().ToListAsync(),
                UserRolesMap = userRolesMap
            };

            return PartialView("_ProjectBoard", vm);
        }

        // ==================== GET PROJECTS LIST ====================
        [HttpGet]
        public async Task<IActionResult> GetProjects()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            IQueryable<Project> query = _context.Projects.AsNoTracking();

            if (!isAdmin)
            {
                query = query.Where(p => p.CreatedByUserId == user.Id || p.Members.Any(m => m.UserId == user.Id));
            }

            var projects = await query
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new { p.Id, p.Name })
                .ToListAsync();

            return Json(projects);
        }

        // ==================== CREATE PROJECT ====================
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
                return BadRequest("Project name is required");

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var project = new Project
            {
                Name = model.Name.Trim(),
                Description = model.Description?.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = user.Id
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, id = project.Id, name = project.Name });
        }

        // ==================== CREATE EPIC ====================
        [HttpPost]
        public async Task<IActionResult> CreateEpic([FromBody] CreateEpicRequest model)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                return BadRequest("Epic title is required");

            var project = await _context.Projects.FindAsync(model.ProjectId);
            if (project == null)
                return BadRequest("Project not found");

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Generate WorkItemId: E1, E2, E3...
            var epicCount = await _context.Epics
                .Where(e => e.ProjectId == model.ProjectId)
                .CountAsync();

            var workItemId = $"E{epicCount + 1}";

            var epic = new Epic
            {
                WorkItemId = workItemId,
                Title = model.Title.Trim(),
                Description = model.Description?.Trim(),
                ProjectId = model.ProjectId,
                Order = epicCount + 1,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = user.Id,
                AssignedToUserId = model.AssignedToUserId
            };

            _context.Epics.Add(epic);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, id = epic.Id, workItemId = epic.WorkItemId });
        }

        // ==================== CREATE FEATURE ====================
        [HttpPost]
        public async Task<IActionResult> CreateFeature([FromBody] CreateFeatureRequest model)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                return BadRequest("Feature title is required");

            var epic = await _context.Epics.FindAsync(model.EpicId);
            if (epic == null)
                return BadRequest("Epic not found");

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Generate WorkItemId: E1F1, E1F2...
            var featureCount = await _context.Features
                .Where(f => f.EpicId == model.EpicId)
                .CountAsync();

            var workItemId = $"{epic.WorkItemId}F{featureCount + 1}";

            var feature = new Feature
            {
                WorkItemId = workItemId,
                Title = model.Title.Trim(),
                Description = model.Description?.Trim(),
                EpicId = model.EpicId,
                Order = featureCount + 1,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = user.Id,
                AssignedToUserId = model.AssignedToUserId
            };

            _context.Features.Add(feature);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, id = feature.Id, workItemId = feature.WorkItemId });
        }

        // ==================== CREATE STORY ====================
        [HttpPost]
        public async Task<IActionResult> CreateStory([FromBody] CreateStoryRequest model)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                return BadRequest("Story title is required");

            var feature = await _context.Features
                .Include(f => f.Epic)
                .FirstOrDefaultAsync(f => f.Id == model.FeatureId);

            if (feature == null)
                return BadRequest("Feature not found");

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Generate WorkItemId: E1F1S1, E1F1S2...
            var storyCount = await _context.Stories
                .Where(s => s.FeatureId == model.FeatureId)
                .CountAsync();

            var workItemId = $"{feature.WorkItemId}S{storyCount + 1}";

            var story = new Story
            {
                WorkItemId = workItemId,
                Title = model.Title.Trim(),
                Description = model.Description?.Trim(),
                FeatureId = model.FeatureId,
                Order = storyCount + 1,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = user.Id,
                AssignedToUserId = model.AssignedToUserId
            };

            _context.Stories.Add(story);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, id = story.Id, workItemId = story.WorkItemId });
        }

        // ==================== CREATE TASK (Project Hierarchy) ====================
        [HttpPost]
        public async Task<IActionResult> CreateProjectTask([FromBody] CreateProjectTaskRequest model)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                return BadRequest("Task title is required");

            var story = await _context.Stories
                .Include(s => s.Feature)
                    .ThenInclude(f => f.Epic)
                        .ThenInclude(e => e.Project)
                .FirstOrDefaultAsync(s => s.Id == model.StoryId);

            if (story == null)
                return BadRequest("Story not found");

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Generate WorkItemId: E1F1S1T1, E1F1S1T2...
            var taskCount = await _context.TaskItems
                .Where(t => t.StoryId == model.StoryId)
                .CountAsync();

            var workItemId = $"{story.WorkItemId}T{taskCount + 1}";

            // Get or create a default column for project tasks
            var defaultColumn = await _context.TeamColumns
                .FirstOrDefaultAsync(c => c.TeamName == "Development" && c.ColumnName == "ToDo");

            if (defaultColumn == null)
            {
                // Create a default column if none exists
                defaultColumn = new TeamColumn
                {
                    TeamName = "Development",
                    ColumnName = "ToDo",
                    Order = 1
                };
                _context.TeamColumns.Add(defaultColumn);
                await _context.SaveChangesAsync();
            }

            var task = new TaskItem
            {
                WorkItemId = workItemId,
                Title = model.Title.Trim(),
                Description = model.Description?.Trim() ?? "",
                StoryId = model.StoryId,
                ProjectId = story.Feature?.Epic?.ProjectId,
                TeamName = "Development",
                ColumnId = defaultColumn.Id,
                Status = Models.Enums.TaskStatus.ToDo,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = user.Id,
                AssignedToUserId = model.AssignedToUserId ?? user.Id,
                AssignedByUserId = user.Id,
                AssignedAt = DateTime.UtcNow
            };

            _context.TaskItems.Add(task);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, id = task.Id, workItemId = task.WorkItemId });
        }

        // ==================== SEARCH ====================
        [HttpGet]
        public async Task<IActionResult> Search(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return Json(new List<WorkItemSearchResult>());

            var query = q.Trim().ToUpper();
            var results = new List<WorkItemSearchResult>();

            // Search Epics
            var epics = await _context.Epics
                .AsNoTracking()
                .Include(e => e.Project)
                .Where(e => e.WorkItemId.Contains(query) || e.Title.Contains(q))
                .Take(10)
                .ToListAsync();

            results.AddRange(epics.Select(e => new WorkItemSearchResult
            {
                WorkItemId = e.WorkItemId,
                Type = "Epic",
                Title = e.Title,
                Breadcrumb = $"{e.Project?.Name} > {e.Title}",
                Id = e.Id
            }));

            // Search Features
            var features = await _context.Features
                .AsNoTracking()
                .Include(f => f.Epic).ThenInclude(e => e.Project)
                .Where(f => f.WorkItemId.Contains(query) || f.Title.Contains(q))
                .Take(10)
                .ToListAsync();

            results.AddRange(features.Select(f => new WorkItemSearchResult
            {
                WorkItemId = f.WorkItemId,
                Type = "Feature",
                Title = f.Title,
                Breadcrumb = $"{f.Epic?.Project?.Name} > {f.Epic?.Title} > {f.Title}",
                Id = f.Id
            }));

            // Search Stories
            var stories = await _context.Stories
                .AsNoTracking()
                .Include(s => s.Feature).ThenInclude(f => f.Epic).ThenInclude(e => e.Project)
                .Where(s => s.WorkItemId.Contains(query) || s.Title.Contains(q))
                .Take(10)
                .ToListAsync();

            results.AddRange(stories.Select(s => new WorkItemSearchResult
            {
                WorkItemId = s.WorkItemId,
                Type = "Story",
                Title = s.Title,
                Breadcrumb = $"{s.Feature?.Epic?.Project?.Name} > {s.Feature?.Epic?.Title} > {s.Feature?.Title} > {s.Title}",
                Id = s.Id
            }));

            // Search Tasks
            var tasks = await _context.TaskItems
                .AsNoTracking()
                .Include(t => t.Story).ThenInclude(s => s.Feature).ThenInclude(f => f.Epic).ThenInclude(e => e.Project)
                .Where(t => t.WorkItemId != null && (t.WorkItemId.Contains(query) || t.Title.Contains(q)))
                .Take(10)
                .ToListAsync();

            results.AddRange(tasks.Select(t => new WorkItemSearchResult
            {
                WorkItemId = t.WorkItemId ?? "",
                Type = "Task",
                Title = t.Title,
                Breadcrumb = $"{t.Story?.Feature?.Epic?.Project?.Name} > {t.Story?.Feature?.Epic?.Title} > {t.Story?.Feature?.Title} > {t.Story?.Title} > {t.Title}",
                Id = t.Id
            }));

            return Json(results.Take(20));
        }

        // ==================== REORDER EPICS ====================
        [HttpPost]
        public async Task<IActionResult> ReorderEpics([FromBody] List<int> epicIds)
        {
            if (epicIds == null || epicIds.Count == 0)
                return BadRequest();

            var epics = await _context.Epics
                .Where(e => epicIds.Contains(e.Id))
                .ToListAsync();

            for (int i = 0; i < epicIds.Count; i++)
            {
                var epic = epics.FirstOrDefault(e => e.Id == epicIds[i]);
                if (epic != null)
                    epic.Order = i + 1;
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // ==================== REORDER FEATURES ====================
        [HttpPost]
        public async Task<IActionResult> ReorderFeatures([FromBody] List<int> featureIds)
        {
            if (featureIds == null || featureIds.Count == 0)
                return BadRequest();

            var features = await _context.Features
                .Where(f => featureIds.Contains(f.Id))
                .ToListAsync();

            for (int i = 0; i < featureIds.Count; i++)
            {
                var feature = features.FirstOrDefault(f => f.Id == featureIds[i]);
                if (feature != null)
                    feature.Order = i + 1;
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // ==================== REORDER STORIES ====================
        [HttpPost]
        public async Task<IActionResult> ReorderStories([FromBody] List<int> storyIds)
        {
            if (storyIds == null || storyIds.Count == 0)
                return BadRequest();

            var stories = await _context.Stories
                .Where(s => storyIds.Contains(s.Id))
                .ToListAsync();

            for (int i = 0; i < storyIds.Count; i++)
            {
                var story = stories.FirstOrDefault(s => s.Id == storyIds[i]);
                if (story != null)
                    story.Order = i + 1;
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // ==================== DELETE EPIC ====================
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteEpic([FromBody] int epicId)
        {
            var epic = await _context.Epics
                .Include(e => e.Features)
                .FirstOrDefaultAsync(e => e.Id == epicId);

            if (epic == null) return NotFound();

            if (epic.Features.Any())
                return BadRequest("Delete all features first");

            _context.Epics.Remove(epic);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // ==================== DELETE FEATURE ====================
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteFeature([FromBody] int featureId)
        {
            var feature = await _context.Features
                .Include(f => f.Stories)
                .FirstOrDefaultAsync(f => f.Id == featureId);

            if (feature == null) return NotFound();

            if (feature.Stories.Any())
                return BadRequest("Delete all stories first");

            _context.Features.Remove(feature);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // ==================== DELETE STORY ====================
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteStory([FromBody] int storyId)
        {
            var story = await _context.Stories
                .Include(s => s.Tasks)
                .FirstOrDefaultAsync(s => s.Id == storyId);

            if (story == null) return NotFound();

            if (story.Tasks.Any())
                return BadRequest("Delete all tasks first");

            _context.Stories.Remove(story);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // ==================== UPDATE EPIC ====================
        [HttpPost]
        public async Task<IActionResult> UpdateEpic([FromBody] CreateEpicRequest model)
        {
            var epic = await _context.Epics.FindAsync(model.Id);
            if (epic == null) return NotFound();

            epic.Title = model.Title.Trim();
            epic.Description = model.Description?.Trim();
            epic.AssignedToUserId = model.AssignedToUserId;
            epic.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok();
        }

        // ==================== UPDATE FEATURE ====================
        [HttpPost]
        public async Task<IActionResult> UpdateFeature([FromBody] CreateFeatureRequest model)
        {
            var feature = await _context.Features.FindAsync(model.Id);
            if (feature == null) return NotFound();

            feature.Title = model.Title.Trim();
            feature.Description = model.Description?.Trim();
            feature.AssignedToUserId = model.AssignedToUserId;
            feature.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok();
        }

        // ==================== UPDATE STORY ====================
        [HttpPost]
        public async Task<IActionResult> UpdateStory([FromBody] CreateStoryRequest model)
        {
            var story = await _context.Stories.FindAsync(model.Id);
            if (story == null) return NotFound();

            story.Title = model.Title.Trim();
            story.Description = model.Description?.Trim();
            story.AssignedToUserId = model.AssignedToUserId;
            story.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok();
        }
        // ==================== MEMBERS = [NEW] ====================
        [HttpGet]
        public async Task<IActionResult> GetProjectMembers(int projectId)
        {
            var project = await _context.Projects
                .Include(p => p.Members).ThenInclude(m => m.User)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null) return NotFound();

            var members = project.Members.Select(m => new ProjectMemberViewModel
            {
                UserId = m.UserId,
                Name = m.User?.Name ?? m.User?.UserName,
                Email = m.User?.Email,
                Role = m.ProjectRole
            }).ToList();

            return Json(members);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> AddMemberToProject([FromBody] AddProjectMemberRequest model)
        {
            var project = await _context.Projects.FindAsync(model.ProjectId);
            if (project == null) return NotFound();

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return BadRequest("User not found");

            // Check if already a member
            if (await _context.ProjectMembers.AnyAsync(m => m.ProjectId == model.ProjectId && m.UserId == model.UserId))
                return BadRequest("User is already a member of this project");

            var member = new ProjectMember
            {
                ProjectId = model.ProjectId,
                UserId = model.UserId,
                ProjectRole = model.Role ?? "Contributor",
                AddedAt = DateTime.UtcNow
            };

            _context.ProjectMembers.Add(member);
            await _context.SaveChangesAsync();

            // Notify the user in real-time
            await _hubContext.Clients.User(model.UserId).SendAsync("ProjectAccessUpdated");

            return Ok(new { success = true });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> RemoveMemberFromProject([FromBody] AddProjectMemberRequest model)
        {
            var member = await _context.ProjectMembers
                .FirstOrDefaultAsync(m => m.ProjectId == model.ProjectId && m.UserId == model.UserId);

            if (member == null) return NotFound();

            _context.ProjectMembers.Remove(member);
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        // ==================== QUICK ASSIGN ====================
        [HttpPost]
        public async Task<IActionResult> QuickAssignWorkItem([FromBody] QuickAssignRequest model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            switch (model.ItemType.ToLower())
            {
                case "epic":
                    var epic = await _context.Epics.FindAsync(model.ItemId);
                    if (epic == null) return NotFound();
                    epic.AssignedToUserId = model.UserId;
                    epic.UpdatedAt = DateTime.UtcNow;
                    break;

                case "feature":
                    var feature = await _context.Features.FindAsync(model.ItemId);
                    if (feature == null) return NotFound();
                    feature.AssignedToUserId = model.UserId;
                    feature.UpdatedAt = DateTime.UtcNow;
                    break;

                case "story":
                    var story = await _context.Stories.FindAsync(model.ItemId);
                    if (story == null) return NotFound();
                    story.AssignedToUserId = model.UserId;
                    story.UpdatedAt = DateTime.UtcNow;
                    break;

                case "task":
                    var task = await _context.TaskItems.FindAsync(model.ItemId);
                    if (task == null) return NotFound();
                    
                    // Log history for task assignment
                    await _historyService.LogAssignment(task.Id, model.UserId ?? "", user.Id);
                    
                    task.AssignedToUserId = model.UserId;
                    task.UpdatedAt = DateTime.UtcNow;
                    break;

                default:
                    return BadRequest("Invalid item type");
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ==================== UPDATE TASK ====================
        [HttpPost]
        public async Task<IActionResult> UpdateProjectTask([FromBody] CreateProjectTaskRequest model)
        {
            var task = await _context.TaskItems.FindAsync(model.Id);
            if (task == null) return NotFound();

            task.Title = model.Title.Trim();
            task.Description = model.Description?.Trim() ?? "";
            task.AssignedToUserId = model.AssignedToUserId;
            task.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok();
        }

        // ==================== DELETE TASK ====================
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteProjectTask([FromBody] int taskId)
        {
            var task = await _context.TaskItems.FindAsync(taskId);
            if (task == null) return NotFound();

            _context.TaskItems.Remove(task);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // ==================== DELETE PROJECT ====================
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteProject([FromBody] int projectId)
        {
            var project = await _context.Projects
                .Include(p => p.Epics).ThenInclude(e => e.Features).ThenInclude(f => f.Stories).ThenInclude(s => s.Tasks)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null) return NotFound();

            // Explicitly remove the project. 
            // EF Core will handle cascade delete if configured, or we rely on the loaded graph removal.
            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}
