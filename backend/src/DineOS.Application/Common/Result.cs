namespace DineOS.Application.Common;

public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public IReadOnlyList<string>? Errors { get; }

    private Result(T value) { IsSuccess = true; Value = value; }
    private Result(string error, IReadOnlyList<string>? errors = null)
    {
        IsSuccess = false;
        Error = error;
        Errors = errors;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string error) => new(error);
    public static Result<T> Failure(string error, IReadOnlyList<string> errors) => new(error, errors);
}

public class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public IReadOnlyList<string>? Errors { get; }

    private Result(bool success, string? error, IReadOnlyList<string>? errors)
    {
        IsSuccess = success;
        Error = error;
        Errors = errors;
    }

    public static Result Success() => new(true, null, null);
    public static Result Failure(string error) => new(false, error, null);
    public static Result Failure(string error, IReadOnlyList<string> errors) => new(false, error, errors);
}
