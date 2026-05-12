using Hangfire.Dashboard;

namespace DineOS.Api.Auth;

/// <summary>
/// Restricts the Hangfire dashboard to authenticated users in the SuperAdmin role.
/// Local-development convenience is opt-in via <c>Hangfire:Dashboard:AllowAnonymous</c>.
/// </summary>
public sealed class SuperAdminDashboardAuthorizationFilter(bool allowAnonymous)
    : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        if (allowAnonymous)
            return true;

        return httpContext.User.Identity?.IsAuthenticated == true
               && httpContext.User.IsInRole("SuperAdmin");
    }
}
