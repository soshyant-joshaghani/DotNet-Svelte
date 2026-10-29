using System.Text.Json;
using DotnetSvelte.Core.Config;

namespace DotnetSvelte.Core.Errors;

public sealed record ErrorBody(string Detail);

public sealed class ErrorHandlingMiddleware(
    RequestDelegate next,
    Settings settings,
    ILogger<ErrorHandlingMiddleware> log)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (Exception ex) when (!ctx.Response.HasStarted)
        {
            ctx.Response.Clear();
            switch (ex)
            {
                case AppException app:
                    if (app.BearerChallenge) ctx.Response.Headers.WWWAuthenticate = "Bearer";
                    await Write(ctx, app.StatusCode, app.Detail);
                    return;
                case BadHttpRequestException or JsonException:
                    await Write(ctx, 422, "Invalid request");
                    return;
                default:
                    log.LogError(ex, "Unhandled exception");
                    await Write(ctx, 500, settings.IsLocal ? ex.Message : "Internal Server Error");
                    return;
            }
        }

        if (!ctx.Response.HasStarted && ctx.Response.ContentLength is null && ctx.Response.ContentType is null)
        {
            switch (ctx.Response.StatusCode)
            {
                case 404: await Write(ctx, 404, "Not Found"); break;
                case 405: await Write(ctx, 405, "Method Not Allowed"); break;
            }
        }
    }

    private static Task Write(HttpContext ctx, int status, string detail)
    {
        ctx.Response.StatusCode = status;
        return ctx.Response.WriteAsJsonAsync(new ErrorBody(detail), Json.Options);
    }
}
