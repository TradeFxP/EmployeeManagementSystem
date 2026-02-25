namespace UserRoles.Models
{
    public class Story
    {
        public int Id { get; set; }
        
        // Auto-generated ID: E1F1S1, E1F1S2, E2F1S1...
        public string WorkItemId { get; set; } = string.Empty;
        
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Order { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Parent Feature
        public int FeatureId { get; set; }
        public Feature? Feature { get; set; }

        // Created by
        public string? CreatedByUserId { get; set; }
        public Users? CreatedByUser { get; set; }

        // Assigned to
        public string? AssignedToUserId { get; set; }
        public Users? AssignedToUser { get; set; }

        // Navigation - Tasks under this Story
        public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    }
}
