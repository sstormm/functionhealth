using System.Security.Claims;
using System.Text.Encodings.Web;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using TodoApi.Db;

namespace TodoApi.Auth;

public class SessionAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Session";
    public static readonly TimeSpan SessionTtl = TimeSpan.FromHours(24);
    public static readonly TimeSpan RefreshThreshold = TimeSpan.FromHours(12);

    public const string TokenHashClaimType = "session_token_hash";

    private readonly ISqliteConnectionFactory _factory;

    public SessionAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISqliteConnectionFactory factory) : base(options, logger, encoder)
    {
        _factory = factory;
    }

    private record SessionRow
    {
        public string UserId { get; init; } = "";
        public DateTime ExpiresAt { get; init; }
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var values))
            return AuthenticateResult.NoResult();

        var header = values.ToString();
        if (string.IsNullOrEmpty(header) ||
            !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token = header["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
            return AuthenticateResult.NoResult();

        // The Sessions.Token column stores SHA-256(token), not the raw token.
        // Hash the inbound value and look up by hash; if the DB is ever
        // exposed, live tokens don't leak.
        var tokenHash = TokenHash.Sha256(token);

        using var conn = _factory.OpenConnection();
        var session = await conn.QuerySingleOrDefaultAsync<SessionRow>(
            "SELECT UserId, ExpiresAt FROM Sessions WHERE Token = @tokenHash",
            new { tokenHash });

        if (session is null)
            return AuthenticateResult.Fail("Invalid token");

        var now = DateTime.UtcNow;
        if (session.ExpiresAt <= now)
        {
            // Opportunistic cleanup: an expired token won't be honored again,
            // so drop the row now instead of leaving it to accumulate.
            await conn.ExecuteAsync(
                "DELETE FROM Sessions WHERE Token = @tokenHash",
                new { tokenHash });
            return AuthenticateResult.Fail("Token expired");
        }

        if (session.ExpiresAt - now < RefreshThreshold)
        {
            var newExpiresAt = now + SessionTtl;
            await conn.ExecuteAsync(
                "UPDATE Sessions SET ExpiresAt = @newExpiresAt WHERE Token = @tokenHash",
                new { newExpiresAt, tokenHash });
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, session.UserId),
            // Stash the hash (not the raw token) so logout can identify the row
            // without re-hashing. The raw token never crosses the trust boundary
            // beyond the Authorization header on this request.
            new Claim(TokenHashClaimType, tokenHash),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return AuthenticateResult.Success(ticket);
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";
        await Response.WriteAsJsonAsync(new
        {
            error = new { code = "UNAUTHENTICATED", message = "Authentication required." }
        });
    }
}
