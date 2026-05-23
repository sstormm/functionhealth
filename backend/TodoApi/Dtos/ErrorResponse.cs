namespace TodoApi.Dtos;

public record ApiErrorBody
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
}

public record ApiErrorResponse
{
    public ApiErrorBody Error { get; init; } = new();

    public static ApiErrorResponse Of(string code, string message) =>
        new() { Error = new ApiErrorBody { Code = code, Message = message } };
}
