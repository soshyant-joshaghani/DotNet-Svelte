using DotnetSvelte.Core.Config;
using DotnetSvelte.Modules.Base.Auth;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DotnetSvelte.Core.OpenApi;

public static class OpenApiSetup
{
    public const string SchemeName = "OAuth2PasswordBearer";

    public static IServiceCollection AddFoxgOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, _) =>
            {
                var settings = context.ApplicationServices.GetRequiredService<Settings>();
                document.Info = new OpenApiInfo { Title = settings.ProjectName, Version = "0.1.0" };
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        Password = new OpenApiOAuthFlow
                        {
                            TokenUrl = new Uri($"{settings.ApiV1Str}/base/login/access-token", UriKind.Relative),
                            Scopes = new Dictionary<string, string>(),
                        },
                    },
                };
                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, _) =>
            {
                if (context.Description.ActionDescriptor.EndpointMetadata.OfType<RequiresAuth>().Any())
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(SchemeName, context.Document)] = [],
                    });
                }
                return Task.CompletedTask;
            });
        });
        return services;
    }

    public static void MapFoxgOpenApi(this IEndpointRouteBuilder app, Settings settings) =>
        app.MapOpenApi($"{settings.ApiV1Str}/openapi.json");
}
