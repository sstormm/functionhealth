using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoApi.Auth;
using TodoApi.Db;
using TodoApi.Dtos;

namespace TodoApi.Controllers;

[ApiController]
[Authorize]
public class ItemsController : ControllerBase
{
    private const int TextMaxLength = 500;

    private readonly ISqliteConnectionFactory _factory;

    public ItemsController(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    private record ItemRow
    {
        public string Id { get; init; } = "";
        public string ListId { get; init; } = "";
        public string Text { get; init; } = "";
        public long Completed { get; init; }
        public long Order { get; init; }
        public DateTime? CompletedAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }

    [HttpGet("/api/lists/{listId:guid}/items")]
    public async Task<IActionResult> GetItems(Guid listId)
    {
        var userId = User.GetUserId().ToString();
        var listIdStr = listId.ToString();

        using var conn = _factory.OpenConnection();
        var owns = await conn.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM Lists WHERE Id = @listId AND UserId = @userId",
            new { listId = listIdStr, userId });
        if (owns == 0) return NotFound(ApiErrorResponse.Of("NOT_FOUND", "List not found."));

        var open = await conn.QueryAsync<ItemRow>(@"
            SELECT Id, ListId, Text, Completed, ""Order"", CompletedAt, CreatedAt, UpdatedAt
            FROM Items
            WHERE ListId = @listId AND Completed = 0
            ORDER BY ""Order"" ASC, CreatedAt ASC",
            new { listId = listIdStr });

        var completed = await conn.QueryAsync<ItemRow>(@"
            SELECT Id, ListId, Text, Completed, ""Order"", CompletedAt, CreatedAt, UpdatedAt
            FROM Items
            WHERE ListId = @listId AND Completed = 1
            ORDER BY CompletedAt DESC",
            new { listId = listIdStr });

        return Ok(new ItemsResponse
        {
            Open = open.Select(Map).ToList(),
            Completed = completed.Select(Map).ToList(),
        });
    }

    [HttpPost("/api/lists/{listId:guid}/items")]
    public async Task<IActionResult> CreateItem(Guid listId, [FromBody] CreateItemRequest? body)
    {
        var text = body?.Text?.Trim() ?? "";
        if (text.Length == 0)
            return BadRequest(ApiErrorResponse.Of("BLANK_TEXT", "Item text can't be blank."));
        if (text.Length > TextMaxLength)
            return BadRequest(ApiErrorResponse.Of("TEXT_TOO_LONG", $"Item text must be {TextMaxLength} characters or fewer."));

        var userId = User.GetUserId().ToString();
        var listIdStr = listId.ToString();
        using var conn = _factory.OpenConnection();

        var owns = await conn.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM Lists WHERE Id = @listId AND UserId = @userId",
            new { listId = listIdStr, userId });
        if (owns == 0) return NotFound(ApiErrorResponse.Of("NOT_FOUND", "List not found."));

        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        using var tx = conn.BeginTransaction();
        await conn.ExecuteAsync(@"
            INSERT INTO Items (Id, ListId, Text, Completed, ""Order"", CompletedAt, CreatedAt, UpdatedAt)
            VALUES (@id, @listId, @text, 0,
                    (SELECT IFNULL(MAX(""Order""), 0) + 1 FROM Items WHERE ListId = @listId AND Completed = 0),
                    NULL, @now, @now)",
            new { id = id.ToString(), listId = listIdStr, text, now }, transaction: tx);

        await conn.ExecuteAsync(
            "UPDATE Lists SET UpdatedAt = @now WHERE Id = @listId",
            new { now, listId = listIdStr }, transaction: tx);

        var row = await conn.QuerySingleAsync<ItemRow>(@"
            SELECT Id, ListId, Text, Completed, ""Order"", CompletedAt, CreatedAt, UpdatedAt
            FROM Items WHERE Id = @id",
            new { id = id.ToString() }, transaction: tx);
        tx.Commit();

