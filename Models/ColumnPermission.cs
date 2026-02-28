using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserRoles.Models
{
    public class ColumnPermission
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public Users? User { get; set; }

        public int ColumnId { get; set; }

        [ForeignKey("ColumnId")]
        public TeamColumn? Column { get; set; }

        // Column-specific permissions
        public bool CanRename { get; set; }
        public bool CanDelete { get; set; }
        public bool CanAddTask { get; set; }
        public bool CanClearTasks { get; set; }

        // Column-specific Task permissions
        public bool CanAssignTask { get; set; }
        public bool CanEditTask { get; set; }
        public bool CanDeleteTask { get; set; }
        public bool CanViewHistory { get; set; }
    }
}
