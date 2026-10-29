using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DotnetSvelte.Core.Cache;
using DotnetSvelte.Core.Db;
using DotnetSvelte.Core.Jobs;
using DotnetSvelte.Modules.Apps.Sample;
using DotnetSvelte.Modules.Base.Users;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DotnetSvelte.Tests;

public sealed class FakeUserRepository : IUserRepository
{
    public List<User> Items { get; } = [];

    public Task<User?> GetByIdAsync(Guid id) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetByEmailAsync(string email) => Task.FromResult(Items.FirstOrDefault(u => u.Email == email));

    public Task<(IReadOnlyList<User> Items, int Total)> ListAsync(int skip, int limit) =>
        Task.FromResult<(IReadOnlyList<User>, int)>(
            ([.. Items.OrderBy(u => u.Email, StringComparer.Ordinal).Skip(skip).Take(limit)], Items.Count));

    public Task AddAsync(User user)
    {
        Items.Add(user);
        return Task.CompletedTask;
    }

    public Task SaveAsync(User user) => Task.CompletedTask;

    public Task DeleteAsync(User user)
    {
        Items.Remove(user);
        return Task.CompletedTask;
    }
}

public sealed class FakeNoteRepository : INoteRepository
{
    public List<Note> Items { get; } = [];

    public Task<IReadOnlyList<Note>> ListByOwnerAsync(Guid ownerId) =>
        Task.FromResult<IReadOnlyList<Note>>([.. Items.Where(n => n.OwnerId == ownerId).OrderByDescending(n => n.UpdatedAt)]);

    public Task<Note?> GetByIdAsync(Guid id) => Task.FromResult(Items.FirstOrDefault(n => n.Id == id));

    public Task AddAsync(Note note)
    {
        Items.Add(note);
        return Task.CompletedTask;
    }

    public Task SaveAsync(Note note) => Task.CompletedTask;

    public Task DeleteAsync(Note note)
    {
        Items.Remove(note);
        return Task.CompletedTask;
    }

    public Task DeleteByOwnerAsync(Guid ownerId)
    {
        Items.RemoveAll(n => n.OwnerId == ownerId);
        return Task.CompletedTask;
    }
}

public sealed class FakeJobQueue : IJobQueue
{
    public List<(string Task, JsonElement Args)> Jobs { get; } = [];

    public Task<string> EnqueueAsync(string task, object args)
    {
        Jobs.Add((task, JsonSerializer.SerializeToElement(args)));
        return Task.FromResult(Guid.NewGuid().ToString());
    }
}

public class TestApp : WebApplicationFactory<Program>
{
    private readonly string _environment;

    public TestApp() : this("local")
    {
    }

    protected TestApp(string environment) => _environment = environment;

    public const string AdminEmail = "admin@example.com";
    public const string AdminPassword = "Admin@1234";

    public FakeUserRepository Users { get; } = new();
    public FakeNoteRepository Notes { get; } = new();
    public InMemoryCache Cache { get; } = new();
    public FakeJobQueue Jobs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ENVIRONMENT", _environment);
        builder.UseSetting("SECRET_KEY", "test-secret-key-not-for-production");
        builder.UseSetting("FIRST_SUPERUSER", AdminEmail);
        builder.UseSetting("FIRST_SUPERUSER_PASSWORD", AdminPassword);

        builder.ConfigureServices(services =>
        {
            services.Remove(services.Single(d => d.ImplementationType == typeof(DbMigrationService)));
            services.RemoveAll<IUserRepository>();
            services.RemoveAll<INoteRepository>();
            services.RemoveAll<ICache>();
            services.RemoveAll<IJobQueue>();
            services.AddSingleton<IUserRepository>(Users);
            services.AddSingleton<INoteRepository>(Notes);
            services.AddSingleton<ICache>(Cache);
            services.AddSingleton<IJobQueue>(Jobs);
        });
    }

    public async Task<User> AddUserAsync(string email, string password = "password123",
        bool superuser = false, bool active = true)
    {
        var user = new User
        {
            Email = email,
            IsActive = active,
            IsSuperuser = superuser,
            HashedPassword = DotnetSvelte.Core.Security.PasswordHasher.Hash(password),
        };
        await Users.AddAsync(user);
        return user;
    }

    public async Task<HttpClient> ClientAsync(string? email = null, string password = "password123")
    {
        var client = CreateClient();
        if (email is null) return client;

        var response = await client.PostAsync("/api/v1/base/login/access-token", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["username"] = email, ["password"] = password }));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("access_token").GetString());
        return client;
    }

    public Task<HttpClient> AdminClientAsync() => ClientAsync(AdminEmail, AdminPassword);

    public async Task<(User User, HttpClient Client)> NewUserClientAsync(bool superuser = false)
    {
        var user = await AddUserAsync($"{Guid.NewGuid():N}@example.com", superuser: superuser);
        return (user, await ClientAsync(user.Email));
    }
}

public sealed class StagingApp() : TestApp("staging");

public static class HttpExtensions
{
    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    public static async Task<string> DetailAsync(this HttpResponseMessage response) =>
        (await response.JsonAsync()).GetProperty("detail").GetString()!;
}
