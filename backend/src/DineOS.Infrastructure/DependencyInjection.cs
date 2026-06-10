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
using DineOS.Infrastructure.Persistence.Seed;
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
        services.Configure<StaffSessionOptions>(configuration.GetSection(StaffSessionOptions.SectionName));
        services.AddScoped<IStaffSessionService, StaffSessionService>();
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
        // Signup:FirstLoginUrl is required — a hardcoded localhost fallback
        // would silently ship to non-dev environments and send freshly
        // provisioned owners to a dead link. Fail fast on startup instead.
        services.AddOptions<SignupOptions>()
            .Bind(configuration.GetSection(SignupOptions.SectionName))
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.FirstLoginUrl)
                     && Uri.TryCreate(o.FirstLoginUrl, UriKind.Absolute, out _),
                "Signup:FirstLoginUrl must be configured as an absolute URL (e.g. https://app.example.com/first-login).")
            .ValidateOnStart();
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

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var connString = configuration["Redis:ConnectionString"] ?? "localhost:6379";
            var options = ConfigurationOptions.Parse(connString);
            // AbortOnConnectFail=false returns a multiplexer that connects lazily
            // and retries the *configured* endpoint in the background, so a
            // transient/startup Redis outage does not throw here. We deliberately
            // do NOT fall back to a hardcoded localhost:6379 — inside Docker (or any
            // non-local deploy) that points at a non-existent server and would
            // silently drop a real password/SSL, masking the real misconfiguration.
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
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
        services.AddScoped<OwnerSecurityRemediationJob>();
        services.AddScoped<DemoProvisioningJob>();
        services.AddScoped<DemoWelcomeEmailJob>();
        services.AddScoped<DemoCredentialsResendJob>();
        services.AddScoped<DemoCleanupJob>();
        services.AddHostedService<RecurringJobRegistrar>();

        // ── Demo access (#216) ─────────────────────────────────────────────
        services.Configure<DemoOptions>(configuration.GetSection(DemoOptions.SectionName));
        services.AddScoped<IDemoAccessService, DemoAccessService>();
        services.AddSingleton<IDemoTenantSeeder, DemoTenantSeeder>();

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

        // ── Feature flags (Unleash) ────────────────────────────────────────
        // When Unleash is configured (Unleash:Enabled=true) flags resolve from the
        // Unleash server with background polling (runtime toggle, no redeploy);
        // otherwise a no-op provider returns each call's default, so dev/test/CI
        // behave exactly as if no flag system existed. Client construction is guarded
        // so a misconfigured/unreachable Unleash degrades to defaults, never a crash.
        services.Configure<UnleashOptions>(configuration.GetSection(UnleashOptions.SectionName));
        if (configuration.GetValue($"{UnleashOptions.SectionName}:Enabled", false))
        {
            services.AddSingleton<IFeatureFlags>(sp =>
            {
                var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<UnleashOptions>>().Value;
                var logger = sp.GetRequiredService<ILogger<UnleashFeatureFlags>>();
                try
                {
                    // Fully qualified: the Unleash namespace also defines an
                    // IHttpClientFactory that would clash with System.Net.Http's.
                    var settings = new Unleash.UnleashSettings
                    {
                        AppName              = opts.AppName,
                        UnleashApi           = new Uri(opts.ApiUrl),
                        FetchTogglesInterval = TimeSpan.FromSeconds(opts.FetchTogglesIntervalSeconds),
                        CustomHttpHeaders    = new Dictionary<string, string> { ["Authorization"] = opts.ApiToken },
                    };
                    return new UnleashFeatureFlags(new Unleash.DefaultUnleash(settings), logger);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to initialise Unleash; feature flags will use defaults.");
                    return new DefaultFeatureFlags();
                }
            });
        }
        else
        {
            services.AddSingleton<IFeatureFlags, DefaultFeatureFlags>();
        }

        // ── Incident triage (Alertmanager webhook) ─────────────────────────
        services.Configure<AlertWebhookOptions>(
            configuration.GetSection(AlertWebhookOptions.SectionName));
        services.AddScoped<IIncidentTriageService, IncidentTriageService>();

        // ── Slack notifications ────────────────────────────────────────────
        services.Configure<SlackOptions>(configuration.GetSection(SlackOptions.SectionName));
        services.AddHttpClient(SlackNotifier.HttpClientName, (sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SlackOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        });
        services.AddScoped<ISlackNotifier>(sp =>
            new SlackNotifier(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(SlackNotifier.HttpClientName),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SlackOptions>>(),
                sp.GetRequiredService<ILogger<SlackNotifier>>()));

        return services;
    }
}
