using DineOS.Application;
using DineOS.Infrastructure;
using DineOS.Infrastructure.Persistence;
using DineOS.Api.Auth;
using DineOS.Api.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using System.Reflection;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdminOnly",   p => p.RequireRole("SuperAdmin"));
    options.AddPolicy("ManagerAndAbove",  p => p.RequireRole("SuperAdmin", "Manager"));
    options.AddPolicy("CashierAndAbove",  p => p.RequireRole("SuperAdmin", "Manager", "Cashier"));
    options.AddPolicy("KitchenStaffOnly", p => p.RequireRole("KitchenStaff"));
});

builder.Services.AddHttpClient();
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DineOS API",
        Version = "v1",
        Description = "Restaurant Management System — DineOS backend API"
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

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"])
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// Auto-apply pending migrations on startup (development-safe; production should use explicit migration steps)
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

// Intercepts 401/403 responses that have no body (JWT auth/authz failures)
// and returns a structured JSON error payload.
// Must be placed before UseAuthentication so it wraps those responses.
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
