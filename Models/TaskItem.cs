namespace UserRoles.Models
{
    public class TaskItem
    {
        public int Id { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }

        public UserRoles.Models.Enums.TaskStatus Status { get; set; }
        public string AssignedToUserId { get; set; }
        public Users AssignedToUser { get; set; }

        public string CreatedByUserId { get; set; }
        public Users CreatedByUser { get; set; }   // ✅ ADD THIS

        public DateTime CreatedAt { get; set; }


        // Team info
        public string TeamName { get; set; }

        // Column (FK)
        public int ColumnId { get; set; }
        public TeamColumn Column { get; set; }
    }

}
