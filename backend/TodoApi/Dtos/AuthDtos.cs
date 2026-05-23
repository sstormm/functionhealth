namespace TodoApi.Dtos;

public record LoginRequest
{
    public string? Email { get; init; }
    public string? Password { get; init; }
}

public record UserResponse
{
    public Guid Id { get; init; }
    public string Email { get; init; } = "";
}

public record LoginResponse
{
    public string Token { get; init; } = "";
    public UserResponse User { get; init; } = new();
}
