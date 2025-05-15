using WebApi.Data.Context;
using WebApi.Data.Entities;
using WebApi.Interfaces;

namespace WebApi.Repositories;

public class AppUserRepository(ApplicationDbContext context) : BaseRepository<AppUserEntity>(context), IAppUserRepository
{
    private readonly ApplicationDbContext _context = context;
}
