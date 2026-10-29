using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotnetSvelte.Modules.Apps.Sample;

public sealed class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public Guid OwnerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class NoteMap : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> b)
    {
        b.ToTable("note");
        b.HasKey(n => n.Id);
        b.Property(n => n.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(n => n.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
        b.Property(n => n.Content).HasColumnName("content").HasMaxLength(10000).IsRequired();
        b.Property(n => n.OwnerId).HasColumnName("owner_id");
        b.Property(n => n.CreatedAt).HasColumnName("created_at");
        b.Property(n => n.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(n => n.OwnerId).HasDatabaseName("ix_note_owner_id");
    }
}
