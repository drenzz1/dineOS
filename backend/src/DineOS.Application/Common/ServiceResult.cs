namespace DineOS.Application.Common;

public enum ServiceErrorKind
{
    None,
    NotFound,
    ValidationFailed,
    BadRequest,
    Conflict,
    Unauthorized,
    UnprocessableEntity
}

public class ServiceResult<T>
{
    public bool IsSuccess => Error == ServiceErrorKind.None;
    public bool IsCreated { get; }
    public T? Value { get; }
    public ServiceErrorKind Error { get; }
    public string? Message { get; }
    public IReadOnlyList<string>? Errors { get; }

    private ServiceResult(T? value, ServiceErrorKind error, string? message, IReadOnlyList<string>? errors, bool isCreated)
    {
        Value = value;
        Error = error;
        Message = message;
        Errors = errors;
        IsCreated = isCreated;
    }

    public static ServiceResult<T> Ok(T value, string? message = null) =>
        new(value, ServiceErrorKind.None, message, null, isCreated: false);

    public static ServiceResult<T> Created(T value, string? message = null) =>
        new(value, ServiceErrorKind.None, message, null, isCreated: true);

    public static ServiceResult<T> NotFound(string message) =>
        new(default, ServiceErrorKind.NotFound, message, null, isCreated: false);

    public static ServiceResult<T> ValidationFailed(string message, IReadOnlyList<string> errors) =>
        new(default, ServiceErrorKind.ValidationFailed, message, errors, isCreated: false);

    public static ServiceResult<T> BadRequest(string message) =>
        new(default, ServiceErrorKind.BadRequest, message, null, isCreated: false);

    public static ServiceResult<T> Conflict(string message) =>
        new(default, ServiceErrorKind.Conflict, message, null, isCreated: false);

    public static ServiceResult<T> UnprocessableEntity(string message) =>
        new(default, ServiceErrorKind.UnprocessableEntity, message, null, isCreated: false);
}
