using UserRoles.Models.Enums;

namespace UserRoles.Models
{
    public class TaskHistory
    {
        public int Id { get; set; }
        
        // Link to task
        public int TaskId { get; set; }
        public TaskItem Task { get; set; }
        
        // Type of change
        public TaskHistoryChangeType ChangeType { get; set; }
        
        // Field-level changes
        public string? FieldChanged { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        
        // Column movement tracking
        public int? FromColumnId { get; set; }
        public TeamColumn? FromColumn { get; set; }
        public int? ToColumnId { get; set; }
        public TeamColumn? ToColumn { get; set; }
        public int? TimeSpentInSeconds { get; set; }
        
        // Audit fields
        public string ChangedByUserId { get; set; }
        public Users ChangedByUser { get; set; }
        public DateTime ChangedAt { get; set; }
        
        // Additional context (JSON for flexibility)
        public string? Details { get; set; }
    }
}
