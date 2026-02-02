using System.Collections.Generic;
using UserRoles.Models;

namespace UserRoles.ViewModels
{
    public class TeamBoardViewModel
    {
        public string TeamName { get; set; }

        public List<TeamColumn> Columns { get; set; }

        // All tasks belonging to the team
        public List<TaskItem> Tasks { get; set; }
    }
}
