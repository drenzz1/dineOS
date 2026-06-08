using DineOS.Application.Common;
using System.Net;
using System.Text.Json;

namespace DineOS.Api.Middleware;

public class ExceptionMiddleware(
    RequestDelegate next,
    ILogger<ExceptionMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        // Outside Development, never echo a raw exception message to the client:
        // framework/library-thrown Argument/KeyNotFound exceptions can carry
        // internal detail (parameter names, paths) that aids reconnaissance.
        // The full exception (incl. message + stack) is already logged above and
        // is correlatable via correlationId. In Development we keep ex.Message so
        // developers still see the specific error. Status-code mapping is kept in
        // both cases.
        var isDev = environment.IsDevelopment();

        var (status, message) = ex switch
        {
            KeyNotFoundException        => (HttpStatusCode.NotFound,            isDev ? ex.Message : "The requested resource was not found."),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized,        "Unauthorized"),
            ArgumentException           => (HttpStatusCode.BadRequest,          isDev ? ex.Message : "The request was invalid."),
            _                           => (HttpStatusCode.InternalServerError, "An unexpected error occurred")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)status;

        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString()
            ?? context.TraceIdentifier;

        var payload = new
        {
            success = false,
            message,
            correlationId,
            timestamp = DateTime.UtcNow
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
