using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using UserRoles.Data;
using UserRoles.Models;
using UserRoles.Models.Enums;
using UserRoles.Services;
using System.Diagnostics;

namespace UserRoles.Controllers
{
    [Authorize]
    public class ExcelImportController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly ITaskHistoryService _historyService;

        public ExcelImportController(
            AppDbContext context,
            UserManager<Users> userManager,
            ITaskHistoryService historyService)
        {
            _context = context;
            _userManager = userManager;
            _historyService = historyService;
        }

        // ═══════════════════════════════════════════════════════
        //  POST /ExcelImport/Preview
        //  Upload .xlsx → return headers + sample rows + count
        // ═══════════════════════════════════════════════════════
        [HttpPost]
        public async Task<IActionResult> Preview(IFormFile file, string teamName)
        {
            if (!await IsAuthorized(teamName))
                return Forbid();

            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "No file uploaded." });

            var ext = Path.GetExtension(file.FileName).ToLower();
            if (ext != ".xlsx" && ext != ".xls")
                return BadRequest(new { success = false, message = "Only .xlsx and .xls files are supported." });

            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheets.First();

                var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
                var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

                if (lastRow < 1 || lastCol < 1)
                    return BadRequest(new { success = false, message = "Excel file is empty." });

                // Extract headers (Row 1)
                var headers = new List<string>();
                for (int col = 1; col <= lastCol; col++)
                {
                    var cellValue = worksheet.Cell(1, col).GetString()?.Trim();
                    headers.Add(string.IsNullOrWhiteSpace(cellValue) ? $"Column {col}" : cellValue);
                }

                // Extract sample rows (up to 5)
                var sampleRows = new List<List<string>>();
                int dataRowCount = lastRow - 1; // exclude header
                int sampleCount = Math.Min(5, dataRowCount);

                for (int row = 2; row <= 1 + sampleCount; row++)
                {
                    var rowData = new List<string>();
                    for (int col = 1; col <= lastCol; col++)
                    {
                        rowData.Add(worksheet.Cell(row, col).GetString() ?? "");
                    }
                    sampleRows.Add(rowData);
                }

                return Ok(new
                {
                    success = true,
                    fileName = file.FileName,
                    headers,
                    sampleRows,
                    totalRows = dataRowCount,
                    totalColumns = lastCol
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Error reading file: {ex.Message}" });
            }
        }

        // ═══════════════════════════════════════════════════════
        //  POST /ExcelImport/Import
        //  Upload .xlsx + teamName + columnId → bulk create tasks
        // ═══════════════════════════════════════════════════════
        [HttpPost]
        public async Task<IActionResult> Import(
            IFormFile file,
            string teamName,
            int columnId,
            int? titleColumnIndex,
            int? descriptionColumnIndex)
        {
            if (!await IsAuthorized(teamName))
                return Forbid();

            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "No file uploaded." });

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            // Validate column exists
            var column = await _context.TeamColumns
                .FirstOrDefaultAsync(c => c.Id == columnId && c.TeamName == teamName);

            if (column == null)
                return BadRequest(new { success = false, message = "Target column not found." });

            var stopwatch = Stopwatch.StartNew();

            // Create import log
            var importLog = new ExcelImportLog
            {
                FileName = file.FileName,
                TeamName = teamName,
                ColumnId = columnId,
                ColumnName = column.ColumnName,
                ImportedByUserId = user.Id,
                ImportedAt = DateTime.UtcNow,
                Status = "Processing"
            };
            _context.ExcelImportLogs.Add(importLog);
            await _context.SaveChangesAsync();

            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheets.First();

                var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
                var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

                if (lastRow < 2)
                    return BadRequest(new { success = false, message = "No data rows found (only header)." });

                // ─── 1. Extract headers ───
                var headers = new List<string>();
                for (int col = 1; col <= lastCol; col++)
                {
                    var cellValue = worksheet.Cell(1, col).GetString()?.Trim();
                    headers.Add(string.IsNullOrWhiteSpace(cellValue) ? $"Column {col}" : cellValue);
                }

                importLog.TotalRows = lastRow - 1;

                // ─── 2. Create/Get custom fields for each column ───
                var existingFields = await _context.TaskCustomFields
                    .Where(f => f.TeamName == teamName && f.IsActive)
                    .ToListAsync();

                var maxOrder = existingFields.Any()
                    ? existingFields.Max(f => f.Order)
                    : 0;

                var fieldMap = new Dictionary<int, int>(); // Excel col index → FieldId

                for (int i = 0; i < headers.Count; i++)
                {
                    // Skip columns mapped to Title or Description
                    if (titleColumnIndex.HasValue && i == titleColumnIndex.Value) continue;
                    if (descriptionColumnIndex.HasValue && i == descriptionColumnIndex.Value) continue;

                    var headerName = headers[i];

                    // Check if field already exists (case-insensitive)
                    var existingField = existingFields
                        .FirstOrDefault(f => f.FieldName.Equals(headerName, StringComparison.OrdinalIgnoreCase));

                    if (existingField != null)
                    {
                        fieldMap[i] = existingField.Id;
                    }
                    else
                    {
                        maxOrder++;
                        var newField = new TaskCustomField
                        {
                            FieldName = headerName,
                            FieldType = "Text",
                            IsRequired = false,
                            IsActive = true,
                            TeamName = teamName,
                            Order = maxOrder,
                            CreatedByUserId = user.Id,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.TaskCustomFields.Add(newField);
                        await _context.SaveChangesAsync();
                        fieldMap[i] = newField.Id;
                        existingFields.Add(newField);
                    }
                }

                // ─── 3. Bulk create tasks ───
                int successCount = 0;
                int errorCount = 0;
                var errors = new List<string>();
                int batchSize = 50;

                for (int row = 2; row <= lastRow; row++)
                {
                    try
                    {
                        // Get title from mapped column or first column or "Row N"
                        string title;
                        if (titleColumnIndex.HasValue && titleColumnIndex.Value < headers.Count)
                        {
                            title = worksheet.Cell(row, titleColumnIndex.Value + 1).GetString()?.Trim() ?? "";
                        }
                        else
                        {
                            title = worksheet.Cell(row, 1).GetString()?.Trim() ?? "";
                        }

                        if (string.IsNullOrWhiteSpace(title))
                            title = $"Imported Row {row - 1}";

                        // Get description from mapped column
                        string description = "";
                        if (descriptionColumnIndex.HasValue && descriptionColumnIndex.Value < headers.Count)
                        {
                            description = worksheet.Cell(row, descriptionColumnIndex.Value + 1).GetString()?.Trim() ?? "";
                        }

                        var task = new TaskItem
                        {
                            Title = title,
                            Description = description,
                            ColumnId = columnId,
                            TeamName = teamName,
                            Priority = TaskPriority.Medium,
                            Status = UserRoles.Models.Enums.TaskStatus.ToDo,
                            CreatedByUserId = user.Id,
                            AssignedToUserId = user.Id,
                            AssignedByUserId = user.Id,
                            AssignedAt = DateTime.UtcNow,
                            CreatedAt = DateTime.UtcNow,
                            CurrentColumnEntryAt = DateTime.UtcNow
                        };

                        _context.TaskItems.Add(task);
                        await _context.SaveChangesAsync();

                        // Create custom field values
                        foreach (var kvp in fieldMap)
                        {
                            var cellValue = worksheet.Cell(row, kvp.Key + 1).GetString() ?? "";
                            if (!string.IsNullOrWhiteSpace(cellValue))
                            {
                                _context.TaskFieldValues.Add(new TaskFieldValue
                                {
                                    TaskId = task.Id,
                                    FieldId = kvp.Value,
                                    Value = cellValue.Trim(),
                                    CreatedAt = DateTime.UtcNow
                                });
                            }
                        }

                        // Log task creation
                        await _historyService.LogTaskCreated(task.Id, user.Id);

                        successCount++;

                        // Batch save
                        if (successCount % batchSize == 0)
                        {
                            await _context.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        if (errors.Count < 10)
                            errors.Add($"Row {row}: {ex.Message}");
                    }
                }

                // Final save
                await _context.SaveChangesAsync();

                stopwatch.Stop();

                // Update import log
                importLog.SuccessCount = successCount;
                importLog.ErrorCount = errorCount;
                importLog.Status = errorCount == 0 ? "Completed" : "Completed with errors";
                importLog.DurationMs = stopwatch.ElapsedMilliseconds;
                importLog.ErrorDetails = errors.Any() ? string.Join("\n", errors) : null;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Successfully imported {successCount} of {importLog.TotalRows} tasks.",
                    successCount,
                    errorCount,
                    totalRows = importLog.TotalRows,
                    durationMs = stopwatch.ElapsedMilliseconds,
                    importLogId = importLog.Id,
                    errors = errors.Take(5)
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                importLog.Status = "Failed";
                importLog.ErrorDetails = ex.Message;
                importLog.DurationMs = stopwatch.ElapsedMilliseconds;
                await _context.SaveChangesAsync();

                return StatusCode(500, new
                {
                    success = false,
                    message = $"Import failed: {ex.Message}",
                    importLogId = importLog.Id
                });
            }
        }

        // ═══════════════════════════════════════════════════════
        //  GET /ExcelImport/History?teamName=...
        // ═══════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> History(string teamName)
        {
            if (!await IsAuthorized(teamName))
                return Forbid();

            var logs = await _context.ExcelImportLogs
                .Where(l => l.TeamName == teamName)
                .Include(l => l.ImportedByUser)
                .OrderByDescending(l => l.ImportedAt)
                .Take(20)
                .Select(l => new
                {
                    l.Id,
                    l.FileName,
                    l.TotalRows,
                    l.SuccessCount,
                    l.ErrorCount,
                    l.Status,
                    l.DurationMs,
                    l.ColumnName,
                    importedBy = l.ImportedByUser != null ? l.ImportedByUser.UserName : "Unknown",
                    importedAt = l.ImportedAt.ToString("dd MMM yyyy, hh:mm tt")
                })
                .ToListAsync();

            return Ok(new { success = true, logs });
        }

        // ═══════════════════════════════════════════════════════
        //  Helper: Check if user is Admin/Manager/Sub-Manager
        // ═══════════════════════════════════════════════════════
        private async Task<bool> IsAuthorized(string teamName)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return false;

            if (await _userManager.IsInRoleAsync(user, "Admin")) return true;

            if (string.IsNullOrWhiteSpace(teamName))
            {
                // Fallback for role-based if teamName is missing (shouldn't happen with new JS)
                var roles = await _userManager.GetRolesAsync(user);
                return roles.Any(r => r.Equals("Manager", StringComparison.OrdinalIgnoreCase) || 
                                     r.Equals("Sub-Manager", StringComparison.OrdinalIgnoreCase));
            }

            var perms = await _context.BoardPermissions
        .Where(p => p.UserId == user.Id && p.TeamName.ToLower().Trim() == teamName.ToLower().Trim())
        .OrderByDescending(p => p.Id)
        .FirstOrDefaultAsync();

            return perms?.CanImportExcel ?? false;
        }
    }
}
