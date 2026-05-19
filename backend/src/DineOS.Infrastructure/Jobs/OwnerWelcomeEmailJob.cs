using DineOS.Application.Authentication;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Notifications;
using DineOS.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineOS.Infrastructure.Jobs;

/// <summary>
/// Hangfire job: sends the post-checkout welcome email to a tenant owner
/// containing the Keycloak temp password and a link to the account console.
/// Enqueued by <c>BillingService.ApplyCheckoutCompletedAsync</c> (#205).
/// </summary>
/// <remarks>
/// The temp password is serialized into Hangfire's Postgres job arguments.
/// That is acceptable because it is single-use and Keycloak forces a reset
/// on first login; job rows are short-lived under the existing retention.
/// </remarks>
public sealed class OwnerWelcomeEmailJob(
    AppDbContext db,
    IEmailSender emailSender,
    IEmailTemplateRenderer templates,
    IOptions<KeycloakOptions> keycloakOptions,
    ILogger<OwnerWelcomeEmailJob> logger) : IEmailJob
{
    public const string Subject = "Welcome to DineOS — set your password";

    [AutomaticRetry(
        Attempts = 3,
        DelaysInSeconds = new[] { 10, 30, 90 },
        OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task SendAsync(long tenantId, string tempPassword, CancellationToken ct)
    {
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.DeletedAt == null, ct);

        if (tenant is null)
        {
            logger.LogWarning(
                "Owner welcome email skipped — tenant not found. TenantId={TenantId}",
                tenantId);
            return;
        }

        var opts = keycloakOptions.Value;
        var publicUrl = (opts.PublicAuthServerUrl ?? opts.AuthServerUrl ?? string.Empty).TrimEnd('/');
        var accountUrl = $"{publicUrl}/realms/{opts.Realm}/account";

        var model = new OwnerWelcomeEmailModel(
            OwnerName:      tenant.OwnerName,
            RestaurantName: tenant.Name,
            Email:          tenant.OwnerEmail,
            TempPassword:   tempPassword,
            AccountUrl:     accountUrl);

        var html = await templates.RenderAsync("OwnerWelcome", model, ct);
        var text = $"""
                    Hi {tenant.OwnerName},

                    Your DineOS account for {tenant.Name} is ready.

                      Email:    {tenant.OwnerEmail}
                      Password: {tempPassword}

                    Set a new password on first sign-in: {accountUrl}
                    """;

        await emailSender.SendAsync(tenant.OwnerEmail, Subject, text, html, ct);

        logger.LogInformation(
            "Owner welcome email sent: TenantId={TenantId} Email={OwnerEmail}",
            tenant.Id, tenant.OwnerEmail);
    }
}
