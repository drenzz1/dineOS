using DineOS.Application.Interfaces.Repositories;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Authentication;
using DineOS.Application.Options;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Persistence.Interceptors;
using DineOS.Infrastructure.Repositories;
using DineOS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace DineOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ICurrentTenantService, HttpContextTenantService>();
        services.AddSingleton<IPinHasher, PinHasher>();
        services.Configure<KeycloakOptions>(configuration.GetSection(KeycloakOptions.SectionName));
        services.AddHttpClient(KeycloakAuthService.HttpClientName);
        services.AddScoped<IKeycloakAuthService, KeycloakAuthService>();

        services.AddScoped<AuditInterceptor>();
        services.AddScoped<SoftDeleteInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
            options.AddInterceptors(
                sp.GetRequiredService<AuditInterceptor>(),
                sp.GetRequiredService<SoftDeleteInterceptor>()
            );
        });

        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IHealthService, HealthService>();

        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<IAdminRestaurantService, AdminRestaurantService>();
        services.AddScoped<IMenuService, MenuService>();
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IShiftNoteService, ShiftNoteService>();
        services.AddScoped<IKitchenService, KitchenService>();
        services.AddScoped<IReportsService, ReportsService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IRestaurantService, RestaurantService>();
        services.AddScoped<IShiftService, ShiftService>();

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var connString = configuration["Redis:ConnectionString"] ?? "localhost:6379";
            try
            {
                var options = ConfigurationOptions.Parse(connString);
                options.AbortOnConnectFail = false;
                return ConnectionMultiplexer.Connect(options);
            }
            catch (Exception ex)
            {
                var logger = sp.GetRequiredService<ILogger<ConnectionMultiplexer>>();
                logger.LogWarning(ex, "Redis unavailable at {ConnectionString}. Token blacklisting will not function.", connString);
                return ConnectionMultiplexer.Connect("localhost:6379,abortConnect=false");
            }
        });
        services.AddSingleton<ITokenBlacklistService, TokenBlacklistService>();
        services.AddSingleton<ICacheService, RedisCacheService>();

        // ── AI (Anthropic) ─────────────────────────────────────────────────
        services.Configure<AnthropicOptions>(configuration.GetSection(AnthropicOptions.SectionName));
        services.AddHttpClient<IAiClient, AnthropicAiClient>(AnthropicAiClient.HttpClientName, (sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AnthropicOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout     = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("anthropic-version", opts.ApiVersion);
            if (!string.IsNullOrWhiteSpace(opts.ApiKey))
                client.DefaultRequestHeaders.Add("x-api-key", opts.ApiKey);
        });
        services.AddScoped<IAiMenuService, AiMenuService>();

        return services;
    }
}
