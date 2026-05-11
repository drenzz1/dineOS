using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
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
        });
    }

    private sealed class NoOpOrderNotificationService : IOrderNotificationService
    {
        public Task BroadcastOrderCreatedAsync(long tenantId, OrderDto order, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task BroadcastOrderStatusChangedAsync(long tenantId, long orderId, string oldStatus, string newStatus, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _db.DisposeAsync();
        Dispose();
    }
}
