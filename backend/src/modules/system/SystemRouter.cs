using DotnetSvelte.Core.Jobs;
using DotnetSvelte.Modules.Base;
using DotnetSvelte.Modules.Base.Users;

namespace DotnetSvelte.Modules.Sys;

public sealed record PrivateUserCreate
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public string? FullName { get; init; }
}

public sealed record JobEnqueued(string JobId, string Message);

public static class SystemRouter
{
    public static void MapSystem(this IEndpointRouteBuilder api)
    {
        api.MapGet("/utils/health-check", () => true)
            .WithTags("[SYSTEM] System - Utils")
            .WithName("utils_health_check");
    }

    /// <summary>Local-development helpers; only mapped when ENVIRONMENT=local.</summary>
    public static void MapPrivate(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/private").WithTags("[SYSTEM] System - Private");

        group.MapGet("/ping", () => new MessageResponse("private ok"))
            .WithName("private_ping");

        group.MapPost("/users", (PrivateUserCreate body, UserService users) =>
                users.CreateAsync(new UserCreate { Email = body.Email, Password = body.Password, FullName = body.FullName }))
            .WithName("private_create_user");

        group.MapPost("/jobs/ping", async (IJobQueue jobs, string message = "ping") =>
            {
                var id = await jobs.EnqueueAsync("ping", new { message });
                return new JobEnqueued(id, message);
            })
            .WithName("private_enqueue_ping_job");
    }
}
