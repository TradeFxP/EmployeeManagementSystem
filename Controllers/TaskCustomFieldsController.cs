using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserRoles.Data;
using UserRoles.Models;
using UserRoles.DTOs;
using UserRoles.Services;

namespace UserRoles.Controllers
{
    [Authorize]
    public class TaskCustomFieldsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        public TaskCustomFieldsController(AppDbContext context, UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> GetCustomFields(string? team)
        {
            var fields = await _context.TaskCustomFields
                .Where(f => f.IsActive && f.TeamName == team)
                .OrderBy(f => f.Order)
                .Select(f => new
                {
                    f.Id,
                    f.FieldName,
                    f.FieldType,
                    f.IsRequired,
                    f.DropdownOptions,
                    f.Order,
                    f.TeamName
                })
                .ToListAsync();

            return Ok(fields);
        }

        [HttpPost]
        public async Task<IActionResult> CreateCustomField([FromBody] CreateFieldRequest model)
        {
            if (string.IsNullOrWhiteSpace(model.FieldName))
                return BadRequest("Field name is required");

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var maxOrder = await _context.TaskCustomFields
                .MaxAsync(f => (int?)f.Order) ?? 0;

            var field = new TaskCustomField
            {
                FieldName = model.FieldName.Trim(),
                FieldType = model.FieldType,
                IsRequired = model.IsRequired,
                DropdownOptions = model.DropdownOptions,
                IsActive = true,
                Order = maxOrder + 1,
                CreatedByUserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                TeamName = model.TeamName
            };

            _context.TaskCustomFields.Add(field);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, fieldId = field.Id });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateCustomField([FromBody] UpdateFieldRequest model)
        {
            var field = await _context.TaskCustomFields.FindAsync(model.FieldId);
            if (field == null)
                return NotFound("Field not found");

            if (!string.IsNullOrWhiteSpace(model.FieldName))
                field.FieldName = model.FieldName.Trim();

            if (!string.IsNullOrWhiteSpace(model.FieldType))
                field.FieldType = model.FieldType;

            if (model.IsRequired.HasValue)
                field.IsRequired = model.IsRequired.Value;

            if (model.DropdownOptions != null)
                field.DropdownOptions = model.DropdownOptions;

            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCustomField([FromBody] int fieldId)
        {
            var field = await _context.TaskCustomFields.FindAsync(fieldId);
            if (field == null)
                return NotFound("Field not found");

            field.IsActive = false;
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ReorderCustomFields([FromBody] List<int> fieldIds)
        {
            if (fieldIds == null || !fieldIds.Any())
                return BadRequest("No field IDs provided");

            var fields = await _context.TaskCustomFields
                .Where(f => fieldIds.Contains(f.Id))
                .ToListAsync();

            for (int i = 0; i < fieldIds.Count; i++)
            {
                var field = fields.FirstOrDefault(f => f.Id == fieldIds[i]);
                if (field != null)
                {
                    field.Order = i + 1;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetTaskCustomFields(int taskId)
        {
            if (taskId <= 0) return BadRequest("Invalid task id");

            var values = await _context.TaskFieldValues
                .Where(v => v.TaskId == taskId && v.Field.IsActive)
                .Select(v => new { v.FieldId, v.Value })
                .ToListAsync();

            var dict = values.ToDictionary(v => v.FieldId, v => v.Value ?? string.Empty);

            return Ok(dict);
        }

        [HttpPost]
        public async Task<IActionResult> UploadCustomFieldImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            if (file.Length > 2 * 1024 * 1024)
                return BadRequest("File size exceeds 2MB limit");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
                return BadRequest("Invalid file type");

            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                var fileBytes = ms.ToArray();
                var base64String = Convert.ToBase64String(fileBytes);
                var contentType = file.ContentType;
                var dataUrl = $"data:{contentType};base64,{base64String}";

                return Ok(new { success = true, url = dataUrl });
            }
        }

        [HttpGet]
        [Route("Tasks/GetFieldImage/{taskId}/{fieldId}")]
        [Route("Tasks/GetFieldImageById/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFieldImage(int? taskId, int? fieldId, int? id)
        {
            TaskFieldValue fieldValue = null;

            if (id.HasValue)
            {
                fieldValue = await _context.TaskFieldValues.FindAsync(id.Value);
            }
            else if (taskId.HasValue && fieldId.HasValue)
            {
                fieldValue = await _context.TaskFieldValues
                    .OrderByDescending(v => v.Id)
                    .FirstOrDefaultAsync(v => v.TaskId == taskId.Value && v.FieldId == fieldId.Value);
            }

            if (fieldValue == null || fieldValue.ImageData == null)
                return NotFound();

            return File(fieldValue.ImageData, fieldValue.ImageMimeType ?? "image/jpeg", fieldValue.FileName ?? "image.jpg");
        }
    }
}
