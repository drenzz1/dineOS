using DineOS.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Security.Claims;
using System.Text.Json;

namespace DineOS.Tests.Unit;

public class TenantIsolationMiddlewareTests
{
    private readonly ILogger<TenantIsolationMiddleware> _logger =
        Substitute.For<ILogger<TenantIsolationMiddleware>>();

    private TenantIsolationMiddleware Mw(RequestDelegate next) =>
        new(next, _logger);

    private static DefaultHttpContext BuildContext(ClaimsPrincipal? user = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        if (user is not null)
            ctx.User = user;
        return ctx;
    }

    private static ClaimsPrincipal AuthUser(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    private static async Task<string> ReadBodyAsync(DefaultHttpContext ctx)
    {
        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        return await new StreamReader(ctx.Response.Body).ReadToEndAsync();
    }

    // ── Unauthenticated ────────────────────────────────────────────────────
    [Fact]
    public async Task InvokeAsync_UnauthenticatedRequest_CallsNext()
    {
        var called = false;
        var ctx = BuildContext(); // no user → IsAuthenticated = false

        await Mw(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(ctx);

        Assert.True(called);
        Assert.Equal(200, ctx.Response.StatusCode);
    }

    // ── SuperAdmin bypass ──────────────────────────────────────────────────
    [Fact]
    public async Task InvokeAsync_SuperAdmin_CallsNextWithoutTenantCheck()
    {
        var called = false;
        var ctx = BuildContext(AuthUser(
            new Claim(ClaimTypes.Role, "SuperAdmin")));

        await Mw(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(ctx);

        Assert.True(called);
        Assert.Null(ctx.Items["TenantId"]);
    }

    // ── Missing tenant_id claim ────────────────────────────────────────────
    [Fact]
    public async Task InvokeAsync_MissingTenantIdClaim_Returns403()
    {
        var ctx = BuildContext(AuthUser(new Claim("sub", "user-1")));

        await Mw(_ => Task.CompletedTask).InvokeAsync(ctx);

        Assert.Equal(403, ctx.Response.StatusCode);
        Assert.Contains("Tenant context is required", await ReadBodyAsync(ctx));
    }

    [Fact]
    public async Task InvokeAsync_NonNumericTenantIdClaim_Returns403()
    {
        var ctx = BuildContext(AuthUser(new Claim("tenant_id", "not-a-number")));

        await Mw(_ => Task.CompletedTask).InvokeAsync(ctx);

        Assert.Equal(403, ctx.Response.StatusCode);
    }

    // ── Valid tenant_id, no header ─────────────────────────────────────────
    [Fact]
    public async Task InvokeAsync_ValidTenantId_SetsHttpContextItem_AndCallsNext()
    {
        var called = false;
        var ctx = BuildContext(AuthUser(new Claim("tenant_id", "42")));

        await Mw(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(ctx);

        Assert.True(called);
        Assert.Equal(42L, ctx.Items["TenantId"]);
    }

    // ── X-Tenant-ID header ────────────────────────────────────────────────
    [Fact]
    public async Task InvokeAsync_HeaderMatchesJwt_CallsNext()
    {
        var called = false;
        var ctx = BuildContext(AuthUser(new Claim("tenant_id", "5")));
        ctx.Request.Headers["X-Tenant-ID"] = "5";

        await Mw(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(ctx);

        Assert.True(called);
    }

    [Fact]
    public async Task InvokeAsync_HeaderDoesNotMatchJwt_Returns403()
    {
        var ctx = BuildContext(AuthUser(new Claim("tenant_id", "5")));
        ctx.Request.Headers["X-Tenant-ID"] = "99";

        await Mw(_ => Task.CompletedTask).InvokeAsync(ctx);

        Assert.Equal(403, ctx.Response.StatusCode);
        Assert.Contains("Tenant ID mismatch", await ReadBodyAsync(ctx));
    }

    [Fact]
    public async Task InvokeAsync_NonNumericHeader_Returns403()
    {
        var ctx = BuildContext(AuthUser(new Claim("tenant_id", "5")));
        ctx.Request.Headers["X-Tenant-ID"] = "bad-value";

        await Mw(_ => Task.CompletedTask).InvokeAsync(ctx);

        Assert.Equal(403, ctx.Response.StatusCode);
    }

    // ── Route-level tenantId ───────────────────────────────────────────────
    [Fact]
    public async Task InvokeAsync_RouteValueMatchesJwt_CallsNext()
    {
        var called = false;
        var ctx = BuildContext(AuthUser(new Claim("tenant_id", "7")));
        ctx.Request.RouteValues["tenantId"] = "7";

        await Mw(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(ctx);

        Assert.True(called);
    }

    [Fact]
    public async Task InvokeAsync_RouteValueDoesNotMatchJwt_Returns403()
    {
        var ctx = BuildContext(AuthUser(new Claim("tenant_id", "7")));
        ctx.Request.RouteValues["tenantId"] = "99";

        await Mw(_ => Task.CompletedTask).InvokeAsync(ctx);

        Assert.Equal(403, ctx.Response.StatusCode);
        Assert.Contains("not permitted", await ReadBodyAsync(ctx));
    }

    // ── Response body is valid JSON ────────────────────────────────────────
    [Fact]
    public async Task InvokeAsync_Forbidden_ResponseBodyIsValidJson()
    {
        var ctx = BuildContext(AuthUser(new Claim("sub", "user-1")));

        await Mw(_ => Task.CompletedTask).InvokeAsync(ctx);

        var body = await ReadBodyAsync(ctx);
        var doc = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.Equal("application/json", ctx.Response.ContentType);
    }
}
