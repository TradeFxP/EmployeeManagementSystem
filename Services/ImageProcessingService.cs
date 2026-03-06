using Microsoft.Extensions.Logging;

namespace UserRoles.Services
{
    /// <summary>
    /// Handles base64 image parsing, validation, and storage-ready data preparation.
    /// Replaces duplicated image-processing blocks in TasksController.
    /// </summary>
    public interface IImageProcessingService
    {
        ImageProcessResult ProcessBase64Image(string base64Value, int fieldId);
    }

    public class ImageProcessResult
    {
        public bool Success { get; set; }
        public byte[]? ImageData { get; set; }
        public string? MimeType { get; set; }
        public string? FileName { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ImageProcessingService : IImageProcessingService
    {
        private readonly ILogger<ImageProcessingService> _logger;

        // Max image size: 2 MB
        private const int MaxImageBytes = 2 * 1024 * 1024;

        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/gif", "image/webp"
        };

        public ImageProcessingService(ILogger<ImageProcessingService> logger)
        {
            _logger = logger;
        }

        public ImageProcessResult ProcessBase64Image(string base64Value, int fieldId)
        {
            try
            {
                if (string.IsNullOrEmpty(base64Value) || !base64Value.StartsWith("data:image/"))
                    return new ImageProcessResult { Success = false, ErrorMessage = "Not a valid base64 image string." };

                var parts = base64Value.Split(',');
                if (parts.Length < 2)
                    return new ImageProcessResult { Success = false, ErrorMessage = "Malformed base64 image data." };

                var header = parts[0];   // e.g. "data:image/png;base64"
                var base64Data = parts[1];

                // Extract MIME type
                var mimeType = header.Split(':')[1].Split(';')[0];

                // Validate MIME type
                if (!AllowedMimeTypes.Contains(mimeType))
                {
                    _logger.LogWarning("Rejected image upload with MIME type {MimeType} for field {FieldId}", mimeType, fieldId);
                    return new ImageProcessResult { Success = false, ErrorMessage = $"Image type '{mimeType}' is not allowed. Use JPEG, PNG, GIF, or WebP." };
                }

                var imageData = Convert.FromBase64String(base64Data);

                // Validate file size
                if (imageData.Length > MaxImageBytes)
                {
                    _logger.LogWarning("Rejected oversized image ({Size} bytes) for field {FieldId}", imageData.Length, fieldId);
                    return new ImageProcessResult { Success = false, ErrorMessage = $"Image exceeds the maximum allowed size of {MaxImageBytes / (1024 * 1024)} MB." };
                }

                return new ImageProcessResult
                {
                    Success = true,
                    ImageData = imageData,
                    MimeType = mimeType,
                    FileName = $"upload_{fieldId}_{DateTime.UtcNow.Ticks}"
                };
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "Invalid base64 data for field {FieldId}", fieldId);
                return new ImageProcessResult { Success = false, ErrorMessage = "Invalid image data format." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image for field {FieldId}", fieldId);
                return new ImageProcessResult { Success = false, ErrorMessage = "Failed to process image." };
            }
        }
    }
}
