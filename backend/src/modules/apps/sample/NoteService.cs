using DotnetSvelte.Core.Cache;
using DotnetSvelte.Core.Errors;
using DotnetSvelte.Modules.Base.Users;

namespace DotnetSvelte.Modules.Apps.Sample;

/// <summary>Sample notes CRUD with a Redis read-through cache (list and single note); writes invalidate.</summary>
public sealed class NoteService(INoteRepository notes, ICache cache)
{
    private const string Prefix = "sample:notes:v1:";
    private static readonly TimeSpan ListTtl = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan NoteTtl = TimeSpan.FromSeconds(300);

    private static string ListKey(Guid owner) => $"{Prefix}list:{owner}";
    private static string NoteKey(Guid owner, Guid id) => $"{Prefix}note:{owner}:{id}";

    private async Task InvalidateOwnerAsync(Guid owner)
    {
        await cache.DeletePrefixAsync(ListKey(owner));
        await cache.DeletePrefixAsync($"{Prefix}note:{owner}:");
    }

    public async Task<IReadOnlyList<NotePublic>> ListAsync(User user)
    {
        var key = ListKey(user.Id);
        if (await cache.GetAsync<List<NotePublic>>(key) is { } cached) return cached;

        var result = (await notes.ListByOwnerAsync(user.Id)).Select(NotePublic.From).ToList();
        await cache.SetAsync(key, result, ListTtl);
        return result;
    }

    public async Task<NotePublic> CreateAsync(User user, NoteCreate data)
    {
        var title = CleanTitle(data.Title);
        Validate.MaxLength(data.Content, "content", 10000);
        var content = (data.Content ?? "").Trim();

        var now = DateTime.UtcNow;
        var note = new Note
        {
            Title = title,
            Content = content,
            OwnerId = user.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await notes.AddAsync(note);

        var result = NotePublic.From(note);
        await InvalidateOwnerAsync(user.Id);
        await cache.SetAsync(NoteKey(user.Id, note.Id), result, NoteTtl);
        return result;
    }

    public async Task<NotePublic> GetAsync(User user, Guid id)
    {
        var key = NoteKey(user.Id, id);
        if (await cache.GetAsync<NotePublic>(key) is { } cached)
        {
            if (cached.OwnerId != user.Id) throw NotOwner();
            return cached;
        }

        var note = await Owned(user, id);
        var result = NotePublic.From(note);
        await cache.SetAsync(key, result, NoteTtl);
        return result;
    }

    public async Task<NotePublic> UpdateAsync(User user, Guid id, NoteUpdate data)
    {
        var note = await Owned(user, id);
        if (data.Title is not null) note.Title = CleanTitle(data.Title);
        if (data.Content is not null)
        {
            Validate.MaxLength(data.Content, "content", 10000);
            note.Content = data.Content.Trim();
        }

        if (data.Title is not null || data.Content is not null)
        {
            note.UpdatedAt = DateTime.UtcNow;
            await notes.SaveAsync(note);
        }

        var result = NotePublic.From(note);
        await InvalidateOwnerAsync(user.Id);
        await cache.SetAsync(NoteKey(user.Id, id), result, NoteTtl);
        return result;
    }

    public async Task DeleteAsync(User user, Guid id)
    {
        var note = await Owned(user, id);
        await notes.DeleteAsync(note);
        await cache.DeleteAsync(NoteKey(user.Id, id));
        await InvalidateOwnerAsync(user.Id);
    }

    private async Task<Note> Owned(User user, Guid id)
    {
        var note = await notes.GetByIdAsync(id) ?? throw AppException.NotFound("Note not found");
        return note.OwnerId == user.Id ? note : throw NotOwner();
    }

    private static AppException NotOwner() => AppException.Forbidden("Not allowed to access this note");

    private static string CleanTitle(string title)
    {
        Validate.Length(title, "title", 0, 255);
        var trimmed = title.Trim();
        return trimmed.Length == 0 ? throw AppException.Invalid("Title cannot be empty") : trimmed;
    }
}
