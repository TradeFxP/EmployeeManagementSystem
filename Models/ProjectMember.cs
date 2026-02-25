using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserRoles.Models
{
    public class ProjectMember
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        public Project? Project { get; set; }

        public string UserId { get; set; } = string.Empty;
        public Users? User { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        // Custom Role in project (Optional: Project Admin, Contributor, etc.)
        public string? ProjectRole { get; set; }
    }
}
