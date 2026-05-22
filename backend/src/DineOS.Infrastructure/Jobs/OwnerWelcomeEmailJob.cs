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
    IOptions<SignupOptions> signupOptions,
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

        // Send the owner to the dineOS frontend's first-login flow rather
        // than Keycloak's account console. That FE page calls
        // /api/v1/auth/first-login-password-change which rotates the temp
        // password AND signs the user straight into the dineOS app. Landing
        // them in the Keycloak account console (the previous behaviour)
        // rotated the password but never logged them into dineOS, which is
        // why "the credentials don't work" from the user's perspective.
        //
        // Uri.EscapeDataString (not HttpUtility.UrlEncode) — the latter
        // encodes spaces as '+', which the query parser on the FE will
        // then treat as a literal '+' inside email addresses.
        //
        // Trailing-separator handling: a configured URL ending in '?' or
        // '&' is already opened for a query string, so append the param
        // directly without inserting another separator (avoids producing
        // "...?&email=…" or "...&&email=…").
        var firstLoginUrl = AppendEmailParam(
            signupOptions.Value.FirstLoginUrl.Trim(),
            tenant.OwnerEmail);

        var model = new OwnerWelcomeEmailModel(
            OwnerName:      tenant.OwnerName,
            RestaurantName: tenant.Name,
            Email:          tenant.OwnerEmail,
            TempPassword:   tempPassword,
            FirstLoginUrl:  firstLoginUrl);

        var html = await templates.RenderAsync("OwnerWelcome", model, ct);
        var text = $"""
                    Hi {tenant.OwnerName},

                    Your DineOS account for {tenant.Name} is ready.

                      Email:    {tenant.OwnerEmail}
                      Password: {tempPassword}

                    The password above is temporary and works only once. Open
                    the link below to set a permanent password and sign in:

                    {firstLoginUrl}
                    """;

        await emailSender.SendAsync(tenant.OwnerEmail, Subject, text, html, ct);

        logger.LogInformation(
            "Owner welcome email sent: TenantId={TenantId} Email={OwnerEmail}",
            tenant.Id, tenant.OwnerEmail);
    }

    internal static string AppendEmailParam(string baseUrl, string email)
    {
        var encoded = Uri.EscapeDataString(email);
        if (string.IsNullOrEmpty(baseUrl))
            return $"?email={encoded}";

        var last = baseUrl[^1];
        if (last == '?' || last == '&')
            return $"{baseUrl}email={encoded}";

        var separator = baseUrl.Contains('?') ? '&' : '?';
        return $"{baseUrl}{separator}email={encoded}";
    }
}
