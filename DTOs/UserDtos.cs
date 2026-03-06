namespace UserRoles.DTOs
{
    // ═══════ Role Management ═══════
    public class ChangeRoleRequest
    {
        public string UserId { get; set; } = "";
        public string NewRole { get; set; } = "";
        public string? ParentId { get; set; }
    }
}
