namespace WebApi.Models;

public class RefreshToken
{
    public string Token { get; set; } = null!;

    public DateTime Created { get; set; } = DateTime.UtcNow;

    public DateTime Expires { get; set; }

    public int RefreshTokenFamilyId { get; set; }
}
