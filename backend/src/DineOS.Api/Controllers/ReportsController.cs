using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

/// <summary>Reporting endpoints — Manager and above.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reports")]
[Produces("application/json")]
[Authorize(Policy = Policies.ManagerAndAbove)]
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

    /// <summary>Returns the top 20 ordered menu items by quantity. Defaults to the last 30 days.</summary>
    [HttpGet("items")]
    [ProducesResponseType(typeof(ApiResponse<ItemsReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetItemsReport(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct) =>
        (await reportsService.GetItemsReportAsync(from, to, ct)).ToActionResult();

    /// <summary>Returns paginated order history with line items and payment info. Defaults to the last 30 days.</summary>
    [HttpGet("orders/history")]
    [ProducesResponseType(typeof(ApiResponse<OrderHistoryReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetOrderHistory(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        (await reportsService.GetOrderHistoryAsync(from, to, page, pageSize, ct)).ToActionResult();
}
