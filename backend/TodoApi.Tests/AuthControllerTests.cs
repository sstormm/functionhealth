using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TodoApi.Dtos;

namespace TodoApi.Tests;

public class AuthControllerTests : IDisposable
{
    private readonly TodoApiFactory _factory = new();
    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        using var client = _factory.Client();
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "alice@example.com", password = "alice123" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal("alice@example.com", body.User.Email);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401WithInvalidCredentials()
    {
        using var client = _factory.Client();
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "alice@example.com", password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("INVALID_CREDENTIALS", err!.Error.Code);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401WithInvalidCredentials()
    {
        using var client = _factory.Client();
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "nobody@example.com", password = "alice123" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("INVALID_CREDENTIALS", err!.Error.Code);
    }

    [Fact]
    public async Task Login_MalformedBody_Returns400()
    {
        using var client = _factory.Client();
        var resp = await client.PostAsJsonAsync("/api/auth/login", new { });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Login_InvalidEmailFormat_Returns401WithInvalidCredentials()
    {
        // Bad-format identifier must NOT leak via a distinct 400 — same 401 + code
        // as wrong password or unknown email, so callers can't infer which field failed.
        using var client = _factory.Client();
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "alice-without-at-sign", password = "alice123" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("INVALID_CREDENTIALS", err!.Error.Code);
    }

    [Fact]
    public async Task Logout_AuthenticatedUser_Returns204AndInvalidatesToken()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var logoutResp = await client.PostAsync("/api/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logoutResp.StatusCode);

        var meResp = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meResp.StatusCode);
    }

    [Fact]
    public async Task Logout_NoToken_Returns401()
    {
        using var client = _factory.Client();
        var resp = await client.PostAsync("/api/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Me_AuthenticatedUser_ReturnsUserInfo()
    {
        var token = await _factory.LoginAsAlice();
        using var client = _factory.Client(token);

        var resp = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var user = await resp.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal("alice@example.com", user!.Email);
    }

    [Fact]
    public async Task Me_NoToken_Returns401()
    {
        using var client = _factory.Client();
        var resp = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Me_JunkToken_Returns401()
    {
        using var client = _factory.Client();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");
        var resp = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Me_ExpiredToken_Returns401()
    {
        var token = await _factory.LoginAsAlice();
        _factory.SetSessionExpiry(token, DateTime.UtcNow.AddHours(-1));

        using var client = _factory.Client(token);
        var resp = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Request_WhenSessionWithinRefreshThreshold_ExtendsExpiresAt()
    {
        var token = await _factory.LoginAsAlice();
        var oneHourOut = DateTime.UtcNow.AddHours(1);
        _factory.SetSessionExpiry(token, oneHourOut);

        using var client = _factory.Client(token);
        var resp = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var newExpiry = _factory.GetSessionExpiry(token);
        var expectedNewExpiry = DateTime.UtcNow.AddHours(24);
        Assert.True((expectedNewExpiry - newExpiry).Duration() < TimeSpan.FromSeconds(10),
            $"Expected sliding refresh to push ExpiresAt to ~{expectedNewExpiry:O}, got {newExpiry:O}");
    }

    [Fact]
    public async Task Request_WhenSessionAboveRefreshThreshold_DoesNotExtendExpiresAt()
    {
        var token = await _factory.LoginAsAlice();
        var twentyHoursOut = DateTime.UtcNow.AddHours(20);
        _factory.SetSessionExpiry(token, twentyHoursOut);

        using var client = _factory.Client(token);
        var resp = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var afterExpiry = _factory.GetSessionExpiry(token);
        Assert.True((twentyHoursOut - afterExpiry).Duration() < TimeSpan.FromSeconds(1),
            $"Expected ExpiresAt unchanged at ~{twentyHoursOut:O}, got {afterExpiry:O}");
    }
}
