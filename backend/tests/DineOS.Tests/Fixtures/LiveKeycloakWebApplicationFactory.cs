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
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Testcontainers.PostgreSql;

namespace DineOS.Tests.Fixtures;

public sealed class LiveKeycloakWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly KeycloakContainerFixture _keycloak;

    // Pre-built once in InitializeAsync and injected into JwtBearerOptions.ConfigurationManager.
    // JwtBearerHandler skips its own lazy fetch when ConfigurationManager is already set,
    // eliminating the silent-failure path that leaves zero signing keys and zero ValidIssuers.
    private ConfigurationManager<OpenIdConnectConfiguration>? _configManager;
    private OpenIdConnectConfiguration? _oidcConfig;

    private readonly string _uploadsRoot =
        Path.Combine(Path.GetTempPath(), $"dineos-uploads-live-{Guid.NewGuid():N}");

    public string UploadsRoot => _uploadsRoot;

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("dineos_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public LiveKeycloakWebApplicationFactory(KeycloakContainerFixture keycloak)
    {
        _keycloak = keycloak;
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _db.StartAsync();
        // Keycloak is already running — started by the collection-scoped KeycloakContainerFixture.

        // Build the OIDC ConfigurationManager once and fetch the config eagerly.
        // PostConfigure injects this manager directly into JwtBearerOptions so the handler
        // never enters its lazy-creation path (which silently fails on some environments,
        // leaving zero signing keys and zero ValidIssuers → 401 for every bearer token).
        _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{_keycloak.Authority}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = false });
        _oidcConfig = await _configManager.GetConfigurationAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // appsettings.Development.json is NOT loaded in the Testing environment, so every
        // Keycloak key that Program.cs consumes must be injected explicitly here.
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{FileStorageOptions.SectionName}:RootPath"] = _uploadsRoot,
                ["ConnectionStrings:DefaultConnection"]        = _db.GetConnectionString(),

                // Point the JwtBearer middleware at the Testcontainer realm.
                // Audience "dineos-api" matches the dineos-api-audience protocol mapper in
                // realm-export.json (included.client.audience = "dineos-api").
                // RequireHttpsMetadata = false because the Testcontainer exposes plain HTTP;
                // no BackchannelHttpHandler override is needed for plain-HTTP connections.
                ["Keycloak:Authority"]           = _keycloak.Authority,
                ["Keycloak:MetadataAddress"]     = $"{_keycloak.Authority}/.well-known/openid-configuration",
                ["Keycloak:Audience"]            = "dineos-api",
                ["Keycloak:Realm"]               = _keycloak.Realm,
                ["Keycloak:ClientId"]            = "dineos-frontend",
                ["Keycloak:RequireHttpsMetadata"] = "false",

                // Signup:FirstLoginUrl is bound with ValidateOnStart in
                // Infrastructure/DependencyInjection.cs — required for the
                // host to boot under the "Testing" environment.
                [$"{SignupOptions.SectionName}:FirstLoginUrl"] = "http://localhost:3000/first-login",
            }));

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_db.GetConnectionString()));

            services.AddControllers()
                .AddApplicationPart(typeof(CustomWebApplicationFactory).Assembly);

            var notifDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IOrderNotificationService));
            if (notifDescriptor is not null)
                services.Remove(notifDescriptor);
            services.AddSingleton<IOrderNotificationService, NoOpOrderNotificationService>();

            var cacheDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ICacheService));
            if (cacheDescriptor is not null)
                services.Remove(cacheDescriptor);
            services.AddSingleton<ICacheService, NullCacheService>();

            // Program.cs reads builder.Configuration before WebApplicationFactory's
            // ConfigureAppConfiguration sources are available, so keycloakOptions in the
            // AddJwtBearer closure captures null Authority/Audience. PostConfigure runs
            // after all Configure callbacks and can safely override those nulls.
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                opts =>
                {
                    opts.Authority            = _keycloak.Authority;
                    opts.Audience             = "dineos-api";
                    opts.MetadataAddress      = $"{_keycloak.Authority}/.well-known/openid-configuration";
                    opts.RequireHttpsMetadata = false;

                    // Hand the pre-built manager to the handler so it never enters the lazy-
                    // creation path. The manager already holds the cached config, so no HTTP
                    // request is made at validation time.
                    opts.ConfigurationManager = _configManager;

                    // Replace the entire TVP so no stale ValidIssuer/ValidAudience
                    // from Program.cs can cause validation to reject the real Keycloak token.
                    // Issuer and audience are already validated transitively (the token comes
                    // straight from the Testcontainer Keycloak); cryptographic signature check
                    // is what matters for test coverage of RBAC rules.
                    if (_oidcConfig is { } cfg)
                    {
                        opts.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                        {
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKeys        = cfg.SigningKeys,
                            ValidateIssuer           = false,
                            ValidateAudience         = false,
                            ValidateLifetime         = true,
                        };
                    }
                });

            var blacklistDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ITokenBlacklistService));
            if (blacklistDescriptor is not null)
                services.Remove(blacklistDescriptor);
            services.AddSingleton<ITokenBlacklistService, NullTokenBlacklistService>();
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (Directory.Exists(_uploadsRoot))
            Directory.Delete(_uploadsRoot, recursive: true);

        await _db.DisposeAsync();
        Dispose();
    }

    private sealed class NoOpOrderNotificationService : IOrderNotificationService
    {
        public Task BroadcastOrderCreatedAsync(
            long tenantId, OrderDto order, CancellationToken ct = default) => Task.CompletedTask;

        public Task BroadcastOrderStatusChangedAsync(
            long tenantId, long orderId, string oldStatus, string newStatus,
            CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class NullCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) =>
            Task.FromResult(default(T?));

        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<T> GetOrSetAsync<T>(
            string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl,
            CancellationToken ct = default) => factory(ct);
    }

    private sealed class NullTokenBlacklistService : ITokenBlacklistService
    {
        public Task BlacklistAsync(string jti, TimeSpan ttl) => Task.CompletedTask;
        public Task<bool> IsBlacklistedAsync(string jti) => Task.FromResult(false);
    }
}
