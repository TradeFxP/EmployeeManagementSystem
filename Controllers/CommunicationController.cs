using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UserRoles.Controllers
{
    [Authorize]
    public class CommunicationController : Controller
    {
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
    }
}
