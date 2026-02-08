using System.ComponentModel.DataAnnotations;

namespace UserRoles.ViewModels
{
    public class CreateUserViewModel
    {
        // ================= NAME =================
        [Required(ErrorMessage = "Name is required.")]
        [StringLength(20, ErrorMessage = "Name must be letters only and maximum 20 characters.")]
        [RegularExpression(@"^[A-Za-z\s]+$", ErrorMessage = "Name must contain only letters.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "LastName is required.")]
        [StringLength(20, ErrorMessage = "Name must be letters only and maximum 20 characters.")]
        [RegularExpression(@"^[A-Za-z\s]+$", ErrorMessage = "Name must contain only letters.")]
        public string LastName { get; set; } = string.Empty;

        // ================= EMAIL (PERFECT VALIDATION) =================
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [RegularExpression(
            @"^(?!.*\.\.)[a-zA-Z0-9._%+-]+@[a-zA-Z0-9-]+\.[a-zA-Z]{2,}$",
            ErrorMessage = "Enter a valid email address."
        )]
        public string Email { get; set; } = string.Empty;

        public string? Role { get; set; }


        // ✅ NEW
        public string? ManagerId { get; set; }

    }
}
