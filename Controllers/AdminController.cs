using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
using UserRoles.Models;
//using UserRoles.Data;

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
    //public IActionResult Index()
    //{
        
    //    var topRoles = _userManager.Users
    //        .Where(x => x.ParentUserId == null)
    //        .ToList();

    //    return View(topRoles);
    //}
    //public IActionResult GetChildren(int parentId)
    //{
    //    var parentIds = parentId.ToString();
    //    var children = _userManager.Users
    //        .Where(x => x.ParentUserId == parentIds)
    //        .ToList();

    //    return Json(children);
    //}
}
