using DineOS.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace DineOS.Api.Controllers;

internal static class ServiceResultExtensions
{
    public static IActionResult ToActionResult<T>(this ServiceResult<T> result)
    {
        if (result.IsSuccess)
        {
            var ok = ApiResponse<T>.Ok(result.Value!, result.Message);
            return result.IsCreated
                ? new ObjectResult(ok) { StatusCode = StatusCodes.Status201Created }
                : new OkObjectResult(ok);
        }

        var failure = ApiResponse.Fail(result.Message ?? "Request failed.", result.Errors);

        return result.Error switch
        {
            ServiceErrorKind.ValidationFailed =>
                result.ValidationErrors is { Count: > 0 }
                    ? BuildValidationProblem(result.ValidationErrors)
                    : new BadRequestObjectResult(failure),
            ServiceErrorKind.BadRequest =>
                new BadRequestObjectResult(failure),
            ServiceErrorKind.NotFound =>
                new NotFoundObjectResult(failure),
            ServiceErrorKind.Conflict =>
                new ConflictObjectResult(failure),
            ServiceErrorKind.Unauthorized =>
                new UnauthorizedObjectResult(failure),
            ServiceErrorKind.UnprocessableEntity =>
                new UnprocessableEntityObjectResult(failure),
            ServiceErrorKind.ServiceUnavailable =>
                new ObjectResult(failure) { StatusCode = StatusCodes.Status503ServiceUnavailable },
            _ => new ObjectResult(failure) { StatusCode = StatusCodes.Status500InternalServerError }
        };
    }

    private static ObjectResult BuildValidationProblem(IReadOnlyList<ValidationError> errors)
    {
        var dict = errors
            .GroupBy(e => e.Code)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.Message).ToArray());

        return new BadRequestObjectResult(new ValidationProblemDetails(dict)
        {
            Title  = "Validation failed.",
            Status = StatusCodes.Status400BadRequest,
        });
    }
}
