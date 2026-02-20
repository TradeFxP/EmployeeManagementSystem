namespace UserRoles.Models
{
    public class TaskCustomField
    {
        public int Id { get; set; }
        
        public string FieldName { get; set; } = string.Empty;
        
        // Field type: "Text", "Number", "Date", "Dropdown"
        public string FieldType { get; set; } = "Text";
        
        public bool IsRequired { get; set; } = false;

        // Comma-separated or JSON list of options for Dropdown type
        public string? DropdownOptions { get; set; }
        
        // Soft delete support
        public bool IsActive { get; set; } = true;
        
        // Team association (for team-specific fields)
        public string? TeamName { get; set; }
        
        // Display order
        public int Order { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Created by
        public string? CreatedByUserId { get; set; }
        public Users? CreatedByUser { get; set; }
        
        // Navigation - Field values for tasks
        public ICollection<TaskFieldValue> FieldValues { get; set; } = new List<TaskFieldValue>();
    }
}
