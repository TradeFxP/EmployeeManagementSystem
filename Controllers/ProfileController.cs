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

                Address = user.Address,
                Email = user.Email,
                MobileNumber = user.MobileNumber,
                //AlternativeMobileNumber = user.AlternativeMobileNumber,
                ////Address = user.Address,
                //UserRole = user.UserRole,
                //BloodGroup = user.BloodGroup,
                //DateOfJoing = user.DateOfJoing,
                //DateOfBirth = user.DateOfBirth,
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
                Address = user.Address,
                MobileNumber = user.MobileNumber,
                //AlternativeMobileNumber = user.AlternativeMobileNumber,
                //Address = user.Address,
                //UserRole = user.UserRole,
                //BloodGroup = user.BloodGroup,
                //DateOfJoing = user.DateOfJoing,
                //DateOfBirth = user.DateOfBirth,

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
            user.Address = model.Address.Trim();

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
<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
  <h2 style='color: #2c3e50;'>Admin Email Updated</h2>
  <p>Your admin email has been updated successfully.</p>
  <p>For security reasons, please set a new password using the button below:</p>
  <p style='margin: 24px 0;'>
    <a href='{resetLink}' style='display: inline-block; background: #e67e22; color: #fff; padding: 12px 28px; text-decoration: none; border-radius: 4px; font-weight: bold;'>Set New Password</a>
  </p>
  <p style='color: #777;'>This will permanently disable access from your old email.</p>
  <p style='color: #888; font-size: 12px; margin-top: 24px;'>This is an automated message. Please do not reply.</p>
</div>",
                    "AdminEmailChange",
                    user.Id);

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

