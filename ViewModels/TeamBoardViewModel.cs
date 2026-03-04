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

        // Custom field definitions
        public List<TaskCustomField> CustomFields { get; set; } = new();

        // ✅ User's permissions for this board
        public BoardPermission? UserPermissions { get; set; }

        public Team? TeamSettings { get; set; }

        // Map of UserId to their primary role (used for frontend filtering)
        public Dictionary<string, string> UserRolesMap { get; set; } = new();
        public Dictionary<string, string> UserTeamsMap { get; set; } = new();

        // User's column-specific permissions for this board
        public List<ColumnPermission> ColumnPermissions { get; set; } = new();

        // Grouped users for assignment dropdown
        public List<TeamGroupViewModel> TeamGroups { get; set; } = new();

        // Top-level management (Admins/Managers) - not repeated per team
        public List<UserHierarchyItem> ManagementUsers { get; set; } = new();
    }

    public class UserHierarchyItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public int Level { get; set; } // 0: Admin/Manager/Top-level SM, 1: Users under SM
    }

    public class TeamGroupViewModel
    {
        public string TeamName { get; set; } = string.Empty;
        public List<UserHierarchyItem> Users { get; set; } = new();
    }
}
