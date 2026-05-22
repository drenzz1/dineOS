using DineOS.Application.Interfaces.Services;
using DineOS.Application.Notifications;
using DineOS.Application.Options;
using DineOS.Domain.Entities;
using DineOS.Infrastructure.Jobs;
using DineOS.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DineOS.Tests.Unit;

/// <summary>
/// Regression tests for #205: the welcome email must point owners at the
/// dineOS first-login page (configured via <see cref="SignupOptions"/>),
/// not the Keycloak account console, and the owner email must be
/// URL-encoded via <see cref="Uri.EscapeDataString"/> so '+'-containing
/// addresses survive the query-string round trip.
/// </summary>
public class OwnerWelcomeEmailJobTests
{
    private static (
        OwnerWelcomeEmailJob job,
        AppDbContext db,
        IEmailSender sender,
        IEmailTemplateRenderer templates)
        CreateSut(string firstLoginUrl)
    {
        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns((long?)null);

        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantSvc);

        var sender    = Substitute.For<IEmailSender>();
        var templates = Substitute.For<IEmailTemplateRenderer>();

        templates.RenderAsync(Arg.Any<string>(), Arg.Any<OwnerWelcomeEmailModel>(), Arg.Any<CancellationToken>())
                 .Returns("<html>welcome</html>");

        var options = Options.Create(new SignupOptions { FirstLoginUrl = firstLoginUrl });

        var job = new OwnerWelcomeEmailJob(
            db, sender, templates, options,
            NullLogger<OwnerWelcomeEmailJob>.Instance);

        return (job, db, sender, templates);
    }

    private static Tenant SeedTenant(AppDbContext db, string ownerEmail = "jane@example.com")
    {
        var tenant = new Tenant
        {
            Name       = "Olio & Sale",
            Slug       = "olio-and-sale",
            OwnerName  = "Jane Doe",
            OwnerEmail = ownerEmail,
            Phone      = "1",
            City       = "C",
            IsActive   = true,
        };
        db.Tenants.Add(tenant);
        db.SaveChanges();
        return tenant;
    }

    [Fact]
    public async Task SendAsync_RendersFirstLoginUrlFromConfigWithUrlEncodedEmail()
    {
        var (job, db, _, templates) = CreateSut("https://app.example.com/first-login");
        var tenant = SeedTenant(db, ownerEmail: "owner+tag@example.com");

        await job.SendAsync(tenant.Id, "TempPass!23", CancellationToken.None);

        await templates.Received(1).RenderAsync(
            "OwnerWelcome",
            Arg.Is<OwnerWelcomeEmailModel>(m =>
                m.FirstLoginUrl == "https://app.example.com/first-login?email=owner%2Btag%40example.com"
                && m.Email == "owner+tag@example.com"
                && m.TempPassword == "TempPass!23"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_AppendsEmailQueryWhenBaseUrlAlreadyHasQueryString()
    {
        var (job, db, _, templates) = CreateSut("https://app.example.com/first-login?utm=email");
        var tenant = SeedTenant(db, ownerEmail: "jane@example.com");

        await job.SendAsync(tenant.Id, "TempPass!23", CancellationToken.None);

        await templates.Received(1).RenderAsync(
            "OwnerWelcome",
            Arg.Is<OwnerWelcomeEmailModel>(m =>
                m.FirstLoginUrl == "https://app.example.com/first-login?utm=email&email=jane%40example.com"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("https://app.example.com/first-login?",  "https://app.example.com/first-login?email=jane%40example.com")]
    [InlineData("https://app.example.com/first-login?utm=email&", "https://app.example.com/first-login?utm=email&email=jane%40example.com")]
    public async Task SendAsync_HandlesTrailingSeparatorWithoutDoublingIt(
        string configuredUrl, string expectedUrl)
    {
        var (job, db, _, templates) = CreateSut(configuredUrl);
        var tenant = SeedTenant(db, ownerEmail: "jane@example.com");

        await job.SendAsync(tenant.Id, "TempPass!23", CancellationToken.None);

        await templates.Received(1).RenderAsync(
            "OwnerWelcome",
            Arg.Is<OwnerWelcomeEmailModel>(m => m.FirstLoginUrl == expectedUrl),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_PlainTextBodyContainsFirstLoginUrl()
    {
        var (job, db, sender, _) = CreateSut("https://app.example.com/first-login");
        var tenant = SeedTenant(db, ownerEmail: "owner+tag@example.com");

        await job.SendAsync(tenant.Id, "TempPass!23", CancellationToken.None);

        await sender.Received(1).SendAsync(
            "owner+tag@example.com",
            OwnerWelcomeEmailJob.Subject,
            Arg.Is<string>(text =>
                text.Contains("https://app.example.com/first-login?email=owner%2Btag%40example.com")
                && text.Contains("TempPass!23")),
            "<html>welcome</html>",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_TenantMissing_NoOps()
    {
        var (job, _, sender, templates) = CreateSut("https://app.example.com/first-login");

        await job.SendAsync(9999, "TempPass!23", CancellationToken.None);

        await templates.DidNotReceiveWithAnyArgs().RenderAsync<OwnerWelcomeEmailModel>(default!, default!, default);
        await sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public void SendAsync_HasAutomaticRetryAttribute_With3Attempts()
    {
        var method = typeof(OwnerWelcomeEmailJob).GetMethod(nameof(OwnerWelcomeEmailJob.SendAsync))!;
        var attr = (AutomaticRetryAttribute)method
            .GetCustomAttributes(typeof(AutomaticRetryAttribute), inherit: false)
            .Single();

        Assert.Equal(3, attr.Attempts);
        Assert.Equal(AttemptsExceededAction.Fail, attr.OnAttemptsExceeded);
    }
}
