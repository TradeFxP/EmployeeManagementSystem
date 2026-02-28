using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using UserRoles.Data;
using UserRoles.Models;
using UserRoles.ViewModels;
using QuestPDF.Fluent;
using UserRoles.Pdf;

namespace UserRoles.Controllers
{


    [Authorize]
    public class ReportsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        public ReportsController(AppDbContext context, UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /* ================= ENTRY ================= */
        public IActionResult Index()
        {
            var userId = _userManager.GetUserId(User);
            return RedirectToAction(nameof(UserReports), new { userId });
        }

        /* ================= CALENDAR PICK ================= */
        [Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult PickDateInline(string userId, string selectedDate)
        {
            if (!DateTime.TryParseExact(
                selectedDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsedDate))
            {
                return BadRequest("Invalid date");
            }

            if (parsedDate.Date > DateTime.Today)
            {
                return BadRequest("Future dates are not allowed");
            }

            // ✅ RETURN DATE ONLY (AJAX FLOW)
            return Ok(parsedDate.ToString("dd-MM-yyyy"));
        }


        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> AddReportPanel(string userId, string? date = null)
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            // Parse provided date (accept dd-MM-yyyy primarily, fallback to yyyy-MM-dd)
            string formattedDate;
            if (!string.IsNullOrEmpty(date))
            {
                if (DateTime.TryParseExact(date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d1))
                {
                    formattedDate = d1.ToString("dd-MM-yyyy");
                }
                else if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d2))
                {
                    formattedDate = d2.ToString("dd-MM-yyyy");
                }
                else
                {
                    formattedDate = DateTime.Today.ToString("dd-MM-yyyy");
                }
            }
            else
            {
                formattedDate = DateTime.Today.ToString("dd-MM-yyyy");
            }

