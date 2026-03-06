using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserRoles.Models
{
    public class AssignedTask
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public string Priority { get; set; } = "Medium"; // Low, Medium, High

        [Required]
        public string AssignedById { get; set; } = string.Empty;

        [Required]
        public string AssignedToId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey(nameof(AssignedById))]
        public Users AssignedBy { get; set; } = null!;

        [ForeignKey(nameof(AssignedToId))]
        public Users AssignedTo { get; set; } = null!;

        public string Status { get; set; } = "New";

    }
}
