using System;

namespace UserRoles.Models
{
    public class MoveRequest
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int TaskId { get; set; }
        public string TaskTitle { get; set; } = string.Empty;
        public int FromColumnId { get; set; }
        public string FromColumnName { get; set; } = string.Empty;
        public int ToColumnId { get; set; }
        public string ToColumnName { get; set; } = string.Empty;
        public string RequestedByUserId { get; set; } = string.Empty;
        public string RequestedByUserName { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
        public string? AdminReply { get; set; }
        public DateTime? HandledAt { get; set; }
        public string? HandledByUserName { get; set; }
        public bool IsNew { get; set; } = true;
    }
}
