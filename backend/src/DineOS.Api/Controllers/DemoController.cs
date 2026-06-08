using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DemoAccess;
using DineOS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

/// <summary>
/// Public demo access endpoint (#216). Anonymous + rate-limited.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/demo")]
[Produces("application/json")]
public class DemoController(IDemoAccessService demoAccess) : ControllerBase
{
    /// <summary>
    /// Requests demo access for the supplied email. Always returns 202
    /// regardless of whether the row was new, reused, expired-and-reset, or
    /// dropped by the honeypot — we do not leak account existence.
    /// </summary>
    [HttpPost("request")]
    [AllowAnonymous]
    [EnableRateLimiting("demo-request")]
    [ProducesResponseType(typeof(ApiResponse<RequestDemoAccessResponse>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RequestAccess(
        [FromBody] RequestDemoAccessRequest request,
        CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await demoAccess.RequestAsync(request, ip, ct);

        if (result.IsSuccess)
        {
            return Accepted(ApiResponse<RequestDemoAccessResponse>.Ok(result.Value!));
        }
        return result.ToActionResult();
    }
}
