using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebApi.Data.Entities;

namespace WebApi.Data.Context;

public class ApplicationDbContext : IdentityDbContext<AppUserEntitiy>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<AppUserEntitiy> AppUsers { get; set; } = null!;
    public DbSet<AppUserRefreshTokenEntity> AppUsersRefreshTokens { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<IdentityRole>().HasData(
            new IdentityRole { Id = "11111111-1111-1111-1111-111111111111", Name = "Admin", NormalizedName = "ADMIN" },
            new IdentityRole { Id = "22222222-2222-2222-2222-222222222222", Name = "User", NormalizedName = "USER" }
        );
    }

}
