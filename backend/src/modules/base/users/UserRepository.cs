using DotnetSvelte.Core.Db;
using Microsoft.EntityFrameworkCore;

namespace DotnetSvelte.Modules.Base.Users;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<(IReadOnlyList<User> Items, int Total)> ListAsync(int skip, int limit);
    Task AddAsync(User user);
    Task SaveAsync(User user);
    Task DeleteAsync(User user);
}

public sealed class EfUserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id) => db.Users.FirstOrDefaultAsync(u => u.Id == id);

    public Task<User?> GetByEmailAsync(string email) => db.Users.FirstOrDefaultAsync(u => u.Email == email);

    public async Task<(IReadOnlyList<User> Items, int Total)> ListAsync(int skip, int limit)
    {
        var total = await db.Users.CountAsync();
        var items = await db.Users.AsNoTracking().OrderBy(u => u.Email).Skip(skip).Take(limit).ToListAsync();
        return (items, total);
    }

    public async Task AddAsync(User user)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    public Task SaveAsync(User user) => db.SaveChangesAsync();

    public async Task DeleteAsync(User user)
    {
        db.Users.Remove(user);
        await db.SaveChangesAsync();
    }
}
