using DotnetSvelte.Core.Errors;
using DotnetSvelte.Modules.Base.Users;

namespace DotnetSvelte.Modules.Base.Auth;

/// <summary>Endpoint metadata: read by <see cref="AuthMiddleware"/> and by the OpenAPI security requirement.</summary>
public sealed record RequiresAuth(bool Superuser = false);

public static class AuthEndpointExtensions
{
    internal const string UserKey = "foxg.current_user";

    public static User CurrentUser(this HttpContext http) => (User)http.Items[UserKey]!;

    public static TBuilder RequireUser<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new RequiresAuth());

    public static TBuilder RequireSuperuser<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new RequiresAuth(Superuser: true));
}

/// <summary>Authenticates before parameter binding, so a bad token wins over a bad body.</summary>
public sealed class AuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, AuthService auth)
    {
        if (http.GetEndpoint()?.Metadata.GetMetadata<RequiresAuth>() is { } requirement)
        {
            var header = http.Request.Headers.Authorization.ToString();
            var user = await auth.AuthenticateAsync(header.Length > 0 ? header : null);
            if (requirement.Superuser && !user.IsSuperuser)
                throw AppException.Forbidden("The user doesn't have enough privileges");
            http.Items[AuthEndpointExtensions.UserKey] = user;
        }

        await next(http);
    }
}
