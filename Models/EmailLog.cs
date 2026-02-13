using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserRoles.Models
{
    public class EmailLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(256)]
        public string ToEmail { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// Sent or Failed
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Sent";

        /// <summary>
        /// Error details when Status == "Failed"
        /// </summary>
        [MaxLength(2000)]
        public string? ErrorMessage { get; set; }

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The user who triggered the email (null for system-generated)
        /// </summary>
        [MaxLength(450)]
        public string? SentByUserId { get; set; }

        [ForeignKey("SentByUserId")]
        public Users? SentByUser { get; set; }

        /// <summary>
        /// Categorizes the email: AccountCreated, PasswordReset, AdminEmailChange, Other
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string EmailType { get; set; } = "Other";
    }
}
