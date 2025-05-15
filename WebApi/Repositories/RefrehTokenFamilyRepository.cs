using WebApi.Data.Context;
using WebApi.Data.Entities;
using WebApi.Interfaces;

namespace WebApi.Repositories;

public class RefrehTokenFamilyRepository(ApplicationDbContext context) : BaseRepository<RefreshTokenFamilyEntity>(context), IRefreshTokenFamilyRepository
{
    private readonly ApplicationDbContext _context = context;
}
