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
            ServiceErrorKind.NotFound =>
                new NotFoundObjectResult(failure),
            ServiceErrorKind.ValidationFailed or ServiceErrorKind.BadRequest =>
                new BadRequestObjectResult(failure),
            ServiceErrorKind.Conflict =>
                new ConflictObjectResult(failure),
            ServiceErrorKind.Unauthorized =>
                new UnauthorizedObjectResult(failure),
            ServiceErrorKind.UnprocessableEntity =>
                new UnprocessableEntityObjectResult(failure),
            _ => new ObjectResult(failure) { StatusCode = StatusCodes.Status500InternalServerError }
        };
    }
}
