using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserRoles.Data;
using UserRoles.Models;
using UserRoles.ViewModels;

namespace UserRoles.Controllers
{
    [Authorize]
    public class ProjectController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        public ProjectController(AppDbContext context, UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==================== INDEX ====================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var projects = await _context.Projects
                .Include(p => p.CreatedByUser)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(projects);
        }

        // ==================== BOARD VIEW ====================
        [HttpGet]
        public async Task<IActionResult> Board(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Epics.OrderBy(e => e.Order))
                    .ThenInclude(e => e.Features.OrderBy(f => f.Order))
                        .ThenInclude(f => f.Stories.OrderBy(s => s.Order))
                            .ThenInclude(s => s.Tasks)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            var vm = new ProjectBoardViewModel
            {
                Project = project,
                Epics = project.Epics.ToList(),
                AssignableUsers = await _userManager.Users.ToListAsync()
            };

            return PartialView("_ProjectBoard", vm);
        }

        // ==================== GET PROJECTS LIST ====================
        [HttpGet]
        public async Task<IActionResult> GetProjects()
        {
            var projects = await _context.Projects
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
                CreatedByUserId = user.Id
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
                CreatedByUserId = user.Id
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
                CreatedByUserId = user.Id
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
                AssignedToUserId = user.Id,
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
            var epic = await _context.Epics.FindAsync(model.ProjectId);
            if (epic == null) return NotFound();

            epic.Title = model.Title.Trim();
            epic.Description = model.Description?.Trim();
            epic.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok();
        }

        // ==================== UPDATE FEATURE ====================
        [HttpPost]
        public async Task<IActionResult> UpdateFeature([FromBody] CreateFeatureRequest model)
        {
            var feature = await _context.Features.FindAsync(model.EpicId);
            if (feature == null) return NotFound();

            feature.Title = model.Title.Trim();
            feature.Description = model.Description?.Trim();
            feature.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok();
        }

        // ==================== UPDATE STORY ====================
        [HttpPost]
        public async Task<IActionResult> UpdateStory([FromBody] CreateStoryRequest model)
        {
            var story = await _context.Stories.FindAsync(model.FeatureId);
            if (story == null) return NotFound();

            story.Title = model.Title.Trim();
            story.Description = model.Description?.Trim();
            story.UpdatedAt = DateTime.UtcNow;

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
