using UserRoles.Models.Enums;

namespace UserRoles.ViewModels
{
    public class CreateTaskViewModel
    {

        public int ColumnId { get; set; }

        public string? Title { get; set; }
        public string? Description { get; set; }

        // Optional: Link task to a project for hierarchical ID generation
        public int? ProjectId { get; set; }
        
        // Priority level
        public TaskPriority Priority { get; set; } = TaskPriority.Medium;
        
        // Custom field values: FieldId -> Value
        public Dictionary<int, string>? CustomFieldValues { get; set; }

        public DateTime? DueDate { get; set; }
    }
}
