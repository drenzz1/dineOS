using DineOS.Application.Authorization;
using DineOS.Application.Common;
using System.Security.Claims;
using System.Text.Json;

namespace DineOS.Api.Middleware;

public class TenantIsolationMiddleware(RequestDelegate next, ILogger<TenantIsolationMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip unauthenticated requests — auth middleware owns the 401
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        // SuperAdmin operates platform-wide; no tenant scope enforced
        if (context.User.IsInRole(Roles.SuperAdmin))
        {
            await next(context);
            return;
        }

        // 1. JWT claim is the authoritative tenant source
        var jwtClaim = context.User.FindFirstValue("tenant_id");
        if (string.IsNullOrEmpty(jwtClaim) || !long.TryParse(jwtClaim, out var jwtTenantId))
        {
            logger.LogWarning(
                "Authenticated user has no valid tenant_id claim. Sub: {Sub}",
                context.User.FindFirstValue("sub"));
            await WriteForbiddenAsync(context, "Tenant context is required.");
            return;
        }

        // 2. X-Tenant-ID header is a hint only — reject if it contradicts the JWT
        var header = context.Request.Headers["X-Tenant-ID"].FirstOrDefault();
        if (!string.IsNullOrEmpty(header))
        {
            if (!long.TryParse(header, out var headerTenantId) || headerTenantId != jwtTenantId)
            {
                logger.LogWarning(
                    "X-Tenant-ID header '{Header}' does not match JWT tenant_id '{Claim}'. Sub: {Sub}",
                    header, jwtTenantId, context.User.FindFirstValue("sub"));
                await WriteForbiddenAsync(context, "Tenant ID mismatch.");
                return;
            }
        }

        // 3. Route-level check for tenant-scoped routes (e.g. /api/v1/{tenantId}/...)
        var routeValue = context.GetRouteValue("tenantId")?.ToString();
        if (!string.IsNullOrEmpty(routeValue))
        {
            if (!long.TryParse(routeValue, out var routeTenantId) || routeTenantId != jwtTenantId)
            {
                logger.LogWarning(
                    "Route tenantId '{Route}' does not match JWT tenant_id '{Claim}'. Sub: {Sub}",
                    routeValue, jwtTenantId, context.User.FindFirstValue("sub"));
                await WriteForbiddenAsync(context, "Access to this tenant's resources is not permitted.");
                return;
            }
        }

        // 4. Resolved tenant ID is available to all downstream components
        context.Items["TenantId"] = jwtTenantId;

        await next(context);
    }

    private static Task WriteForbiddenAsync(HttpContext context, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsync(
            JsonSerializer.Serialize(ApiResponse.Fail(message), JsonOptions));
    }
}