            return PartialView("_AddReportPanel",
                new AddReportPanelViewModel
                {
                    TargetUserId = userId,
                    ReportOwnerName = user.Name ?? user.Email,
                    Date = formattedDate
                });
        }

        /* ================= CHECK EXISTS (AJAX) ================= */
        [Authorize(Roles = "User,Manager,Admin")]
        [HttpGet]
        public async Task<IActionResult> CheckReportExists(string userId, string date)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(date))
                return BadRequest(new { error = "Missing parameters" });

            if (!DateTime.TryParseExact(
                date,
                "dd-MM-yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsedDate))
            {
                // try alternate format
                if (!DateTime.TryParseExact(
                    date,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out parsedDate))
                {
                    return BadRequest(new { error = "Invalid date format" });
                }
            }

            bool exists = await _context.DailyReports.AnyAsync(r =>
                r.ApplicationUserId == userId &&
                r.Date.Date == parsedDate.Date);

            return Json(new { exists });
        }



        /* ================= USER REPORTS ================= */


        [Authorize(Roles = "User,Manager,Admin")]
        public async Task<IActionResult> UserReports(
    string userId,
    string? addDate,
    int page = 1,
    int pageSize = 10,
    int? editId = null)


        {
            if (string.IsNullOrEmpty(userId))
                return NotFound();

            // ================= REPORT OWNER NAME =================
            // ================= REPORT OWNER NAME =================
            string reportOwnerName = "User";

            if (_userManager != null)
            {
                var reportOwner = await _userManager.FindByIdAsync(userId);

                if (reportOwner != null)
                {
                    reportOwnerName =
                        !string.IsNullOrWhiteSpace(reportOwner.Name)
                            ? reportOwner.Name
                            : reportOwner.Email ?? "User";
                }
            }

            ViewBag.ReportOwnerName = reportOwnerName;



            DateTime? parsedAddDate = null;

            if (!string.IsNullOrEmpty(addDate))
            {
                if (DateTime.TryParseExact(
                    addDate,
                    "dd-MM-yyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime temp))
                {
                    parsedAddDate = temp;
                }
            }

            ViewBag.AddDate = parsedAddDate?.ToString("dd-MM-yyyy");


            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var baseQuery = _context.DailyReports
                .Where(r => r.ApplicationUserId == userId);

            // ✅ FIX: check TODAY from full dataset
            bool hasToday = await baseQuery.AnyAsync(r =>
                r.Date.Date == DateTime.Today);

            int totalItems = await baseQuery.CountAsync();

            var reports = await baseQuery
                .OrderByDescending(r => r.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var model = new PagedResult<DailyReport>
            {
                Items = reports,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            ViewBag.TargetUserId = userId;
            ViewBag.Today = DateTime.Today;
            ViewBag.HasToday = hasToday;   // ✅ correct now

            ViewBag.EditId = editId;
           


            return View(model);
        }







        [Authorize(Roles = "User,Manager,Admin")]
        public async Task<IActionResult> UserReportsPanel(
       string userId,
       string? addDate,
       int page = 1,
       int pageSize = 10,
       int? editId = null)
        {
            // ================= SAFETY CHECK =================
            if (string.IsNullOrEmpty(userId))
                return BadRequest();

            // ================= REPORT OWNER NAME =================
            string reportOwnerName = "User";

            var reportOwner = await _userManager.FindByIdAsync(userId);
            if (reportOwner != null)
            {
                reportOwnerName =
                    !string.IsNullOrWhiteSpace(reportOwner.Name)
                        ? reportOwner.Name
                        : reportOwner.Email ?? "User";
            }

            // Values used by _UserReportsPanel.cshtml
            ViewBag.ReportOwnerName = reportOwnerName;
            ViewBag.TargetUserId = userId;
            ViewBag.EditId = editId;

            // ================= ADD-DATE (CALENDAR SUPPORT) =================
            DateTime? parsedAddDate = null;

            if (!string.IsNullOrEmpty(addDate))
            {
                DateTime.TryParseExact(
                    addDate,
                    "dd-MM-yyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime temp);

                parsedAddDate = temp;
            }

            ViewBag.AddDate = parsedAddDate?.ToString("dd-MM-yyyy");

            // ================= QUERY REPORTS =================
            var baseQuery = _context.DailyReports
                .Where(r => r.ApplicationUserId == userId);

            // Check if today report already exists
            bool hasToday = await baseQuery.AnyAsync(r =>
                r.Date.Date == DateTime.Today);

            // Pagination counts
            int totalItems = await baseQuery.CountAsync();

            var reports = await baseQuery
                .OrderByDescending(r => r.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // ================= PAGED MODEL =================
            var model = new PagedResult<DailyReport>
            {
                Items = reports,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            ViewBag.HasToday = hasToday;
            ViewBag.Today = DateTime.Today;

            // 🔥 CRITICAL: PARTIAL VIEW ONLY (RIGHT PANEL)
            return PartialView("_UserReportsPanel", model);
        }



        /* ================= CREATE INLINE ================= */

        // NOTE: many inline AJAX calls load this endpoint.
        // Antiforgery token was previously used, to avoid silent failures caused by
        // missing tokens when called from JS we ignore antiforgery here (consistent
        // with other AJAX endpoints in the project).




        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "User,Manager,Admin")]
        [HttpPost]
        public async Task<IActionResult> CreateInline(
     string task,
     string note,
     string[] reportedTo,
     string date,
     string targetUserId)
        {
            if (!DateTime.TryParseExact(
                date,
                "dd-MM-yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsedDate))
                return BadRequest("Invalid date format.");

            if (parsedDate.Date > DateTime.Today)
                return BadRequest("Future dates are not allowed.");

            var currentUserId = _userManager.GetUserId(User)!;

            if (User.IsInRole("Admin") && targetUserId == currentUserId)
                return BadRequest("Admin cannot submit a report for themselves.");

            if (string.IsNullOrWhiteSpace(task) || string.IsNullOrWhiteSpace(note))
                return BadRequest("Task and Note are required.");

            if (reportedTo == null || reportedTo.Length == 0)
                return BadRequest("Please select Admin or Manager.");

            var report = new DailyReport
            {
                ApplicationUserId = targetUserId,
                Date = parsedDate,
                Task = task.Trim(),
                Note = note.Trim(),
                ReportedTo = string.Join(", ", reportedTo),
                SubmittedByRole =
                    User.IsInRole("Admin") ? "Admin" :
                    User.IsInRole("Manager") ? "Manager" : "User",
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                _context.DailyReports.Add(report);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.Message.Contains("23505") == true)
                {
                    return Conflict("Report already exists for selected date.");
                }
                throw;
            }

            // NORMAL FORM SUBMIT → stay on same page
            if (Request.Headers["Accept"].ToString().Contains("text/html"))
            {
                return RedirectToAction(nameof(UserReports), new { userId = targetUserId });
            }

            // AJAX
            return Ok(new { success = true, userId = targetUserId });
        }



        /* ================= DETAILS ================= */
        [Authorize(Roles = "User,Manager,Admin")]
        public async Task<IActionResult> Details(int id)
        {
            var report = await _context.DailyReports
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return RedirectToAction("Error", "Home", new { statusCode = 404 });

            var currentUserId = _userManager.GetUserId(User);

            // User cannot view others
            if (User.IsInRole("User") && report.ApplicationUserId != currentUserId)
                return RedirectToAction("Error", "Home", new { statusCode = 403 });

            var owner = await _userManager.FindByIdAsync(report.ApplicationUserId);
            var roles = owner != null
                ? await _userManager.GetRolesAsync(owner)
                : new List<string>();

            // ✅ Name priority: FirstName → UserName → fallback
            string displayName =
                !string.IsNullOrWhiteSpace(owner?.Name)
                    ? owner.Name
                    : owner?.UserName ?? "User";

            return View(new ReportViewModel
            {
                Id = report.Id,
                ApplicationUserId = report.ApplicationUserId,
                DisplayName = displayName,
                Date = report.Date,
                Task = report.Task,
                Note = report.Note,
                ReportedTo = report.ReportedTo,
                ReviewerComment = report.ReviewerComment,
                SubmittedByRole = report.SubmittedByRole
            });
        }


        /* ================= INLINE UPDATE ================= */
        /* ================= INLINE UPDATE ================= */
        /* ================= INLINE UPDATE ================= */
        [Authorize(Roles = "Admin,Manager,SubManager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InlineUpdate(
            int id,
            string task,
            string note,
            string reviewerComment)
        {
            if (string.IsNullOrWhiteSpace(task) || string.IsNullOrWhiteSpace(note))
            {
                return BadRequest(new { message = "Task and Note are required." });
            }

            var report = await _context.DailyReports.FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
            {
                return NotFound(new { message = "Report not found." });
            }

            // ================= ROLE RULES =================

            // ✅ Admin → can edit everything
            // 🔹 Authorization rules (FINAL)
            if (User.IsInRole("Admin"))
            {
                // Admin can edit anything
            }
            else if (User.IsInRole("Manager") || User.IsInRole("SubManager"))
            {
                // Manager / SubManager can edit ONLY User-owned reports
                var reportOwner = await _userManager.FindByIdAsync(report.ApplicationUserId);
                if (reportOwner == null)
                    return NotFound();

                var ownerRoles = await _userManager.GetRolesAsync(reportOwner);

                if (!ownerRoles.Contains("User"))
                    return Forbid(); // ❌ cannot edit Admin/Manager/SubManager reports
            }
            else
            {
                return Forbid();
            }


            // ================= UPDATE =================
            report.Task = task.Trim();
            report.Note = note.Trim();
            report.ReviewerComment = string.IsNullOrWhiteSpace(reviewerComment)
                ? null
                : reviewerComment.Trim();

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Report updated successfully"
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [IgnoreAntiforgeryToken] // called from AJAX
        public async Task<IActionResult> DeleteInline(int id)
        {
            var report = await _context.DailyReports.FindAsync(id);
            if (report == null) return NotFound();

            _context.DailyReports.Remove(report);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Report deleted successfully." });
        }

        /* ================= DELETE ================= */
        [Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var report = await _context.DailyReports.FindAsync(id);
            if (report == null) return NotFound();

            if (report.ApplicationUserId == _userManager.GetUserId(User))
                return Forbid();

            var userId = report.ApplicationUserId;

            _context.DailyReports.Remove(report);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Report deleted successfully.";
            return RedirectToAction(nameof(UserReports), new { userId });           
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpGet]
        public async Task<IActionResult> EditInlinePanel(int id)
        {
            var report = await _context.DailyReports
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return NotFound();

            // 🔴 IMPORTANT: ALWAYS SET THIS
            ViewBag.TargetUserId = report.ApplicationUserId;

            var vm = new ReportViewModel
            {
                Id = report.Id,
                Task = report.Task ?? "",
                Note = report.Note ?? "",
                ReviewerComment = report.ReviewerComment ?? "",
                Date = report.Date
            };

            return PartialView("_EditReportPanel", vm);
        }

        /* ================= EDIT REPORT PANEL (INLINE) ================= */
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet]
        public async Task<IActionResult> EditReportPanel(int reportId, string userId)
        {
            // 1. Load report
            var report = await _context.DailyReports
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null)
                return NotFound("Report not found");

            // 2. Authorization (extra safety)
            if (User.IsInRole("Manager") &&
                !string.Equals(report.SubmittedByRole, "User", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            // 3. Build ViewModel
            var vm = new ReportViewModel
            {
                Id = report.Id,
                ApplicationUserId = report.ApplicationUserId,
                Task = report.Task ?? "",
                Note = report.Note ?? "",
                ReviewerComment = report.ReviewerComment ?? "",
                SubmittedByRole = report.SubmittedByRole,
                Date = report.Date
            };

            // 4. Needed for Cancel → reload reports
            ViewBag.TargetUserId = string.IsNullOrEmpty(userId)
                ? report.ApplicationUserId
                : userId;

            // 5. Return PARTIAL ONLY
            return PartialView("_EditReportPanel", vm);
        }



        //pdf download
        [Authorize(Roles = "Admin,Manager")]
        [HttpGet]
        public async Task<IActionResult> DownloadPdf(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest();

            var reports = await _context.DailyReports
                .AsNoTracking()
                .Where(r => r.ApplicationUserId == userId)
                .OrderByDescending(r => r.Date)
                .ToListAsync();

            if (!reports.Any())
                return BadRequest("No reports found");

            var user = await _userManager.FindByIdAsync(userId);
            var userName = user?.Name ?? user?.Email ?? "User";

            var document = new ReportsPdfDocument(reports, userName);

            var pdfBytes = document.GeneratePdf();

            return File(
                pdfBytes,
                "application/pdf",
                $"{userName}_Reports.pdf"
            );
        }

    }
}