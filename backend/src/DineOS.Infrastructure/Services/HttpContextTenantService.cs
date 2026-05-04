using DineOS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;

namespace DineOS.Infrastructure.Services;

public class HttpContextTenantService(IHttpContextAccessor httpContextAccessor) : ICurrentTenantService
{
    public long? TenantId =>
        httpContextAccessor.HttpContext?.Items["TenantId"] is long id ? id : null;
}
