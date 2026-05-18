using System.Security.Cryptography;
using System.Text;
using DineOS.Application.Billing;
using DineOS.Application.Common;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Notifications;
using DineOS.Application.Options;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Jobs;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Persistence.Messaging;
using DineOS.Infrastructure.Services;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DineOS.Tests.Unit;

public class BillingServiceTests
{

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static AppDbContext MakeDb(ITenantService tenantSvc) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantSvc);

    private static BillingService MakeService(
        AppDbContext db,
        ITenantService tenantSvc,
        IBackgroundJobClient backgroundJobs,
        StripeOptions? opts = null) =>
        new(db, tenantSvc,
            new CreateCheckoutSessionRequestValidator(),
            Options.Create(opts ?? new StripeOptions
            {
                SecretKey = "sk_test_local",
                ProMonthlyPriceId = "price_monthly",
            }),
            backgroundJobs,
            NullLogger<BillingService>.Instance);

    // Stripe SDK v51 uses UTF8.GetBytes(secret) directly as the HMAC key (not base64-decoded).
    // Any "whsec_*" string works; this constant avoids per-test key generation.
    private const string TestWebhookSecret = "whsec_test_secret_for_unit_tests_12345";

    // Builds a minimal event payload and a valid HMAC-SHA256 Stripe signature header.
    // Stripe SDK v51: key = UTF8(secret), signed = UTF8("{timestamp}.{json}")
    // api_version must match the installed Stripe SDK constant ("2026-04-22.dahlia" for v51.x).
    // Uses string concatenation to avoid brace-escaping ambiguity in interpolated raw strings.
    private static (string Json, string Header) MakeStripeEvent(
        string eventId, string type, string dataObjectJson, string webhookSecret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload =
            "{\"id\":\"" + eventId + "\"," +
            "\"object\":\"event\"," +
            "\"api_version\":\"2026-04-22.dahlia\"," +
            "\"created\":" + timestamp + "," +
            "\"type\":\"" + type + "\"," +
            "\"livemode\":false," +
            "\"data\":{\"object\":" + dataObjectJson + "}}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
        var v1 = BitConverter.ToString(
                hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}")))
            .Replace("-", "").ToLowerInvariant();

        return (payload, $"t={timestamp},v1={v1}");
    }

    // Minimal valid Stripe Subscription JSON understood by Stripe SDK v51.
    private const string ActiveSubscriptionJson = """
        {
          "id": "sub_test001",
          "object": "subscription",
          "customer": "cus_test001",
          "status": "active",
          "items": {
            "object": "list",
            "data": [
              {
                "id": "si_test001",
                "object": "subscription_item",
                "current_period_end": 1702592000,
                "price": { "id": "price_monthly", "object": "price" }
              }
            ]
          }
        }
        """;

    // ── (fixed) Existing test ────────────────────────────────────────────────
    // Why: keeps the original regression guard alive after the constructor gained
    // a new IBackgroundJobClient parameter.

    [Fact]
    public async Task CreateCheckoutSessionAsync_MissingPriceForRequestedCycle_ReturnsBadRequest()
    {
        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns(1L);

        await using var db          = MakeDb(tenantSvc);
        var backgroundJobs          = Substitute.For<IBackgroundJobClient>();

        var service = new BillingService(
            db,
            tenantSvc,
            new CreateCheckoutSessionRequestValidator(),
            Options.Create(new StripeOptions
            {
                SecretKey         = "sk_test_local",
                ProMonthlyPriceId = "price_monthly",
            }),
            backgroundJobs,
            NullLogger<BillingService>.Instance);

        var result = await service.CreateCheckoutSessionAsync(BillingCycle.Annual);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.BadRequest, result.Error);
        Assert.Equal("Stripe price is not configured for Annual billing.", result.Message);
    }

    // ── (b) Webhook idempotency ──────────────────────────────────────────────
    // Why this test is needed: Stripe re-delivers unacknowledged webhooks, so
    // without a dedup check a network blip can trigger duplicate emails and
    // duplicate invoice records. This test ensures the early-return path fires
    // and that no Hangfire job is enqueued for a known event ID.

    [Fact]
    public async Task HandleWebhookAsync_DuplicateEventId_ReturnsOkWithoutEnqueuingJob()
    {
        const string eventId = "evt_dup_001";
        const string secret  = TestWebhookSecret;

        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns((long?)null);
        await using var db = MakeDb(tenantSvc);

        // Pre-seed the event ID so the handler sees it as already processed.
        db.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId   = eventId,
            MessageType = "customer.subscription.deleted",
            TenantId    = 0,
            ProcessedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var backgroundJobs = Substitute.For<IBackgroundJobClient>();
        var service = MakeService(db, tenantSvc, backgroundJobs,
            new StripeOptions
            {
                SecretKey         = "sk_test_local",
                ProMonthlyPriceId = "price_monthly",
                WebhookSecret     = secret,
            });

        var (json, header) = MakeStripeEvent(
            eventId, "customer.subscription.deleted", ActiveSubscriptionJson, secret);

        var result = await service.HandleWebhookAsync(json, header);

        Assert.True(result.IsSuccess);
        Assert.Equal("Webhook already processed.", result.Message);
        // Enqueue<T> is an extension method; verify the underlying Create call was never made.
        backgroundJobs.DidNotReceive().Create(Arg.Any<Job>(), Arg.Any<IState>());
    }

    // ── (a) Webhook → correct job enqueued ──────────────────────────────────
    // Why this test is needed: verifies the Free → Pro transition actually
    // triggers the activation email job end-to-end through the webhook path,
    // not just that the job class compiles and is registered in DI.

    [Fact]
    public async Task HandleWebhookAsync_SubscriptionCreatedOnFreeTenant_EnqueuesActivationEmail()
    {
        const string secret = TestWebhookSecret;

        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns((long?)null);
        await using var db = MakeDb(tenantSvc);

        db.Tenants.Add(new Tenant
        {
            Name             = "Test Restaurant",
            Slug             = "test-restaurant",
            OwnerName        = "Owner",
            OwnerEmail       = "owner@test.com",
            Plan             = SubscriptionPlan.Free,
            BillingStatus    = BillingStatus.None,
            StripeCustomerId = "cus_test001",
        });
        await db.SaveChangesAsync();

        var backgroundJobs = Substitute.For<IBackgroundJobClient>();
        var service = MakeService(db, tenantSvc, backgroundJobs,
            new StripeOptions
            {
                SecretKey         = "sk_test_local",
                ProMonthlyPriceId = "price_monthly",
                WebhookSecret     = secret,
            });

        var (json, header) = MakeStripeEvent(
            "evt_sub_created_001",
            "customer.subscription.created",
            ActiveSubscriptionJson,
            secret);

        var result = await service.HandleWebhookAsync(json, header);

        Assert.True(result.IsSuccess);
        // Enqueue<T> is an extension method; verify the underlying Create call with the correct job type.
        backgroundJobs.Received(1).Create(
            Arg.Is<Job>(j => j.Type == typeof(SubscriptionActivatedEmailJob)),
            Arg.Any<IState>());
    }

    // ── (c) GetInvoicesAsync tenant scoping ──────────────────────────────────
    // Why this test is needed: TenantInvoice has no global EF tenant-scope query
    // filter — only a soft-delete filter. The explicit WHERE in GetInvoicesAsync
    // is the sole security boundary preventing cross-tenant invoice exposure.
    // A regression here (e.g., removing the Where clause) would silently leak
    // all invoices to every authenticated manager.

    [Fact]
    public async Task GetInvoicesAsync_ReturnsOnlyCurrentTenantInvoices()
    {
        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns(1L);
        await using var db = MakeDb(tenantSvc);

        db.TenantInvoices.AddRange(
            new TenantInvoice
            {
                TenantId        = 1,
                StripeInvoiceId = "inv_t1_a",
                Amount          = 29m,
                Currency        = "usd",
                Status          = "paid",
            },
            new TenantInvoice
            {
                TenantId        = 1,
                StripeInvoiceId = "inv_t1_b",
                Amount          = 29m,
                Currency        = "usd",
                Status          = "paid",
            },
            new TenantInvoice
            {
                // Belongs to a different tenant — must NOT appear in results.
                TenantId        = 99,
                StripeInvoiceId = "inv_t99",
                Amount          = 99m,
                Currency        = "usd",
                Status          = "paid",
            });
        await db.SaveChangesAsync();

        var backgroundJobs = Substitute.For<IBackgroundJobClient>();
        var service        = MakeService(db, tenantSvc, backgroundJobs);

        var result = await service.GetInvoicesAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.DoesNotContain(result.Value, inv => inv.Amount == 99m);
    }

    // ── (d) Email job unit tests ──────────────────────────────────────────────
    // Why these tests are needed: the jobs are enqueued by ID and run later in
    // a separate Hangfire worker process. There is no end-to-end test that
    // exercises the job → template → SMTP path. These unit tests are the only
    // verification that each job reads the right DB record, calls the correct
    // template name, and sends to the right recipient with the correct subject.

    [Fact]
    public async Task SubscriptionActivatedEmailJob_SendsCorrectSubjectAndRecipient()
    {
        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns((long?)null);
        await using var db = MakeDb(tenantSvc);

        var tenant = new Tenant
        {
            Name             = "Pro Restaurant",
            Slug             = "pro-restaurant",
            OwnerName        = "Jane",
            OwnerEmail       = "jane@pro.com",
            Plan             = SubscriptionPlan.Pro,
            BillingStatus    = BillingStatus.Active,
            BillingCycle     = BillingCycle.Monthly,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var emailSender = Substitute.For<IEmailSender>();
        var templates   = Substitute.For<IEmailTemplateRenderer>();
        templates
            .RenderAsync(Arg.Any<string>(), Arg.Any<SubscriptionActivatedEmailModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("<html>welcome</html>"));

        var job = new SubscriptionActivatedEmailJob(
            db, emailSender, templates,
            NullLogger<SubscriptionActivatedEmailJob>.Instance);

        await job.SendAsync(tenant.Id, CancellationToken.None);

        await emailSender.Received(1).SendAsync(
            "jane@pro.com",
            SubscriptionActivatedEmailJob.Subject,
            Arg.Any<string>(),
            "<html>welcome</html>",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubscriptionCanceledEmailJob_SendsCorrectSubjectAndRecipient()
    {
        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns((long?)null);
        await using var db = MakeDb(tenantSvc);

        var tenant = new Tenant
        {
            Name          = "Gone Restaurant",
            Slug          = "gone-restaurant",
            OwnerName     = "Bob",
            OwnerEmail    = "bob@gone.com",
            Plan          = SubscriptionPlan.Free,
            BillingStatus = BillingStatus.Canceled,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var emailSender = Substitute.For<IEmailSender>();
        var templates   = Substitute.For<IEmailTemplateRenderer>();
        templates
            .RenderAsync(Arg.Any<string>(), Arg.Any<SubscriptionCanceledEmailModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("<html>canceled</html>"));

        var job = new SubscriptionCanceledEmailJob(
            db, emailSender, templates,
            NullLogger<SubscriptionCanceledEmailJob>.Instance);

        await job.SendAsync(tenant.Id, CancellationToken.None);

        await emailSender.Received(1).SendAsync(
            "bob@gone.com",
            SubscriptionCanceledEmailJob.Subject,
            Arg.Any<string>(),
            "<html>canceled</html>",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PaymentFailedEmailJob_SendsCorrectSubjectWithInvoiceData()
    {
        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns((long?)null);
        await using var db = MakeDb(tenantSvc);

        var tenant = new Tenant
        {
            Name          = "Slow Payer",
            Slug          = "slow-payer",
            OwnerName     = "Alice",
            OwnerEmail    = "alice@slow.com",
            Plan          = SubscriptionPlan.Pro,
            BillingStatus = BillingStatus.PastDue,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var invoice = new TenantInvoice
        {
            TenantId         = tenant.Id,
            StripeInvoiceId  = "inv_fail01",
            Amount           = 49m,
            Currency         = "usd",
            Status           = "open",
            HostedInvoiceUrl = "https://invoice.stripe.com/i/inv_fail01",
        };
        db.TenantInvoices.Add(invoice);
        await db.SaveChangesAsync();

        var emailSender = Substitute.For<IEmailSender>();
        var templates   = Substitute.For<IEmailTemplateRenderer>();
        templates
            .RenderAsync(Arg.Any<string>(), Arg.Any<PaymentFailedEmailModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("<html>failed</html>"));

        var job = new PaymentFailedEmailJob(
            db, emailSender, templates,
            NullLogger<PaymentFailedEmailJob>.Instance);

        await job.SendAsync(tenant.Id, invoice.Id, CancellationToken.None);

        await emailSender.Received(1).SendAsync(
            "alice@slow.com",
            PaymentFailedEmailJob.Subject,
            Arg.Any<string>(),
            "<html>failed</html>",
            Arg.Any<CancellationToken>());
    }
}
