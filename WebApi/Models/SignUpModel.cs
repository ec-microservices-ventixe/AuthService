namespace WebApi.Models;

public class SignUpModel
{
    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string ConfirmPassword { get; set; } = null!;
}
