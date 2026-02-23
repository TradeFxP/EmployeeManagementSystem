using System.Collections.Generic;
using UserRoles.Models;

namespace UserRoles.ViewModels
{
    public class TeamBoardViewModel
    {
        // ✅ Teams visible to the logged-in user
        public List<string> AvailableTeams { get; set; } = new();

        // ✅ All team names for assignment dropdown
        public List<string> AllTeamNames { get; set; } = new();

        // ✅ Columns shown on the board
        public List<TeamColumn> Columns { get; set; } = new();

        // ✅ Tasks inside columns (THIS FIXES YOUR ERROR)
        public List<TaskItem> Tasks { get; set; } = new();

        // ✅ Optional (already used in some views)
        public string? TeamName { get; set; }

        // ✅ ADD THIS
        public List<Users> AssignableUsers { get; set; } = new();
        
        // Custom field definitions
        public List<TaskCustomField> CustomFields { get; set; } = new();

        // ✅ User's permissions for this board
        public BoardPermission? UserPermissions { get; set; }
        
        // Users who can appear in the "Filter By Assignor" dropdown
        public List<Users> Assignors { get; set; } = new();

        public Team? TeamSettings { get; set; }
    }
}
