using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApi.Data.Entities;

[Index(nameof(Token), IsUnique = true)]
public class AppUserRefreshTokenEntity
{
    [Key]
    public int Id { get; set; }


    [Column(TypeName = "varchar(1000)")]

    public string Token { get; set; } = null!;

    public DateTime Created { get; set; } = DateTime.UtcNow;

    public DateTime Expires { get; set; }


    public bool HasRotated = false;


    public bool IsLocked = false;


    [ForeignKey(nameof(User))]
    public string UserId { get; set; } = null!;
    public AppUserEntity User { get; set; } = null!;


    [ForeignKey(nameof(RefreshTokenFamily))]
    public int RefreshTokenFamilyId { get; set; }
    public RefreshTokenFamilyEntity RefreshTokenFamily { get; set; } = null!;
}
