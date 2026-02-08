using System;
using System.ComponentModel.DataAnnotations;

namespace UserRoles.ViewModels
{
    public class ReportViewModel
    {
        public int Id { get; set; }

        public string ApplicationUserId { get; set; } = string.Empty;

        // ✅ Display Name instead of Email
        public string DisplayName { get; set; } = string.Empty;

        public string? UserName { get; set; }

        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        public DateTime Date { get; set; }

        public string Task { get; set; } = string.Empty;

        public string Note { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select who you are reporting to.")]
        public string ReportedTo { get; set; } = string.Empty;


        // ✅ FIX: Added to match views
        public string SubmittedByRole { get; set; } = string.Empty;

        public string? ReviewerComment { get; set; }

       // public DateTime CreatedAt { get; set; }

    }
}
