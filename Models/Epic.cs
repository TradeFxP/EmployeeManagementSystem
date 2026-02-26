namespace UserRoles.Models
{
    public class Epic
    {
        public int Id { get; set; }
        
        // Auto-generated ID: E1, E2, E3...
        public string WorkItemId { get; set; } = string.Empty;
        
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Order { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Parent Project
        public int ProjectId { get; set; }
        public Project? Project { get; set; }

        // Created by
        public string? CreatedByUserId { get; set; }
        public Users? CreatedByUser { get; set; }

        // Assigned to
        public string? AssignedToUserId { get; set; }
        public Users? AssignedToUser { get; set; }

        // Navigation - Features under this Epic
        public ICollection<Feature> Features { get; set; } = new List<Feature>();
    }
}
