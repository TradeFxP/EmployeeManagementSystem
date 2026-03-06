using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UserRoles.Data;
using UserRoles.Models;
using UserRoles.Services;
using UserRoles.ViewModels;
using UserRoles.DTOs;

namespace UserRoles.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class OrgChartController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly AppDbContext _context;
        private readonly IUserHierarchyService _hierarchyService;

        public OrgChartController(
            UserManager<Users> userManager,
            AppDbContext context,
            IUserHierarchyService hierarchyService)
        {
            _userManager = userManager;
            _context = context;
            _hierarchyService = hierarchyService;
        }

        public async Task<IActionResult> Index()
        {
            // Current logged in user
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var currentUserId = currentUser.Id;
            var currentRoles = await _userManager.GetRolesAsync(currentUser);
            var currentRole = currentRoles.FirstOrDefault() ?? "User";

            // Load all users once (no tracking)
            var allUsers = await _userManager.Users
                .AsNoTracking()
                .ToListAsync();

            // Role lookup map (Id -> role)
            var roleMap = new Dictionary<string, string>();
            foreach (var u in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(u);
                roleMap[u.Id] = roles.FirstOrDefault() ?? "User";
            }

            // Collections used by the view
            List<Users> managers = new List<Users>();
            Dictionary<string, List<Users>> subManagers = new Dictionary<string, List<Users>>();
            Dictionary<string, List<Users>> subManagerUsers = new Dictionary<string, List<Users>>();
            List<Users> adminUsers = new List<Users>(); // users held by Admin (ParentUserId == null and role == User)

            // Root user to display at top of chart (can be Admin or Manager)
            Users rootUser = null;

            if (currentRole == "Admin")
            {
                // Admin sees the full org chart
                rootUser = currentUser;

                // Top-level managers are those with ParentUserId == null
                managers = allUsers
                    .Where(u => roleMap[u.Id] == "Manager" && string.IsNullOrEmpty(u.ParentUserId))
                    .ToList();

                // Sub-manager groups
                subManagers = allUsers
                    .Where(u => roleMap[u.Id] == "Manager" && !string.IsNullOrEmpty(u.ParentUserId))
                    .GroupBy(u => u.ParentUserId)
                    .ToDictionary(g => g.Key!, g => g.ToList());

                // Users grouped by parent manager id (only those with a parent)
                subManagerUsers = allUsers
                    .Where(u => roleMap[u.Id] == "User" && !string.IsNullOrEmpty(u.ParentUserId))
                    .GroupBy(u => u.ParentUserId)
                    .ToDictionary(g => g.Key!, g => g.ToList());

                // Users directly held by Admin (ParentUserId == null, role == User)
                adminUsers = allUsers
                    .Where(u => roleMap[u.Id] == "User" && string.IsNullOrEmpty(u.ParentUserId))
                    .ToList();
            }
            else // currentRole == "Manager"
            {
                // Manager should see only their subtree with themselves as the root
                rootUser = currentUser;

                // Visible manager ids are the manager + all descendant managers
                var visibleIds = await _hierarchyService.GetVisibleManagerIdsAsync(currentUserId);

                // The managers list for the view will contain only the current manager as root
                managers = new List<Users> { currentUser };

                // SubManagers: include only managers in the visible set that have a parent (group by parent)
                subManagers = allUsers
                    .Where(u => roleMap[u.Id] == "Manager" && !string.IsNullOrEmpty(u.ParentUserId) && visibleIds.Contains(u.Id))
                    .GroupBy(u => u.ParentUserId)
                    .ToDictionary(g => g.Key!, g => g.ToList());

                // Users under visible manager ids (include users assigned directly under any visible manager)
                subManagerUsers = allUsers
                    .Where(u => roleMap[u.Id] == "User" && !string.IsNullOrEmpty(u.ParentUserId) && visibleIds.Contains(u.ParentUserId))
                    .GroupBy(u => u.ParentUserId)
                    .ToDictionary(g => g.Key!, g => g.ToList());
            }

            // Expose current role to the view so the UI can hide admin-only controls
            ViewBag.CurrentRole = currentRole;

            ViewBag.Admin = rootUser;
            ViewBag.Managers = managers;
            ViewBag.SubManagers = subManagers;
            ViewBag.SubManagerUsers = subManagerUsers;
            ViewBag.AdminUsers = adminUsers;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> MoveNode([FromBody] MoveOrgNodeRequest model)
        {
            if (model == null) return BadRequest();

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound("User not found");

            // Previous parent for recursive check
            var oldParentId = user.ParentUserId;

            // Update parent in hierarchy
            user.ParentUserId = string.IsNullOrEmpty(model.NewParentId) ? null : model.NewParentId;
            user.ManagerId = user.ParentUserId; // Keep legacy ManagerId synced

            // If user is a sub-manager, ensure children move with them (hierarchy maintenance)
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Manager") && !string.IsNullOrEmpty(user.ParentUserId))
            {
                // Sub-manager moving to a new parent
                await _hierarchyService.CascadeMove(user.Id, user.ParentUserId);
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        public async Task<IActionResult> GetDetails(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "User";
            return PartialView("_UserDetails", (user, role));
        }
    }
}
