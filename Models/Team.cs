using System;
using System.ComponentModel.DataAnnotations;

namespace UserRoles.Models
{
    public class Team
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
