namespace WebApi.Models;

public class ValidateEmailMessage
{
    public string Email { get; set; } = null!;

    public string Token { get; set; } = null!;
}
