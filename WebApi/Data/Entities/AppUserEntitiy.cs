using Microsoft.AspNetCore.Identity;

namespace WebApi.Data.Entities;

public class AppUserEntitiy : IdentityUser
{
    public ICollection<AppUserRefreshTokenEntity> RefreshTokens { get; set; } = [];
}
