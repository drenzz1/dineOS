using DineOS.Application.Interfaces.Messaging;
using DineOS.Application.Interfaces.Repositories;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Authentication;
using DineOS.Application.Options;
using DineOS.Infrastructure.Auth;
using DineOS.Infrastructure.Jobs;
using DineOS.Infrastructure.Messaging;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Persistence.Interceptors;
using DineOS.Infrastructure.Repositories;
using DineOS.Infrastructure.Services;
using Hangfire;
using Hangfire.PostgreSql;
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
        // Admin client caches the service-account token in-memory across
        // requests, so it must be a Singleton (all deps are Singleton-safe).
        services.AddHttpClient(KeycloakAdminClient.HttpClientName);
        services.AddSingleton<IKeycloakAdminClient, KeycloakAdminClient>();

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
        services.AddSingleton<IDatabaseMigrator, EfDatabaseMigrator>();

        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
        // BillingService is registered by both its interface and its concrete
        // type. SignupService (#204) depends on the concrete to reuse the
        // internal BuildCheckoutSessionAsync helper without leaking it onto
        // the public interface.
        services.AddScoped<BillingService>();
        services.AddScoped<IBillingService>(sp => sp.GetRequiredService<BillingService>());
        services.AddScoped<ISignupService, SignupService>();

        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
        services.AddScoped<OrderCreatedMessageHandler>();

        if (configuration.GetValue($"{RabbitMqOptions.SectionName}:Enabled", true))
        {
            services.AddHostedService<RabbitMqTopologyHostedService>();
            services.AddHostedService<RabbitMqOrderCreatedConsumer>();
        }

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

        // ── Email ──────────────────────────────────────────────────────────
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.Configure<EmailVerificationOptions>(
            configuration.GetSection(EmailVerificationOptions.SectionName));
        services.Configure<PaymentNotificationOptions>(
            configuration.GetSection(PaymentNotificationOptions.SectionName));
        services.AddScoped<IEmailSender, MailKitEmailSender>();
        services.AddSingleton<IEmailTemplateRenderer, RazorLightEmailTemplateRenderer>();
        services.AddScoped<IEmailVerificationService, EmailVerificationService>();

        // ── Hangfire ────────────────────────────────────────────────────────
        // Storage: PostgreSQL (reuses the app database). PrepareSchemaIfNecessary
        // creates the hangfire.* schema on first run.
        services.AddHangfire((sp, cfg) =>
        {
            cfg.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
               .UseSimpleAssemblyNameTypeSerializer()
               .UseRecommendedSerializerSettings()
               .UsePostgreSqlStorage(opt =>
               {
                   opt.UseNpgsqlConnection(configuration.GetConnectionString("DefaultConnection"));
               })
               .UseFilter(sp.GetRequiredService<DeadLetterEmailFilter>());
        });

        services.AddSingleton<DeadLetterEmailFilter>();
        services.AddHangfireServer(options =>
        {
            options.WorkerCount = Math.Max(1, Environment.ProcessorCount);
            options.Queues = new[] { "default" };
        });
        services.AddScoped<AccountVerificationEmailJob>();
        services.AddScoped<DailyPaymentSummaryJob>();
        services.AddScoped<OverduePaymentNotificationJob>();
        services.AddScoped<SubscriptionActivatedEmailJob>();
        services.AddScoped<SubscriptionCanceledEmailJob>();
        services.AddScoped<PaymentFailedEmailJob>();
        services.AddScoped<OwnerWelcomeEmailJob>();
        services.AddScoped<OwnerProvisioningJob>();
        services.AddScoped<DemoProvisioningJob>();
        services.AddScoped<DemoWelcomeEmailJob>();
        services.AddScoped<DemoCredentialsResendJob>();
        services.AddScoped<DemoCleanupJob>();
        services.AddHostedService<RecurringJobRegistrar>();

        // ── Demo access (#216) ─────────────────────────────────────────────
        services.Configure<DemoOptions>(configuration.GetSection(DemoOptions.SectionName));
        services.AddScoped<IDemoAccessService, DemoAccessService>();

        // ── AI (Anthropic) ─────────────────────────────────────────────────
        services.AddOptions<AiProviderOptions>()
            .Bind(configuration.GetSection(AiProviderOptions.SectionName))
            .Validate(options =>
                options.Provider is AiProviderOptions.Providers.Anthropic
                    or AiProviderOptions.Providers.OpenAI
                    or AiProviderOptions.Providers.Google,
                "Ai:Provider must be one of Anthropic, OpenAI, or Google.")
            .ValidateOnStart();

        services.AddOptions<AnthropicOptions>()
            .Bind(configuration.GetSection(AnthropicOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Model), "Anthropic:Model must be configured.");
        services.AddOptions<OpenAiOptions>()
            .Bind(configuration.GetSection(OpenAiOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Model), "OpenAI:Model must be configured.");
        services.AddOptions<GoogleAiOptions>()
            .Bind(configuration.GetSection(GoogleAiOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Model), "GoogleAI:Model must be configured.");

        services.AddHttpClient(AnthropicAiClient.HttpClientName, (sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AnthropicOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout     = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("anthropic-version", opts.ApiVersion);
            if (!string.IsNullOrWhiteSpace(opts.ApiKey))
                client.DefaultRequestHeaders.Add("x-api-key", opts.ApiKey);
        });
        services.AddHttpClient(OpenAiClient.HttpClientName, (sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenAiOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout     = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            if (!string.IsNullOrWhiteSpace(opts.ApiKey))
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {opts.ApiKey}");
        });
        services.AddHttpClient(GoogleAiClient.HttpClientName, (sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GoogleAiOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout     = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            if (!string.IsNullOrWhiteSpace(opts.ApiKey))
                client.DefaultRequestHeaders.Add("x-goog-api-key", opts.ApiKey);
        });
        services.AddScoped<IAiClient>(sp =>
        {
            var provider = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiProviderOptions>>().Value.Provider;
            var httpFactory = sp.GetRequiredService<IHttpClientFactory>();

            return provider switch
            {
                AiProviderOptions.Providers.OpenAI => new OpenAiClient(
                    httpFactory.CreateClient(OpenAiClient.HttpClientName),
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenAiOptions>>(),
                    sp.GetRequiredService<ILogger<OpenAiClient>>()),
                AiProviderOptions.Providers.Google => new GoogleAiClient(
                    httpFactory.CreateClient(GoogleAiClient.HttpClientName),
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GoogleAiOptions>>(),
                    sp.GetRequiredService<ILogger<GoogleAiClient>>()),
                _ => new AnthropicAiClient(
                    httpFactory.CreateClient(AnthropicAiClient.HttpClientName),
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AnthropicOptions>>(),
                    sp.GetRequiredService<ILogger<AnthropicAiClient>>()),
            };
        });
        services.AddScoped<IAiMenuService, AiMenuService>();

        return services;
    }
}
