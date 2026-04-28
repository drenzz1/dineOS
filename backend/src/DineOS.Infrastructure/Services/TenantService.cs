using DineOS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace DineOS.Infrastructure.Services;

public class TenantService(IHttpContextAccessor httpContextAccessor) : ITenantService
{
    public long? TenantId
    {
        get
        {
            var claim = httpContextAccessor.HttpContext?.User?.FindFirstValue("tenant_id");
            return long.TryParse(claim, out var id) ? id : null;
        }
    }
}
