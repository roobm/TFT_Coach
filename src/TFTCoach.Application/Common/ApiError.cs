namespace TFTCoach.Application.Common;

public sealed class ApiError
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public IReadOnlyDictionary<string, string[]>? Details { get; init; }
}

public static class ApiErrors
{
    public static ApiError Validation(
        string code,
        string message,
        IReadOnlyDictionary<string, string[]>? details = null) => new()
        {
            Code = code,
            Message = message,
            Details = details,
        };

    public static ApiError NotFound(string code, string message) => new()
    {
        Code = code,
        Message = message,
    };

    public static ApiError ExternalDependency(string code, string message) => new()
    {
        Code = code,
        Message = message,
    };

    public static ApiError Unexpected(string message) => new()
    {
        Code = "internal.unexpected",
        Message = message,
    };
}
