using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UserRoles.Data;
using UserRoles.Helpers;
using UserRoles.Models;
using UserRoles.Services;
using UserRoles.ViewModels;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static QuestPDF.Helpers.Colors;

namespace UserRoles.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class UsersController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailService _emailService;
        private readonly AppDbContext _context;

        public UsersController(
            UserManager<Users> userManager,
            RoleManager<IdentityRole> roleManager,
            IEmailService emailService,
            AppDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _emailService = emailService;
            _context = context;
        }

        // Updated OrgChart to support Admin / Manager scoped views:
        // - Admin: full org chart (same behavior as before)
        // - Manager: only the manager's subtree (the manager is the root shown)
        // Note: Users (role "User") are not authorized to view the org chart (enforced at controller level)
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> OrgChart()
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
                var visibleIds = await GetVisibleManagerIdsAsync(currentUserId);

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

        public async Task<IActionResult> GetDetails(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault();
            return PartialView("_UserDetails", (user, role));
        }

        // ================= ORG CHART TREE BUILDER =================
        // Builds unlimited depth hierarchy (Manager → Sub-Manager → Sub-Manager → User)
        private OrgTreeNodeViewModel BuildOrgTree(
            Users root,
            List<Users> allManagers,
            List<Users> allUsers)
        {
            var node = new OrgTreeNodeViewModel
            {
                User = root
            };

            // 1️⃣ Find child MANAGERS (Sub-Managers)
            var childManagers = allManagers
                .Where(m => m.ParentUserId == root.Id)
                .ToList();

            foreach (var manager in childManagers)
            {
                // Recursively build sub-tree
                node.Children.Add(BuildOrgTree(manager, allManagers, allUsers));
            }

            // 2️⃣ Find USERS under this manager / sub-manager
            var childUsers = allUsers
                .Where(u => u.ParentUserId == root.Id)
                .ToList();

            foreach (var user in childUsers)
            {
                node.Children.Add(new OrgTreeNodeViewModel
                {
                    User = user
                });
            }

            return node;
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignUser(string userId, string managerId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            // Admin holds user
            if (managerId == "ADMIN")
            {
                user.ParentUserId = null;
                user.ManagerId = null;
            }
            else
            {
                // Assign to selected manager
                user.ParentUserId = managerId;
                user.ManagerId = managerId;
            }

            await _userManager.UpdateAsync(user);

            TempData["Success"] = "User reassigned successfully.";
            return RedirectToAction(nameof(Index), new
            {
                search = Request.Query["search"].ToString(),
                managerId = Request.Query["managerId"].ToString()
            });
        }

        /* ================= USERS LIST ================= */

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Managers(string editId = null)
        {
            ViewBag.EditId = editId;

            var managers = await _userManager.GetUsersInRoleAsync("Manager");

            return View(managers);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateManager(string id, string name, string email)
        {
            var manager = await _userManager.FindByIdAsync(id);
            if (manager == null) return NotFound();

            manager.Name = name.Trim();
            manager.Email = email.Trim();
            manager.UserName = email.Trim();

            await _userManager.UpdateAsync(manager);

            TempData["Success"] = "Manager updated successfully.";
            return RedirectToAction(nameof(Managers));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ConfirmDeleteManager(string managerId)
        {
            var manager = await _userManager.FindByIdAsync(managerId);
            if (manager == null) return NotFound();

            var users = await _userManager.Users
                .Where(u => u.ParentUserId == managerId)
                .ToListAsync();

            ViewBag.Manager = manager;
            ViewBag.Managers = await _userManager.GetUsersInRoleAsync("Manager");

            return View(users);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteManager(
            string managerId,
            string targetManagerId,
            bool shiftAll,
            List<string>? selectedUserIds)
        {
            var manager = await _userManager.FindByIdAsync(managerId);
            if (manager == null)
                return NotFound();

            var users = await _userManager.Users
                .Where(u => u.ParentUserId == managerId)
                .ToListAsync();

            // 🚨 BLOCK DELETE IF USERS EXIST AND NO TARGET
            if (users.Any() && string.IsNullOrEmpty(targetManagerId))
            {
                TempData["Error"] = "You must reassign users before deleting the manager.";
                return RedirectToAction(nameof(ConfirmDeleteManager), new { managerId });
            }

            // 🔁 SHIFT ALL USERS
            if (shiftAll)
            {
                foreach (var user in users)
                {
                    user.ParentUserId = targetManagerId == "ADMIN" ? null : targetManagerId;
                    user.ManagerId = targetManagerId == "ADMIN" ? null : targetManagerId;
                    await _userManager.UpdateAsync(user);
                }
            }
            // 🔄 SHIFT SELECTED USERS
            else
            {
                if (selectedUserIds == null || !selectedUserIds.Any())
                {
                    TempData["Error"] = "Select users or choose shift all.";
                    return RedirectToAction(nameof(ConfirmDeleteManager), new { managerId });
                }

                foreach (var user in users.Where(u => selectedUserIds.Contains(u.Id)))
                {
                    user.ParentUserId = targetManagerId == "ADMIN" ? null : targetManagerId;
                    user.ManagerId = targetManagerId == "ADMIN" ? null : targetManagerId;
                    await _userManager.UpdateAsync(user);
                }
            }

            // 🔐 FINAL SAFETY CHECK
            bool stillAssigned = await _userManager.Users
                .AnyAsync(u => u.ParentUserId == managerId || u.ManagerId == managerId);

            if (stillAssigned)
            {
                TempData["Error"] = "Some users are still assigned to this manager.";
                return RedirectToAction(nameof(ConfirmDeleteManager), new { managerId });
            }

            // ✅ CLEANUP TASK HISTORY (Set ChangedByUser to NULL)
            var historyRecords = await _context.TaskHistories.Where(h => h.ChangedByUserId == managerId).ToListAsync();
            foreach (var record in historyRecords)
            {
                record.ChangedByUserId = null;
            }
            if (historyRecords.Any())
            {
                await _context.SaveChangesAsync();
            }

            // ✅ NOW SAFE TO HARD DELETE
            await PurgeUserReferences(manager.Id);
            var result = await _userManager.DeleteAsync(manager);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Managers));
            }

            TempData["Success"] = "Manager deactivated and users reassigned successfully.";
            return RedirectToAction(nameof(Managers));
        }

        public async Task<IActionResult> Index(
            int page = 1,
            int pageSize = 10,
            string search = "",
            string? editId = null,
            string? managerId = null)
        {
            // ---------------- SAFETY ----------------
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            ViewBag.EditId = editId;
            ViewBag.Search = search;
            ViewBag.SelectedManagerId = managerId;

            var currentUserId = _userManager.GetUserId(User);
            bool isAdmin = User.IsInRole("Admin");
            bool isManager = User.IsInRole("Manager");

            // Manager restrictions
            if (isManager && managerId == "ADMIN")
            {
                TempData["Error"] = "Managers cannot assign users to Admin.";
                return RedirectToAction(nameof(Index));
            }

            // Prevent self assignment
            if (managerId == currentUserId)
            {
                TempData["Error"] = "You cannot assign to yourself.";
                return RedirectToAction(nameof(Index));
            }

            // ---------------- MANAGER DATA ----------------
            var allManagers = await GetAllManagersAsync(); // ✅ DEFINE IT FIRST

            // ================= ASSIGNABLE MANAGERS =================
            ViewBag.AssignableManagers = new List<Users>(); // always safe

            if (isAdmin)
            {
                // Admin can assign to ALL managers
                ViewBag.AssignableManagers = allManagers;
            }
            else if (isManager)
            {
                // Manager can assign only to visible sub-managers
                var visibleIds = await GetVisibleManagerIdsAsync(currentUserId);

                ViewBag.AssignableManagers = allManagers
                    .Where(m => visibleIds.Contains(m.Id) && m.Id != currentUserId)
                    .ToList();
            }
            else
            {
                ViewBag.AssignableManagers = new List<Users>();
            }

            // ---------------- USERS QUERY ----------------
            IQueryable<Users> usersQuery = _userManager.Users
                .AsNoTracking()
                .Where(u => u.Id != currentUserId); // ✅ ONLY HERE

            // ================= MANAGER VISIBILITY =================
            if (isManager)
            {
                var visibleIds = await GetVisibleManagerIdsAsync(currentUserId);

                usersQuery = usersQuery.Where(u =>
                    visibleIds.Contains(u.ParentUserId) ||
                    visibleIds.Contains(u.Id)
                );
            }

            // Admin filter
            if (isAdmin && !string.IsNullOrEmpty(managerId))
            {
                if (managerId == "ADMIN")
                    usersQuery = usersQuery.Where(u => u.ParentUserId == null);
                else
                    usersQuery = usersQuery.Where(u => u.ParentUserId == managerId);
            }

            // Search
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                usersQuery = usersQuery.Where(u =>
                    u.Name != null && u.Name.ToLower().Contains(search));
            }

            // ================= EXECUTE QUERY =================
            var users = await usersQuery.ToListAsync();

            // ================= ATTACH ROLES =================
            var result = new List<(Users User, string Role)>();

            foreach (var user in users)
            {
                // 🚫 ABSOLUTE SAFETY: NEVER SHOW LOGGED-IN USER
                var roles = await _userManager.GetRolesAsync(user);
                string role = roles.FirstOrDefault() ?? "User";

                // Manager cannot see Admin accounts
                if (isManager && role == "Admin")
                    continue;

                result.Add((user, role));
            }

            // ================= SORT =================
            result = result
                .OrderByDescending(x => x.Role == "Manager")
                .ThenBy(x => x.User.Name)
                .ToList();

            // ================= PAGINATION (AFTER SEARCH) =================
            int totalItems = result.Count;

            var pagedItems = result
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var model = new PagedResult<(Users User, string Role)>
            {
                Items = pagedItems,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> SearchUsers(string term)
        {
            var currentUserId = _userManager.GetUserId(User);
            bool isAdmin = User.IsInRole("Admin");
            bool isManager = User.IsInRole("Manager");

            IQueryable<Users> query = _userManager.Users
                .AsNoTracking()
                .Where(u => u.Id != currentUserId); // 🚫 never show self

            // ---------------- MANAGER VISIBILITY ----------------
            if (isManager)
            {
                var visibleIds = await GetVisibleManagerIdsAsync(currentUserId);

                query = query.Where(u =>
                    visibleIds.Contains(u.ParentUserId) ||
                    visibleIds.Contains(u.Id)
                );
            }

            // ---------------- SEARCH ----------------
            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.Trim().ToLower();

                query = query.Where(u =>
                    (u.Name != null && u.Name.ToLower().Contains(term)) ||
                    (u.Email != null && u.Email.ToLower().Contains(term))
                );
            }

            var users = await query.ToListAsync();

            var result = new List<(Users User, string Role)>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                string role = roles.FirstOrDefault() ?? "User";

                if (isManager && role == "Admin")
                    continue;

                result.Add((user, role));
            }

            result = result
                .OrderByDescending(x => x.Role == "Manager")
                .ThenBy(x => x.User.Name)
                .ToList();

            return PartialView("_UserSearchResults", result);
        }

