using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/health")]
[Produces("application/json")]
[EnableRateLimiting("public")]
public class HealthController(IHealthService healthService) : ControllerBase
{
    /// <summary>Returns the current health status of the API.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<HealthStatus>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var status = await healthService.GetStatusAsync(ct);
        return Ok(ApiResponse<HealthStatus>.Ok(status, "API is healthy"));
    }
}
