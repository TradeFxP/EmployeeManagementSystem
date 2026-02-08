using System.ComponentModel.DataAnnotations;

public class UserEditViewModel
{
    public string Id { get; set; } = "";
    [Required] public string FirstName { get; set; } = "";
    [Required] public string LastName { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [DataType(DataType.Password)] public string? NewPassword { get; set; }
}
