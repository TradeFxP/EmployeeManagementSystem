using System.ComponentModel.DataAnnotations;

namespace UserRoles.ViewModels
{
    public class ProfileViewModel
    {
        [Required]
        [Display(Name = "FirstName")]
        [StringLength(20, ErrorMessage = "Name cannot exceed 20 characters.")]
        [RegularExpression(@"^[A-Za-z\s]+$", ErrorMessage = "Name must contain only letters.")]
        public string? FirstName { get; set; }

        [Required]
        [Display(Name = "LastName")]
        [StringLength(20, ErrorMessage = "Name cannot exceed 20 characters.")]
        [RegularExpression(@"^[A-Za-z\s]+$", ErrorMessage = "Name must contain only letters.")]
        public string? LastName { get; set; } = null;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Display(Name = "Mobile Number")]
        [Required]
        [RegularExpression(@"^[0-9]+$", ErrorMessage = "Only numbers allowed")]

        public string? MobileNumber { get; set; }

        [Display(Name = "Alternate Mobile Number")]
        
        [RegularExpression(@"^[0-9]+$", ErrorMessage = "Only numbers allowed")]
        public string? AlternateMobileNumber { get; set; }
        // View state

        [Display(Name = "Gender")]
        [Required]
        public string? Gender { get; set; }

        [Display(Name = "Blood Group")]
        [Required]
        public string? BloodGroup { get; set; }

        [Display(Name = "Date Of Birth")]
        [Required]
        public DateTime? DateOfBirth { get; set; } = DateTime.MinValue;

        [Display(Name = "Date Of Joining")]
        [Required]
        public DateTime? DateOfJoining { get; set; } = DateTime.MinValue;
        public bool IsEditMode { get; set; }

        // 🔑 Role-based permission
        public bool CanEditEmail { get; set; }
    }
}
