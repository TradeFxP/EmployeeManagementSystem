using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UserRoles.Models;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserManager<Users> _userManager;

    public AdminController(UserManager<Users> userManager)
    {
        _userManager = userManager;
    }

    // 🔧 TEMP FIX METHOD
    public async Task<IActionResult> FixUserRoles()
    {
        var users = _userManager.Users.ToList();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);

            // If user has no role → assign User
            if (!roles.Any())
            {
                await _userManager.AddToRoleAsync(user, "User");
            }
        }

        return Content("User roles fixed. Logout and login again.");
    }
}
