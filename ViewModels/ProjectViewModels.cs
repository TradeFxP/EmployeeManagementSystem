using UserRoles.Models;

namespace UserRoles.ViewModels
{
    public class ProjectBoardViewModel
    {
        public Project? Project { get; set; }
        public List<Epic> Epics { get; set; } = new();
        public List<Users> AssignableUsers { get; set; } = new();
    }

    public class CreateProjectRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class CreateEpicRequest
    {
        public int ProjectId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? AssignedToUserId { get; set; }
    }

    public class CreateFeatureRequest
    {
        public int EpicId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? AssignedToUserId { get; set; }
    }

    public class CreateStoryRequest
    {
        public int FeatureId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? AssignedToUserId { get; set; }
    }

    public class ProjectMemberViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
    }

    public class CreateProjectTaskRequest
    {
        public int StoryId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? AssignedToUserId { get; set; }
    }

    public class AddProjectMemberRequest
    {
        public int ProjectId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string? Role { get; set; }
    }

    public class MoveWorkItemRequest
    {
        public string ItemType { get; set; } = string.Empty; // epic, feature, story, task
        public int ItemId { get; set; }
        public int NewParentId { get; set; }
        public int NewOrder { get; set; }
    }

    public class WorkItemSearchResult
    {
        public string WorkItemId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Breadcrumb { get; set; } = string.Empty;
        public int Id { get; set; }
    }

    public class QuickAssignRequest
    {
        public string ItemType { get; set; } = string.Empty; // epic, feature, story, task
        public int ItemId { get; set; }
        public string UserId { get; set; } = string.Empty;
    }
}
