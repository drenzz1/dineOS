using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DineOS.Tests.Integration;

// Test-only controller — registered via AddApplicationPart in CustomWebApplicationFactory.
// Never included in the production build.
[ApiController]
[Route("test")]
public class TestController : ControllerBase
{
    public record ValidateRequest(string? Name);

    // Throws ArgumentException on bad input so ExceptionMiddleware returns ApiResponse.Fail + 400
    [AllowAnonymous]
    [HttpPost("validate")]
    public IActionResult Validate([FromBody] ValidateRequest? request)
    {
        if (string.IsNullOrWhiteSpace(request?.Name))
            throw new ArgumentException("Name is required");

        return Ok();
    }

    // Protected endpoint — returns 401 without a valid Bearer token
    [Authorize]
    [HttpGet("secure")]
    public IActionResult Secure() => Ok(new { message = "authorized" });

    // No auth attributes by design. Program.cs fallback policy should still protect it.
    [HttpGet("fallback-protected")]
    public IActionResult FallbackProtected() => Ok(new { message = "fallback authorized" });
}
