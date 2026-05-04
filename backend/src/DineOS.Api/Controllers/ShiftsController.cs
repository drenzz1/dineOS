using DineOS.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DineOS.Api.Controllers;

/// <summary>Shift scheduling endpoints — Manager and above.</summary>
[ApiController]
[Route("api/v1/shifts")]
[Produces("application/json")]
[Authorize(Policy = "ManagerAndAbove")]
public class ShiftsController : ControllerBase
{
    /// <summary>Lists all shifts.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetShifts() =>
        Ok(ApiResponse<object>.Ok(Array.Empty<object>(), "Shift list"));

    /// <summary>Creates a new shift.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult CreateShift() =>
        StatusCode(StatusCodes.Status201Created, ApiResponse.Ok("Shift created"));

    /// <summary>Updates a shift.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult UpdateShift(Guid id) =>
        Ok(ApiResponse.Ok($"Shift {id} updated"));

    /// <summary>Deletes a shift.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult DeleteShift(Guid id) =>
        Ok(ApiResponse.Ok($"Shift {id} deleted"));
}
