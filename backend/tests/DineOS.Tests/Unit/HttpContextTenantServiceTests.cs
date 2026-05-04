using DineOS.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace DineOS.Tests.Unit;

public class HttpContextTenantServiceTests
{
    private static HttpContextTenantService Build(IHttpContextAccessor accessor) =>
        new(accessor);

    [Fact]
    public void TenantId_ItemsContainLong_ReturnsThatValue()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        var ctx = new DefaultHttpContext();
        ctx.Items["TenantId"] = 42L;
        accessor.HttpContext.Returns(ctx);

        Assert.Equal(42L, Build(accessor).TenantId);
    }

    [Fact]
    public void TenantId_ItemsKeyMissing_ReturnsNull()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(new DefaultHttpContext());

        Assert.Null(Build(accessor).TenantId);
    }

    [Fact]
    public void TenantId_ItemsValueIsWrongType_ReturnsNull()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        var ctx = new DefaultHttpContext();
        ctx.Items["TenantId"] = "not-a-long";
        accessor.HttpContext.Returns(ctx);

        Assert.Null(Build(accessor).TenantId);
    }

    [Fact]
    public void TenantId_HttpContextIsNull_ReturnsNull()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);

        Assert.Null(Build(accessor).TenantId);
    }
}
