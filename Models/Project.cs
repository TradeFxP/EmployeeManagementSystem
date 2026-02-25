namespace UserRoles.Models
{
    public class Project
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Created by
        public string CreatedByUserId { get; set; } = string.Empty;
        public Users? CreatedByUser { get; set; }

        // Navigation - Epics under this project
        public ICollection<Epic> Epics { get; set; } = new List<Epic>();

        // Navigation - Project Members
        public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();

        // Navigation - Direct tasks (for team tasks linked to project)
        public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    }
}
