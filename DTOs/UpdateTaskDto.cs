namespace UserRoles.DTOs
{
    // DTO used ONLY for AJAX task updates
    // Keeps controller safe from over-posting
    // Does NOT represent a View
    public class UpdateTaskDto
    {
        public int TaskId { get; set; }   // Which task
        public string Title { get; set; } // Updated title
        public string Description { get; set; } // Updated description
    }
}
