using DineOS.Application.Billing;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Jobs;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Persistence.Messaging;
using FluentValidation;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Stripe;
using Stripe.Checkout;

namespace DineOS.Infrastructure.Services;

public class BillingService(
    AppDbContext db,
    ITenantService tenantService,
    IValidator<CreateCheckoutSessionRequest> checkoutValidator,
    IOptions<StripeOptions> options,
    IBackgroundJobClient backgroundJobs,
    ILogger<BillingService> logger) : IBillingService
{
    private readonly StripeOptions _opts = options.Value;

    public async Task<ServiceResult<StripeRedirectDto>> CreateCheckoutSessionAsync(
        BillingCycle cycle,
        CancellationToken ct = default)
    {
        var validation = await checkoutValidator.ValidateAsync(new CreateCheckoutSessionRequest { Cycle = cycle }, ct);
        if (!validation.IsValid)
        {
            return ServiceResult<StripeRedirectDto>.ValidationFailed(
                "Validation failed",
                validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        if (!_opts.IsConfigured)
            return ServiceResult<StripeRedirectDto>.BadRequest("Stripe billing is not configured on this server.");

        var priceId = cycle switch
        {
            BillingCycle.Monthly => _opts.ProMonthlyPriceId,
            BillingCycle.Annual  => _opts.ProAnnualPriceId,
            _ => throw new InvalidOperationException("Unsupported billing cycle.")
        };
        if (string.IsNullOrWhiteSpace(priceId))
            return ServiceResult<StripeRedirectDto>.BadRequest($"Stripe price is not configured for {cycle} billing.");

        if (tenantService.TenantId is not { } tenantId)
            return ServiceResult<StripeRedirectDto>.BadRequest("Tenant context is required.");

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null)
            return ServiceResult<StripeRedirectDto>.NotFound($"Tenant {tenantId} not found.");

        var sessionResult = await BuildCheckoutSessionAsync(tenant, cycle, ct);
        if (!sessionResult.IsSuccess)
            return ServiceResult<StripeRedirectDto>.BadRequest(sessionResult.Message ?? "Failed to create checkout session.");

        return ServiceResult<StripeRedirectDto>.Ok(new StripeRedirectDto { Url = sessionResult.Value!.Url });
    }

    /// <summary>
    /// Creates a Stripe Checkout session for the given tenant. Used by both the
    /// authenticated billing flow and the public signup flow (#204). Caller is
    /// responsible for persisting any returned IDs (the tenant entity is
    /// updated in-place but NOT saved here, except for the lazy customer-id
    /// creation which is its own atomic save).
    /// </summary>
    internal async Task<ServiceResult<Session>> BuildCheckoutSessionAsync(
        Tenant tenant,
        BillingCycle cycle,
        CancellationToken ct)
    {
        if (!_opts.IsConfigured)
            return ServiceResult<Session>.BadRequest("Stripe billing is not configured on this server.");

        var priceId = cycle switch
        {
            BillingCycle.Monthly => _opts.ProMonthlyPriceId,
            BillingCycle.Annual  => _opts.ProAnnualPriceId,
            _ => throw new InvalidOperationException("Unsupported billing cycle.")
        };

        if (string.IsNullOrWhiteSpace(priceId))
            return ServiceResult<Session>.BadRequest($"Stripe price is not configured for {cycle} billing.");

        StripeConfiguration.ApiKey = _opts.SecretKey;

        var customerId = tenant.StripeCustomerId;
        if (string.IsNullOrWhiteSpace(customerId))
        {
            var customer = await new CustomerService().CreateAsync(new CustomerCreateOptions
            {
                Email = tenant.OwnerEmail,
                Name  = tenant.Name,
                Metadata = new Dictionary<string, string> { ["tenantId"] = tenant.Id.ToString() }
            }, cancellationToken: ct);
            customerId = customer.Id;
            tenant.StripeCustomerId = customerId;
            await db.SaveChangesAsync(ct);
        }

        var session = await new SessionService().CreateAsync(new SessionCreateOptions
        {
            Mode = "subscription",
            Customer = customerId,
            LineItems = new List<SessionLineItemOptions>
            {
                new() { Price = priceId, Quantity = 1 }
            },
            SuccessUrl = _opts.CheckoutSuccessUrl,
            CancelUrl  = _opts.CheckoutCancelUrl,
            Locale     = "auto",
            ClientReferenceId = tenant.Id.ToString(),
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["tenantId"] = tenant.Id.ToString(),
                    ["cycle"]    = cycle.ToString()
                }
            }
        }, cancellationToken: ct);

        logger.LogInformation(
            "Stripe checkout session created: TenantId={TenantId} SessionId={SessionId} Cycle={Cycle}",
            tenant.Id, session.Id, cycle);

        return ServiceResult<Session>.Ok(session);
    }

    public async Task<ServiceResult<StripeRedirectDto>> CreatePortalSessionAsync(CancellationToken ct = default)
    {
        if (!_opts.IsConfigured)
            return ServiceResult<StripeRedirectDto>.BadRequest("Stripe billing is not configured on this server.");

        if (tenantService.TenantId is not { } tenantId)
            return ServiceResult<StripeRedirectDto>.BadRequest("Tenant context is required.");

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null)
            return ServiceResult<StripeRedirectDto>.NotFound($"Tenant {tenantId} not found.");

        if (string.IsNullOrWhiteSpace(tenant.StripeCustomerId))
            return ServiceResult<StripeRedirectDto>.UnprocessableEntity(
                "This tenant has no Stripe customer. Subscribe to a paid plan first.");

        StripeConfiguration.ApiKey = _opts.SecretKey;

        var session = await new Stripe.BillingPortal.SessionService().CreateAsync(
            new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer  = tenant.StripeCustomerId,
                ReturnUrl = _opts.PortalReturnUrl
            }, cancellationToken: ct);

        return ServiceResult<StripeRedirectDto>.Ok(new StripeRedirectDto { Url = session.Url });
    }

    public async Task<ServiceResult<BillingSubscriptionDto>> GetSubscriptionAsync(CancellationToken ct = default)
    {
        if (tenantService.TenantId is not { } tenantId)
            return ServiceResult<BillingSubscriptionDto>.BadRequest("Tenant context is required.");

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null)
            return ServiceResult<BillingSubscriptionDto>.NotFound($"Tenant {tenantId} not found.");

        return ServiceResult<BillingSubscriptionDto>.Ok(new BillingSubscriptionDto
        {
            Plan                 = tenant.Plan.ToString(),
            BillingStatus        = tenant.BillingStatus.ToString(),
            BillingCycle         = tenant.BillingCycle?.ToString(),
            CurrentPeriodEnd     = tenant.CurrentPeriodEnd,
            HasStripeSubscription = !string.IsNullOrWhiteSpace(tenant.StripeSubscriptionId),
        });
    }

    public async Task<ServiceResult<IReadOnlyList<TenantInvoiceDto>>> GetInvoicesAsync(CancellationToken ct = default)
    {
        if (tenantService.TenantId is not { } tenantId)
            return ServiceResult<IReadOnlyList<TenantInvoiceDto>>.BadRequest("Tenant context is required.");

        var invoices = await db.TenantInvoices
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new TenantInvoiceDto
            {
                Id               = i.Id,
                Amount           = i.Amount,
                Currency         = i.Currency,
                Status           = i.Status,
                PaidAt           = i.PaidAt,
                HostedInvoiceUrl = i.HostedInvoiceUrl,
                CreatedAt        = i.CreatedAt,
            })
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<TenantInvoiceDto>>.Ok(invoices);
    }

    public async Task<ServiceResult<string>> HandleWebhookAsync(
        string eventJson,
        string signature,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.WebhookSecret))
            return ServiceResult<string>.BadRequest("Stripe webhook secret is not configured.");

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(eventJson, signature, _opts.WebhookSecret);
        }
        catch (StripeException ex)
        {
            logger.LogWarning(ex, "Stripe webhook signature verification failed.");
            return ServiceResult<string>.BadRequest("Invalid webhook signature.");
        }

        // Idempotency — skip duplicate deliveries before any side effects.
        if (await db.ProcessedMessages.AnyAsync(m => m.MessageId == stripeEvent.Id, ct))
        {
            logger.LogInformation(
                "Duplicate Stripe webhook skipped: EventId={EventId} Type={Type}",
                stripeEvent.Id, stripeEvent.Type);
            return ServiceResult<string>.Ok(stripeEvent.Id, "Webhook already processed.");
        }

        db.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId   = stripeEvent.Id,
            MessageType = stripeEvent.Type,
            TenantId    = 0,
            ProcessedAt = DateTime.UtcNow,
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            logger.LogInformation(
                "Duplicate Stripe webhook skipped after insert conflict: EventId={EventId} Type={Type}",
                stripeEvent.Id, stripeEvent.Type);
            return ServiceResult<string>.Ok(stripeEvent.Id, "Webhook already processed.");
        }

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                await ApplyCheckoutCompletedAsync((Session)stripeEvent.Data.Object, ct);
                break;

            case "customer.subscription.created":
            case "customer.subscription.updated":
                await ApplySubscriptionAsync((Subscription)stripeEvent.Data.Object, ct);
                break;

            case "customer.subscription.deleted":
                await ApplySubscriptionDeletedAsync((Subscription)stripeEvent.Data.Object, ct);
                break;

            case "invoice.paid":
                await ApplyInvoiceAsync((Invoice)stripeEvent.Data.Object, false, ct);
                break;

            case "invoice.payment_failed":
                await ApplyInvoiceAsync((Invoice)stripeEvent.Data.Object, true, ct);
                break;

            default:
                logger.LogInformation("Stripe webhook ignored event type {Type}", stripeEvent.Type);
                break;
        }

        return ServiceResult<string>.Ok(stripeEvent.Id, "Webhook handled.");
    }

    private async Task ApplyCheckoutCompletedAsync(Session session, CancellationToken ct)
    {
        // Public signup flow (#204): client_reference_id is the pending tenant id.
        // For the authenticated upgrade flow, the subscription webhook handles
        // state transitions, so this branch is a no-op there.
        if (!long.TryParse(session.ClientReferenceId, out var tenantId))
        {
            logger.LogWarning(
                "Stripe checkout.session.completed missing or invalid client_reference_id: SessionId={SessionId}",
                session.Id);
            return;
        }

        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null)
        {
            logger.LogWarning(
                "Stripe checkout.session.completed for unknown tenant: TenantId={TenantId} SessionId={SessionId}",
                tenantId, session.Id);
            return;
        }

        if (tenant.BillingStatus != BillingStatus.Incomplete)
        {
            logger.LogInformation(
                "Stripe checkout.session.completed ignored — tenant already provisioned: TenantId={TenantId} Status={Status}",
                tenant.Id, tenant.BillingStatus);
            return;
        }

        tenant.BillingStatus       = BillingStatus.Active;
        tenant.Plan                = SubscriptionPlan.Pro;
        tenant.StripeCustomerId    = session.CustomerId    ?? tenant.StripeCustomerId;
        tenant.StripeSubscriptionId = session.SubscriptionId ?? tenant.StripeSubscriptionId;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Public signup completed: TenantId={TenantId} SessionId={SessionId} CustomerId={CustomerId} SubscriptionId={SubscriptionId}",
            tenant.Id, session.Id, tenant.StripeCustomerId, tenant.StripeSubscriptionId);

        // TODO #TBD-3: trigger owner Keycloak provisioning (TenantPaymentCompleted event).
    }

    private async Task ApplySubscriptionAsync(Subscription sub, CancellationToken ct)
    {
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.StripeCustomerId == sub.CustomerId, ct);
        if (tenant is null)
        {
            logger.LogWarning("Stripe subscription event for unknown customer {CustomerId}", sub.CustomerId);
            return;
        }

        var wasAlreadyPro = tenant.Plan == SubscriptionPlan.Pro;

        tenant.StripeSubscriptionId = sub.Id;
        tenant.BillingStatus        = MapStatus(sub.Status);
        tenant.CurrentPeriodEnd     = sub.Items?.Data.FirstOrDefault()?.CurrentPeriodEnd;
        tenant.BillingCycle         = InferCycle(sub);
        tenant.Plan                 = tenant.BillingStatus is BillingStatus.Active or BillingStatus.Trialing
            ? SubscriptionPlan.Pro
            : SubscriptionPlan.Free;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Stripe subscription applied: TenantId={TenantId} Status={Status} Cycle={Cycle}",
            tenant.Id, tenant.BillingStatus, tenant.BillingCycle);

        if (!wasAlreadyPro && tenant.Plan == SubscriptionPlan.Pro)
        {
            backgroundJobs.Enqueue<SubscriptionActivatedEmailJob>(
                job => job.SendAsync(tenant.Id, CancellationToken.None));
            logger.LogInformation(
                "Subscription activated email enqueued: TenantId={TenantId}", tenant.Id);
        }
    }

    private async Task ApplySubscriptionDeletedAsync(Subscription sub, CancellationToken ct)
    {
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.StripeCustomerId == sub.CustomerId, ct);
        if (tenant is null) return;

        tenant.BillingStatus        = BillingStatus.Canceled;
        tenant.StripeSubscriptionId = null;
        tenant.CurrentPeriodEnd     = null;
        tenant.BillingCycle         = null;
        tenant.Plan                 = SubscriptionPlan.Free;

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Stripe subscription canceled for TenantId={TenantId}", tenant.Id);

        backgroundJobs.Enqueue<SubscriptionCanceledEmailJob>(
            job => job.SendAsync(tenant.Id, CancellationToken.None));
        logger.LogInformation(
            "Subscription canceled email enqueued: TenantId={TenantId}", tenant.Id);
    }

    private async Task ApplyInvoiceAsync(Invoice invoice, bool isPaymentFailed, CancellationToken ct)
    {
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.StripeCustomerId == invoice.CustomerId, ct);
        if (tenant is null)
        {
            logger.LogWarning("Stripe invoice event for unknown customer {CustomerId}", invoice.CustomerId);
            return;
        }

        var existing = await db.TenantInvoices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.StripeInvoiceId == invoice.Id, ct);

        TenantInvoice invoiceRecord;
        if (existing is null)
        {
            invoiceRecord = new TenantInvoice
            {
                TenantId         = tenant.Id,
                StripeInvoiceId  = invoice.Id,
                Amount           = invoice.AmountPaid / 100m,
                Currency         = invoice.Currency,
                Status           = invoice.Status,
                PaidAt           = invoice.Status == "paid" ? DateTime.UtcNow : null,
                HostedInvoiceUrl = invoice.HostedInvoiceUrl,
            };
            db.TenantInvoices.Add(invoiceRecord);
        }
        else
        {
            existing.Status           = invoice.Status;
            existing.PaidAt           = invoice.Status == "paid" ? existing.PaidAt ?? DateTime.UtcNow : existing.PaidAt;
            existing.HostedInvoiceUrl = invoice.HostedInvoiceUrl;
            invoiceRecord = existing;
        }

        if (isPaymentFailed)
            tenant.BillingStatus = BillingStatus.PastDue;

        // SaveChangesAsync before enqueue so invoiceRecord.Id is populated
        // (EF sets the identity value after the INSERT) and the job can read
        // fresh data from the database when it executes.
        await db.SaveChangesAsync(ct);

        if (isPaymentFailed)
        {
            backgroundJobs.Enqueue<PaymentFailedEmailJob>(
                job => job.SendAsync(tenant.Id, invoiceRecord.Id, CancellationToken.None));
            logger.LogInformation(
                "Payment failed email enqueued: TenantId={TenantId} InvoiceId={InvoiceId}",
                tenant.Id, invoiceRecord.Id);
        }
    }

    private static BillingStatus MapStatus(string stripeStatus) => stripeStatus switch
    {
        "trialing"           => BillingStatus.Trialing,
        "active"             => BillingStatus.Active,
        "past_due"           => BillingStatus.PastDue,
        "canceled"           => BillingStatus.Canceled,
        "incomplete"         => BillingStatus.Incomplete,
        "incomplete_expired" => BillingStatus.Canceled,
        "unpaid"             => BillingStatus.PastDue,
        _                    => BillingStatus.None,
    };

    private BillingCycle? InferCycle(Subscription sub)
    {
        var priceId = sub.Items?.Data.FirstOrDefault()?.Price?.Id;
        if (string.IsNullOrEmpty(priceId)) return null;
        if (priceId == _opts.ProMonthlyPriceId) return BillingCycle.Monthly;
        if (priceId == _opts.ProAnnualPriceId)  return BillingCycle.Annual;
        return null;
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