        return Created($"/api/items/{id}", Map(row));
    }

    [HttpPatch("/api/items/{id:guid}")]
    public async Task<IActionResult> UpdateItem(Guid id, [FromBody] UpdateItemRequest? body)
    {
        if (body == null)
            return BadRequest(ApiErrorResponse.Of("MALFORMED_BODY", "Request body is required."));

        string? newText = null;
        if (body.Text != null)
        {
            var t = body.Text.Trim();
            if (t.Length == 0)
                return BadRequest(ApiErrorResponse.Of("BLANK_TEXT", "Item text can't be blank."));
            if (t.Length > TextMaxLength)
                return BadRequest(ApiErrorResponse.Of("TEXT_TOO_LONG", $"Item text must be {TextMaxLength} characters or fewer."));
            newText = t;
        }

        var userId = User.GetUserId().ToString();
        var idStr = id.ToString();
        using var conn = _factory.OpenConnection();

        var item = await conn.QuerySingleOrDefaultAsync<ItemRow>(@"
            SELECT Id, ListId, Text, Completed, ""Order"", CompletedAt, CreatedAt, UpdatedAt
            FROM Items
            WHERE Id = @id AND ListId IN (SELECT Id FROM Lists WHERE UserId = @userId)",
            new { id = idStr, userId });
        if (item is null)
            return NotFound(ApiErrorResponse.Of("NOT_FOUND", "Item not found."));

        var now = DateTime.UtcNow;
        var finalText = newText ?? item.Text;
        var finalCompleted = item.Completed;
        DateTime? finalCompletedAt = item.CompletedAt;
        var finalOrder = item.Order;

        using var tx = conn.BeginTransaction();
        if (body.Completed.HasValue)
        {
            var wantsCompleted = body.Completed.Value;
            var wasCompleted = item.Completed != 0;
            if (wantsCompleted && !wasCompleted)
            {
                finalCompleted = 1;
                finalCompletedAt = now;
            }
            else if (!wantsCompleted && wasCompleted)
            {
                finalCompleted = 0;
                finalCompletedAt = null;
                var maxOpen = await conn.ExecuteScalarAsync<long?>(@"
                    SELECT IFNULL(MAX(""Order""), 0) FROM Items
                    WHERE ListId = @listId AND Completed = 0",
                    new { listId = item.ListId }, transaction: tx);
                finalOrder = (maxOpen ?? 0) + 1;
            }
        }

        await conn.ExecuteAsync(@"
            UPDATE Items
            SET Text = @text,
                Completed = @completed,
                CompletedAt = @completedAt,
                ""Order"" = @order,
                UpdatedAt = @now
            WHERE Id = @id",
            new
            {
                text = finalText,
                completed = finalCompleted,
                completedAt = finalCompletedAt,
                order = finalOrder,
                now,
                id = idStr,
            }, transaction: tx);

        await conn.ExecuteAsync(
            "UPDATE Lists SET UpdatedAt = @now WHERE Id = @listId",
            new { now, listId = item.ListId }, transaction: tx);

        var updated = await conn.QuerySingleAsync<ItemRow>(@"
            SELECT Id, ListId, Text, Completed, ""Order"", CompletedAt, CreatedAt, UpdatedAt
            FROM Items WHERE Id = @id",
            new { id = idStr }, transaction: tx);
        tx.Commit();
        return Ok(Map(updated));
    }

    [HttpDelete("/api/items/{id:guid}")]
    public async Task<IActionResult> DeleteItem(Guid id)
    {
        var userId = User.GetUserId().ToString();
        var idStr = id.ToString();
        using var conn = _factory.OpenConnection();

        var listId = await conn.QuerySingleOrDefaultAsync<string?>(@"
            SELECT ListId FROM Items
            WHERE Id = @id AND ListId IN (SELECT Id FROM Lists WHERE UserId = @userId)",
            new { id = idStr, userId });
        if (listId is null)
            return NotFound(ApiErrorResponse.Of("NOT_FOUND", "Item not found."));

        using var tx = conn.BeginTransaction();
        await conn.ExecuteAsync("DELETE FROM Items WHERE Id = @id", new { id = idStr }, transaction: tx);
        await conn.ExecuteAsync(
            "UPDATE Lists SET UpdatedAt = @now WHERE Id = @listId",
            new { now = DateTime.UtcNow, listId }, transaction: tx);
        tx.Commit();

        return NoContent();
    }

    private static ItemResponse Map(ItemRow row) => new()
    {
        Id = Guid.Parse(row.Id),
        ListId = Guid.Parse(row.ListId),
        Text = row.Text,
        Completed = row.Completed != 0,
        Order = (int)row.Order,
        CompletedAt = row.CompletedAt,
        CreatedAt = row.CreatedAt,
        UpdatedAt = row.UpdatedAt,
    };
}
