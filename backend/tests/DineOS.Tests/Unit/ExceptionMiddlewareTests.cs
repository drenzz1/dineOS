using DineOS.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DineOS.Tests.Unit;

public class ExceptionMiddlewareTests
{
    private readonly ILogger<ExceptionMiddleware> _logger =
        Substitute.For<ILogger<ExceptionMiddleware>>();

    private static IHostEnvironment Env(string environmentName)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environmentName);
        return env;
    }

    private ExceptionMiddleware Build(RequestDelegate next, string environmentName = "Development")
        => new(next, _logger, Env(environmentName));

    private static DefaultHttpContext BuildContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    private static async Task<string> ReadBodyAsync(DefaultHttpContext ctx)
    {
        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        return await new StreamReader(ctx.Response.Body).ReadToEndAsync();
    }

    [Fact]
    public async Task InvokeAsync_NoException_CallsNextDelegate()
    {
        var called = false;
        var mw = Build(_ => { called = true; return Task.CompletedTask; });

        await mw.InvokeAsync(BuildContext());

        Assert.True(called);
    }

    [Fact]
    public async Task InvokeAsync_KeyNotFoundException_Returns404WithMessageInDevelopment()
    {
        var mw = Build(_ => throw new KeyNotFoundException("item not found"));
        var ctx = BuildContext();

        await mw.InvokeAsync(ctx);

        Assert.Equal(404, ctx.Response.StatusCode);
        Assert.Contains("item not found", await ReadBodyAsync(ctx));
    }

    [Fact]
    public async Task InvokeAsync_UnauthorizedAccessException_Returns401()
    {
        var mw = Build(_ => throw new UnauthorizedAccessException());
        var ctx = BuildContext();

        await mw.InvokeAsync(ctx);

        Assert.Equal(401, ctx.Response.StatusCode);
        Assert.Contains("Unauthorized", await ReadBodyAsync(ctx));
    }

    [Fact]
    public async Task InvokeAsync_ArgumentException_Returns400WithMessageInDevelopment()
    {
        var mw = Build(_ => throw new ArgumentException("bad input"));
        var ctx = BuildContext();

        await mw.InvokeAsync(ctx);

        Assert.Equal(400, ctx.Response.StatusCode);
        Assert.Contains("bad input", await ReadBodyAsync(ctx));
    }

    [Fact]
    public async Task InvokeAsync_UnknownException_Returns500WithGenericMessage()
    {
        var mw = Build(_ => throw new InvalidOperationException("crash"));
        var ctx = BuildContext();

        await mw.InvokeAsync(ctx);

        Assert.Equal(500, ctx.Response.StatusCode);
        Assert.Contains("unexpected error", await ReadBodyAsync(ctx));
    }

    [Fact]
    public async Task InvokeAsync_KeyNotFoundException_MasksMessageOutsideDevelopment()
    {
        var mw = Build(_ => throw new KeyNotFoundException("secret internal detail"), "Production");
        var ctx = BuildContext();

        await mw.InvokeAsync(ctx);

        var body = await ReadBodyAsync(ctx);
        Assert.Equal(404, ctx.Response.StatusCode);
        Assert.DoesNotContain("secret internal detail", body);
        Assert.Contains("not found", body);
    }

    [Fact]
    public async Task InvokeAsync_ArgumentException_MasksMessageOutsideDevelopment()
    {
        var mw = Build(_ => throw new ArgumentException("Parameter 'connectionString' was null"), "Production");
        var ctx = BuildContext();

        await mw.InvokeAsync(ctx);

        var body = await ReadBodyAsync(ctx);
        Assert.Equal(400, ctx.Response.StatusCode);
        Assert.DoesNotContain("connectionString", body);
        Assert.Contains("invalid", body);
    }
}
