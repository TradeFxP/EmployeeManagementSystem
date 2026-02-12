using System.ComponentModel.DataAnnotations;

namespace UserRoles.ViewModels
{
    public class ProfileViewModel
    {
        [Required]
        [Display(Name = "Name")]
        [StringLength(20, ErrorMessage = "Name cannot exceed 20 characters.")]
        [RegularExpression(@"^[A-Za-z\s]+$", ErrorMessage = "Name must contain only letters.")]
        public string FirstName { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Mobile Number")]
        [Required]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Mobile number must be exactly 10 digits.")]
        public string MobileNumber { get; set; }

        //[Required]
        //[RegularExpression(@"^\d{10}$", ErrorMessage = "Mobile number must be exactly 10 digits.")]

        //public string AlternativeMobileNumber { get; set; }
        //[Required]
        //[Display(Name = "UserRole")]
        //public string UserRole { get; set; }
        ////public string Address { get; set; }

        //[Required]
        //public string BloodGroup { get; set; }
        //[Required]
        //[Display(Name = "Date of Joining")]
        //public DateTime? DateOfJoing { get; set; }
        //[Required]
        //[Display(Name = "Date of Birth")]
        //public DateTime? DateOfBirth { get; set; }




        public string Address { get; set; }

        // View state
        public bool IsEditMode { get; set; }

        // 🔑 Role-based permission
        public bool CanEditEmail { get; set; }
    }
}
