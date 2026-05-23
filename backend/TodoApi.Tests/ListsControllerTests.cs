using System.Net;
using System.Net.Http.Json;
using TodoApi.Dtos;

namespace TodoApi.Tests;

public class ListsControllerTests : IDisposable
{
    private readonly TodoApiFactory _factory = new();
    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetLists_NoToken_Returns401()
    {
        using var client = _factory.Client();
        var resp = await client.GetAsync("/api/lists");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task GetLists_AuthenticatedUser_ReturnsOnlyOwnLists()
    {
        var alice = await _factory.LoginAsAlice();
        var bob = await _factory.LoginAsBob();

        using var aliceClient = _factory.Client(alice);
        using var bobClient = _factory.Client(bob);

        await aliceClient.PostAsJsonAsync("/api/lists", new { name = "AliceA" });
        await aliceClient.PostAsJsonAsync("/api/lists", new { name = "AliceB" });
        await bobClient.PostAsJsonAsync("/api/lists", new { name = "BobOnly" });

        var aliceLists = await aliceClient.GetFromJsonAsync<List<ListResponse>>("/api/lists");
        Assert.NotNull(aliceLists);
        Assert.DoesNotContain(aliceLists!, l => l.Name == "BobOnly");
        Assert.Contains(aliceLists, l => l.Name == "AliceA");
        Assert.Contains(aliceLists, l => l.Name == "AliceB");
    }

    [Fact]
    public async Task GetLists_SortedAlphabetically()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        await client.PostAsJsonAsync("/api/lists", new { name = "Zebra" });
        await client.PostAsJsonAsync("/api/lists", new { name = "Apple" });
        await client.PostAsJsonAsync("/api/lists", new { name = "Mango" });

        var lists = await client.GetFromJsonAsync<List<ListResponse>>("/api/lists");
        Assert.NotNull(lists);
        var names = lists!.Select(l => l.Name).ToList();
        var sorted = names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(sorted, names);
    }

    [Fact]
    public async Task CreateList_ValidName_Returns201()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var resp = await client.PostAsJsonAsync("/api/lists", new { name = "Reading" });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<ListResponse>();
        Assert.Equal("Reading", body!.Name);
        Assert.Equal(0, body.OpenCount);
        Assert.Equal(0, body.DoneCount);
    }

    [Fact]
    public async Task CreateList_BlankName_Returns400WithBlankName()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var resp = await client.PostAsJsonAsync("/api/lists", new { name = "" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("BLANK_NAME", err!.Error.Code);
    }

    [Fact]
    public async Task CreateList_WhitespaceOnlyName_Returns400WithBlankName()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var resp = await client.PostAsJsonAsync("/api/lists", new { name = "    " });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("BLANK_NAME", err!.Error.Code);
    }

    [Fact]
    public async Task CreateList_DuplicateNameSameUser_Returns409WithDuplicateName()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        await client.PostAsJsonAsync("/api/lists", new { name = "Reading" });
        // Same name, different casing + surrounding whitespace
        var resp = await client.PostAsJsonAsync("/api/lists", new { name = "  reading  " });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("DUPLICATE_NAME", err!.Error.Code);
    }

    [Fact]
    public async Task CreateList_DuplicateNameDifferentUser_Returns201()
    {
        var alice = await _factory.LoginAsAlice();
        var bob = await _factory.LoginAsBob();

        using var aliceClient = _factory.Client(alice);
        using var bobClient = _factory.Client(bob);

        await aliceClient.PostAsJsonAsync("/api/lists", new { name = "Shared" });
        var resp = await bobClient.PostAsJsonAsync("/api/lists", new { name = "Shared" });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task CreateList_NoToken_Returns401()
    {
        using var client = _factory.Client();
        var resp = await client.PostAsJsonAsync("/api/lists", new { name = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task RenameList_ValidName_Returns200()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var created = await CreateListAsync(client, "Old");
        var resp = await client.PatchAsJsonAsync($"/api/lists/{created.Id}", new { name = "New" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ListResponse>();
        Assert.Equal("New", body!.Name);
    }

    [Fact]
    public async Task RenameList_BlankName_Returns400()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var created = await CreateListAsync(client, "Old");
        var resp = await client.PatchAsJsonAsync($"/api/lists/{created.Id}", new { name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("BLANK_NAME", err!.Error.Code);
    }

    [Fact]
    public async Task RenameList_DuplicateName_Returns409()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        await CreateListAsync(client, "Existing");
        var another = await CreateListAsync(client, "Another");

        var resp = await client.PatchAsJsonAsync($"/api/lists/{another.Id}", new { name = "Existing" });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("DUPLICATE_NAME", err!.Error.Code);
    }

    [Fact]
    public async Task RenameList_OtherUsersList_Returns404()
    {
        var alice = await _factory.LoginAsAlice();
        var bob = await _factory.LoginAsBob();

        using var aliceClient = _factory.Client(alice);
        using var bobClient = _factory.Client(bob);

        var bobList = await CreateListAsync(bobClient, "BobsList");
        var resp = await aliceClient.PatchAsJsonAsync($"/api/lists/{bobList.Id}", new { name = "Hacked" });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task RenameList_NonexistentId_Returns404()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var resp = await client.PatchAsJsonAsync(
            $"/api/lists/{Guid.NewGuid()}", new { name = "NoSuch" });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task RenameList_NoToken_Returns401()
    {
        using var client = _factory.Client();
        var resp = await client.PatchAsJsonAsync(
            $"/api/lists/{Guid.NewGuid()}", new { name = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteList_OwnList_Returns204AndRemovesItems()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var list = await CreateListAsync(client, "ToDelete");
        await client.PostAsJsonAsync($"/api/lists/{list.Id}/items", new { text = "child item 1" });
        await client.PostAsJsonAsync($"/api/lists/{list.Id}/items", new { text = "child item 2" });

        Assert.Equal(2, _factory.ItemCountForList(list.Id));

        var resp = await client.DeleteAsync($"/api/lists/{list.Id}");
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.Equal(0, _factory.ItemCountForList(list.Id));
    }

    [Fact]
    public async Task DeleteList_OtherUsersList_Returns404()
    {
        var alice = await _factory.LoginAsAlice();
        var bob = await _factory.LoginAsBob();

        using var aliceClient = _factory.Client(alice);
        using var bobClient = _factory.Client(bob);

        var bobList = await CreateListAsync(bobClient, "BobsList");
        var resp = await aliceClient.DeleteAsync($"/api/lists/{bobList.Id}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteList_NonexistentId_Returns404()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var resp = await client.DeleteAsync($"/api/lists/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteList_NoToken_Returns401()
    {
        using var client = _factory.Client();
        var resp = await client.DeleteAsync($"/api/lists/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    private static async Task<ListResponse> CreateListAsync(HttpClient client, string name)
    {
        var resp = await client.PostAsJsonAsync("/api/lists", new { name });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ListResponse>())!;
    }
}
