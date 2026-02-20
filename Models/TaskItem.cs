namespace UserRoles.Models
{
    public class TaskItem
    {
        public int Id { get; set; }

        // Auto-generated ID: P1T1, P1T2 (for project-linked tasks) or E1F1S1T1 (for story-linked tasks)
        public string? WorkItemId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        
        // Priority level
        public UserRoles.Models.Enums.TaskPriority Priority { get; set; } = UserRoles.Models.Enums.TaskPriority.Medium;

        public DateTime? DueDate { get; set; }

        // Optional: Direct link to Project (for team tasks with project context)
        public int? ProjectId { get; set; }
        public Project? Project { get; set; }

        // Optional: Parent Story (for full hierarchy via Epic/Feature/Story)
        public int? StoryId { get; set; }
        public Story? Story { get; set; }

        public UserRoles.Models.Enums.TaskStatus Status { get; set; }
        public string AssignedToUserId { get; set; } = null!;
        public Users AssignedToUser { get; set; } = null!;

        public string? AssignedByUserId { get; set; }
        public Users? AssignedByUser { get; set; }

        public DateTime? AssignedAt { get; set; }


        public string CreatedByUserId { get; set; } = null!;
        public Users CreatedByUser { get; set; } = null!;   // ✅ ADD THIS

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
        // Team info
        public string TeamName { get; set; } = string.Empty;

        // Column (FK)
        public int ColumnId { get; set; }
        public TeamColumn Column { get; set; } = null!;
        
        // Custom field values
        public ICollection<TaskFieldValue> CustomFieldValues { get; set; } = new List<TaskFieldValue>();
        
        // Column tracking
        public DateTime CurrentColumnEntryAt { get; set; } // When task entered current column
        
        // Previous column (for returning failed review tasks)
        public int? PreviousColumnId { get; set; }
        public TeamColumn? PreviousColumn { get; set; }
        
        // ================= REVIEW WORKFLOW =================
        public UserRoles.Models.Enums.ReviewStatus ReviewStatus { get; set; } = UserRoles.Models.Enums.ReviewStatus.None;
        public string? ReviewNote { get; set; }
        
        public string? ReviewedByUserId { get; set; }
        public Users? ReviewedByUser { get; set; }
        public DateTime? ReviewedAt { get; set; }
        
        // Completed by tracking
        public string? CompletedByUserId { get; set; }
        public Users? CompletedByUser { get; set; }
        public DateTime? CompletedAt { get; set; }
        
        // ================= HISTORY ARCHIVAL =================
        public bool IsArchived { get; set; } = false;
        public DateTime? ArchivedAt { get; set; }
        
        // History tracking
        public ICollection<TaskHistory> History { get; set; } = new List<TaskHistory>();
    }

}
