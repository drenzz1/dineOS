namespace DineOS.Application.Common;

public record ValidationError(string Code, string Message);

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
    public IReadOnlyList<ValidationError>? ValidationErrors { get; }

    private ServiceResult(T? value, ServiceErrorKind error, string? message, IReadOnlyList<string>? errors, IReadOnlyList<ValidationError>? validationErrors, bool isCreated)
    {
        Value            = value;
        Error            = error;
        Message          = message;
        Errors           = errors;
        ValidationErrors = validationErrors;
        IsCreated        = isCreated;
    }

    public static ServiceResult<T> Ok(T value, string? message = null) =>
        new(value, ServiceErrorKind.None, message, null, null, isCreated: false);

    public static ServiceResult<T> Created(T value, string? message = null) =>
        new(value, ServiceErrorKind.None, message, null, null, isCreated: true);

    public static ServiceResult<T> NotFound(string message) =>
        new(default, ServiceErrorKind.NotFound, message, null, null, isCreated: false);

    /// <summary>Validation failure with plain string messages — used by existing service methods.</summary>
    public static ServiceResult<T> ValidationFailed(string message, IReadOnlyList<string> errors) =>
        new(default, ServiceErrorKind.ValidationFailed, message, errors, null, isCreated: false);

    /// <summary>Validation failure with structured error codes — produces RFC 7807 ProblemDetails in the API response.</summary>
    public static ServiceResult<T> ValidationFailed(string message, IReadOnlyList<ValidationError> errors) =>
        new(default, ServiceErrorKind.ValidationFailed, message,
            errors.Select(e => e.Message).ToList(), errors, isCreated: false);

    public static ServiceResult<T> BadRequest(string message) =>
        new(default, ServiceErrorKind.BadRequest, message, null, null, isCreated: false);

    public static ServiceResult<T> Conflict(string message) =>
        new(default, ServiceErrorKind.Conflict, message, null, null, isCreated: false);

    public static ServiceResult<T> UnprocessableEntity(string message) =>
        new(default, ServiceErrorKind.UnprocessableEntity, message, null, null, isCreated: false);
}