//        /* ================= CREATE USER ================= */
//        [Authorize(Roles = "Admin")]
//        public async Task<IActionResult> Create()
//        {
//            ViewBag.Managers = await _userManager.GetUsersInRoleAsync("Manager");
//            return View();
//        }

//        [Authorize(Roles = "Admin")]
//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> Create(
//            string name,
//            string email,
//            string role,
//            string managerId) // 🔹      THIS
//        {
//            /* ================= NAME VALIDATION ================= */
//            if (string.IsNullOrWhiteSpace(name))
//            {
//                ModelState.AddModelError("Name", "Name is required.");
//                return View();
//            }
                    
//            name = name.Trim();

//            if (name.Length > 20)
//            {
//                ModelState.AddModelError("Name", "Name must be maximum 20 characters.");
//                return View();      
//            }

//            if (!Regex.IsMatch(name, @"^[A-Za-z\s]+$"))
//            {
//                ModelState.AddModelError("Name", "Name must contain only letters.");
//                return View();
//            }

//            /* ================= EMAIL VALIDATION ================= */
//            if (string.IsNullOrWhiteSpace(email))
//            {
//                ModelState.AddModelError("Email", "Email is required.");
//                return View();      
//            }

//            email = email.Trim();

//            if (!Regex.IsMatch(
//                    email,
//                    @"^(?!.*\.\.)[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+\.[A-Za-z]{2,}$"))
//            {
//                ModelState.AddModelError("Email", "Enter a valid email address.");
//                return View();
//            }

