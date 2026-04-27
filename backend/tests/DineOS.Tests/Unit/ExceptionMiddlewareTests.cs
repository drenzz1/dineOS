using DineOS.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DineOS.Tests.Unit;

public class ExceptionMiddlewareTests
{
    private readonly ILogger<ExceptionMiddleware> _logger =
        Substitute.For<ILogger<ExceptionMiddleware>>();

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
        var mw = new ExceptionMiddleware(_ => { called = true; return Task.CompletedTask; }, _logger);

        await mw.InvokeAsync(BuildContext());

        Assert.True(called);
    }

    [Fact]
    public async Task InvokeAsync_KeyNotFoundException_Returns404WithMessage()
    {
        var mw = new ExceptionMiddleware(_ => throw new KeyNotFoundException("item not found"), _logger);
        var ctx = BuildContext();

        await mw.InvokeAsync(ctx);

        Assert.Equal(404, ctx.Response.StatusCode);
        Assert.Contains("item not found", await ReadBodyAsync(ctx));
    }

    [Fact]
    public async Task InvokeAsync_UnauthorizedAccessException_Returns401()
    {
        var mw = new ExceptionMiddleware(_ => throw new UnauthorizedAccessException(), _logger);
        var ctx = BuildContext();

        await mw.InvokeAsync(ctx);

        Assert.Equal(401, ctx.Response.StatusCode);
        Assert.Contains("Unauthorized", await ReadBodyAsync(ctx));
    }

    [Fact]
    public async Task InvokeAsync_ArgumentException_Returns400WithMessage()
    {
        var mw = new ExceptionMiddleware(_ => throw new ArgumentException("bad input"), _logger);
        var ctx = BuildContext();

        await mw.InvokeAsync(ctx);

        Assert.Equal(400, ctx.Response.StatusCode);
        Assert.Contains("bad input", await ReadBodyAsync(ctx));
    }

    [Fact]
    public async Task InvokeAsync_UnknownException_Returns500WithGenericMessage()
    {
        var mw = new ExceptionMiddleware(_ => throw new InvalidOperationException("crash"), _logger);
        var ctx = BuildContext();

        await mw.InvokeAsync(ctx);

        Assert.Equal(500, ctx.Response.StatusCode);
        Assert.Contains("unexpected error", await ReadBodyAsync(ctx));
    }
}
