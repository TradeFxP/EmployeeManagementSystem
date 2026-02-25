namespace UserRoles.Models
{
    public class TaskFieldValue
    {
        public int Id { get; set; }
        
        // Parent task
        public int TaskId { get; set; }
        public TaskItem? Task { get; set; }
        
        // Field definition
        public int FieldId { get; set; }
        public TaskCustomField? Field { get; set; }
        
        // Value stored as string (converted based on FieldType)
        public string? Value { get; set; }

        // Large binary data for Image fields (Database storage)
        public byte[]? ImageData { get; set; }
        public string? ImageMimeType { get; set; }
        public string? FileName { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
