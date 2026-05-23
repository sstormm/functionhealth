namespace TodoApi.Models;

public record User
{
    public Guid Id { get; init; }
    public string Email { get; init; } = "";
    public string PasswordHash { get; init; } = "";
    public DateTime CreatedAt { get; init; }
}
