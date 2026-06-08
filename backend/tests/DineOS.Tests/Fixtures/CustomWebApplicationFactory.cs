using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Testcontainers.PostgreSql;

namespace DineOS.Tests.Fixtures;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Shared with tests so they can sign tokens with the same key the app validates
    internal const string TestJwtSecret = "integration-test-secret-key-min-32-chars!!";

    // Isolated temp directory for file-upload tests — cleaned up in DisposeAsync
    private readonly string _uploadsRoot =
        Path.Combine(Path.GetTempPath(), $"dineos-uploads-{Guid.NewGuid():N}");

    public string UploadsRoot => _uploadsRoot;

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("dineos_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _db.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Point FileStorage:RootPath at the temp directory so upload tests use a
        // real writable path without depending on /app/uploads existing in CI.
        //
        // Also redirect the default connection string at the Testcontainers DB
        // so anything wired off `IConfiguration.GetConnectionString("DefaultConnection")`
        // — most importantly Hangfire's storage — uses the same Postgres as EF.
        // `_db.GetConnectionString()` is read lazily when the host is built, which
        // happens after `InitializeAsync` has started the container.
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{FileStorageOptions.SectionName}:RootPath"] = _uploadsRoot,
                ["RabbitMq:Enabled"] = "false",
                ["ConnectionStrings:DefaultConnection"]       = _db.GetConnectionString(),
                // Signup:FirstLoginUrl is bound with ValidateOnStart in
                // Infrastructure/DependencyInjection.cs — without a value
                // here the test host fails to boot under the "Testing"
                // environment because appsettings.json deliberately ships
                // it empty (no silent fallback to localhost in prod).
                [$"{SignupOptions.SectionName}:FirstLoginUrl"] = "http://localhost:3000/first-login",
            }));

        builder.ConfigureServices(services =>
        {
            // Replace real DB with the Testcontainer connection
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_db.GetConnectionString()));

            // Override Keycloak OIDC with a local symmetric key so tests need no Keycloak instance
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null!;
                options.MetadataAddress = null!;
                options.ConfigurationManager = null;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(TestJwtSecret)),
                    ValidateIssuer = false,
                    ValidateAudience = false
                };
            });

            // Register test-only controllers defined in this assembly
            services.AddControllers()
                .AddApplicationPart(typeof(CustomWebApplicationFactory).Assembly);

            // Replace SignalR-backed notification service with a no-op — Redis is not
            // available in CI and notifications are not under test here.
            var notifDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IOrderNotificationService));
            if (notifDescriptor is not null)
                services.Remove(notifDescriptor);
            services.AddSingleton<IOrderNotificationService, NoOpOrderNotificationService>();

            // Replace Redis-backed cache with a no-op — menu (and other) service tests
            // must not depend on a live Redis instance.
            var cacheDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ICacheService));
            if (cacheDescriptor is not null)
                services.Remove(cacheDescriptor);
            services.AddSingleton<ICacheService, NullCacheService>();
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (Directory.Exists(_uploadsRoot))
            Directory.Delete(_uploadsRoot, recursive: true);

        // Dispose the web host (and any WithWebHostBuilder-derived hosts) ASYNCHRONOUSLY
        // and BEFORE the Postgres container. The app registers IAsyncDisposable-only
        // services (e.g. NpgsqlDataSource, Hangfire); a synchronous Dispose() throws
        // InvalidOperationException, which xUnit reports as a collection-cleanup failure
        // (non-zero exit even though every test passed). Tearing Postgres down first would
        // also break those services' shutdown.
        await ((IAsyncDisposable)this).DisposeAsync();
        await _db.DisposeAsync();
    }

    private sealed class NoOpOrderNotificationService : IOrderNotificationService
    {
        public Task BroadcastOrderCreatedAsync(long tenantId, OrderDto order, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task BroadcastOrderStatusChangedAsync(long tenantId, long orderId, string oldStatus, string newStatus, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class NullCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) =>
            Task.FromResult(default(T?));

        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken ct = default) =>
            Task.CompletedTask;

        // Always miss — factory is always called, caching is a no-op in tests
        public Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct = default) =>
            factory(ct);
    }
}
