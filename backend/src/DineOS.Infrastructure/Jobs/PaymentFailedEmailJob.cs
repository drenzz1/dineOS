using DineOS.Application.Interfaces.Services;
using DineOS.Application.Notifications;
using DineOS.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Jobs;

/// <summary>
/// Hangfire job: sends a payment-failed alert email when Stripe reports that
/// an invoice could not be collected, prompting the owner to update their card.
/// </summary>
public sealed class PaymentFailedEmailJob(
    AppDbContext db,
    IEmailSender emailSender,
    IEmailTemplateRenderer templates,
    ILogger<PaymentFailedEmailJob> logger) : IEmailJob
{
    public const string Subject = "DineOS — payment failed";

    [AutomaticRetry(
        Attempts = 3,
        DelaysInSeconds = new[] { 10, 30, 90 },
        OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task SendAsync(long tenantId, long invoiceId, CancellationToken ct)
    {
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.DeletedAt == null, ct);

        if (tenant is null)
        {
            logger.LogWarning(
                "Payment failed email skipped — tenant not found. TenantId={TenantId}",
                tenantId);
            return;
        }

        var invoice = await db.TenantInvoices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

        if (invoice is null)
        {
            logger.LogWarning(
                "Payment failed email skipped — invoice not found. TenantId={TenantId} InvoiceId={InvoiceId}",
                tenantId, invoiceId);
            return;
        }

        var model = new PaymentFailedEmailModel(
            tenant.OwnerName,
            tenant.Name,
            invoice.Amount,
            invoice.Currency.ToUpperInvariant(),
            invoice.HostedInvoiceUrl);

        var html = await templates.RenderAsync("PaymentFailed", model, ct);
        var text = $"A payment of {invoice.Amount:C} for your dineOS subscription at {tenant.Name} failed. Please update your payment method.";

        await emailSender.SendAsync(tenant.OwnerEmail, Subject, text, html, ct);

        logger.LogInformation(
            "Payment failed email sent: TenantId={TenantId} InvoiceId={InvoiceId}",
            tenantId, invoiceId);
    }
}
