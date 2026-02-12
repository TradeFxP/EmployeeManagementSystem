using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserRoles.Models
{
    public class DailyReport
    {
        public int Id { get; set; }

        public string ApplicationUserId { get; set; }

        public DateTime Date { get; set; }

        public string Task { get; set; }

        public string Note { get; set; }

        public string ReportedTo { get; set; }

        public string SubmittedByRole { get; set; }

        // ✅ MUST EXIST
        public string? ReviewerComment { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

}

