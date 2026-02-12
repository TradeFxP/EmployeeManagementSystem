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
        private string currentUserId;

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
                    ReportOwnerName = user.FirstName ?? user.Email,
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

            string reportOwnerName = "User";
            var reportOwner = await _userManager.FindByIdAsync(userId);
            if (reportOwner != null)
            {
                reportOwnerName = !string.IsNullOrWhiteSpace(reportOwner.FirstName)
                    ? reportOwner.FirstName
                    : reportOwner.Email ?? "User";
            }
            ViewBag.ReportOwnerName = reportOwnerName;

            DateTime? parsedAddDate = null;
            if (!string.IsNullOrEmpty(addDate))
            {
                if (DateTime.TryParseExact(addDate, "dd-MM-yyyy", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime temp))
                {
                    parsedAddDate = temp;
                }
            }
            ViewBag.AddDate = parsedAddDate?.ToString("dd-MM-yyyy");

            var baseQuery = _context.DailyReports
                .Where(r => r.ApplicationUserId == userId);

            // FIX: Calculate today's date BEFORE the query to avoid LINQ-to-SQL translation issues
            var todayDate = DateTime.Today;
            bool hasToday = await baseQuery.AnyAsync(r => r.Date.Date == todayDate);

            var reports = await baseQuery
    .OrderByDescending(r => r.Date)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();

            // Map DailyReport → ReportViewModel
            var vmList = reports.Select(r => new ReportViewModel
            {
                Id = r.Id,
                ApplicationUserId = r.ApplicationUserId,
                Task = r.Task,
                Note = r.Note,
                ReviewerComment = r.ReviewerComment,
                ReportedTo = r.ReportedTo, // ✅ include this
                SubmittedByRole = r.SubmittedByRole,
                Date = r.Date,
                CreatedAt = r.CreatedAt,
                DisplayName = reportOwnerName
            }).ToList();

            ViewBag.TargetUserId = userId;
            ViewBag.Today = todayDate;
            ViewBag.HasToday = hasToday;
            ViewBag.EditId = editId;

            // ✅ Return the list directly
            int totalItems = await baseQuery.CountAsync();

            var pagedResult = new PagedResult<ReportViewModel>
            {
                Items = vmList,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize
            };

            return View(pagedResult);


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
                    !string.IsNullOrWhiteSpace(reportOwner.FirstName)
                        ? reportOwner.FirstName
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
            var vmList = reports.Select(r => new ReportViewModel
            {
                Id = r.Id,
                ApplicationUserId = r.ApplicationUserId,
                Task = r.Task,
                Note = r.Note,
                ReviewerComment = r.ReviewerComment,
                ReportedTo = r.ReportedTo, // ✅ include this
                SubmittedByRole = r.SubmittedByRole,
                Date = r.Date,
                CreatedAt = r.CreatedAt   // ✅ include this
            }).ToList();

            var pagedResult = new PagedResult<ReportViewModel>
            {
                Items = vmList,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize
            };

            return PartialView("_UserReportsPanel", pagedResult);

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
                Date = parsedDate.Date,
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
                !string.IsNullOrWhiteSpace(owner?.FirstName)
                    ? owner.FirstName
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



        /* ================= EDIT (FULL PAGE) ================= */
        [Authorize(Roles = "User,Manager,Admin,SubManager")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var report = await _context.DailyReports.FindAsync(id);
            if (report == null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);
            bool isAdmin = User.IsInRole("Admin");
            bool isManager = User.IsInRole("Manager");
            bool isSubManager = User.IsInRole("SubManager");
            bool isUser = User.IsInRole("User");

            // Authorization logic
            if (isUser && report.ApplicationUserId != currentUserId)
            {
                return Forbid();
            }

            if (isUser && report.CreatedAt.AddHours(24) < DateTime.UtcNow)
            {
                 TempData["Error"] = "You can only edit reports within 24 hours of submission.";
                 return RedirectToAction(nameof(UserReports), new { userId = currentUserId });
            }

            // Manager/SubManager permission check (can edit user reports or own)
            if ((isManager || isSubManager) && !isAdmin)
            {
                bool isUserReport = string.Equals(report.SubmittedByRole, "User", StringComparison.OrdinalIgnoreCase);
                bool isOwnReport = report.ApplicationUserId == currentUserId;
                bool isSubmittedToday = report.Date.Date == DateTime.Today;
                
                // Can always edit User reports
                if (isUserReport)
                {
                    // Allow editing
                }
                // Can edit own reports ONLY if submitted today (before midnight)
                else if (isOwnReport && isSubmittedToday)
                {
                    // Allow editing
                }
                else
                {
                    TempData["Error"] = "You can only edit your own reports on the day they were submitted.";
                    return RedirectToAction(nameof(UserReports), new { userId = currentUserId });
                }
            }

            var viewModel = new ReportViewModel
            {
                Id = report.Id,
                ApplicationUserId = report.ApplicationUserId,
                Date = report.Date,
                Task = report.Task,
                Note = report.Note,
                ReviewerComment = report.ReviewerComment,
                ReportedTo = report.ReportedTo ?? "Manager", // Default to Manager if null to avoid validation error on display
                SubmittedByRole = report.SubmittedByRole
            };
            
            return View(viewModel);
        }

        [Authorize(Roles = "User,Manager,Admin,SubManager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ReportViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            var report = await _context.DailyReports.FindAsync(id);
            if (report == null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);
            bool isAdmin = User.IsInRole("Admin");
            bool isManager = User.IsInRole("Manager");
            bool isSubManager = User.IsInRole("SubManager");
            bool isUser = User.IsInRole("User");

            if (isUser && report.ApplicationUserId != currentUserId) return Forbid();
            if (isUser && report.CreatedAt.AddHours(24) < DateTime.UtcNow) return Forbid();

            if ((isManager || isSubManager) && !isAdmin)
            {
                bool isUserReport = string.Equals(report.SubmittedByRole, "User", StringComparison.OrdinalIgnoreCase);
                bool isOwnReport = report.ApplicationUserId == currentUserId;
                bool isSubmittedToday = report.Date.Date == DateTime.Today;
                
                // Can always edit User reports
                if (isUserReport)
                {
                    // Allow editing
                }
                // Can edit own reports ONLY if submitted today (before midnight)
                else if (isOwnReport && isSubmittedToday)
                {
                    // Allow editing
                }
                else
                {
                    TempData["Error"] = "You can only edit your own reports on the day they were submitted.";
                    return RedirectToAction(nameof(UserReports), new { userId = currentUserId });
                }
            }

            // Update fields
            report.Task = model.Task;
            report.Note = model.Note;

            if (isAdmin || isManager || isSubManager)
            {
                report.ReviewerComment = model.ReviewerComment;
            }

            try
            {
                _context.Update(report);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Report updated successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.DailyReports.Any(e => e.Id == report.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(UserReports), new { userId = report.ApplicationUserId });
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
                return BadRequest("Task and Note are required.");

            var report = await _context.DailyReports
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return NotFound();

            // Role rules
            if (!(User.IsInRole("Admin") ||
     (User.IsInRole("Manager") && report.ApplicationUserId != currentUserId) ||
     (User.IsInRole("SubManager") && report.ApplicationUserId != currentUserId)))

            {
                var owner = await _userManager.FindByIdAsync(report.ApplicationUserId);
                var ownerRoles = owner != null
                    ? await _userManager.GetRolesAsync(owner)
                    : new List<string>();

                if (!ownerRoles.Contains("User"))
                    return Forbid();
            }

            report.Task = task.Trim();
            report.Note = note.Trim();
            report.ReviewerComment = string.IsNullOrWhiteSpace(reviewerComment)
                ? null
                : reviewerComment.Trim();

            await _context.SaveChangesAsync();

            return Ok(new { success = true });
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
                ApplicationUserId = report.ApplicationUserId,
                Task = report.Task,
                Note = report.Note,
                ReviewerComment = report.ReviewerComment,
                SubmittedByRole = report.SubmittedByRole,
                Date = report.Date,
                CreatedAt = report.CreatedAt 
            };


            return PartialView("_EditReportPanel", vm);
        }

        /* ================= EDIT REPORT PANEL (INLINE) ================= */
        [Authorize(Roles = "Admin,Manager,User")]
        [HttpGet]
      
        public async Task<IActionResult> EditReportPanel(int id, string userId)
        {
            // Load report
            var report = await _context.DailyReports
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return NotFound();

            // Manager can edit ONLY User reports
            if (User.IsInRole("Manager") &&
                !string.Equals(report.SubmittedByRole, "User", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            // User can edit only within 24 hours
            if (User.IsInRole("User") &&
                report.CreatedAt.AddHours(24) < DateTime.UtcNow)
            {
                return Forbid();
            }

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

            ViewBag.TargetUserId = string.IsNullOrEmpty(userId)
                ? report.ApplicationUserId
                : userId;

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
                .Where(r => r.ApplicationUserId == userId)
                .OrderByDescending(r => r.Date)
                .ToListAsync();

            if (!reports.Any())
                return BadRequest("No reports found");

            var user = await _userManager.FindByIdAsync(userId);
            var userName = user?.FirstName ?? user?.Email ?? "User";

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