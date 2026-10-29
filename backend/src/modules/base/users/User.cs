using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotnetSvelte.Modules.Base.Users;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public bool IsSuperuser { get; set; }
    public string? FullName { get; set; }
    public string HashedPassword { get; set; } = "";
}

public sealed class UserMap : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("user");
        b.HasKey(u => u.Id);
        b.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(u => u.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
        b.Property(u => u.IsActive).HasColumnName("is_active");
        b.Property(u => u.IsSuperuser).HasColumnName("is_superuser");
        b.Property(u => u.FullName).HasColumnName("full_name").HasMaxLength(255);
        b.Property(u => u.HashedPassword).HasColumnName("hashed_password").IsRequired();
        b.HasIndex(u => u.Email).IsUnique().HasDatabaseName("ix_user_email");
    }
}
