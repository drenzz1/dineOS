using DineOS.Application.Interfaces.Services;
using DineOS.Application.Notifications;
using DineOS.Application.Options;
using DineOS.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineOS.Infrastructure.Jobs;

/// <summary>
/// Hangfire job: issues a fresh account-verification code for a tenant's
/// owner email, renders the Razor template, and sends the email.
/// Retries are owned by Hangfire; permanent failure routes to the DLQ filter.
/// </summary>
public sealed class AccountVerificationEmailJob(
    AppDbContext db,
    IEmailVerificationService verificationService,
    IEmailSender emailSender,
    IEmailTemplateRenderer templates,
    IOptions<EmailVerificationOptions> verificationOptions,
    ILogger<AccountVerificationEmailJob> logger) : IEmailJob
{
    public const string Subject = "Verify your DineOS account";

    [AutomaticRetry(
        Attempts = 3,
        DelaysInSeconds = new[] { 10, 30, 90 },
        OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task SendAsync(long tenantId, CancellationToken ct)
    {
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.DeletedAt == null, ct);

        if (tenant is null)
        {
            logger.LogWarning(
                "Account verification email skipped — tenant not found. TenantId={TenantId}",
                tenantId);
            return;
        }

        if (tenant.OwnerEmailVerified)
        {
            logger.LogInformation(
                "Account verification email skipped — already verified. TenantId={TenantId}",
                tenantId);
            return;
        }

        var code = await verificationService.IssueAccountVerificationCodeAsync(tenantId, ct);

        var model = new AccountVerificationEmailModel(
            OwnerName:      tenant.OwnerName,
            RestaurantName: tenant.Name,
            Code:           code,
            CodeTtlMinutes: verificationOptions.Value.CodeTtlMinutes);

        var html = await templates.RenderAsync("AccountVerification", model, ct);
        var text = $"""
                    Hi {tenant.OwnerName},

                    Your verification code for {tenant.Name} on DineOS:

                        {code}

                    The code expires in {verificationOptions.Value.CodeTtlMinutes} minutes.
                    """;

        await emailSender.SendAsync(tenant.OwnerEmail, Subject, text, html, ct);
    }
}
