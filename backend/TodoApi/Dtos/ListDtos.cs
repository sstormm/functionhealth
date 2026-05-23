namespace TodoApi.Dtos;

public record ListResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public DateTime UpdatedAt { get; init; }
    public int OpenCount { get; init; }
    public int DoneCount { get; init; }
}

public record CreateListRequest
{
    public string? Name { get; init; }
}

public record RenameListRequest
{
    public string? Name { get; init; }
}
