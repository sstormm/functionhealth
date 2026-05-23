using System.Net;
using System.Net.Http.Json;
using TodoApi.Dtos;

namespace TodoApi.Tests;

public class ItemsControllerTests : IDisposable
{
    private readonly TodoApiFactory _factory = new();
    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetItems_OwnList_Returns200WithOpenAndCompletedSplit()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var list = await CreateListAsync(client, "Tasks");
        var open = await CreateItemAsync(client, list.Id, "open task");
        var done = await CreateItemAsync(client, list.Id, "completed task");
        await CompleteItemAsync(client, done.Id);

        var resp = await client.GetAsync($"/api/lists/{list.Id}/items");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ItemsResponse>();

        Assert.Contains(body!.Open, i => i.Id == open.Id);
        Assert.Contains(body.Completed, i => i.Id == done.Id);
        Assert.DoesNotContain(body.Open, i => i.Id == done.Id);
        Assert.DoesNotContain(body.Completed, i => i.Id == open.Id);
    }

    [Fact]
    public async Task GetItems_OpenItemsSortedByOrderThenCreatedAt()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var list = await CreateListAsync(client, "Tasks");
        var first = await CreateItemAsync(client, list.Id, "first");
        var second = await CreateItemAsync(client, list.Id, "second");
        var third = await CreateItemAsync(client, list.Id, "third");

        var body = await client.GetFromJsonAsync<ItemsResponse>($"/api/lists/{list.Id}/items");
        var openIds = body!.Open.Select(i => i.Id).ToList();
        Assert.Equal(new[] { first.Id, second.Id, third.Id }, openIds);

        // verify also that Orders are strictly ascending
        var orders = body.Open.Select(i => i.Order).ToList();
        Assert.Equal(orders.OrderBy(o => o), orders);
    }

    [Fact]
    public async Task GetItems_CompletedItemsSortedByCompletedAtDesc()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var list = await CreateListAsync(client, "Tasks");
        var a = await CreateItemAsync(client, list.Id, "a");
        var b = await CreateItemAsync(client, list.Id, "b");
        var c = await CreateItemAsync(client, list.Id, "c");

        await CompleteItemAsync(client, a.Id);
        await Task.Delay(15);
        await CompleteItemAsync(client, b.Id);
        await Task.Delay(15);
        await CompleteItemAsync(client, c.Id);

        var body = await client.GetFromJsonAsync<ItemsResponse>($"/api/lists/{list.Id}/items");
        var ids = body!.Completed.Select(i => i.Id).ToList();
        Assert.Equal(new[] { c.Id, b.Id, a.Id }, ids);
    }

    [Fact]
    public async Task GetItems_OtherUsersList_Returns404()
    {
        var alice = await _factory.LoginAsAlice();
        var bob = await _factory.LoginAsBob();

        using var aliceClient = _factory.Client(alice);
        using var bobClient = _factory.Client(bob);

        var bobList = await CreateListAsync(bobClient, "BobsList");
        var resp = await aliceClient.GetAsync($"/api/lists/{bobList.Id}/items");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetItems_NoToken_Returns401()
    {
        using var client = _factory.Client();
        var resp = await client.GetAsync($"/api/lists/{Guid.NewGuid()}/items");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task CreateItem_ValidText_Returns201WithOrderAtEnd()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var list = await CreateListAsync(client, "Tasks");
        var first = await CreateItemAsync(client, list.Id, "first");
        var second = await CreateItemAsync(client, list.Id, "second");
        var third = await CreateItemAsync(client, list.Id, "third");

        Assert.True(second.Order > first.Order);
        Assert.True(third.Order > second.Order);
    }

    [Fact]
    public async Task CreateItem_BlankText_Returns400WithBlankText()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var list = await CreateListAsync(client, "Tasks");
        var resp = await client.PostAsJsonAsync($"/api/lists/{list.Id}/items", new { text = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("BLANK_TEXT", err!.Error.Code);
    }

    [Fact]
    public async Task CreateItem_TextOver500Chars_Returns400WithTextTooLong()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var list = await CreateListAsync(client, "Tasks");
        var resp = await client.PostAsJsonAsync(
            $"/api/lists/{list.Id}/items", new { text = new string('x', 501) });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("TEXT_TOO_LONG", err!.Error.Code);
    }

    [Fact]
    public async Task CreateItem_OtherUsersList_Returns404()
    {
        var alice = await _factory.LoginAsAlice();
        var bob = await _factory.LoginAsBob();

        using var aliceClient = _factory.Client(alice);
        using var bobClient = _factory.Client(bob);

        var bobList = await CreateListAsync(bobClient, "BobsList");
        var resp = await aliceClient.PostAsJsonAsync(
            $"/api/lists/{bobList.Id}/items", new { text = "sneaky" });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task CreateItem_NoToken_Returns401()
    {
        using var client = _factory.Client();
        var resp = await client.PostAsJsonAsync(
            $"/api/lists/{Guid.NewGuid()}/items", new { text = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task UpdateItem_EditText_Returns200()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var list = await CreateListAsync(client, "Tasks");
        var item = await CreateItemAsync(client, list.Id, "Buy milk");

        var resp = await client.PatchAsJsonAsync($"/api/items/{item.Id}",
            new { text = "Buy almond milk" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ItemResponse>();
        Assert.Equal("Buy almond milk", body!.Text);
    }

    [Fact]
    public async Task UpdateItem_BlankText_Returns400()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var list = await CreateListAsync(client, "Tasks");
        var item = await CreateItemAsync(client, list.Id, "x");

        var resp = await client.PatchAsJsonAsync($"/api/items/{item.Id}", new { text = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("BLANK_TEXT", err!.Error.Code);
    }

    [Fact]
    public async Task UpdateItem_TextOver500Chars_Returns400()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var list = await CreateListAsync(client, "Tasks");
        var item = await CreateItemAsync(client, list.Id, "x");

        var resp = await client.PatchAsJsonAsync(
            $"/api/items/{item.Id}", new { text = new string('x', 501) });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("TEXT_TOO_LONG", err!.Error.Code);
    }

    [Fact]
    public async Task UpdateItem_CompleteItem_SetsCompletedAtAndKeepsOrder()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var list = await CreateListAsync(client, "Tasks");
        var item = await CreateItemAsync(client, list.Id, "task");
        var originalOrder = item.Order;

        var resp = await client.PatchAsJsonAsync($"/api/items/{item.Id}", new { completed = true });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ItemResponse>();

        Assert.True(body!.Completed);
        Assert.NotNull(body.CompletedAt);
        Assert.Equal(originalOrder, body.Order);
    }

    [Fact]
    public async Task UpdateItem_UncompleteItem_ClearsCompletedAtAndMovesToBottom()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var list = await CreateListAsync(client, "Tasks");
        var first = await CreateItemAsync(client, list.Id, "first");  // Order=1
        var second = await CreateItemAsync(client, list.Id, "second"); // Order=2
        var third = await CreateItemAsync(client, list.Id, "third");   // Order=3

        // Complete the first, then uncomplete it; it should move to Order = max+1 (i.e., 4)
        await CompleteItemAsync(client, first.Id);

        var resp = await client.PatchAsJsonAsync($"/api/items/{first.Id}", new { completed = false });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ItemResponse>();

        Assert.False(body!.Completed);
        Assert.Null(body.CompletedAt);
        Assert.True(body.Order > third.Order,
            $"Expected uncompleted item to be ordered after others (>{third.Order}), got {body.Order}");
    }

    [Fact]
    public async Task UpdateItem_OtherUsersItem_Returns404()
    {
        var alice = await _factory.LoginAsAlice();
        var bob = await _factory.LoginAsBob();

        using var aliceClient = _factory.Client(alice);
        using var bobClient = _factory.Client(bob);

        var bobList = await CreateListAsync(bobClient, "BobsList");
        var bobItem = await CreateItemAsync(bobClient, bobList.Id, "Bob's task");

        var resp = await aliceClient.PatchAsJsonAsync(
            $"/api/items/{bobItem.Id}", new { text = "hacked" });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task UpdateItem_NonexistentId_Returns404()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var resp = await client.PatchAsJsonAsync(
            $"/api/items/{Guid.NewGuid()}", new { text = "x" });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task UpdateItem_NoToken_Returns401()
    {
        using var client = _factory.Client();
        var resp = await client.PatchAsJsonAsync(
            $"/api/items/{Guid.NewGuid()}", new { text = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteItem_OwnItem_Returns204()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var list = await CreateListAsync(client, "Tasks");
        var item = await CreateItemAsync(client, list.Id, "delete me");

        var resp = await client.DeleteAsync($"/api/items/{item.Id}");
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteItem_OtherUsersItem_Returns404()
    {
        var alice = await _factory.LoginAsAlice();
        var bob = await _factory.LoginAsBob();

        using var aliceClient = _factory.Client(alice);
        using var bobClient = _factory.Client(bob);

        var bobList = await CreateListAsync(bobClient, "BobsList");
        var bobItem = await CreateItemAsync(bobClient, bobList.Id, "Bob's task");

        var resp = await aliceClient.DeleteAsync($"/api/items/{bobItem.Id}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteItem_NoToken_Returns401()
    {
        using var client = _factory.Client();
        var resp = await client.DeleteAsync($"/api/items/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    private static async Task<ListResponse> CreateListAsync(HttpClient client, string name)
    {
        var resp = await client.PostAsJsonAsync("/api/lists", new { name });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ListResponse>())!;
    }

    private static async Task<ItemResponse> CreateItemAsync(HttpClient client, Guid listId, string text)
    {
        var resp = await client.PostAsJsonAsync($"/api/lists/{listId}/items", new { text });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ItemResponse>())!;
    }

    private static async Task CompleteItemAsync(HttpClient client, Guid itemId)
    {
        var resp = await client.PatchAsJsonAsync($"/api/items/{itemId}", new { completed = true });
        resp.EnsureSuccessStatusCode();
    }
}
