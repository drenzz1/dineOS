using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
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
public class ReportsController(IReportsService reportsService) : ControllerBase
{
    /// <summary>Returns the sales report. Defaults to the last 30 days when no range is specified.</summary>
    [HttpGet("sales")]
    [ProducesResponseType(typeof(ApiResponse<SalesReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetSalesReport(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct) =>
        (await reportsService.GetSalesReportAsync(from, to, ct)).ToActionResult();

    /// <summary>Returns the orders report. Defaults to the last 30 days when no range is specified.</summary>
    [HttpGet("orders")]
    [ProducesResponseType(typeof(ApiResponse<OrdersReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetOrdersReport(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct) =>
        (await reportsService.GetOrdersReportAsync(from, to, ct)).ToActionResult();

    /// <summary>Returns the staff activity report.</summary>
    [HttpGet("staff")]
    [ProducesResponseType(typeof(ApiResponse<StaffReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetStaffReport(CancellationToken ct) =>
        (await reportsService.GetStaffReportAsync(ct)).ToActionResult();
}
