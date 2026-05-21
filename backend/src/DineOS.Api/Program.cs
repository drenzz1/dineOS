using Asp.Versioning;
using DineOS.Api.Auth;
using DineOS.Api.Hubs;
using DineOS.Api.Middleware;
using DineOS.Api.Services;
using DineOS.Application;
using DineOS.Application.Authentication;
using DineOS.Application.Authorization;
using DineOS.Application.Common;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Infrastructure;
using Hangfire;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
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

    builder.Services.AddSingleton<IOrderNotificationService, OrderNotificationService>();

    var redisConnStr = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
    builder.Services.AddSignalR()
        .AddStackExchangeRedis(options =>
        {
            options.Configuration = StackExchange.Redis.ConfigurationOptions.Parse(redisConnStr);
            options.Configuration.AbortOnConnectFail = false;
        });

    // ── Authentication ────────────────────────────────────────────────────────────
    var keycloakOptions = builder.Configuration
        .GetSection(KeycloakOptions.SectionName)
        .Get<KeycloakOptions>() ?? new KeycloakOptions();
    var keycloakAuthorizationEndpoint = keycloakOptions.GetAuthorizationEndpoint();
    var keycloakTokenEndpoint = keycloakOptions.GetTokenEndpoint();

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = keycloakOptions.GetIssuerAuthority();
            options.Audience = keycloakOptions.Audience;
            options.RequireHttpsMetadata = keycloakOptions.RequireHttpsMetadata;

            if (!string.IsNullOrEmpty(keycloakOptions.MetadataAddress))
                options.MetadataAddress = keycloakOptions.MetadataAddress;
            var metadataAddress = builder.Configuration["Keycloak:MetadataAddress"];
            if (!string.IsNullOrEmpty(metadataAddress))
                options.MetadataAddress = metadataAddress;

            // SignalR: browsers can't set Authorization headers on WebSocket connections,
            // so the JS client passes the JWT as ?access_token=<token> on hub URLs.
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var token = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(token) &&
                        context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        context.Token = token;
                    return Task.CompletedTask;
                }
            };
        });
    builder.Services.AddTransient<IClaimsTransformation, KeycloakRolesTransformation>();

    // ── Authorization ─────────────────────────────────────────────────────────────
    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        options.AddPolicy(Policies.SuperAdminOnly,   p => p.RequireRole(Roles.SuperAdmin));
        options.AddPolicy(Policies.ManagerAndAbove,  p => p.RequireRole(Roles.SuperAdmin, Roles.Manager));
        options.AddPolicy(Policies.CashierAndAbove,  p => p.RequireRole(Roles.SuperAdmin, Roles.Manager, Roles.Cashier));
        options.AddPolicy(Policies.KitchenStaffOnly, p => p.RequireRole(Roles.KitchenStaff));
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

        // AI endpoints — much tighter cap to bound LLM cost per tenant.
        // Each call typically uses 300–600 input tokens + ≤400 output tokens.
        options.AddFixedWindowLimiter("ai-expensive", policy =>
        {
            policy.PermitLimit = 10;
            policy.Window = TimeSpan.FromMinutes(1);
            policy.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            policy.QueueLimit = 0;
        });

        // Anonymous owner-facing email-verification confirm — partitioned by
        // remote IP so one noisy client cannot lock everyone out. The cap is
        // tight because a legitimate owner needs at most a handful of tries
        // (the code itself is also capped via FailedAttempts).
        options.AddPolicy("email-verification-confirm", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                }));

        // Demo access (#216). Partitioned on a composite (email + IP) key when
        // the body carries an email; otherwise by IP alone. 3 requests/hour
        // per email keeps re-submits cheap while blocking enumeration; the
        // IP-only bucket (set to a higher 10/hour ceiling) catches bots that
        // sweep many emails from one host.
        options.AddPolicy("demo-request", httpContext =>
        {
            string emailKey = string.Empty;
            if (httpContext.Request.HasJsonContentType()
                && httpContext.Request.ContentLength is > 0 and < 4096)
            {
                httpContext.Request.EnableBuffering();
                using var reader = new StreamReader(
                    httpContext.Request.Body, leaveOpen: true);
                // AddPolicy is a sync callback, but Microsoft.AspNetCore.TestHost
                // (and Kestrel with AllowSynchronousIO=false) forbids Stream.Read
                // on the request body. Await the async read and unwrap.
                var bodyText = reader.ReadToEndAsync().GetAwaiter().GetResult();
                httpContext.Request.Body.Position = 0;
                try
                {
                    using var doc = JsonDocument.Parse(bodyText);
                    if (doc.RootElement.TryGetProperty("email", out var emailEl) &&
                        emailEl.ValueKind == JsonValueKind.String)
                    {
                        var email = emailEl.GetString();
                        if (!string.IsNullOrWhiteSpace(email))
                            emailKey = email.Trim().ToLowerInvariant();
                    }
                }
                catch (JsonException)
                {
                    // Malformed JSON falls through; the controller returns 400.
                }
            }

            var ipKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // When email is present, bucket on email (tighter limit). Otherwise
            // bucket on IP. Both partitions share the same policy options
            // exposed to the user; the per-IP path gets the looser ceiling.
            if (!string.IsNullOrEmpty(emailKey))
            {
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"demo-email:{emailKey}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 3,
                        Window = TimeSpan.FromHours(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            }

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"demo-ip:{ipKey}",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromHours(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                });
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
                Use **Authorize** in Swagger to sign in through Keycloak with OAuth2 Authorization Code + PKCE.
                For scripts, obtain a JWT access token from `POST /api/v1/auth/login` and supply it as:
                `Authorization: Bearer <token>`.

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

        if (!string.IsNullOrWhiteSpace(keycloakAuthorizationEndpoint) &&
            !string.IsNullOrWhiteSpace(keycloakTokenEndpoint))
        {
            options.AddSecurityDefinition("Keycloak", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Description = "Sign in with Keycloak using Authorization Code + PKCE.",
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri(keycloakAuthorizationEndpoint),
                        TokenUrl = new Uri(keycloakTokenEndpoint),
                        Scopes = new Dictionary<string, string>
                        {
                            ["openid"] = "OpenID Connect sign-in",
                            ["profile"] = "User profile claims",
                            ["email"] = "User email claim"
                        }
                    }
                }
            });

            options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Keycloak")] = ["openid", "profile", "email"]
            });
        }

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
            options.IncludeXmlComments(xmlPath);

        var applicationXmlPath = Path.Combine(AppContext.BaseDirectory, "DineOS.Application.xml");
        if (File.Exists(applicationXmlPath))
            options.IncludeXmlComments(applicationXmlPath);
    });

    // ── CORS ──────────────────────────────────────────────────────────────────────
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
            policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"])
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()); // Required for SignalR WebSocket connections from browsers
    });

    var app = builder.Build();

    // Auto-apply pending migrations on startup
    await app.Services.GetRequiredService<IDatabaseMigrator>().MigrateAsync();

    // Demo tenant seed (#216) — idempotent; gated by Demo:Enabled.
    await app.Services.GetRequiredService<IDemoTenantSeeder>().SeedAsync();

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
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "DineOS API v1");

            var swaggerClientId = keycloakOptions.GetClientId();
            if (!string.IsNullOrWhiteSpace(swaggerClientId))
            {
                c.OAuthClientId(swaggerClientId);
                c.OAuthAppName("dineOS API Swagger");
                c.OAuthScopes("openid", "profile", "email");
                c.OAuthUsePkce();
            }
        });
    }

    app.UseCors("AllowFrontend");

    // ── Uploads static file serving (dev or explicit opt-in only) ────────────────
    var fileStorageOpts = builder.Configuration
        .GetSection(FileStorageOptions.SectionName)
        .Get<FileStorageOptions>() ?? new FileStorageOptions();

    if (app.Environment.IsDevelopment() || fileStorageOpts.ServeLocally)
    {
        var uploadsRoot = Path.GetFullPath(fileStorageOpts.RootPath);
        Directory.CreateDirectory(uploadsRoot);

        var contentTypeProvider = new FileExtensionContentTypeProvider();
        contentTypeProvider.Mappings.Clear();
        contentTypeProvider.Mappings[".jpg"]  = "image/jpeg";
        contentTypeProvider.Mappings[".jpeg"] = "image/jpeg";
        contentTypeProvider.Mappings[".png"]  = "image/png";
        contentTypeProvider.Mappings[".webp"] = "image/webp";

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider        = new PhysicalFileProvider(uploadsRoot),
            RequestPath         = fileStorageOpts.UrlBasePath,
            ContentTypeProvider = contentTypeProvider,
            OnPrepareResponse   = ctx =>
                ctx.Context.Response.Headers.CacheControl = "public, max-age=3600",
        });
    }

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
    app.MapHub<OrderUpdatesHub>("/hubs/orders");

    // ── Hangfire dashboard ────────────────────────────────────────────────────
    // Anonymous access is allowed by default in Development for ergonomics;
    // production environments require an authenticated SuperAdmin.
    var dashboardAllowAnonymous =
        builder.Configuration.GetValue<bool?>("Hangfire:Dashboard:AllowAnonymous")
        ?? app.Environment.IsDevelopment();

    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new SuperAdminDashboardAuthorizationFilter(dashboardAllowAnonymous) },
        DashboardTitle = "DineOS Background Jobs",
    });

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
