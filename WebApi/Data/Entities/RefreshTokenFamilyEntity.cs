using System.ComponentModel.DataAnnotations;

namespace WebApi.Data.Entities;

public class RefreshTokenFamilyEntity
{
    [Key]
    public int Id { get; set; }

    public bool IsLocked { get; set; } = false;

    public ICollection<AppUserRefreshTokenEntity> AppUserRefreshTokens { get; set; } = [];
}
