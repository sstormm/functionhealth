using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoApi.Auth;
using TodoApi.Db;
using TodoApi.Dtos;

namespace TodoApi.Controllers;

[ApiController]
[Route("api/lists")]
[Authorize]
public class ListsController : ControllerBase
{
    private const int NameMaxLength = 100;

    private readonly ISqliteConnectionFactory _factory;

    public ListsController(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    private record ListRow
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public DateTime UpdatedAt { get; init; }
        public long OpenCount { get; init; }
        public long DoneCount { get; init; }
    }

    [HttpGet]
    public async Task<IActionResult> GetLists()
    {
        var userId = User.GetUserId().ToString();
        using var conn = _factory.OpenConnection();
        var rows = await conn.QueryAsync<ListRow>(@"
            SELECT l.Id, l.Name, l.UpdatedAt,
                   (SELECT COUNT(*) FROM Items i WHERE i.ListId = l.Id AND i.Completed = 0) AS OpenCount,
                   (SELECT COUNT(*) FROM Items i WHERE i.ListId = l.Id AND i.Completed = 1) AS DoneCount
            FROM Lists l
            WHERE l.UserId = @userId
            ORDER BY l.Name COLLATE NOCASE ASC",
            new { userId });

        return Ok(rows.Select(Map));
    }

    [HttpPost]
    public async Task<IActionResult> CreateList([FromBody] CreateListRequest? body)
    {
        var name = body?.Name?.Trim() ?? "";
        if (name.Length == 0)
            return BadRequest(ApiErrorResponse.Of("BLANK_NAME", "List name can't be blank."));
        if (name.Length > NameMaxLength)
            return BadRequest(ApiErrorResponse.Of("NAME_TOO_LONG", $"List name must be {NameMaxLength} characters or fewer."));

        var userId = User.GetUserId().ToString();
        using var conn = _factory.OpenConnection();

        var exists = await conn.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM Lists WHERE UserId = @userId AND Name = @name COLLATE NOCASE",
            new { userId, name });
        if (exists > 0)
            return Conflict(ApiErrorResponse.Of("DUPLICATE_NAME", $"A list named \"{name}\" already exists."));

        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await conn.ExecuteAsync(@"
            INSERT INTO Lists (Id, UserId, Name, CreatedAt, UpdatedAt)
            VALUES (@id, @userId, @name, @now, @now)",
            new { id = id.ToString(), userId, name, now });

        var resp = new ListResponse
        {
            Id = id,
            Name = name,
            UpdatedAt = now,
            OpenCount = 0,
            DoneCount = 0,
        };
        return CreatedAtAction(nameof(GetLists), new { id }, resp);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> RenameList(Guid id, [FromBody] RenameListRequest? body)
    {
        var name = body?.Name?.Trim() ?? "";
        if (name.Length == 0)
            return BadRequest(ApiErrorResponse.Of("BLANK_NAME", "List name can't be blank."));
        if (name.Length > NameMaxLength)
            return BadRequest(ApiErrorResponse.Of("NAME_TOO_LONG", $"List name must be {NameMaxLength} characters or fewer."));

        var userId = User.GetUserId().ToString();
        var idStr = id.ToString();
        using var conn = _factory.OpenConnection();

        var existing = await conn.QuerySingleOrDefaultAsync<ListRow>(@"
            SELECT l.Id, l.Name, l.UpdatedAt,
                   (SELECT COUNT(*) FROM Items i WHERE i.ListId = l.Id AND i.Completed = 0) AS OpenCount,
                   (SELECT COUNT(*) FROM Items i WHERE i.ListId = l.Id AND i.Completed = 1) AS DoneCount
            FROM Lists l
            WHERE l.Id = @id AND l.UserId = @userId",
            new { id = idStr, userId });
        if (existing is null) return NotFound(ApiErrorResponse.Of("NOT_FOUND", "List not found."));

        var conflict = await conn.ExecuteScalarAsync<long>(@"
            SELECT COUNT(*) FROM Lists
            WHERE UserId = @userId AND Name = @name COLLATE NOCASE AND Id <> @id",
            new { userId, name, id = idStr });
        if (conflict > 0)
            return Conflict(ApiErrorResponse.Of("DUPLICATE_NAME", $"A list named \"{name}\" already exists."));

        var now = DateTime.UtcNow;
        await conn.ExecuteAsync(
            "UPDATE Lists SET Name = @name, UpdatedAt = @now WHERE Id = @id AND UserId = @userId",
            new { name, now, id = idStr, userId });

        return Ok(Map(existing with { Name = name, UpdatedAt = now }));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteList(Guid id)
    {
        var userId = User.GetUserId().ToString();
        using var conn = _factory.OpenConnection();
        var affected = await conn.ExecuteAsync(
            "DELETE FROM Lists WHERE Id = @id AND UserId = @userId",
            new { id = id.ToString(), userId });
        if (affected == 0)
            return NotFound(ApiErrorResponse.Of("NOT_FOUND", "List not found."));
        return NoContent();
    }

    private static ListResponse Map(ListRow row) => new()
    {
        Id = Guid.Parse(row.Id),
        Name = row.Name,
        UpdatedAt = row.UpdatedAt,
        OpenCount = (int)row.OpenCount,
        DoneCount = (int)row.DoneCount,
    };
}
