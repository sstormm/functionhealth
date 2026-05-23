using System.Security.Cryptography;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoApi.Auth;
using TodoApi.Db;
using TodoApi.Dtos;

namespace TodoApi.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public class AuthController : ControllerBase
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly PasswordService _passwords;

    public AuthController(ISqliteConnectionFactory factory, PasswordService passwords)
    {
        _factory = factory;
        _passwords = passwords;
    }

    private record UserRow
    {
        public string Id { get; init; } = "";
        public string Email { get; init; } = "";
        public string PasswordHash { get; init; } = "";
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest? body)
    {
        var email = body?.Email?.Trim().ToLowerInvariant();
        var password = body?.Password;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return BadRequest(ApiErrorResponse.Of("MALFORMED_BODY", "Username and password are required."));

        using var conn = _factory.OpenConnection();
        var user = await conn.QuerySingleOrDefaultAsync<UserRow>(
            "SELECT Id, Email, PasswordHash FROM Users WHERE Email = @email COLLATE NOCASE",
            new { email });

        // Always run PBKDF2 verify — against the user's hash if they exist, or
        // a static dummy hash if not — so failure-path timing doesn't leak
        // whether the supplied identifier maps to a known user.
        var hashToVerify = user?.PasswordHash ?? _passwords.DummyHash;
        var matches = _passwords.Verify(hashToVerify, password);

        if (user is null || !matches)
        {
            return Unauthorized(ApiErrorResponse.Of(
                "INVALID_CREDENTIALS",
                "Username and password don't match."));
        }

        var token = NewToken();
        // Store SHA-256(token), not the raw token. The raw value goes to the
        // client in the response below; the DB never holds it.
        var tokenHash = TokenHash.Sha256(token);
        var now = DateTime.UtcNow;
        await conn.ExecuteAsync(@"
            INSERT INTO Sessions (Token, UserId, CreatedAt, ExpiresAt)
            VALUES (@tokenHash, @userId, @now, @expiresAt)",
            new
            {
                tokenHash,
                userId = user.Id,
                now,
                expiresAt = now + SessionAuthHandler.SessionTtl,
            });

        return Ok(new LoginResponse
        {
            Token = token,
            User = new UserResponse
            {
                Id = Guid.Parse(user.Id),
                Email = user.Email,
            },
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var tokenHash = User.GetSessionTokenHash();
        if (string.IsNullOrEmpty(tokenHash)) return NoContent();

        using var conn = _factory.OpenConnection();
        await conn.ExecuteAsync(
            "DELETE FROM Sessions WHERE Token = @tokenHash",
            new { tokenHash });
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.GetUserId();
        using var conn = _factory.OpenConnection();
        var user = await conn.QuerySingleOrDefaultAsync<UserRow>(
            "SELECT Id, Email, PasswordHash FROM Users WHERE Id = @id",
            new { id = userId.ToString() });

        if (user is null)
            return Unauthorized(ApiErrorResponse.Of("UNAUTHENTICATED", "Session invalid."));

        return Ok(new UserResponse
        {
            Id = Guid.Parse(user.Id),
            Email = user.Email,
        });
    }

    private static string NewToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