//            if (await _userManager.FindByEmailAsync(email) != null)
//            {
//                ModelState.AddModelError("Email", "Email already exists.");
//                return View();
//            }

//            bool isAdmin = User.IsInRole("Admin");
//            bool isManager = User.IsInRole("Manager");

//            // Manager can create only Users under himself
//            if (isManager)
//            {
//                role = "User";
//                managerId = _userManager.GetUserId(User);
//            }

//            // Admin must select manager for User
//            if (isAdmin && role == "User")
//            {
//                if (string.IsNullOrEmpty(managerId))
//                {
//                    ModelState.AddModelError("ManagerId", "Please select Admin or a Manager.");
//                    ViewBag.Managers = await _userManager.GetUsersInRoleAsync("Manager");
//                    return View();
//                }
//            }

//            if (!isAdmin && role == "Admin")
//                return Forbid();

//            role ??= "User";

//            /* ================= PASSWORD ================= */
//            string generatedPassword = PasswordHelper.GeneratePassword();

//            /* ================= USER CREATION ================= */
//            var user = new Users
//            {
//                Name = name,
//                Email = email,
//                UserName = email,
//                EmailConfirmed = true,
//                ParentUserId =
//                    role == "Admin"
//                    ? null
//                    : managerId == "ADMIN"
//                        ? null
//                        : managerId,
//                ManagerId =
//                    role == "Admin"
//                    ? null
//                    : managerId == "ADMIN"
//                        ? null
//                        : managerId
//            };

