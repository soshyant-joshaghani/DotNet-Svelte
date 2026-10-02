using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotnetSvelte.Modules.Apps.Sample;

namespace DotnetSvelte.Tests;

public class NoteTests : IClassFixture<TestApp>
{
    private const string Notes = "/api/v1/sample/notes";
    private readonly TestApp _app;

    public NoteTests(TestApp app) => _app = app;

    private static Guid IdOf(JsonElement note) => Guid.Parse(note.GetProperty("id").GetString()!);

    [Fact]
    public async Task SampleRoot_NeedsNoAuth()
    {
        var response = await _app.CreateClient().GetAsync("/api/v1/sample");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Sample module — see /sample/notes for the canonical CRUD example",
            (await response.JsonAsync()).GetProperty("message").GetString());
    }

    [Fact]
    public async Task Notes_WithoutToken_Return401()
    {
        var client = _app.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Notes)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Notes, new { title = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{Notes}/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Create_TrimsFields_AndReturns201WithContract()
    {
        var (user, client) = await _app.NewUserClientAsync();

        var response = await client.PostAsJsonAsync(Notes, new { title = "  Hello  ", content = "  body \n" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var note = await response.JsonAsync();
        Assert.Equal("Hello", note.GetProperty("title").GetString());
        Assert.Equal("body", note.GetProperty("content").GetString());
        Assert.Equal(user.Id.ToString(), note.GetProperty("owner_id").GetString());
        Assert.EndsWith("Z", note.GetProperty("created_at").GetString());
        Assert.EndsWith("Z", note.GetProperty("updated_at").GetString());
    }

    [Fact]
    public async Task Create_ContentDefaultsToEmpty()
    {
        var (_, client) = await _app.NewUserClientAsync();

        var note = await (await client.PostAsJsonAsync(Notes, new { title = "t" })).JsonAsync();

        Assert.Equal("", note.GetProperty("content").GetString());
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    public async Task Create_WithBlankTitle_Returns422(string title)
    {
        var (_, client) = await _app.NewUserClientAsync();

        var response = await client.PostAsJsonAsync(Notes, new { title });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("Title cannot be empty", await response.DetailAsync());
    }

    [Fact]
    public async Task Create_WithOversizedFields_Returns422()
    {
        var (_, client) = await _app.NewUserClientAsync();

        var longTitle = await client.PostAsJsonAsync(Notes, new { title = new string('a', 256) });
        var longContent = await client.PostAsJsonAsync(Notes, new { title = "ok", content = new string('a', 10001) });
        var noTitle = await client.PostAsJsonAsync(Notes, new { content = "x" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, longTitle.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, longContent.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noTitle.StatusCode);
        Assert.IsType<string>(await noTitle.DetailAsync());
    }

    [Fact]
    public async Task Crud_RoundTrip()
    {
        var (_, client) = await _app.NewUserClientAsync();
        var created = await (await client.PostAsJsonAsync(Notes, new { title = "first", content = "a" })).JsonAsync();
        var id = IdOf(created);

        var read = await client.GetAsync($"{Notes}/{id}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal("first", (await read.JsonAsync()).GetProperty("title").GetString());

        var patched = await client.PatchAsJsonAsync($"{Notes}/{id}", new { title = "  second " });
        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);
        var body = await patched.JsonAsync();
        Assert.Equal("second", body.GetProperty("title").GetString());
        Assert.Equal("a", body.GetProperty("content").GetString());

        var list = await (await client.GetAsync(Notes)).JsonAsync();
        Assert.Equal(["second"], list.EnumerateArray().Select(n => n.GetProperty("title").GetString()));

        var deleted = await client.DeleteAsync($"{Notes}/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal("", await deleted.Content.ReadAsStringAsync());

        var gone = await client.GetAsync($"{Notes}/{id}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.Equal("Note not found", await gone.DetailAsync());
        Assert.Empty((await (await client.GetAsync(Notes)).JsonAsync()).EnumerateArray());
    }

    [Fact]
    public async Task Update_WithBlankTitle_Returns422()
    {
        var (_, client) = await _app.NewUserClientAsync();
        var id = IdOf(await (await client.PostAsJsonAsync(Notes, new { title = "t" })).JsonAsync());

        var response = await client.PatchAsJsonAsync($"{Notes}/{id}", new { title = "  " });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("Title cannot be empty", await response.DetailAsync());
    }

    [Fact]
    public async Task OtherOwnersNote_Returns403_AndUnknownReturns404()
    {
        var (_, owner) = await _app.NewUserClientAsync();
        var (_, intruder) = await _app.NewUserClientAsync();
        var id = IdOf(await (await owner.PostAsJsonAsync(Notes, new { title = "private" })).JsonAsync());

        foreach (var response in new[]
        {
            await intruder.GetAsync($"{Notes}/{id}"),
            await intruder.PatchAsJsonAsync($"{Notes}/{id}", new { title = "hijack" }),
            await intruder.DeleteAsync($"{Notes}/{id}"),
        })
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("Not allowed to access this note", await response.DetailAsync());
        }

        Assert.Equal("private", _app.Notes.Items.Single(n => n.Id == id).Title);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"{Notes}/{Guid.NewGuid()}")).StatusCode);
        Assert.Empty((await (await intruder.GetAsync(Notes)).JsonAsync()).EnumerateArray());
    }

    [Fact]
    public async Task List_IsCachedThenInvalidatedByWrites()
    {
        var (user, client) = await _app.NewUserClientAsync();
        var listKey = $"sample:notes:v1:list:{user.Id}";

        await client.PostAsJsonAsync(Notes, new { title = "one" });
        Assert.DoesNotContain(listKey, _app.Cache.Keys);

        await client.GetAsync(Notes);
        Assert.Contains(listKey, _app.Cache.Keys);
        Assert.Equal(TimeSpan.FromSeconds(120), _app.Cache.TtlOf(listKey));

        _app.Notes.Items.Add(new Note { Title = "sneaked", OwnerId = user.Id });
        var stale = await (await client.GetAsync(Notes)).JsonAsync();
        Assert.Single(stale.EnumerateArray());

        await client.PostAsJsonAsync(Notes, new { title = "two" });
        Assert.DoesNotContain(listKey, _app.Cache.Keys);

        var fresh = await (await client.GetAsync(Notes)).JsonAsync();
        Assert.Equal(3, fresh.GetArrayLength());
    }

    [Fact]
    public async Task Note_IsCachedOnCreateAndRead_AndDroppedOnDelete()
    {
        var (user, client) = await _app.NewUserClientAsync();
        var created = await (await client.PostAsJsonAsync(Notes, new { title = "cached" })).JsonAsync();
        var id = IdOf(created);
        var noteKey = $"sample:notes:v1:note:{user.Id}:{id}";

        Assert.Contains(noteKey, _app.Cache.Keys);
        Assert.Equal(TimeSpan.FromSeconds(300), _app.Cache.TtlOf(noteKey));

        _app.Notes.Items.Single(n => n.Id == id).Title = "changed behind the cache";
        Assert.Equal("cached", (await (await client.GetAsync($"{Notes}/{id}")).JsonAsync()).GetProperty("title").GetString());

        var patched = await (await client.PatchAsJsonAsync($"{Notes}/{id}", new { content = "x" })).JsonAsync();
        Assert.Equal("changed behind the cache", patched.GetProperty("title").GetString());
        Assert.Contains(noteKey, _app.Cache.Keys);

        await client.DeleteAsync($"{Notes}/{id}");
        Assert.DoesNotContain(noteKey, _app.Cache.Keys);
    }

    [Fact]
    public async Task CacheIsScopedPerOwner()
    {
        var (a, clientA) = await _app.NewUserClientAsync();
        var (b, clientB) = await _app.NewUserClientAsync();

        await clientA.PostAsJsonAsync(Notes, new { title = "a" });
        await clientA.GetAsync(Notes);
        await clientB.GetAsync(Notes);

        Assert.Contains($"sample:notes:v1:list:{a.Id}", _app.Cache.Keys);
        Assert.Contains($"sample:notes:v1:list:{b.Id}", _app.Cache.Keys);
        Assert.Empty((await (await clientB.GetAsync(Notes)).JsonAsync()).EnumerateArray());
    }
}
