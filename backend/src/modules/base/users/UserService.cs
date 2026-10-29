using DotnetSvelte.Core.Errors;
using DotnetSvelte.Core.Security;
using DotnetSvelte.Modules.Apps.Sample;

namespace DotnetSvelte.Modules.Base.Users;

public sealed class UserService(IUserRepository users, INoteRepository notes)
{
    public async Task<UsersPublic> ListAsync(int skip, int limit)
    {
        if (skip < 0 || limit < 0) throw AppException.Invalid("skip and limit must not be negative");
        var (items, total) = await users.ListAsync(skip, limit);
        return new UsersPublic([.. items.Select(UserPublic.From)], total);
    }

    public async Task<UserPublic> CreateAsync(UserCreate data)
    {
        data.Validate();
        if (await users.GetByEmailAsync(data.Email) is not null)
            throw AppException.BadRequest("The user with this email already exists in the system.");

        var user = new User
        {
            Email = data.Email,
            IsActive = data.IsActive,
            IsSuperuser = data.IsSuperuser,
            FullName = data.FullName,
            HashedPassword = PasswordHasher.Hash(data.Password),
        };
        await users.AddAsync(user);
        return UserPublic.From(user);
    }

    public async Task<UserPublic> GetAsync(Guid id, User current)
    {
        if (id == current.Id) return UserPublic.From(current);
        if (!current.IsSuperuser) throw AppException.Forbidden("The user doesn't have enough privileges");
        var user = await users.GetByIdAsync(id) ?? throw AppException.NotFound("User not found");
        return UserPublic.From(user);
    }

    public async Task<UserPublic> UpdateAsync(Guid id, UserUpdate data)
    {
        data.Validate();
        var user = await users.GetByIdAsync(id)
            ?? throw AppException.NotFound("The user with this id does not exist in the system");

        if (!string.IsNullOrEmpty(data.Email))
        {
            var existing = await users.GetByEmailAsync(data.Email);
            if (existing is not null && existing.Id != id)
                throw AppException.Conflict("User with this email already exists");
            user.Email = data.Email;
        }
        if (data.Password is not null) user.HashedPassword = PasswordHasher.Hash(data.Password);
        if (data.FullNameProvided) user.FullName = data.FullName;
        if (data.IsActive is { } active) user.IsActive = active;
        if (data.IsSuperuser is { } superuser) user.IsSuperuser = superuser;

        await users.SaveAsync(user);
        return UserPublic.From(user);
    }

    public async Task<MessageResponse> DeleteAsync(Guid id, User current)
    {
        var user = await users.GetByIdAsync(id) ?? throw AppException.NotFound("User not found");
        if (user.Id == current.Id)
            throw AppException.Forbidden("Super users are not allowed to delete themselves");

        await notes.DeleteByOwnerAsync(id);
        await users.DeleteAsync(user);
        return new MessageResponse("User deleted successfully");
    }
}
