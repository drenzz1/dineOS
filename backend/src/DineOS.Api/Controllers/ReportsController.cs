using Asp.Versioning;
using DineOS.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

/// <summary>Reporting endpoints — Manager and above.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reports")]
[Produces("application/json")]
[Authorize(Policy = "ManagerAndAbove")]
[EnableRateLimiting("authenticated")]
public class ReportsController : ControllerBase
{
    /// <summary>Returns the sales report.</summary>
    [HttpGet("sales")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult GetSalesReport() =>
        Ok(ApiResponse<object>.Ok(new { }, "Sales report"));

    /// <summary>Returns the orders report.</summary>
    [HttpGet("orders")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult GetOrdersReport() =>
        Ok(ApiResponse<object>.Ok(new { }, "Orders report"));

    /// <summary>Returns the staff activity report.</summary>
    [HttpGet("staff")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult GetStaffReport() =>
        Ok(ApiResponse<object>.Ok(new { }, "Staff report"));
}
