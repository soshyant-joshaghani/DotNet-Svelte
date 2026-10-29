using DotnetSvelte.Modules.Base.Auth;

namespace DotnetSvelte.Modules.Base.Users;

public static class UserRouter
{
    public static void MapUsers(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/base/users").WithTags("[SUPERADMIN] Core - User Management");

        group.MapGet("/admin", (UserService users, int skip = 0, int limit = 100) => users.ListAsync(skip, limit))
            .RequireSuperuser()
            .WithName("users_list");

        group.MapPost("/admin", (UserCreate body, UserService users) => users.CreateAsync(body))
            .RequireSuperuser()
            .WithName("users_create");

        group.MapGet("/{id}/admin", (Guid id, HttpContext http, UserService users) =>
                users.GetAsync(id, http.CurrentUser()))
            .RequireUser()
            .WithName("users_get");

        group.MapPatch("/{id}/admin", (Guid id, UserUpdate body, UserService users) =>
                users.UpdateAsync(id, body))
            .RequireSuperuser()
            .WithName("users_update");

        group.MapDelete("/{id}/admin", (Guid id, HttpContext http, UserService users) =>
                users.DeleteAsync(id, http.CurrentUser()))
            .RequireSuperuser()
            .WithName("users_delete");
    }
}
