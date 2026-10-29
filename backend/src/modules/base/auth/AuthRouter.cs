using DotnetSvelte.Core.Errors;
using DotnetSvelte.Modules.Base.Users;

namespace DotnetSvelte.Modules.Base.Auth;

public static class AuthRouter
{
    public static void MapAuth(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/base/login").WithTags("[BASE] Auth");

        group.MapPost("/access-token", async (HttpContext http, AuthService auth) =>
            {
                if (!http.Request.HasFormContentType)
                    throw AppException.Invalid("username and password are required as form fields");

                var form = await http.Request.ReadFormAsync();
                if (!form.TryGetValue("username", out var username) || !form.TryGetValue("password", out var password))
                    throw AppException.Invalid("username and password are required");

                return await auth.LoginAsync(username.ToString(), password.ToString());
            })
            .Accepts<LoginForm>("application/x-www-form-urlencoded")
            .WithName("auth_login_access_token");

        group.MapGet("/me", (HttpContext http) => UserPublic.From(http.CurrentUser()))
            .RequireUser()
            .WithName("auth_login_me");
    }
}
