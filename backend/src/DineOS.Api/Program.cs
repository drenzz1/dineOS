using Asp.Versioning;
using DineOS.Api.Auth;
using DineOS.Api.Middleware;
using DineOS.Application;
using DineOS.Application.Common;
using DineOS.Infrastructure;
using DineOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using System.Reflection;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── Authentication ────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"];
        options.Audience = builder.Configuration["Keycloak:Audience"];
        options.RequireHttpsMetadata = false;

        var metadataAddress = builder.Configuration["Keycloak:MetadataAddress"];
        if (!string.IsNullOrEmpty(metadataAddress))
            options.MetadataAddress = metadataAddress;
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
    // Unauthenticated / public endpoints (e.g. /health)
    options.AddFixedWindowLimiter("public", policy =>
    {
        policy.PermitLimit = 60;
        policy.Window = TimeSpan.FromMinutes(1);
        policy.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        policy.QueueLimit = 5;
    });

    // Authenticated API endpoints
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
            Obtain a JWT access token from Keycloak and supply it as:
            `Authorization: Bearer <token>`

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

app.UseMiddleware<ExceptionMiddleware>();

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
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
    {
        status = code,
        error = code == 401 ? "Unauthorized" : "Forbidden",
        correlationId = ctx.TraceIdentifier ?? Guid.NewGuid().ToString(),
        timestamp = DateTime.UtcNow
    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
});

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantIsolationMiddleware>();
app.MapControllers();

app.Run();

public partial class Program { }
