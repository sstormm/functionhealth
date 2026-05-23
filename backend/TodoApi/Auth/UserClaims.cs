using System.Security.Claims;

namespace TodoApi.Auth;

public static class UserClaims
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? throw new InvalidOperationException("No user id claim on principal.");
        return Guid.Parse(raw);
    }

    /// <summary>
    /// SHA-256 hash of the session token, identifying the Sessions row that
    /// authenticated this request. The raw token is never stored in claims.
    /// </summary>
    public static string? GetSessionTokenHash(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(SessionAuthHandler.TokenHashClaimType);
}
