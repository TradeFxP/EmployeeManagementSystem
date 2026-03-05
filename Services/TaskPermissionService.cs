using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UserRoles.Data;
using UserRoles.Models;
using System.Security.Claims;

namespace UserRoles.Services
{
    public interface ITaskPermissionService
    {
        Task<bool> AuthorizeBoardAction(ClaimsPrincipal user, string teamName, string action);
    }

    public class TaskPermissionService : ITaskPermissionService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        public TaskPermissionService(AppDbContext context, UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<bool> AuthorizeBoardAction(ClaimsPrincipal user, string teamName, string action)
        {
            if (user.IsInRole("Admin")) return true;

            var appUser = await _userManager.GetUserAsync(user);
            if (appUser == null) return false;

            var perms = await _context.BoardPermissions
                .Where(p => p.UserId == appUser.Id && p.TeamName.ToLower().Trim() == teamName.ToLower().Trim())
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            if (perms == null) return false;

            return action switch
            {
                "AddColumn" => perms.CanAddColumn,
                "RenameColumn" => perms.CanRenameColumn,
                "ReorderColumns" => perms.CanReorderColumns,
                "DeleteColumn" => perms.CanDeleteColumn,
                "EditAllFields" => perms.CanEditAllFields,
                "DeleteTask" => perms.CanDeleteTask,
                "ReviewTask" => perms.CanReviewTask,
                "ImportExcel" => perms.CanImportExcel,
                "AssignTask" => perms.CanAssignTask,
                _ => false
            };
        }
    }
}
