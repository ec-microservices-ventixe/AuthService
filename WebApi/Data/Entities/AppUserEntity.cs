using Microsoft.AspNetCore.Identity;

namespace WebApi.Data.Entities;

public class AppUserEntity : IdentityUser
{
    public ICollection<AppUserRefreshTokenEntity> RefreshTokens { get; set; } = [];
}
