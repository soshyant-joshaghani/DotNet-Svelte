using System.Net;
using System.Net.Http.Json;
using DotnetSvelte.Core.Security;

namespace DotnetSvelte.Tests;

public class UserTests : IClassFixture<TestApp>
{
    private const string Base = "/api/v1/base/users";
    private readonly TestApp _app;

    public UserTests(TestApp app) => _app = app;

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task AdminRoutes_WithoutToken_Return401()
    {
        var client = _app.CreateClient();
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{Base}/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"{Base}/admin", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{Base}/{id}/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PatchAsJsonAsync($"{Base}/{id}/admin", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync($"{Base}/{id}/admin")).StatusCode);
    }

    [Fact]
    public async Task SuperuserRoutes_AsNormalUser_Return403()
    {
        var (_, client) = await _app.NewUserClientAsync();
        var id = Guid.NewGuid();

        var responses = new[]
        {
            await client.GetAsync($"{Base}/admin"),
            await client.PostAsJsonAsync($"{Base}/admin", new { email = NewEmail(), password = "password123" }),
            await client.PatchAsJsonAsync($"{Base}/{id}/admin", new { full_name = "x" }),
            await client.DeleteAsync($"{Base}/{id}/admin"),
        };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("The user doesn't have enough privileges", await response.DetailAsync());
        }
    }

    [Fact]
    public async Task List_ReturnsPagedDataOrderedByEmail()
    {
        var admin = await _app.AdminClientAsync();
        await _app.AddUserAsync("zz-" + NewEmail());
        await _app.AddUserAsync("aa-" + NewEmail());

        var all = await (await admin.GetAsync($"{Base}/admin?skip=0&limit=100")).JsonAsync();
        var emails = all.GetProperty("data").EnumerateArray().Select(u => u.GetProperty("email").GetString()!).ToList();
        Assert.Equal(emails.OrderBy(e => e, StringComparer.Ordinal), emails);
        Assert.Equal(_app.Users.Items.Count, all.GetProperty("count").GetInt32());

        var page = await (await admin.GetAsync($"{Base}/admin?skip=1&limit=1")).JsonAsync();
        Assert.Single(page.GetProperty("data").EnumerateArray());
        Assert.Equal(_app.Users.Items.Count, page.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Create_ThenDuplicate_Returns400()
    {
        var admin = await _app.AdminClientAsync();
        var email = NewEmail();

        var created = await admin.PostAsJsonAsync($"{Base}/admin",
            new { email, password = "password123", full_name = "New One", is_superuser = true });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var body = await created.JsonAsync();
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.True(body.GetProperty("is_active").GetBoolean());
        Assert.True(body.GetProperty("is_superuser").GetBoolean());
        Assert.StartsWith("$2b$", _app.Users.Items.Single(u => u.Email == email).HashedPassword);

        var duplicate = await admin.PostAsJsonAsync($"{Base}/admin", new { email, password = "password123" });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal("The user with this email already exists in the system.", await duplicate.DetailAsync());
    }

    [Theory]
    [InlineData("not-an-email", "password123")]
    [InlineData("ok@example.com", "short")]
    public async Task Create_WithInvalidBody_Returns422String(string email, string password)
    {
        var admin = await _app.AdminClientAsync();

        var response = await admin.PostAsJsonAsync($"{Base}/admin", new { email, password });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.IsType<string>(await response.DetailAsync());
    }

    [Fact]
    public async Task Create_WithMissingField_Returns422()
    {
        var admin = await _app.AdminClientAsync();

        var response = await admin.PostAsJsonAsync($"{Base}/admin", new { email = NewEmail() });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Get_Self_AllowedForNormalUser_OthersForbidden()
    {
        var (user, client) = await _app.NewUserClientAsync();
        var other = await _app.AddUserAsync(NewEmail());

        var self = await client.GetAsync($"{Base}/{user.Id}/admin");
        Assert.Equal(HttpStatusCode.OK, self.StatusCode);
        Assert.Equal(user.Email, (await self.JsonAsync()).GetProperty("email").GetString());

        var forbidden = await client.GetAsync($"{Base}/{other.Id}/admin");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Get_AsSuperuser_FindsOthers_Or404()
    {
        var admin = await _app.AdminClientAsync();
        var other = await _app.AddUserAsync(NewEmail());

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"{Base}/{other.Id}/admin")).StatusCode);

        var missing = await admin.GetAsync($"{Base}/{Guid.NewGuid()}/admin");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("User not found", await missing.DetailAsync());
    }

    [Fact]
    public async Task Get_WithInvalidId_Returns422()
    {
        var admin = await _app.AdminClientAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.GetAsync($"{Base}/not-a-uuid/admin")).StatusCode);
    }

    [Fact]
    public async Task Update_ChangesOnlySentFields_AndRehashesPassword()
    {
        var admin = await _app.AdminClientAsync();
        var user = await _app.AddUserAsync(NewEmail());
        user.FullName = "Before";
        var oldHash = user.HashedPassword;

        var renamed = await admin.PatchAsJsonAsync($"{Base}/{user.Id}/admin", new { is_active = false });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.False((await renamed.JsonAsync()).GetProperty("is_active").GetBoolean());
        Assert.Equal("Before", user.FullName);
        Assert.Equal(oldHash, user.HashedPassword);

        var cleared = await admin.PatchAsJsonAsync($"{Base}/{user.Id}/admin", new { full_name = (string?)null, password = "new-password-1" });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Null(user.FullName);
        Assert.NotEqual(oldHash, user.HashedPassword);
        Assert.True(PasswordHasher.Verify("new-password-1", user.HashedPassword));
    }

    [Fact]
    public async Task Update_Errors()
    {
        var admin = await _app.AdminClientAsync();
        var a = await _app.AddUserAsync(NewEmail());
        var b = await _app.AddUserAsync(NewEmail());

        var missing = await admin.PatchAsJsonAsync($"{Base}/{Guid.NewGuid()}/admin", new { full_name = "x" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("The user with this id does not exist in the system", await missing.DetailAsync());

        var clash = await admin.PatchAsJsonAsync($"{Base}/{a.Id}/admin", new { email = b.Email });
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        Assert.Equal("User with this email already exists", await clash.DetailAsync());

        var same = await admin.PatchAsJsonAsync($"{Base}/{a.Id}/admin", new { email = a.Email });
        Assert.Equal(HttpStatusCode.OK, same.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesUserAndTheirNotes()
    {
        var admin = await _app.AdminClientAsync();
        var (victim, victimClient) = await _app.NewUserClientAsync();
        Assert.Equal(HttpStatusCode.Created,
            (await victimClient.PostAsJsonAsync("/api/v1/sample/notes", new { title = "bye" })).StatusCode);

        var response = await admin.DeleteAsync($"{Base}/{victim.Id}/admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("User deleted successfully", (await response.JsonAsync()).GetProperty("message").GetString());
        Assert.DoesNotContain(_app.Users.Items, u => u.Id == victim.Id);
        Assert.DoesNotContain(_app.Notes.Items, n => n.OwnerId == victim.Id);

        var again = await admin.DeleteAsync($"{Base}/{victim.Id}/admin");
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Equal("User not found", await again.DetailAsync());
    }

    [Fact]
    public async Task Delete_Self_Returns403()
    {
        var (superuser, client) = await _app.NewUserClientAsync(superuser: true);

        var response = await client.DeleteAsync($"{Base}/{superuser.Id}/admin");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Super users are not allowed to delete themselves", await response.DetailAsync());
        Assert.Contains(_app.Users.Items, u => u.Id == superuser.Id);
    }
}
