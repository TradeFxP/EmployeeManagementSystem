using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserRoles.Models
{
    public class BoardPermission
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public Users? User { get; set; }

        [Required]
        public string TeamName { get; set; } = string.Empty;

        // Board management permissions
        public bool CanAddColumn { get; set; }
        public bool CanRenameColumn { get; set; }
        public bool CanReorderColumns { get; set; }
        public bool CanDeleteColumn { get; set; }

        // Task management permissions
        public bool CanEditAllFields { get; set; }
        public bool CanDeleteTask { get; set; }

        // Workflow permissions
        public bool CanReviewTask { get; set; }
        public bool CanImportExcel { get; set; }
        public bool CanAssignTask { get; set; }
    }
}
