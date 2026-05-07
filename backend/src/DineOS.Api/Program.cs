using Asp.Versioning;
using DineOS.Api.Auth;
using DineOS.Api.Middleware;
using DineOS.Application;
using DineOS.Application.Authentication;
using DineOS.Application.Common;
using DineOS.Infrastructure;
using DineOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;

// ── Bootstrap logger (captures startup errors before host is built) ────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ───────────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, config) =>
    {
        config
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Application", "DineOS.Api")
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} " +
                "{Properties:j}{NewLine}{Exception}");

        var lokiUri = ctx.Configuration["Loki:Uri"];
        if (!string.IsNullOrEmpty(lokiUri))
        {
            config.WriteTo.GrafanaLoki(
                lokiUri,
                labels:
                [
                    new LokiLabel { Key = "app",         Value = "dineos-api" },
                    new LokiLabel { Key = "environment", Value = ctx.HostingEnvironment.EnvironmentName }
                ],
                propertiesAsLabels: ["app", "environment"]);
        }
    });

    builder.Services.AddHttpClient();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // ── Authentication ────────────────────────────────────────────────────────────
    var keycloakOptions = builder.Configuration
        .GetSection(KeycloakOptions.SectionName)
        .Get<KeycloakOptions>() ?? new KeycloakOptions();

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = keycloakOptions.GetIssuerAuthority();
            options.Audience = keycloakOptions.Audience;
            options.RequireHttpsMetadata = keycloakOptions.RequireHttpsMetadata;

            if (!string.IsNullOrEmpty(keycloakOptions.MetadataAddress))
                options.MetadataAddress = keycloakOptions.MetadataAddress;
        });
    builder.Services.AddTransient<IClaimsTransformation, KeycloakRolesTransformation>();

    // ── Authorization ─────────────────────────────────────────────────────────────
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("SuperAdminOnly",   p => p.RequireRole("SuperAdmin"));
        options.AddPolicy("ManagerAndAbove",  p => p.RequireRole("SuperAdmin", "Manager"));
        options.AddPolicy("CashierAndAbove",  p => p.RequireRole("SuperAdmin", "Manager", "Cashier"));
        options.AddPolicy("KitchenStaffOnly", p => p.RequireRole("KitchenStaff"));
    });

    // ── API Versioning ────────────────────────────────────────────────────────────
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    // ── Rate Limiting ─────────────────────────────────────────────────────────────
    builder.Services.AddRateLimiter(options =>
    {
        options.AddFixedWindowLimiter("public", policy =>
        {
            policy.PermitLimit = 60;
            policy.Window = TimeSpan.FromMinutes(1);
            policy.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            policy.QueueLimit = 5;
        });

        options.AddFixedWindowLimiter("authenticated", policy =>
        {
            policy.PermitLimit = 300;
            policy.Window = TimeSpan.FromMinutes(1);
            policy.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            policy.QueueLimit = 20;
        });

        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.ContentType = "application/json";
            context.HttpContext.Response.Headers.RetryAfter =
                context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? ((int)retryAfter.TotalSeconds).ToString()
                    : "60";

            await context.HttpContext.Response.WriteAsync(
                JsonSerializer.Serialize(
                    ApiResponse.Fail("Too many requests. Please retry later."),
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                cancellationToken);
        };
    });

    // ── Controllers & Swagger ─────────────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "DineOS API",
            Version = "v1",
            Description = """
                ## DineOS Restaurant Management API

                ### Versioning
                All endpoints are versioned via the URL segment: `/api/v{N}/...`
                Current stable version: **v1**

                ### Authentication
                Obtain a JWT access token from `POST /api/v1/auth/login` and supply it as:
                `Authorization: Bearer <token>`

                ### Correlation IDs
                Every request receives a unique `X-Correlation-ID` response header.
                Pass this header on the request to propagate your own ID (useful for distributed tracing).
                All error envelopes include `correlationId` for log correlation.

                ### Pagination
                **Offset pagination** (stable lists):
                `GET /api/v1/resource?page=1&pageSize=20`
                Response includes `totalCount`, `totalPages`, `hasNextPage`, `hasPreviousPage`.

                **Cursor pagination** (high-frequency feeds — orders, activity):
                `GET /api/v1/resource?cursor=<opaque>&pageSize=20`
                Response includes `nextCursor` and `previousCursor`.

                ### Status codes
                | Code | Meaning |
                |------|---------|
                | 200  | OK |
                | 201  | Created |
                | 400  | Validation / bad request |
                | 401  | Missing or invalid token |
                | 403  | Insufficient role or tenant mismatch |
                | 404  | Resource not found |
                | 422  | Business rule violation |
                | 429  | Rate limit exceeded |
                | 500  | Internal server error |

                ### Idempotency
                `POST` endpoints are **not** idempotent.
                `PUT` and `PATCH` endpoints are idempotent.

                ### Resource naming
                Resources use **kebab-case** plural nouns: `/staff-members`, `/menu-items`.
                Sub-resources are nested: `/api/v1/orders/{id}/items`.
                """
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste a Keycloak access token (without the 'Bearer ' prefix)"
        });
        options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer")] = []
        });

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
            options.IncludeXmlComments(xmlPath);
    });

    // ── CORS ──────────────────────────────────────────────────────────────────────
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
            policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"])
                  .AllowAnyHeader()
                  .AllowAnyMethod());
    });

    var app = builder.Build();

    // Auto-apply pending migrations on startup
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
    }

    // ── Middleware pipeline ───────────────────────────────────────────────────────
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ExceptionMiddleware>();

    // Structured request logging — enriched with CorrelationId, TenantId, UserId
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("CorrelationId", httpContext.Items[CorrelationIdMiddleware.ItemKey]);
            diagnosticContext.Set("TenantId",      httpContext.Items["TenantId"]);
            diagnosticContext.Set("UserId",        httpContext.User.FindFirstValue("sub"));
            diagnosticContext.Set("UserAgent",     httpContext.Request.Headers.UserAgent.ToString());
        };
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "DineOS API v1"));
    }

    app.UseCors("AllowFrontend");
    app.UseHttpsRedirection();
    app.UseRateLimiter();

    // Intercepts 401/403 responses with no body (JWT auth/authz failures)
    app.UseStatusCodePages(async statusCodeContext =>
    {
        var ctx = statusCodeContext.HttpContext;
        if (ctx.Response.StatusCode is not (401 or 403))
            return;

        var code = ctx.Response.StatusCode;
        var correlationId = ctx.Items[CorrelationIdMiddleware.ItemKey]?.ToString()
            ?? ctx.TraceIdentifier;

        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = code,
            error = code == 401 ? "Unauthorized" : "Forbidden",
            correlationId,
            timestamp = DateTime.UtcNow
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    });

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<TenantIsolationMiddleware>();
    app.MapControllers();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
