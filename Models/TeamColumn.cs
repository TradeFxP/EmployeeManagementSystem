using System.Collections.Generic;

namespace UserRoles.Models
{
    public class TeamColumn
    {
        public int Id { get; set; }

        // Development / Testing / Sales
        public string TeamName { get; set; }

        // ToDo, To Test, Leads, Follow Up, etc.
        public string ColumnName { get; set; }

        // Order in board (left → right)
        public int Order { get; set; }

        // ✅ ADD THIS: Tasks that belong to this column
        // One column → many tasks
        public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    }
}
