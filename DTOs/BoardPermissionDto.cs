namespace UserRoles.DTOs
{
    public class BoardPermissionDto
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public bool CanAddColumn { get; set; }
        public bool CanRenameColumn { get; set; }
        public bool CanReorderColumns { get; set; }
        public bool CanDeleteColumn { get; set; }
        public bool CanEditAllFields { get; set; }
        public bool CanDeleteTask { get; set; }
        public bool CanReviewTask { get; set; }
    }
}
