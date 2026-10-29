namespace DotnetSvelte.Modules.Apps.Sample;

public sealed record NoteCreate
{
    public required string Title { get; init; }
    public string Content { get; init; } = "";
}

public sealed record NoteUpdate
{
    public string? Title { get; init; }
    public string? Content { get; init; }
}

public sealed record NotePublic(Guid Id, string Title, string Content, Guid OwnerId, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static NotePublic From(Note n) => new(n.Id, n.Title, n.Content, n.OwnerId, n.CreatedAt, n.UpdatedAt);
}
