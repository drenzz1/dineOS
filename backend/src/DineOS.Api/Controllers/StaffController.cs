using DineOS.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DineOS.Api.Controllers;

/// <summary>Staff management endpoints — Manager and above.</summary>
[ApiController]
[Route("api/v1/staff")]
[Produces("application/json")]
[Authorize(Policy = "ManagerAndAbove")]
public class StaffController : ControllerBase
{
    /// <summary>Lists all staff members.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetStaff() =>
        Ok(ApiResponse<object>.Ok(Array.Empty<object>(), "Staff list"));

    /// <summary>Adds a new staff member.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult AddStaff() =>
        StatusCode(StatusCodes.Status201Created, ApiResponse.Ok("Staff member added"));

    /// <summary>Updates a staff member's details.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult UpdateStaff(Guid id) =>
        Ok(ApiResponse.Ok($"Staff member {id} updated"));

    /// <summary>Removes a staff member.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult RemoveStaff(Guid id) =>
        Ok(ApiResponse.Ok($"Staff member {id} removed"));
}
