using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApi.Data.Entities;

public class AppUserRefreshTokenEntity
{
    [Key]
    public int Id { get; set; }

    [Column(TypeName = "varchar(1000)")]
    public string Token { get; set; } = null!;

    public DateTime Created { get; set; } = DateTime.UtcNow;

    public DateTime Expires { get; set; }

    [ForeignKey(nameof(User))]
    public string UserId { get; set; } = null!;
    public AppUserEntitiy User { get; set; } = null!;
}
