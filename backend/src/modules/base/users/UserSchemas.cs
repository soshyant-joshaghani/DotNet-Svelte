using System.Text.Json.Serialization;
using Check = DotnetSvelte.Core.Errors.Validate;

namespace DotnetSvelte.Modules.Base.Users;

public sealed record UserPublic(Guid Id, string Email, bool IsActive, bool IsSuperuser, string? FullName)
{
    public static UserPublic From(User u) => new(u.Id, u.Email, u.IsActive, u.IsSuperuser, u.FullName);
}

public sealed record UsersPublic(IReadOnlyList<UserPublic> Data, int Count);

public sealed record UserCreate
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public bool IsActive { get; init; } = true;
    public bool IsSuperuser { get; init; }
    public string? FullName { get; init; }

    public void Validate()
    {
        Check.Email(Email);
        Check.Length(Password, "password", 8, 128);
        Check.MaxLength(FullName, "full_name", 255);
    }
}

/// <summary>Partial update: only properties present in the request body change (null full_name clears it).</summary>
public sealed class UserUpdate
{
    private string? _fullName;

    public string? Email { get; set; }
    public string? Password { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsSuperuser { get; set; }

    public string? FullName
    {
        get => _fullName;
        set
        {
            _fullName = value;
            FullNameProvided = true;
        }
    }

    [JsonIgnore]
    public bool FullNameProvided { get; private set; }

    public void Validate()
    {
        if (Email is not null) Check.Email(Email);
        if (Password is not null) Check.Length(Password, "password", 8, 128);
        Check.MaxLength(FullName, "full_name", 255);
    }
}
