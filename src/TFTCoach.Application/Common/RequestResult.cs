namespace TFTCoach.Application.Common;

public sealed class RequestResult<T>
{
    public required bool Success { get; init; }

    public T? Data { get; init; }

    public ApiError? Error { get; init; }

    public static RequestResult<T> Ok(T? data) => new()
    {
        Success = true,
        Data = data,
        Error = null,
    };

    public static RequestResult<T> Fail(ApiError error) => new()
    {
        Success = false,
        Data = default,
        Error = error,
    };
}
