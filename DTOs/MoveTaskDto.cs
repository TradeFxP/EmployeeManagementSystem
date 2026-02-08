namespace UserRoles.DTOs
{
    // Used when dragging task between columns
    public class MoveTaskDto
    {
        public int TaskId { get; set; }
        public int ColumnId { get; set; }
    }
}
