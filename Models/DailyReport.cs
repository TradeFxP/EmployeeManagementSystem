using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserRoles.Models
{
    public class DailyReport
    {
        public int Id { get; set; }

        public string ApplicationUserId { get; set; } = string.Empty;

        public DateTime Date { get; set; }

        public string Task { get; set; } = string.Empty;

        public string Note { get; set; } = string.Empty;

        public string ReportedTo { get; set; } = string.Empty;

        public string SubmittedByRole { get; set; } = string.Empty;

        // ✅ MUST EXIST
        public string? ReviewerComment { get; set; }

        public DateTime CreatedAt { get; set; }
    }

}

