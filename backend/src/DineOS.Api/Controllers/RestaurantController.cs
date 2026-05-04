using DineOS.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DineOS.Api.Controllers;

/// <summary>Restaurant operations endpoints — Manager and above.</summary>
[ApiController]
[Route("api/v1/restaurant")]
[Produces("application/json")]
[Authorize(Policy = "ManagerAndAbove")]
public class RestaurantController : ControllerBase
{
    /// <summary>Gets the restaurant's profile and settings.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetRestaurant() =>
        Ok(ApiResponse<object>.Ok(new { }, "Restaurant info"));

    /// <summary>Updates the restaurant's profile and settings.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult UpdateRestaurant() =>
        Ok(ApiResponse.Ok("Restaurant updated"));

    /// <summary>Lists all tables in the restaurant.</summary>
    [HttpGet("tables")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetTables() =>
        Ok(ApiResponse<object>.Ok(Array.Empty<object>(), "Table list"));

    /// <summary>Adds a new table to the restaurant.</summary>
    [HttpPost("tables")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult AddTable() =>
        StatusCode(StatusCodes.Status201Created, ApiResponse.Ok("Table added"));

    /// <summary>Updates a table's details.</summary>
    [HttpPut("tables/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult UpdateTable(Guid id) =>
        Ok(ApiResponse.Ok($"Table {id} updated"));
}
