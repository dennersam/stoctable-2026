using Microsoft.EntityFrameworkCore;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Infrastructure.Context;

namespace Stoctable.Infrastructure.Repositories;

public class UserRepository(StoctableDbContext context) : Repository<User>(context), IUserRepository
{
    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(u => u.Username == username, ct);

    public async Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken, ct);

    public async Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default)
        => await DbSet.AnyAsync(u => u.Username == username, ct);

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => await DbSet.AnyAsync(u => u.Email == email, ct);
}
