using DineOS.Application.Common;
using DineOS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace DineOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController(IHealthService healthService) : ControllerBase
{
    /// <summary>Returns the current health status of the API.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<HealthStatus>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var status = await healthService.GetStatusAsync(ct);
        return Ok(ApiResponse<HealthStatus>.Ok(status, "API is healthy"));
    }
}
