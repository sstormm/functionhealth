using System.Data;
using Dapper;
using TodoApi.Auth;

namespace TodoApi.Db;

public static class Seed
{
    public static void EnsureSeeded(IDbConnection conn, PasswordService passwords)
    {
        var userCount = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Users");
        if (userCount > 0) return;

        var now = DateTime.UtcNow;
        var users = new[]
        {
            (Id: Guid.NewGuid(), Email: "alice@example.com", Password: "alice123"),
            (Id: Guid.NewGuid(), Email: "bob@example.com",   Password: "bob123"),
        };

        foreach (var u in users)
        {
            conn.Execute(
                "INSERT INTO Users (Id, Email, PasswordHash, CreatedAt) VALUES (@Id, @Email, @Hash, @Now)",
                new { Id = u.Id.ToString(), u.Email, Hash = passwords.Hash(u.Password), Now = now });

            var listId = Guid.NewGuid();
            conn.Execute(
                "INSERT INTO Lists (Id, UserId, Name, CreatedAt, UpdatedAt) VALUES (@Id, @UserId, @Name, @Now, @Now)",
                new { Id = listId.ToString(), UserId = u.Id.ToString(), Name = "Today", Now = now });

            // One open item
            conn.Execute(@"
                INSERT INTO Items (Id, ListId, Text, Completed, ""Order"", CompletedAt, CreatedAt, UpdatedAt)
                VALUES (@Id, @ListId, @Text, 0, 1, NULL, @Now, @Now)",
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    ListId = listId.ToString(),
                    Text = "costco run",
                    Now = now
                });

            // One completed item — Order is irrelevant for completed (sorted by CompletedAt DESC)
            conn.Execute(@"
                INSERT INTO Items (Id, ListId, Text, Completed, ""Order"", CompletedAt, CreatedAt, UpdatedAt)
                VALUES (@Id, @ListId, @Text, 1, 0, @Now, @Now, @Now)",
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    ListId = listId.ToString(),
                    Text = "finish take home interview project",
                    Now = now
                });
        }
    }
}
