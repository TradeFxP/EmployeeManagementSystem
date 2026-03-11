using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using UserRoles.Data;
using System.Linq;
using System;
using System.Net;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using UserRoles.Services;
using UserRoles.Models;
using Microsoft.Extensions.Logging;


namespace UserRoles.Controllers
{
    [Authorize]
    public class CommunicationController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IEmailService _emailService;
        private readonly EmailSettings _settings;
        private readonly Microsoft.AspNetCore.Identity.UserManager<Users> _userManager;
        private readonly ILogger<CommunicationController> _logger;


        public CommunicationController(
            AppDbContext context, 
            IWebHostEnvironment env, 
            IEmailService emailService, 
            IOptions<EmailSettings> settings,
            Microsoft.AspNetCore.Identity.UserManager<Users> userManager,
            ILogger<CommunicationController> logger)
        {
            _context = context;
            _env = env;
            _emailService = emailService;
            _settings = settings.Value;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult GetWhatsAppButton()
        {
            return PartialView("_WhatsAppButton");
        }

        [HttpGet]
        public IActionResult GetEmailButton()
        {
            return PartialView("_EmailButton");
        }

        [HttpGet]
        public async Task<IActionResult> WhatsApp(int id)
        {
            var value = await _context.TaskFieldValues
                .Include(v => v.Field)
                .Where(v => v.TaskId == id)
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefaultAsync(v => v.Field.FieldName.ToLower().Contains("phone") || v.Field.FieldName.ToLower().Contains("mobile"));

            if (value == null || string.IsNullOrEmpty(value.Value))
            {
                return Content("<script>alert('Phone number not found in custom fields.'); window.close();</script>", "text/html");
            }

            var cleanPhone = new string(value.Value.Where(char.IsDigit).ToArray());
            return Redirect($"https://web.whatsapp.com/send?phone={cleanPhone}");
        }

        [HttpGet]
        public async Task<IActionResult> Email(int id, string type)
        {
            var value = await _context.TaskFieldValues
                .Include(v => v.Field)
                .Where(v => v.TaskId == id)
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefaultAsync(v => v.Field.FieldName.ToLower().Contains("email"));

            if (value == null || string.IsNullOrEmpty(value.Value))
            {
                return Content("<script>alert('Email address not found in custom fields.'); window.close();</script>", "text/html");
            }

            string targetUrl = type?.ToLower() switch
            {
                "gmail" => $"https://mail.google.com/mail/?view=cm&fs=1&to={WebUtility.UrlEncode(value.Value)}",
                "outlook" => $"https://outlook.office.com/mail/deeplink/compose?to={WebUtility.UrlEncode(value.Value)}",
                _ => $"mailto:{value.Value}"
            };

            return Redirect(targetUrl);
        }

        [HttpGet]
        public IActionResult GetEmailTemplates()
        {
            var templatePath = Path.Combine(_env.ContentRootPath, "EmailTemplates");
            if (!Directory.Exists(templatePath)) return Json(new List<string>());

            var templates = Directory.GetFiles(templatePath, "*.html")
                                    .Select(f => Path.GetFileNameWithoutExtension(f))
                                    .ToList();

            return Json(templates);
        }

        [HttpGet]
        public async Task<IActionResult> GetTemplatePreview(int taskId, string templateName)
        {
            try 
            {
                var html = await ProcessTemplate(taskId, templateName);
                
                var emailValue = await _context.TaskFieldValues
                    .Include(v => v.Field)
                    .Where(v => v.TaskId == taskId)
                    .FirstOrDefaultAsync(v => v.Field.FieldName.ToLower().Contains("email"));

                var toEmail = emailValue?.Value ?? string.Empty;
                
                // Fetch logged in user's email for "From" preview
                var user = await _userManager.GetUserAsync(User);
                var fromEmail = user?.Email ?? _settings.FromAddress;

                return Json(new { success = true, html = html, toEmail = toEmail, fromEmail = fromEmail });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendZeptoMail(int taskId, string templateName)
        {
            try
            {
                var task = await _context.TaskItems.FindAsync(taskId);
                if (task == null) return NotFound("Task not found");

                var emailValue = await _context.TaskFieldValues
                    .Include(v => v.Field)
                    .Where(v => v.TaskId == taskId)
                    .FirstOrDefaultAsync(v => v.Field.FieldName.ToLower().Contains("email"));

                if (emailValue == null || string.IsNullOrEmpty(emailValue.Value))
                    return BadRequest("Recipient email not found");

                var htmlBody = await ProcessTemplate(taskId, templateName);
                
                // Use a subject line based on the template or a default
                var subject = templateName.Replace("_", " ") + " - JetFyX";

                var user = await _userManager.GetUserAsync(User);

                // Use the standard SendEmailAsync (ZeptoMail) and pass the taskId
                await _emailService.SendEmailAsync(
                    emailValue.Value, 
                    subject, 
                    htmlBody, 
                    "TemplateEmail", 
                    user?.Id,
                    taskId);

                return Ok(new { success = true, message = "Email sent successfully via Zoho." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendOutlookEmail([FromQuery] int taskId, [FromQuery] string templateName)
        {
            try
            {
                var task = await _context.TaskItems.FindAsync(taskId);
                if (task == null) return NotFound("Task not found");

                var emailValue = await _context.TaskFieldValues
                    .Include(v => v.Field)
                    .Where(v => v.TaskId == taskId)
                    .FirstOrDefaultAsync(v => v.Field.FieldName.ToLower().Contains("email"));

                if (emailValue == null || string.IsNullOrEmpty(emailValue.Value))
                    return BadRequest(new { success = false, message = "Recipient email not found" });

                var htmlBody = await ProcessTemplate(taskId, templateName);
                var subject = templateName.Replace("_", " ") + " - JetFyX";

                var user = await _userManager.GetUserAsync(User);
                var fromEmail = user?.Email ?? _settings.FromAddress;
                var fromName = user?.Name ?? user?.UserName ?? _settings.FromName;

                await _emailService.SendEmailSmtpAsync(
                    emailValue.Value,
                    subject,
                    htmlBody,
                    "OutlookEmail",
                    user?.Id,
                    fromEmail,
                    fromName,
                    taskId);

                return Ok(new { success = true, message = "Email sent successfully via Outlook." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send Outlook email for Task {TaskId}", taskId);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> LogWhatsApp(int taskId)
        {
            try
            {
                var task = await _context.TaskItems.FindAsync(taskId);
                if (task == null) return NotFound("Task not found");

                var user = await _userManager.GetUserAsync(User);

                var log = new EmailLog
                {
                    ToEmail = "WhatsApp",
                    Subject = "WhatsApp Communication Initiated",
                    EmailType = "WhatsApp",
                    SentByUserId = user?.Id,
                    TaskId = taskId,
                    Status = "Initiated",
                    SentAt = DateTime.UtcNow
                };

                _context.EmailLogs.Add(log);
                await _context.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log WhatsApp interaction for Task {TaskId}", taskId);
                return BadRequest(new { success = false, message = "Failed to log interaction" });
            }
        }

        private async Task<string> ProcessTemplate(int taskId, string templateName)
        {
            var filePath = Path.Combine(_env.ContentRootPath, "EmailTemplates", $"{templateName}.html");
            if (!System.IO.File.Exists(filePath)) throw new Exception("Template not found");
 
            var html = await System.IO.File.ReadAllTextAsync(filePath);
 
            // Fetch lead data for placeholders
            var task = await _context.TaskItems.FindAsync(taskId);
            var fieldValues = await _context.TaskFieldValues
                .Include(v => v.Field)
                .Where(v => v.TaskId == taskId)
                .ToListAsync();
 
            var firstName = fieldValues.FirstOrDefault(v => v.Field.FieldName.ToLower().Contains("full["))?.Value?.Split(' ')[0] 
                            ?? fieldValues.FirstOrDefault(v => v.Field.FieldName.ToLower().Contains("name"))?.Value?.Split(' ')[0]
                            ?? "Valued Client";
 
            // Get sender info (logged in user)
            var user = await _userManager.GetUserAsync(User);
            var senderName = user?.Name ?? user?.UserName ?? "JetFyX Team";
            
            // Replace placeholders
            html = html.Replace("{{FirstName}}", firstName);
            html = html.Replace("{{SenderName}}", senderName);
            html = html.Replace("{{SenderInfoLine}}", "JetFyX, EO Physical Return Address, 86-90 Paul Street, London, EC2A 4NE, United Kingdom");
            html = html.Replace("{{UnsubscribeURL}}", "#");
            html = html.Replace("{{RewardsURL}}", "#");
 
            return html;
        }

        [HttpGet]
        public async Task<IActionResult> GetCommunicationLogs(string teamName)
        {
            try
            {
                var logs = await _context.EmailLogs
                    .Include(l => l.SentByUser)
                    .Include(l => l.Task)
                    .Where(l => l.Task != null && l.Task.TeamName == teamName && !l.Task.IsArchived)
                    .OrderByDescending(l => l.SentAt)
                    .Select(l => new 
                    {
                        Id = l.Id,
                        SentAt = l.SentAt,
                        Action = l.EmailType == "WhatsApp" ? "WhatsApp Click" : "Email Sent",
                        Subject = l.Subject,
                        LeadName = l.Task!.Title,
                        ToInfo = l.ToEmail,
                        FromInfo = l.FromEmail ?? "",
                        User = l.SentByUser == null ? "System" : l.SentByUser.Name ?? l.SentByUser.UserName,
                        Status = l.Status,
                        TaskId = l.TaskId
                    })
                    .ToListAsync();

                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching communication logs for {TeamName}", teamName);
                return BadRequest(new { success = false, message = "Failed to load logs" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTaskCommunicationLogs([FromQuery] int taskId)
        {
            try
            {
                var logs = await _context.EmailLogs
                    .Include(l => l.SentByUser)
                    .Where(l => l.TaskId == taskId)
                    .OrderByDescending(l => l.SentAt)
                    .Select(l => new 
                    {
                        Id = l.Id,
                        SentAt = l.SentAt,
                        Action = l.EmailType == "WhatsApp" ? "WhatsApp Click" : "Email Sent",
                        Subject = l.Subject,
                        ToInfo = l.ToEmail,
                        FromInfo = l.FromEmail ?? "",
                        User = l.SentByUser == null ? "System" : l.SentByUser.Name ?? l.SentByUser.UserName,
                        Status = l.Status
                    })
                    .ToListAsync();

                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching communication logs for Task {TaskId}", taskId);
                return BadRequest(new { success = false, message = "Failed to load logs" });
            }
        }
    }
}
