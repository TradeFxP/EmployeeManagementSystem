using UserRoles.Models.Enums;

namespace UserRoles.ViewModels
{
    public class UpdateTaskRequest
    {
        public int TaskId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }


        public string AssignedToUserId { get; set; } // optional
        
        // Priority
        public TaskPriority? Priority { get; set; }
        
        // Custom field values: FieldId -> List of strings (to support multiple images per field)
        public Dictionary<int, List<string>>? CustomFieldValues { get; set; }

        public DateTime? DueDate { get; set; }
    }
}
