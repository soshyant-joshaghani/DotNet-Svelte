using DotnetSvelte.Core.Config;
using DotnetSvelte.Core.Security;

namespace DotnetSvelte.Modules.Base.Users;

public sealed class SuperuserSeeder(IServiceProvider services, Settings settings, ILogger<SuperuserSeeder> log)
    : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        if (await users.GetByEmailAsync(settings.FirstSuperuser) is not null) return;

        await users.AddAsync(new User
        {
            Email = settings.FirstSuperuser,
            IsSuperuser = true,
            HashedPassword = PasswordHasher.Hash(settings.FirstSuperuserPassword),
        });
        log.LogInformation("created first superuser {Email}", settings.FirstSuperuser);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
