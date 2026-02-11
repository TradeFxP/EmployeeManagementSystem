using UserRoles.Models.Enums;

namespace UserRoles.ViewModels
{
    public class TaskHistoryDto
    {
        public int Id { get; set; }
        public TaskHistoryChangeType ChangeType { get; set; }
        public string? FieldChanged { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        
        public int? FromColumnId { get; set; }
        public string? FromColumnName { get; set; }
        public int? ToColumnId { get; set; }
        public string? ToColumnName { get; set; }
        public int? TimeSpentInSeconds { get; set; }
        
        public string ChangedByUserId { get; set; }
        public string ChangedByUserName { get; set; }
        public DateTime ChangedAt { get; set; }
        
        public string? Details { get; set; }
    }
}
