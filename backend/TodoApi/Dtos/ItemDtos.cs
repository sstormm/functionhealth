namespace TodoApi.Dtos;

public record ItemResponse
{
    public Guid Id { get; init; }
    public Guid ListId { get; init; }
    public string Text { get; init; } = "";
    public bool Completed { get; init; }
    public int Order { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record ItemsResponse
{
    public List<ItemResponse> Open { get; init; } = new();
    public List<ItemResponse> Completed { get; init; } = new();
}

public record CreateItemRequest
{
    public string? Text { get; init; }
}

public record UpdateItemRequest
{
    public string? Text { get; init; }
    public bool? Completed { get; init; }
}