//            var result = await _userManager.CreateAsync(user, generatedPassword);

//            if (!result.Succeeded)
//            {
//                foreach (var error in result.Errors)
//                    ModelState.AddModelError("", error.Description);

//                return View();
//            }

//            /* ================= ROLE ASSIGN ================= */
//            if (!await _roleManager.RoleExistsAsync(role))
//                await _roleManager.CreateAsync(new IdentityRole(role));

//            await _userManager.AddToRoleAsync(user, role);

//            /* ================= EMAIL SEND ================= */
//            string loginUrl = Url.Action("Login", "Account", null, Request.Scheme)!;

//            string body = $@"
//Hello {name},

//Your account has been created.

//Login Email: {email}
//Temporary Password: {generatedPassword}

//Login URL:
//{loginUrl}

//Please change your password after login.
//";

//            await _emailService.SendEmailAsync(email, "Your Account Credentials", body);

//            TempData["Success"] = "User created successfully.";
//            return RedirectToAction(nameof(Index));
//        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CreateInline(
            string name,
            string email,
            string role,
            string managerId,
            string addType,
            List<string> teams   // ✅ RECEIVES CHECKBOX VALUES
        )
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
                return BadRequest("Name and Email are required.");

            name = name.Trim();
            email = email.Trim().ToLower();

            // Normalize addType values
            addType = addType?.Trim() ?? "User";

            // ================= DUPLICATE EMAIL =================
            var existing = await _userManager.FindByEmailAsync(email);
            // Map addType to target role: "Manager" or "SubManager" -> Manager, otherwise User
            string targetRole = (addType == "Manager" || addType == "SubManager") ? "Manager" : "User";

            if (existing != null)
            {
                var existingRoles = (await _userManager.GetRolesAsync(existing)).ToList();
                var existingRole = existingRoles.FirstOrDefault() ?? "User";

                if (existingRole == "Admin")
                    return BadRequest("Cannot modify an Admin account.");

                // Reassign parent (Admin context -> parent null)
                if (!string.IsNullOrEmpty(managerId) && managerId.ToUpper() == "ADMIN")
                {
                    existing.ParentUserId = null;
                    existing.ManagerId = null;
                }
                else
                {
                    if (string.IsNullOrEmpty(managerId))
                        return BadRequest("Manager is required.");

                    existing.ParentUserId = managerId;
                    existing.ManagerId = managerId;
                }

                // Role adjustments
                if (targetRole == "Manager")
                {
                    if (!await _userManager.IsInRoleAsync(existing, "Manager"))
                    {
                        if (!await _roleManager.RoleExistsAsync("Manager"))
                            await _roleManager.CreateAsync(new IdentityRole("Manager"));

                        await _userManager.AddToRoleAsync(existing, "Manager");
                    }
                }
                else // targetRole == "User"
                {
                    if (await _userManager.IsInRoleAsync(existing, "Manager"))
                    {
                        await _userManager.RemoveFromRoleAsync(existing, "Manager");
                    }

                    if (!await _userManager.IsInRoleAsync(existing, "User"))
                    {
                        if (!await _roleManager.RoleExistsAsync("User"))
                            await _roleManager.CreateAsync(new IdentityRole("User"));

                        await _userManager.AddToRoleAsync(existing, "User");
                    }
                }

                var upd = await _userManager.UpdateAsync(existing);
                if (!upd.Succeeded)
                    return BadRequest(string.Join(", ", upd.Errors.Select(e => e.Description)));

                /* ================================
                   ✅ UPDATE USER TEAMS (EXISTING USER)
                   ================================ */
                var oldTeams = _context.UserTeams.Where(t => t.UserId == existing.Id);
                _context.UserTeams.RemoveRange(oldTeams);

                if (teams != null && teams.Any())
                {
                    foreach (var team in teams)
                    {
                        _context.UserTeams.Add(new UserTeam
                        {
                            UserId = existing.Id,
                            TeamName = team   // ✅ USE LOOP VARIABLE
                        });
                    }

                }

                await _context.SaveChangesAsync();


                TempData["Success"] = "User added/updated successfully.";
                return Ok();

            }

            var password = PasswordHelper.GeneratePassword();

            var user = new Users
            {
                Name = name,
                Email = email,
                UserName = email,
                EmailConfirmed = true
            };

            // ================= HIERARCHY =================
            if (targetRole == "Manager")
            {
                // Manager creation
                if (!string.IsNullOrEmpty(managerId) && managerId.ToUpper() != "ADMIN")
                {
                    // Sub-manager under another manager
                    user.ParentUserId = managerId;
                    user.ManagerId = managerId;
                }
                else
                {
                    // Top-level manager (Admin root)
                    user.ParentUserId = null;
                    user.ManagerId = null;
                }
            }
            else
            {
                // Regular user under Admin or a manager
                if (!string.IsNullOrEmpty(managerId) && managerId.ToUpper() == "ADMIN")
                {
                    user.ParentUserId = null;
                    user.ManagerId = null;
                }
                else
                {
                    if (string.IsNullOrEmpty(managerId))
                        return BadRequest("Manager is required.");

                    user.ParentUserId = managerId;
                    user.ManagerId = managerId;
                }
            }

            // ✅ Assign next sequential numeric ID as the primary ID (Admin=100, others start at 101+)
            var allIds = await _context.Users
                .IgnoreQueryFilters()
                .Select(u => u.Id)
                .ToListAsync();

            int maxId = 100;
            foreach (var idStr in allIds)
            {
                if (int.TryParse(idStr, out int val) && val > maxId)
                {
                    maxId = val;
                }
            }
            user.Id = (maxId + 1).ToString();

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                return BadRequest(string.Join(", ", result.Errors.Select(e => e.Description)));

            if (!await _roleManager.RoleExistsAsync(targetRole))
                await _roleManager.CreateAsync(new IdentityRole(targetRole));

            await _userManager.AddToRoleAsync(user, targetRole);


            /* ================================
   ✅ SAVE USER TEAMS (NEW USER)
   ================================ */
            if (teams != null && teams.Any())
            {
                foreach (var team in teams)
                {
                    _context.UserTeams.Add(new UserTeam
                    {
                        UserId = user.Id,
                        TeamName = team
                    });
                }

                await _context.SaveChangesAsync();
            }


            // ================= SEND WELCOME EMAIL WITH CREDENTIALS =================
            try
            {
                var loginUrl = Url.Action("Login", "Account", null, Request.Scheme)!;
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                var encodedToken = Uri.EscapeDataString(resetToken);
                var resetLink = Url.Action(
                    "ChangePassword", "Account",
                    new { email = email, token = encodedToken },
                    Request.Scheme)!;

                var currentUserId = _userManager.GetUserId(User);

                var htmlBody = $@"
<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
  <h2 style='color: #2c3e50;'>Welcome to the Team!</h2>
  <p>Hello <strong>{name}</strong>,</p>
  <p>Your account has been created successfully. Here are your login credentials:</p>
  <table style='border-collapse: collapse; margin: 16px 0;'>
    <tr><td style='padding: 8px; font-weight: bold;'>Email:</td><td style='padding: 8px;'>{email}</td></tr>
    <tr><td style='padding: 8px; font-weight: bold;'>Temporary Password:</td><td style='padding: 8px;'>{password}</td></tr>
    <tr><td style='padding: 8px; font-weight: bold;'>Role:</td><td style='padding: 8px;'>{targetRole}</td></tr>
  </table>
  <p><a href='{loginUrl}' style='display: inline-block; background: #3498db; color: #fff; padding: 10px 24px; text-decoration: none; border-radius: 4px;'>Login Now</a></p>
  <hr style='margin: 24px 0; border: none; border-top: 1px solid #e0e0e0;' />
  <p>For security, we recommend changing your password immediately:</p>
  <p><a href='{resetLink}' style='display: inline-block; background: #e67e22; color: #fff; padding: 10px 24px; text-decoration: none; border-radius: 4px;'>Set New Password</a></p>
  <p style='color: #888; font-size: 12px; margin-top: 24px;'>This is an automated message. Please do not reply.</p>
</div>";

                await _emailService.SendEmailAsync(
                    email,
                    "Your Account Has Been Created",
                    htmlBody,
                    "AccountCreated",
                    currentUserId);
            }
            catch (Exception ex)
            {
                // Log but don't fail user creation if email fails
                System.Diagnostics.Debug.WriteLine($"Email send failed: {ex.Message}");
            }

            TempData["Success"] = "User added successfully.";
            return Ok();
        }

        // ================= EDIT USER / MANAGER =================
        [Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        [IgnoreAntiforgeryToken] // ✅ REQUIRED FOR AJAX
        public async Task<IActionResult> EditInline(string id, string name, string email)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Invalid user id");

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound("User not found");

            name = name?.Trim();
            email = email?.Trim()?.ToLower();

            // Duplicate email check
            var existing = await _userManager.FindByEmailAsync(email);
            if (existing != null && existing.Id != id)
                return BadRequest("Email already exists");

            // Authorization: Managers can edit only users/sub-managers inside their own scope
            var currentUserId = _userManager.GetUserId(User);
            var isManager = User.IsInRole("Manager");
            var isAdmin = User.IsInRole("Admin");

            if (isManager && !isAdmin)
            {
                // Do not allow managers to edit Admin accounts
                var targetRoles = await _userManager.GetRolesAsync(user);
                if (targetRoles.Contains("Admin"))
                    return Forbid();

                // Allow only direct children (users or sub-managers) of the manager.
                // This keeps the check simple and safe.
                var allowed =
                    !string.IsNullOrEmpty(user.ParentUserId) && user.ParentUserId == currentUserId ||
                    !string.IsNullOrEmpty(user.ManagerId) && user.ManagerId == currentUserId;

                if (!allowed)
                    return Forbid();
            }

            user.Name = name;
            user.Email = email;
            user.UserName = email;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest("Update failed");

            return Ok();
        }

        // ================= DELETE USER / MANAGER =================
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeleteUser(string id, string reassignToId)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest("Invalid user id");

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound("User not found");

            // Find children (use both ParentUserId and ManagerId just in case)
            var children = await _context.Users
                .Where(u => u.ParentUserId == id || u.ManagerId == id)
                .ToListAsync();

            // If deleting manager, reassign children
            if (children.Any())
            {
                if (string.IsNullOrEmpty(reassignToId))
                    return BadRequest("Reassignment required");

                foreach (var child in children)
                {
                    child.ParentUserId = reassignToId == "ADMIN"
                        ? null
                        : reassignToId;
                    child.ManagerId = reassignToId == "ADMIN"
                        ? null
                        : reassignToId;
                }

                await _context.SaveChangesAsync();
            }

            await PurgeUserReferences(user.Id);
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                return BadRequest(string.Join(", ", result.Errors.Select(e => e.Description)));

            return Ok();            
        }

        // ================= CHANGE ROLE (Admin Only) =================
        /// <summary>
        /// Allows Admin to change a user's role between User, Manager, and SubManager.
        /// Updates both Identity roles and the ParentUserId/ManagerId hierarchy fields.
        /// Returns JSON { success, message } for AJAX.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ChangeRole(
            [FromBody] ChangeRoleRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.UserId))
                return BadRequest(new { success = false, message = "Invalid request." });

            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return NotFound(new { success = false, message = "User not found." });

            // Safety: never change Admin role
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Contains("Admin"))
                return BadRequest(new { success = false, message = "Cannot change the Admin role." });

            string newRole = (request.NewRole ?? "").Trim();
            if (newRole != "Manager" && newRole != "SubManager" && newRole != "User")
                return BadRequest(new { success = false, message = "Invalid role specified." });

            // ── 1. Remove all existing roles ──────────────────────────────
            if (currentRoles.Any())
                await _userManager.RemoveFromRolesAsync(user, currentRoles);

            // ── 2. Assign new Identity role ───────────────────────────────
            // SubManager uses Identity role "Manager" (same permissions level)
            string identityRole = (newRole == "SubManager") ? "Manager" : newRole;

            if (!await _roleManager.RoleExistsAsync(identityRole))
                await _roleManager.CreateAsync(new IdentityRole(identityRole));

            await _userManager.AddToRoleAsync(user, identityRole);

            // ── 3. Update hierarchy fields ────────────────────────────────
            if (newRole == "Manager")
            {
                // Promote to top-level Manager (directly under Admin)
                user.ParentUserId = null;
                user.ManagerId = null;
            }
            else if (newRole == "SubManager")
            {
                // Must have a parent manager
                string? parentId = request.ParentId;

                if (string.IsNullOrWhiteSpace(parentId))
                {
                    // Attempt to find a default manager
                    var defaultManager = (await _userManager.GetUsersInRoleAsync("Manager"))
                        .FirstOrDefault(m => m.Id != user.Id && string.IsNullOrEmpty(m.ParentUserId));

                    if (defaultManager == null)
                        return BadRequest(new { success = false, message = "No available top-level manager found to assign as parent." });

                    parentId = defaultManager.Id;
                }

                var parent = await _userManager.FindByIdAsync(parentId);
                if (parent == null)
                    return BadRequest(new { success = false, message = "Parent manager not found." });

                user.ParentUserId = parentId;
                user.ManagerId = parentId;
            }
            else // User
            {
                // Keep existing parent if present; otherwise leave under Admin (null)
                // (Admin can reassign via drag-drop or Assign To button)
            }

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                return BadRequest(new { success = false, message = errors });
            }

            // ── 4. Refresh security stamp so session tokens are invalidated ──
            await _userManager.UpdateSecurityStampAsync(user);

            string displayRole = newRole == "SubManager" ? "Sub-Manager" : newRole;
            return Ok(new { success = true, message = $"Role changed to {displayRole} successfully." });
        }

        // DTO for ChangeRole body
        public class ChangeRoleRequest
        {
            public string UserId { get; set; } = "";
            public string NewRole { get; set; } = "";
            public string? ParentId { get; set; }
        }

        public class MoveOrgNodeRequest
        {
            public string UserId { get; set; }
            public string NewParentId { get; set; }
        }

        //// ================= REASSIGN USER =================
        //[Authorize(Roles = "Admin")]
        //[HttpPost]
        //public async Task<IActionResult> Reassign(string userId, string managerId)
        //{
        //    var user = await _context.Users.FindAsync(userId);
        //    if (user == null)
        //        return BadRequest("User not found");

        //    // 🔥 ADMIN DROP (manager → admin)
        //    if (managerId == "ADMIN")
        //    {
        //        user.ParentUserId = null;
        //        user.ManagerId = null;
        //        await _context.SaveChangesAsync();
        //        return Ok(new { success = true });
        //    }

        //    // 🔽 Manager drop
        //    var manager = await _context.Users.FindAsync(managerId);
        //    if (manager == null)
        //        return BadRequest("Target manager not found");

        //    user.ParentUserId = manager.Id;
        //    user.ManagerId = manager.Id;

        //    await _context.SaveChangesAsync();
        //    return Ok(new { success = true });
        //}






        /// <summary>
        /// Moves entire subtree when a SubManager is reassigned
        /// Keeps child hierarchy intact
        /// </summary>
        private async Task CascadeMove(string subManagerId, string newParentId)
        {
            // Get direct children
            var children = await _context.Users
                .Where(u => u.ParentUserId == subManagerId || u.ManagerId == subManagerId)
                .ToListAsync();

            foreach (var child in children)
            {
                // Children remain under this sub-manager
                child.ParentUserId = subManagerId;
                child.ManagerId = subManagerId;

                // Recursive move for deeper levels
                await CascadeMove(child.Id, subManagerId);
            }
        }



        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> MoveOrgNode([FromBody] MoveOrgNodeRequest model)
        {
            if (string.IsNullOrEmpty(model.UserId))
                return BadRequest("Invalid user");

            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null)
                return NotFound("User not found");

            // ================= ADMIN DROP =================
            if (model.NewParentId == "ADMIN")
            {
                user.ParentUserId = null;   // 🔥 Admin owns the user
                user.ManagerId = null;      // optional (safe reset)

                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }

            // ================= MANAGER / SUBMANAGER DROP =================
            var parent = await _context.Users.FindAsync(model.NewParentId);
            if (parent == null)
                return BadRequest("Target parent not found");

            user.ParentUserId = parent.Id;
            user.ManagerId = parent.Id; // works for Manager + SubManager

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }










        /* ================= INLINE UPDATE (commented out) ================= */

        /* ================= DELETE USER ================= */
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id, int page, int pageSize, string search)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            // 🚨 BLOCK MANAGER DIRECT DELETE
            if (roles.Contains("Manager"))
            {
                TempData["Error"] = "Managers cannot be deleted directly. Please reassign users first.";
                return RedirectToAction(nameof(Managers));
            }

            // ✅ SAFE HARD DELETE FOR NORMAL USERS
            await PurgeUserReferences(user.Id);
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
            }
            else
            {
                TempData["Success"] = "User deleted successfully.";
            }

            return RedirectToAction(nameof(Index), new
            {
                page,
                pageSize,
                search,
                managerId = Request.Query["managerId"].ToString()
            });
        }

        // ================= PURGE USER REFERENCES (HARD DELETE PREP) =================
        private async Task PurgeUserReferences(string userId)
        {
            // 1. Delete records that are purely user-dependent
            var reports = await _context.DailyReports.Where(r => r.ApplicationUserId == userId).ToListAsync();
            _context.DailyReports.RemoveRange(reports);

            var teams = await _context.UserTeams.Where(t => t.UserId == userId).ToListAsync();
            _context.UserTeams.RemoveRange(teams);

            var perms = await _context.BoardPermissions.Where(p => p.UserId == userId).ToListAsync();
            _context.BoardPermissions.RemoveRange(perms);

            // 2. Set NULL for auditing/historical records
            var emailLogs = await _context.EmailLogs.Where(l => l.SentByUserId == userId).ToListAsync();
            foreach (var log in emailLogs) log.SentByUserId = null;

            var histories = await _context.TaskHistories.Where(h => h.ChangedByUserId == userId).ToListAsync();
            foreach (var history in histories) history.ChangedByUserId = null;

            // 3. Application Entities (Projects, Epics, etc.)
            var epics = await _context.Epics.Where(e => e.CreatedByUserId == userId).ToListAsync();
            foreach (var e in epics) e.CreatedByUserId = null;

            var features = await _context.Features.Where(f => f.CreatedByUserId == userId).ToListAsync();
            foreach (var f in features) f.CreatedByUserId = null;

            var projects = await _context.Projects.Where(p => p.CreatedByUserId == userId).ToListAsync();
            foreach (var p in projects) p.CreatedByUserId = null;

            var stories = await _context.Stories.Where(s => s.CreatedByUserId == userId).ToListAsync();
            foreach (var s in stories) s.CreatedByUserId = null;

            var customFields = await _context.TaskCustomFields.Where(cf => cf.CreatedByUserId == userId).ToListAsync();
            foreach (var cf in customFields) cf.CreatedByUserId = null;

            // 4. Task Items (Nullify or Hand over)
            var tasksAssignedBy = await _context.TaskItems.Where(t => t.AssignedByUserId == userId).ToListAsync();
            foreach (var t in tasksAssignedBy) t.AssignedByUserId = null;

            var tasksReviewedBy = await _context.TaskItems.Where(t => t.ReviewedByUserId == userId).ToListAsync();
            foreach (var t in tasksReviewedBy) t.ReviewedByUserId = null;

            var tasksCompletedBy = await _context.TaskItems.Where(t => t.CompletedByUserId == userId).ToListAsync();
            foreach (var t in tasksCompletedBy) t.CompletedByUserId = null;

            var tasksCreatedBy = await _context.TaskItems.Where(t => t.CreatedByUserId == userId).ToListAsync();
            foreach (var t in tasksCreatedBy) t.CreatedByUserId = null;

            // 5. AssignedTasks Join Table (Deleted users can't have assignments)
            var assignedTasks = await _context.AssignedTasks.Where(at => at.AssignedToId == userId || at.AssignedById == userId).ToListAsync();
            _context.AssignedTasks.RemoveRange(assignedTasks);

            await _context.SaveChangesAsync();
        }

        // ================= MANAGER VISIBILITY (Manager login) =================
        // Manager can see:
        //  - himself
        //  - his direct sub-managers
        private async Task<List<string>> GetVisibleManagerIdsAsync(string rootManagerId)
        {
            var managers = await GetAllManagersAsync();

            var result = new List<string> { rootManagerId };

            void Traverse(string parentId)
            {
                var children = managers
                    .Where(m => m.ParentUserId == parentId)
                    .Select(m => m.Id)
                    .ToList();

                foreach (var childId in children)
                {
                    if (!result.Contains(childId))
                    {
                        result.Add(childId);
                        Traverse(childId); // 🔁 recursive
                    }
                }
            }

            Traverse(rootManagerId);
            return result;
        }

        // ================= ADMIN MANAGER TREE =================
        // Returns all users who are Managers (Admin view)
        // ================= ADMIN MANAGER TREE (SAFE VERSION) =================

        // ======================= HIERARCHY HELPERS =======================

        // 1️⃣ Get ALL managers (Admin only)
        private async Task<List<Users>> GetAllManagersAsync()
        {
            var users = await _userManager.Users
                .AsNoTracking()
                .ToListAsync();

            var managers = new List<Users>();

            foreach (var u in users)
            {
                if (await _userManager.IsInRoleAsync(u, "Manager"))
                {
                    managers.Add(u);
                }
            }

            return managers;
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ListEmployeesForAdmin()
        {
            var teams = await _context.Teams.Select(t => t.Name).ToListAsync();
            var users = await _userManager.Users.AsNoTracking().ToListAsync();
            
            var userRoles = new Dictionary<string, string>();
            var userTeams = new Dictionary<string, List<string>>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userRoles[user.Id] = roles.FirstOrDefault() ?? "User";
                
                userTeams[user.Id] = await _context.UserTeams
                    .Where(ut => ut.UserId == user.Id)
                    .Select(ut => ut.TeamName)
                    .ToListAsync();
            }

            ViewBag.AllTeams = teams;
            ViewBag.UserRoles = userRoles;
            ViewBag.UserTeams = userTeams;

            return PartialView("_AdminEmployeesPanel", users);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> UpdateUserTeams(string userId, string teamName, bool isAssigned)
        {
            if (isAssigned)
            {
                // Add if not exists
                bool exists = await _context.UserTeams.AnyAsync(ut => ut.UserId == userId && ut.TeamName == teamName);
                if (!exists)
                {
                    _context.UserTeams.Add(new UserTeam { UserId = userId, TeamName = teamName });
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                // Remove if exists
                var ut = await _context.UserTeams.FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TeamName == teamName);
                if (ut != null)
                {
                    _context.UserTeams.Remove(ut);
                    await _context.SaveChangesAsync();
                }
            }
            return Ok();
        }

        private List<Users> GetDescendantManagers(
            string managerId,
            List<Users> allManagers)
        {
            var result = new List<Users>();

            var children = allManagers
                .Where(m => m.ParentUserId == managerId)
                .ToList();

            foreach (var child in children)
            {
                result.Add(child);
                result.AddRange(GetDescendantManagers(child.Id, allManagers));
            }
                    
            return result;
        }
    }
}