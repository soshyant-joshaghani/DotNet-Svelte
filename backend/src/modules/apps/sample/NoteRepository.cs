using DotnetSvelte.Core.Db;
using Microsoft.EntityFrameworkCore;

namespace DotnetSvelte.Modules.Apps.Sample;

public interface INoteRepository
{
    Task<IReadOnlyList<Note>> ListByOwnerAsync(Guid ownerId);
    Task<Note?> GetByIdAsync(Guid id);
    Task AddAsync(Note note);
    Task SaveAsync(Note note);
    Task DeleteAsync(Note note);
    Task DeleteByOwnerAsync(Guid ownerId);
}

public sealed class EfNoteRepository(AppDbContext db) : INoteRepository
{
    public async Task<IReadOnlyList<Note>> ListByOwnerAsync(Guid ownerId) =>
        await db.Notes.AsNoTracking().Where(n => n.OwnerId == ownerId).OrderByDescending(n => n.UpdatedAt).ToListAsync();

    public Task<Note?> GetByIdAsync(Guid id) => db.Notes.FirstOrDefaultAsync(n => n.Id == id);

    public async Task AddAsync(Note note)
    {
        db.Notes.Add(note);
        await db.SaveChangesAsync();
    }

    public Task SaveAsync(Note note) => db.SaveChangesAsync();

    public async Task DeleteAsync(Note note)
    {
        db.Notes.Remove(note);
        await db.SaveChangesAsync();
    }

    public Task DeleteByOwnerAsync(Guid ownerId) => db.Notes.Where(n => n.OwnerId == ownerId).ExecuteDeleteAsync();
}
