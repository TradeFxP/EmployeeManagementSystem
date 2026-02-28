namespace UserRoles.DTOs
{
    public class BoardPermissionDto
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;

        // Board Level
        public bool CanAddColumn { get; set; }
        public bool CanRenameColumn { get; set; }
        public bool CanReorderColumns { get; set; }
        public bool CanDeleteColumn { get; set; }
        public bool CanEditAllFields { get; set; }
        public bool CanDeleteTask { get; set; }
        public bool CanReviewTask { get; set; }
        public bool CanImportExcel { get; set; }
        public bool CanAssignTask { get; set; }

        // Task Level (Specific permissions requested: Assign, Task, Delete, History)
        // Note: Assign and Delete overlap with board level, but we can reuse or specify.
        // 'Task' might mean 'Can Create Task' or 'Can Edit Task'.
        // 'History' might mean 'Can View History'.
        public bool CanViewHistory { get; set; }

        // Column Level
        public List<ColumnPermissionDto> ColumnPermissions { get; set; } = new List<ColumnPermissionDto>();
    }

    public class ColumnPermissionDto
    {
        public int ColumnId { get; set; }
        public string ColumnName { get; set; } = string.Empty;
        public bool CanRename { get; set; }
        public bool CanDelete { get; set; }
        public bool CanAddTask { get; set; }
        public bool CanClearTasks { get; set; }

        // Task permissions for this column
        public bool CanAssignTask { get; set; }
        public bool CanEditTask { get; set; }
        public bool CanDeleteTask { get; set; }
        public bool CanViewHistory { get; set; }
    }
}
