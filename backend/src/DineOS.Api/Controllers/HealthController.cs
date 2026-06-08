using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/health")]
[Produces("application/json")]
[AllowAnonymous]
[EnableRateLimiting("public")]
public class HealthController(IHealthService healthService) : ControllerBase
{
    /// <summary>Returns the current health status of the API and its dependencies.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<HealthStatus>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HealthStatus>), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var status = await healthService.GetStatusAsync(ct);

        // Unhealthy = a critical dependency (database) is down → 503 so probes
        // stop routing traffic here. Healthy/Degraded still serve requests → 200.
        if (status.Status == "Unhealthy")
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<HealthStatus>.Ok(status, "API is unhealthy"));

        return Ok(ApiResponse<HealthStatus>.Ok(status, $"API is {status.Status.ToLowerInvariant()}"));
    }
}
