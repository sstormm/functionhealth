using System.Security.Cryptography;
using System.Text;

namespace TodoApi.Auth;

/// <summary>
/// Hashes session tokens before they're stored in the Sessions table.
/// The raw token (256 bits of CSPRNG) leaves the server exactly once — in the
/// login response — and is never persisted. Subsequent requests carry the raw
/// token in the Authorization header; the handler re-hashes and looks up by
/// hash. If the database is exposed, no live tokens leak.
///
/// SHA-256 (not PBKDF2) is fine here: the input is already high-entropy
/// random bytes, not a low-entropy password, so per-attempt cost doesn't add
/// meaningful brute-force resistance.
/// </summary>
public static class TokenHash
{
    public static string Sha256(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
