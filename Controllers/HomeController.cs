using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UserRoles.Models;

namespace UserRoles.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly UserManager<Users> _userManager;

        public HomeController(
            ILogger<HomeController> logger,
            UserManager<Users> userManager)
        {
            _logger = logger;
            _userManager = userManager;
        }

        // ❌ Home is not used for navigation
        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        public IActionResult Privacy()
        {
            return View();
        }

        // ✅ RENAMED (was Admin)
        [Authorize(Roles = "Admin")]
        public IActionResult AdminPage()
        {
            return View("Admin");
        }

        // ✅ RENAMED (was User) ❗ FIX
        [Authorize(Roles = "User")]
        public IActionResult UserPage()
        {
            return View("User");
        }

        // ✅ RENAMED (was Manager)
        [Authorize(Roles = "Manager")]
        public IActionResult ManagerPage()
        {
            return View("Manager");
        }

        // ✅ ROLE-BASED REDIRECT (CORE FIX)
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> RedirectByRole()
        {
            if (!base.User.Identity!.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            var user = await _userManager.GetUserAsync(base.User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var roles = await _userManager.GetRolesAsync(user);

            // Admin -> OrgChart
            if (roles.Contains("Admin"))
                return RedirectToAction("Index", "OrgChart");

            // Manager -> OrgChart (scoped)
            if (roles.Contains("Manager"))
                return RedirectToAction("Index", "OrgChart");

            // Regular User -> Reports
            if (roles.Contains("User"))
                return RedirectToAction("Index", "Reports");

            return RedirectToAction("Login", "Account");
        }

        // ✅ ERROR / 404 / 403 / 401
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [AllowAnonymous]
        [Route("Home/Error/{statusCode?}")]
        public IActionResult Error(int? statusCode)
        {
            ViewBag.StatusCode = statusCode;

            ViewBag.Message = statusCode switch
            {
                404 => "Page not found. The URL you entered does not exist.",
                403 => "You are not authorized to access this page.",
                401 => "Please login to continue.",
                _ => "Something went wrong. Please try again."
            };

            return View();
        }
    }
}
