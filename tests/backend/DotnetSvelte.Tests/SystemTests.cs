using System.Net;
using System.Net.Http.Json;

namespace DotnetSvelte.Tests;

public class SystemTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;

    public SystemTests(TestApp app) => _app = app;

    [Fact]
    public async Task HealthCheck_ReturnsTrue()
    {
        var response = await _app.CreateClient().GetAsync("/api/v1/utils/health-check");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("true", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnknownRoute_ReturnsJsonNotFound()
    {
        var response = await _app.CreateClient().GetAsync("/api/v1/nope");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Not Found", await response.DetailAsync());
    }

    [Fact]
    public async Task OpenApi_Docs_AreServed()
    {
        var client = _app.CreateClient();

        var spec = await client.GetAsync("/api/v1/openapi.json");
        Assert.Equal(HttpStatusCode.OK, spec.StatusCode);
        var doc = await spec.JsonAsync();
        Assert.True(doc.GetProperty("paths").TryGetProperty("/api/v1/sample/notes", out _));
        Assert.True(doc.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("OAuth2PasswordBearer", out _));

        var swagger = await client.GetStringAsync("/docs");
        Assert.Contains("swagger-ui", swagger);
        var scalar = await client.GetStringAsync("/sdoc");
        Assert.Contains("elysiajs", scalar);
    }

    [Fact]
    public async Task Cors_AllowsFrontendHost()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/utils/health-check");
        request.Headers.Add("Origin", "http://dashboard.localhost");

        var response = await _app.CreateClient().SendAsync(request);

        Assert.Equal("http://dashboard.localhost", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    [Fact]
    public async Task PrivateRoutes_InLocal_Work()
    {
        var client = _app.CreateClient();

        var ping = await client.GetAsync("/api/v1/private/ping");
        Assert.Equal("private ok", (await ping.JsonAsync()).GetProperty("message").GetString());

        var created = await client.PostAsJsonAsync("/api/v1/private/users",
            new { email = "private@example.com", password = "password123", full_name = "Priv" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal("private@example.com", (await created.JsonAsync()).GetProperty("email").GetString());

        var duplicate = await client.PostAsJsonAsync("/api/v1/private/users",
            new { email = "private@example.com", password = "password123" });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [Fact]
    public async Task PrivateJobsPing_EnqueuesJob()
    {
        var response = await _app.CreateClient().PostAsync("/api/v1/private/jobs/ping?message=hello", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.JsonAsync();
        Assert.Equal("hello", body.GetProperty("message").GetString());
        Assert.True(Guid.TryParse(body.GetProperty("job_id").GetString(), out _));
        Assert.Contains(_app.Jobs.Jobs, j => j.Task == "ping" && j.Args.GetProperty("message").GetString() == "hello");
    }

    [Fact]
    public async Task PrivateRoutes_OutsideLocal_AreNotMapped()
    {
        await using var app = new StagingApp();
        var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/private/ping")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/v1/private/jobs/ping", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/utils/health-check")).StatusCode);
    }

    [Fact]
    public async Task Staging_RefusesDefaultSecrets()
    {
        await using var app = new DefaultSecretsApp();

        Assert.Throws<InvalidOperationException>(() => app.CreateClient());
    }

    private sealed class DefaultSecretsApp : TestApp
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("ENVIRONMENT", "production");
            builder.UseSetting("SECRET_KEY", "changethis");
        }
    }
}
