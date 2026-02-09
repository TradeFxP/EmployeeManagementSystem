namespace UserRoles.Models
{
    public class TaskItem
    {
        public int Id { get; set; }

        // Auto-generated ID: P1T1, P1T2 (for project-linked tasks) or E1F1S1T1 (for story-linked tasks)
        public string? WorkItemId { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }

        // Optional: Direct link to Project (for team tasks with project context)
        public int? ProjectId { get; set; }
        public Project? Project { get; set; }

        // Optional: Parent Story (for full hierarchy via Epic/Feature/Story)
        public int? StoryId { get; set; }
        public Story? Story { get; set; }

        public UserRoles.Models.Enums.TaskStatus Status { get; set; }
        public string AssignedToUserId { get; set; }
        public Users AssignedToUser { get; set; }

        public string? AssignedByUserId { get; set; }
        public Users? AssignedByUser { get; set; }

        public DateTime? AssignedAt { get; set; }


        public string CreatedByUserId { get; set; }
        public Users CreatedByUser { get; set; }   // ✅ ADD THIS

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
        // Team info
        public string TeamName { get; set; }

        // Column (FK)
        public int ColumnId { get; set; }
        public TeamColumn Column { get; set; }
    }

}
