using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using TodoApi.Auth;
using TodoApi.Db;
using TodoApi.Dtos;

namespace TodoApi.Tests;

public class TodoApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public TodoApiFactory()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"todo-test-{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_dbPath}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var existing = services.SingleOrDefault(d => d.ServiceType == typeof(ISqliteConnectionFactory));
            if (existing is not null) services.Remove(existing);
            services.AddSingleton<ISqliteConnectionFactory>(new SqliteConnectionFactory(_connectionString));
        });
    }

    public Task<string> LoginAsAlice() => Login("alice@example.com", "alice123");
    public Task<string> LoginAsBob() => Login("bob@example.com", "bob123");

    public async Task<string> Login(string email, string password)
    {
        using var client = CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }

    public HttpClient Client(string? token = null)
    {
        var client = CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // Test helpers take the raw token (as the caller has it from login) and
    // hash internally — the Sessions.Token column stores SHA-256(token).

    public void SetSessionExpiry(string token, DateTime expiresAt)
    {
        using var conn = OpenRawConnection();
        conn.Execute(
            "UPDATE Sessions SET ExpiresAt = @expiresAt WHERE Token = @tokenHash",
            new { tokenHash = TokenHash.Sha256(token), expiresAt });
    }

    public DateTime GetSessionExpiry(string token)
    {
        using var conn = OpenRawConnection();
        return conn.QuerySingle<DateTime>(
            "SELECT ExpiresAt FROM Sessions WHERE Token = @tokenHash",
            new { tokenHash = TokenHash.Sha256(token) });
    }

    public bool SessionExists(string token)
    {
        using var conn = OpenRawConnection();
        var count = conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM Sessions WHERE Token = @tokenHash",
            new { tokenHash = TokenHash.Sha256(token) });
        return count > 0;
    }

    public long ItemCountForList(Guid listId)
    {
        using var conn = OpenRawConnection();
        return conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM Items WHERE ListId = @listId",
            new { listId = listId.ToString() });
    }

    private SqliteConnection OpenRawConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* best-effort cleanup */ }
        }
    }
}
