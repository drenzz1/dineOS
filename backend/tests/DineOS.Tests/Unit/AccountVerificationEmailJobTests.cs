using DineOS.Application.Interfaces.Services;
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

public class AccountVerificationEmailJobTests
{
    private static (AccountVerificationEmailJob job, AppDbContext db, IEmailSender sender, IEmailVerificationService verifier, IEmailTemplateRenderer templates)
        CreateSut()
    {
        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns((long?)null);

        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantSvc);

        var sender    = Substitute.For<IEmailSender>();
        var verifier  = Substitute.For<IEmailVerificationService>();
        var templates = Substitute.For<IEmailTemplateRenderer>();

        verifier.IssueAccountVerificationCodeAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns("123456");
        templates.RenderAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
                 .Returns("<html>verify</html>");

        var job = new AccountVerificationEmailJob(
            db, verifier, sender, templates,
            Options.Create(new EmailVerificationOptions()),
            NullLogger<AccountVerificationEmailJob>.Instance);

        return (job, db, sender, verifier, templates);
    }

    [Fact]
    public async Task SendAsync_HappyPath_IssuesCodeAndSendsHtmlEmail()
    {
        var (job, db, sender, verifier, templates) = CreateSut();
        var tenant = new Tenant
        {
            Name = "Sushi Bar", Slug = "sushi-bar",
            OwnerName = "Yuki", OwnerEmail = "yuki@example.com",
            Phone = "1", City = "C", IsActive = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        await job.SendAsync(tenant.Id, CancellationToken.None);

        await verifier.Received(1).IssueAccountVerificationCodeAsync(tenant.Id, Arg.Any<CancellationToken>());
        await templates.Received(1).RenderAsync(
            "AccountVerification",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
        await sender.Received(1).SendAsync(
            "yuki@example.com",
            AccountVerificationEmailJob.Subject,
            Arg.Any<string>(),
            "<html>verify</html>",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_TenantMissing_NoOps()
    {
        var (job, _, sender, verifier, _) = CreateSut();

        await job.SendAsync(9999, CancellationToken.None);

        await verifier.DidNotReceiveWithAnyArgs().IssueAccountVerificationCodeAsync(default, default);
        await sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task SendAsync_AlreadyVerified_NoOps()
    {
        var (job, db, sender, verifier, _) = CreateSut();
        var tenant = new Tenant
        {
            Name = "Pizza", Slug = "pizza",
            OwnerName = "Ann", OwnerEmail = "ann@example.com",
            Phone = "1", City = "C", IsActive = true,
            OwnerEmailVerified = true,
            OwnerEmailVerifiedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        await job.SendAsync(tenant.Id, CancellationToken.None);

        await verifier.DidNotReceiveWithAnyArgs().IssueAccountVerificationCodeAsync(default, default);
        await sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public void SendAsync_HasAutomaticRetryAttribute_With3Attempts()
    {
        var method = typeof(AccountVerificationEmailJob).GetMethod(nameof(AccountVerificationEmailJob.SendAsync))!;
        var attr = (AutomaticRetryAttribute)method
            .GetCustomAttributes(typeof(AutomaticRetryAttribute), inherit: false)
            .Single();

        Assert.Equal(3, attr.Attempts);
        Assert.Equal(AttemptsExceededAction.Fail, attr.OnAttemptsExceeded);
    }
}
