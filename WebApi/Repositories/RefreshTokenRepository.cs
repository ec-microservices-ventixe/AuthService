using WebApi.Data.Context;
using WebApi.Data.Entities;
using WebApi.Interfaces;

namespace WebApi.Repositories;

public class RefreshTokenRepository(ApplicationDbContext context) : BaseRepository<AppUserRefreshTokenEntity>(context), IRefreshTokenRepository
{
    private readonly ApplicationDbContext _context = context;
}
