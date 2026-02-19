using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UserRoles.Models;
using UserRoles.Services;
using UserRoles.ViewModels;

namespace UserRoles.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<Users> signInManager;
        private readonly UserManager<Users> userManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly IEmailService emailService;
        private readonly ILogger<AccountController> logger;

        public AccountController(
         SignInManager<Users> signInManager,
         UserManager<Users> userManager,
         RoleManager<IdentityRole> roleManager,
         IEmailService emailService,
         ILogger<AccountController> logger)
        {
            this.signInManager = signInManager;
            this.userManager = userManager;
            this.roleManager = roleManager;
            this.emailService = emailService;
            this.logger = logger;
        }


        /* ===================== LOGIN ===================== */

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await userManager.FindByEmailAsync(model.Email);
            if (user == null || user.IsDeleted)
            {
                ModelState.AddModelError("", "Invalid login attempt.");
                return View(model);
            }

            var result = await signInManager.PasswordSignInAsync(
                user,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: false
            );

            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Invalid login attempt.");
                return View(model);
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            var roles = await userManager.GetRolesAsync(user);

            if (roles.Contains("Admin") || roles.Contains("Manager"))
                return RedirectToAction("OrgChart", "Users");

            return RedirectToAction("Index", "Reports");
        }


        /* ===================== PASSWORD RESET ===================== */

        [HttpGet]
        public IActionResult LoginByCode()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginByCode(string email, string code)
        {
            // 1️⃣ Find user by pending email
            var user = userManager.Users
                .FirstOrDefault(u => u.PendingEmail == email.Trim());

            if (user == null ||
                user.EmailChangeLoginCode != code ||
                user.EmailChangeCodeExpiry < DateTime.UtcNow)
            {
                ModelState.AddModelError("", "Invalid or expired login code.");
                return View();
            }

            // 2️⃣ Commit email change (NOW it becomes permanent)
            user.Email = user.PendingEmail;
            user.UserName = user.PendingEmail;

            // 3️⃣ Clear pending data
            user.PendingEmail = null;
            user.EmailChangeLoginCode = null;
            user.EmailChangeCodeExpiry = null;

            // 4️⃣ Invalidate all old sessions (SECURITY)
            user.SecurityStamp = Guid.NewGuid().ToString();

            await userManager.UpdateAsync(user);

            // 5️⃣ Sign in with NEW email
            await signInManager.SignInAsync(user, isPersistent: false);

            return RedirectToAction("OrgChart", "Users");
        }


        [HttpGet]
        public IActionResult VerifyEmail()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await userManager.FindByEmailAsync(model.Email);

            // UX: show error if email not found
            if (user == null)
            {
                ModelState.AddModelError("Email", "Enter a valid email address.");
                return View(model);
            }

            var today = DateTime.UtcNow.Date;

            if (user.PasswordResetDate == null || user.PasswordResetDate.Value.Date != today)
            {
                user.PasswordResetDate = today;
                user.PasswordResetCount = 0;
            }

            if (user.PasswordResetCount >= 3)
            {
                ModelState.AddModelError(
                    "",
                    "You have completed the maximum of 3 password reset attempts for today."
                );
                return View(model);
            }

            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = Uri.EscapeDataString(token);

            var resetLink = Url.Action(
                "ChangePassword",
                "Account",
                new { email = user.Email, token = encodedToken },
                Request.Scheme
            );

            try
            {
                var htmlBody = $@"
<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
  <h2 style='color: #2c3e50;'>Password Reset Request</h2>
  <p>Hello,</p>
  <p>We received a request to reset your password. Click the button below to set a new password:</p>
  <p style='margin: 24px 0;'>
    <a href='{resetLink}' style='display: inline-block; background: #3498db; color: #fff; padding: 12px 28px; text-decoration: none; border-radius: 4px; font-weight: bold;'>Reset Password</a>
  </p>
  <p style='color: #777;'>If you did not request this, you can safely ignore this email.</p>
  <p style='color: #888; font-size: 12px; margin-top: 24px;'>This is an automated message. Please do not reply.</p>
</div>";

                await emailService.SendEmailAsync(
                    user.Email!,
                    "Reset your password",
                    htmlBody,
                    "PasswordReset",
                    user.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email);
                throw;
            }


            user.PasswordResetCount += 1;
            await userManager.UpdateAsync(user);

            TempData["RemainingAttempts"] = 3 - user.PasswordResetCount;

            return RedirectToAction(nameof(EmailSent));
        }

        /* ===================== CHANGE PASSWORD ===================== */

        [HttpGet]
        public IActionResult ChangePassword(string email, string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
                return RedirectToAction(nameof(VerifyEmail));

            return View(new ChangePasswordViewModel
            {
                Email = email,
                Token = token
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "Invalid request.");
                return View(model);
            }

            var decodedToken = Uri.UnescapeDataString(model.Token);

            var result = await userManager.ResetPasswordAsync(
                user,
                decodedToken,
                model.NewPassword
            );

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);

                return View(model);
            }

            return RedirectToAction(nameof(Login));
        }

        /* ===================== MISC ===================== */

        [HttpGet]
        public IActionResult EmailSent() => View();

        [HttpGet]
        public IActionResult AccessDenied() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }
    }
}
