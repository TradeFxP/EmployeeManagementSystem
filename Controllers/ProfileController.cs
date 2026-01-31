using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using UserRoles.Models;
using UserRoles.Services;
using UserRoles.ViewModels;


namespace UserRoles.Controllers
{
    [Authorize(Roles = "User,Manager,Admin")]
    public class ProfileController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly IEmailService _emailService;

        public ProfileController(
            UserManager<Users> userManager,
            IEmailService emailService)
        {
            _userManager = userManager;
            _emailService = emailService;
        }

        // ================= VIEW PROFILE =================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login", "Account");

            var model = new ProfileViewModel
            {
                FirstName = user.Name,
                Email = user.Email,
                MobileNumber = user.MobileNumber,
                IsEditMode = false,
                CanEditEmail = User.IsInRole("Admin")
            };

            return View(model);
        }

        // ================= EDIT PROFILE =================
        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login", "Account");

            var model = new ProfileViewModel
            {
                FirstName = user.Name,
                Email = user.Email,
                MobileNumber = user.MobileNumber,
                IsEditMode = true,               // 🔑 THIS ENABLES EDIT
                CanEditEmail = User.IsInRole("Admin")
            };

            // 🔁 IMPORTANT: reuse Index view
            return View("Index", model);
        }

        // ================= SAVE PROFILE =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.IsEditMode = true;
                model.CanEditEmail = User.IsInRole("Admin");
                return View("Index", model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login", "Account");

            // ✅ Update common fields
            user.Name = model.FirstName.Trim();
            user.MobileNumber = model.MobileNumber.Trim();

            // ================= ADMIN EMAIL CHANGE =================
            if (User.IsInRole("Admin") && !string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase))
            {
                var newEmail = model.Email.Trim();

                // 1️⃣ Update email immediately
                user.Email = newEmail;
                user.UserName = newEmail;

                // 2️⃣ Kill all old logins (VERY IMPORTANT)
                await _userManager.UpdateSecurityStampAsync(user);
                await _userManager.UpdateAsync(user);

                // 3️⃣ Generate password reset token
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var encodedToken = Uri.EscapeDataString(token);

                var resetLink = Url.Action(
                    "ChangePassword",
                    "Account",
                    new { email = newEmail, token = encodedToken },
                    Request.Scheme
                );

                // 4️⃣ Send reset link to NEW email only
                await _emailService.SendEmailAsync(
                    newEmail,
                    "Set your new admin password",
                    $@"
<p>Your admin email has been updated successfully.</p>
<p>For security reasons, please set a new password using the link below:</p>
<p><a href='{resetLink}'>Set New Password</a></p>
<p>This will permanently disable access from your old email.</p>
"
                );

                // 5️⃣ Force logout immediately
                await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

                TempData["Success"] =
                    "Email updated successfully. Please set a new password from the email sent to you.";

                return RedirectToAction("Login", "Account");
            }

            // ================= NORMAL PROFILE UPDATE =================
            await _userManager.UpdateAsync(user);

            TempData["Success"] = "Profile updated successfully.";
            return RedirectToAction(nameof(Index));
        }



    }
}

