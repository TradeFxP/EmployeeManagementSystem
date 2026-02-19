using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserRoles.Models
{
    public class Users : IdentityUser
    {
        public string? Name { get; set; }

        // ================= HIERARCHY =================
        public string? ParentUserId { get; set; }

        public string? ManagerId { get; set; }

        [ForeignKey("ManagerId")]
        public Users? Manager { get; set; }

        public ICollection<Users> TeamMembers { get; set; }

        // ================= PASSWORD RESET =================
        public int PasswordResetCount { get; set; } = 0;
        public DateTime? PasswordResetDate { get; set; }

        // ================= CONTACT =================
        [Phone]
        public string? MobileNumber { get; set; }


        public string? Address { get; set; }

        // ================= ADMIN EMAIL CHANGE (NEW) =================

        /// <summary>
        /// New email entered by Admin, waiting for confirmation.
        /// Email/UserName are NOT updated until code login succeeds.
        /// </summary>
        public string? PendingEmail { get; set; }

        /// <summary>
        /// One-time login code sent to PendingEmail.
        /// </summary>
        public string? EmailChangeLoginCode { get; set; }

        /// <summary>
        /// Expiry time for EmailChangeLoginCode (eg: now + 10 minutes).
        /// </summary>
        public DateTime? EmailChangeCodeExpiry { get; set; }

    }
}
