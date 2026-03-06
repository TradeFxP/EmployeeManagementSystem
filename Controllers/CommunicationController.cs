using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using UserRoles.Data;
using System.Linq;
using System;
using System.Net;

namespace UserRoles.Controllers
{
    [Authorize]
    public class CommunicationController : Controller
    {
        private readonly AppDbContext _context;

        public CommunicationController(AppDbContext context)
        {
            _context = context;
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
    }
}
