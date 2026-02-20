using System.Collections.Generic;

namespace UserRoles.Models
{
    public class TeamColumn
    {
        public int Id { get; set; }

        // Development / Testing / Sales
        public string TeamName { get; set; } = string.Empty;

        // ToDo, To Test, Leads, Follow Up, etc.
        public string ColumnName { get; set; } = string.Empty;

        // Order in board (left → right)
        public int Order { get; set; }

        // ✅ ADD THIS: Tasks that belong to this column
        // One column → many tasks
        public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    }
}
