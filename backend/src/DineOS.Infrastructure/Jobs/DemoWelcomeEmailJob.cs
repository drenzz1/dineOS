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
/// Sends the demo access welcome email (#216). One template, two subject
/// variants distinguished by <paramref name="isReissue"/>.
/// </summary>
public sealed class DemoWelcomeEmailJob(
    AppDbContext db,
    IEmailSender emailSender,
    IEmailTemplateRenderer templates,
    IOptions<DemoOptions> demoOptions,
    ILogger<DemoWelcomeEmailJob> logger) : IEmailJob
{
    public const string FirstTimeSubject = "Your dineOS demo is ready";
    public const string ReissueSubject   = "Your dineOS demo credentials";

    [AutomaticRetry(
        Attempts = 3,
        DelaysInSeconds = new[] { 10, 30, 90 },
        OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task SendAsync(long demoUserId, string tempPassword, bool isReissue, CancellationToken ct)
    {
        var demoUser = await db.DemoUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == demoUserId && d.DeletedAt == null, ct);

        if (demoUser is null)
        {
            logger.LogWarning(
                "Demo welcome email skipped — DemoUser not found. DemoUserId={DemoUserId}",
                demoUserId);
            return;
        }

        var opts = demoOptions.Value;

        // Prefer the user's own isolated demo tenant; fall back to the shared
        // slug for users provisioned before per-user tenants were introduced.
        var tenant = demoUser.TenantId is not null
            ? await db.Tenants.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == demoUser.TenantId, ct)
            : await db.Tenants.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(t => t.Slug == opts.TenantSlug && t.DeletedAt == null, ct);

        var model = new DemoWelcomeEmailModel(
            Email:          demoUser.Email,
            TempPassword:   tempPassword,
            LoginUrl:       opts.LoginUrl,
            ExpiresAt:      demoUser.ExpiresAt,
            DemoTenantName: tenant?.Name ?? "the dineOS demo",
            IsReissue:      isReissue);

        var html = await templates.RenderAsync("DemoWelcome", model, ct);
        var text = $"""
                    Your dineOS demo is ready.

                      Email:    {demoUser.Email}
                      Password: {tempPassword}

                    Sign in: {opts.LoginUrl}
                    Expires: {demoUser.ExpiresAt:yyyy-MM-dd}
                    """;

        var subject = isReissue ? ReissueSubject : FirstTimeSubject;
        await emailSender.SendAsync(demoUser.Email, subject, text, html, ct);

        demoUser.LastEmailSentAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Demo welcome email sent. DemoUserId={DemoUserId} Email={Email} IsReissue={IsReissue}",
            demoUser.Id, demoUser.Email, isReissue);
    }
}
