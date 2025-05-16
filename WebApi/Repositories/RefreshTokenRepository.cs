using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using WebApi.Data.Context;
using WebApi.Data.Entities;
using WebApi.Interfaces;

namespace WebApi.Repositories;

public class RefreshTokenRepository(ApplicationDbContext context) : BaseRepository<AppUserRefreshTokenEntity>(context), IRefreshTokenRepository
{
    private readonly ApplicationDbContext _context = context;

    public override async Task<IEnumerable<AppUserRefreshTokenEntity>> GetAllAsync()
    {
        return await _context.AppUsersRefreshTokens
            .Include(x => x.User)
            .Include(x => x.RefreshTokenFamily)
            .ToListAsync();
    }

    public override async Task<AppUserRefreshTokenEntity> GetAsync(Expression<Func<AppUserRefreshTokenEntity, bool>> predicate)
    {
        var entity = await _context.AppUsersRefreshTokens
            .Include(x => x.User)
            .Include(x => x.RefreshTokenFamily)
            .FirstOrDefaultAsync(predicate);

        return entity ?? null!;
    }
}
