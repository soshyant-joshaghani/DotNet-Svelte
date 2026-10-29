using System.Net;
using System.Net.Http.Headers;
using DotnetSvelte.Core.Config;
using DotnetSvelte.Core.Security;
using System.Text.Json;

namespace DotnetSvelte.Tests;

public class AuthTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;

    public AuthTests(TestApp app) => _app = app;

    private static FormUrlEncodedContent Form(string username, string password) =>
        new(new Dictionary<string, string> { ["username"] = username, ["password"] = password });

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsBearerToken()
    {
        var response = await _app.CreateClient().PostAsync("/api/v1/base/login/access-token",
            Form(TestApp.AdminEmail, TestApp.AdminPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.JsonAsync();
        Assert.Equal("bearer", body.GetProperty("token_type").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("access_token").GetString()));
    }

    [Theory]
    [InlineData(TestApp.AdminEmail, "wrong-password")]
    [InlineData("nobody@example.com", "password123")]
    public async Task Login_WithBadCredentials_Returns400(string email, string password)
    {
        var response = await _app.CreateClient().PostAsync("/api/v1/base/login/access-token", Form(email, password));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Incorrect email or password", await response.DetailAsync());
    }

    [Fact]
    public async Task Login_InactiveUser_Returns400()
    {
        var user = await _app.AddUserAsync($"{Guid.NewGuid():N}@example.com", active: false);

        var response = await _app.CreateClient().PostAsync("/api/v1/base/login/access-token",
            Form(user.Email, "password123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Inactive user", await response.DetailAsync());
    }

    [Fact]
    public async Task Login_WithoutForm_Returns422String()
    {
        var response = await _app.CreateClient().PostAsync("/api/v1/base/login/access-token",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = "x" }));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.IsType<string>(await response.DetailAsync());
    }

    [Fact]
    public async Task Me_ReturnsCurrentUser()
    {
        var client = await _app.AdminClientAsync();

        var response = await client.GetAsync("/api/v1/base/login/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.JsonAsync();
        Assert.Equal(TestApp.AdminEmail, body.GetProperty("email").GetString());
        Assert.True(body.GetProperty("is_superuser").GetBoolean());
        Assert.True(body.GetProperty("is_active").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("full_name").ValueKind);
        Assert.Matches("^[0-9a-f]{8}-([0-9a-f]{4}-){3}[0-9a-f]{12}$", body.GetProperty("id").GetString()!);
        Assert.False(body.TryGetProperty("hashed_password", out _));
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401NotAuthenticated()
    {
        var response = await _app.CreateClient().GetAsync("/api/v1/base/login/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Not authenticated", await response.DetailAsync());
    }

    [Fact]
    public async Task Me_WithGarbageToken_Returns401WithChallenge()
    {
        var client = _app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        var response = await client.GetAsync("/api/v1/base/login/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Could not validate credentials", await response.DetailAsync());
        Assert.Contains("Bearer", response.Headers.WwwAuthenticate.Select(h => h.Scheme));
    }

    [Fact]
    public async Task Me_WithTokenSignedByOtherKey_Returns401()
    {
        var foreign = new JwtTokenService(new Settings { SecretKey = "another-secret" })
            .Create(Guid.NewGuid(), TimeSpan.FromMinutes(5));
        var client = _app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", foreign);

        var response = await client.GetAsync("/api/v1/base/login/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithExpiredToken_Returns401()
    {
        var admin = _app.Users.Items.First(u => u.Email == TestApp.AdminEmail);
        var settings = new Settings { SecretKey = "test-secret-key-not-for-production" };
        var expired = new JwtTokenService(settings).Create(admin.Id, TimeSpan.FromMinutes(-1));
        var client = _app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expired);

        var response = await client.GetAsync("/api/v1/base/login/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Could not validate credentials", await response.DetailAsync());
    }

    [Fact]
    public async Task Me_WithTokenForDeletedUser_Returns401()
    {
        var (user, client) = await _app.NewUserClientAsync();
        _app.Users.Items.Remove(user);

        var response = await client.GetAsync("/api/v1/base/login/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Could not validate credentials", await response.DetailAsync());
    }

    [Fact]
    public void PasswordHasher_ProducesBcrypt2b_AndVerifiesPythonHash()
    {
        Assert.StartsWith("$2b$", PasswordHasher.Hash("Admin@1234"));

        const string pythonHash = "$2b$12$LACsF0utmWoo5lQrqmGlJOP3BZlXDClW/EP/hHGG9/flMtj5Gnb96";
        Assert.True(PasswordHasher.Verify("Admin@1234", pythonHash));
        Assert.False(PasswordHasher.Verify("nope", pythonHash));
        Assert.False(PasswordHasher.Verify("x", "not-a-hash"));
    }

    [Fact]
    public void Jwt_RoundTripsAndSupportsShortSecrets()
    {
        var tokens = new JwtTokenService(new Settings { SecretKey = "changethis" });
        var id = Guid.NewGuid();

        Assert.Equal(id, tokens.Validate(tokens.Create(id, TimeSpan.FromMinutes(1))));
        Assert.Null(tokens.Validate("a.b.c"));
    }
}
