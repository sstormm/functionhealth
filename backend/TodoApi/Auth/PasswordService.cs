using Microsoft.AspNetCore.Identity;
using TodoApi.Models;

namespace TodoApi.Auth;

public class PasswordService
{
    private readonly PasswordHasher<User> _hasher = new();

    /// <summary>
    /// Pre-computed hash for constant-time login on unknown emails.
    /// Always returns Verify==false; running it keeps PBKDF2 timing the same
    /// whether or not the user exists, so attackers can't probe email
    /// existence by timing the login response.
    /// </summary>
    public string DummyHash { get; }

    public PasswordService()
    {
        DummyHash = _hasher.HashPassword(new User(), "constant-time-dummy");
    }

    public string Hash(string password) => _hasher.HashPassword(new User(), password);

    public bool Verify(string hash, string password)
    {
        var result = _hasher.VerifyHashedPassword(new User(), hash, password);
        return result == PasswordVerificationResult.Success
            || result == PasswordVerificationResult.SuccessRehashNeeded;
    }
}
