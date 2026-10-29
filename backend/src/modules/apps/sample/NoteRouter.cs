using DotnetSvelte.Modules.Base;
using DotnetSvelte.Modules.Base.Auth;

namespace DotnetSvelte.Modules.Apps.Sample;

public static class NoteRouter
{
    public static void MapSample(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/sample").WithTags("[APPS] Sample");

        group.MapGet("", () => new MessageResponse("Sample module — see /sample/notes for the canonical CRUD example"))
            .WithName("sample_root");

        var notes = group.MapGroup("/notes").RequireUser();

        notes.MapGet("", (HttpContext http, NoteService svc) => svc.ListAsync(http.CurrentUser()))
            .WithName("sample_list_notes");

        notes.MapPost("", async (NoteCreate body, HttpContext http, NoteService svc) =>
                Results.Json(await svc.CreateAsync(http.CurrentUser(), body), Core.Config.Json.Options, statusCode: 201))
            .Produces<NotePublic>(201)
            .WithName("sample_create_note");

        notes.MapGet("/{id}", (Guid id, HttpContext http, NoteService svc) => svc.GetAsync(http.CurrentUser(), id))
            .WithName("sample_read_note");

        notes.MapPatch("/{id}", (Guid id, NoteUpdate body, HttpContext http, NoteService svc) =>
                svc.UpdateAsync(http.CurrentUser(), id, body))
            .WithName("sample_update_note");

        notes.MapDelete("/{id}", async (Guid id, HttpContext http, NoteService svc) =>
            {
                await svc.DeleteAsync(http.CurrentUser(), id);
                return Results.NoContent();
            })
            .WithName("sample_delete_note");
    }
}
