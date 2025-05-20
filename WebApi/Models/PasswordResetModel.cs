using System.ComponentModel.DataAnnotations;

namespace WebApi.Models;

public class PasswordResetModel
{
    [Required]
    public string Password { get; set; } = null!;

    [Required]
    [Compare(nameof(Password), ErrorMessage = "Passwords mismatch")]
    public string ConfirmPassword { get; set; } = null!;
}
