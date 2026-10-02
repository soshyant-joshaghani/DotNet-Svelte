using DotnetSvelte.Core.Cache;
using DotnetSvelte.Core.Config;
using DotnetSvelte.Core.Db;
using DotnetSvelte.Core.Errors;
using DotnetSvelte.Core.Jobs;
using DotnetSvelte.Core.OpenApi;
using DotnetSvelte.Core.Security;
using DotnetSvelte.Modules.Apps.Sample;
using DotnetSvelte.Modules.Base.Auth;
using DotnetSvelte.Modules.Base.Users;
using DotnetSvelte.Modules.Sys;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DotnetSvelte;

public static class Bootstrap
{
    public static IServiceCollection AddFoxg(this IServiceCollection services)
    {
        services.AddSingleton(sp => Settings.From(sp.GetRequiredService<IConfiguration>()));
        services.ConfigureHttpJsonOptions(o => Json.Configure(o.SerializerOptions));
        services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);

        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<Settings>((cors, settings) =>
            cors.AddDefaultPolicy(p => p
                .WithOrigins([.. settings.AllCorsOrigins])
                .AllowCredentials()
                .AllowAnyMethod()
                .AllowAnyHeader()));

        services.AddSingleton(sp => new RedisConnection(sp.GetRequiredService<Settings>()));
        services.AddSingleton<ICache, RedisCache>();
        services.AddSingleton<IJobQueue, RedisJobQueue>();
        services.AddSingleton<JwtTokenService>();

        services.AddDbContext<AppDbContext>((sp, o) => o.UseNpgsql(sp.GetRequiredService<Settings>().ConnectionString));
        services.AddHostedService<DbMigrationService>();
        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<INoteRepository, EfNoteRepository>();

        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<NoteService>();
        services.AddHostedService<SuperuserSeeder>();

        return services.AddFoxgOpenApi();
    }

    public static WebApplication UseFoxg(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<Settings>();
        settings.EnsureSecure();

        app.UseMiddleware<ErrorHandlingMiddleware>();
        app.UseCors();
        app.UseMiddleware<AuthMiddleware>();

        app.MapFoxgOpenApi(settings);
        app.MapDocs(settings);

        var api = app.MapGroup(settings.ApiV1Str);
        api.MapSystem();
        if (settings.IsLocal) api.MapPrivate();
        api.MapAuth();
        api.MapUsers();
        api.MapSample();

        return app;
    }
}
