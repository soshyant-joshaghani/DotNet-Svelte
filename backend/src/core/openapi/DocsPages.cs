using DotnetSvelte.Core.Config;

namespace DotnetSvelte.Core.OpenApi;

public static class DocsPages
{
    public static void MapDocs(this IEndpointRouteBuilder app, Settings settings)
    {
        var specUrl = $"{settings.ApiV1Str}/openapi.json";
        var title = System.Net.WebUtility.HtmlEncode(settings.ProjectName);

        var swagger = $$"""
            <!doctype html>
            <html>
              <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1" />
                <title>{{title}} - Swagger UI</title>
                <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/swagger-ui-dist@5/swagger-ui.css" />
              </head>
              <body>
                <div id="swagger-ui"></div>
                <script src="https://cdn.jsdelivr.net/npm/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
                <script>
                  window.ui = SwaggerUIBundle({
                    url: "{{specUrl}}",
                    dom_id: "#swagger-ui",
                    persistAuthorization: true,
                    layout: "BaseLayout",
                  });
                </script>
              </body>
            </html>
            """;

        var scalar = $$"""
            <!doctype html>
            <html>
              <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1" />
                <title>{{title}} - Scalar</title>
              </head>
              <body>
                <div id="app"></div>
                <script src="https://cdn.jsdelivr.net/npm/@scalar/api-reference"></script>
                <script>
                  Scalar.createApiReference("#app", {
                    url: "{{specUrl}}",
                    theme: "elysiajs",
                    layout: "modern",
                    persistAuth: true,
                    showToolbar: "localhost",
                    operationTitleSource: "summary",
                    defaultOpenFirstTag: true,
                    agent: { disabled: true },
                    authentication: { preferredSecurityScheme: "OAuth2PasswordBearer" },
                  });
                </script>
              </body>
            </html>
            """;

        app.MapGet("/docs", () => Results.Content(swagger, "text/html")).ExcludeFromDescription();
        app.MapGet("/sdoc", () => Results.Content(scalar, "text/html")).ExcludeFromDescription();
    }
}
