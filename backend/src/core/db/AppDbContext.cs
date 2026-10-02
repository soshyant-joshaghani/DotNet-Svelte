using DotnetSvelte.Modules.Apps.Sample;
using DotnetSvelte.Modules.Base.Users;
using Microsoft.EntityFrameworkCore;

namespace DotnetSvelte.Core.Db;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
