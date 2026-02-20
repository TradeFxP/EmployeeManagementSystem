using System;
using System.ComponentModel.DataAnnotations;

namespace UserRoles.Models
{
    public class ExcelImportLog
    {
        public int Id { get; set; }

        [Required]
        public string FileName { get; set; } = string.Empty;

        [Required]
        public string TeamName { get; set; } = string.Empty;

        public int ColumnId { get; set; }
        public string ColumnName { get; set; } = string.Empty;

        public int TotalRows { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }

        // Status: Processing, Completed, Failed
        public string Status { get; set; } = "Processing";

        public string? ErrorDetails { get; set; }

        // Who imported
        public string ImportedByUserId { get; set; } = null!;
        public Users? ImportedByUser { get; set; }

        public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

        // Duration in milliseconds
        public long? DurationMs { get; set; }
    }
}
