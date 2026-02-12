namespace UserRoles.Models
{
    public class Feature
    {
        public int Id { get; set; }
        
        // Auto-generated ID: E1F1, E1F2, E2F1...
        public string WorkItemId { get; set; } = string.Empty;
        
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Order { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Parent Epic
        public int EpicId { get; set; }
        public Epic? Epic { get; set; }

        // Created by
        public string? CreatedByUserId { get; set; }
        public Users? CreatedByUser { get; set; }

        // Navigation - Stories under this Feature
        public ICollection<Story> Stories { get; set; } = new List<Story>();
    }
}
