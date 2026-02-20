using System;
using System.ComponentModel.DataAnnotations;

namespace UserRoles.Models
{
    public class Team
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsPriorityVisible { get; set; } = true;
        public bool IsDueDateVisible { get; set; } = true;
        public bool IsTitleVisible { get; set; } = true;
        public bool IsDescriptionVisible { get; set; } = true;
    }
}
