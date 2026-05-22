using DineOS.Application.Interfaces.Services;
using DineOS.Domain.Entities;
using DineOS.Infrastructure.Jobs;
using DineOS.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DineOS.Tests.Unit;

/// <summary>
/// Regression tests for the post-checkout owner provisioning flow (#205).
/// The bug we are guarding against: provisioning previously created
/// owners with a non-temporary password, no UPDATE_PASSWORD required
/// action, and the bespoke "Owner" realm role. That combination turned
/// the emailed password into a permanent credential and broke FE role
/// gating. These assertions lock in the secure baseline.
/// </summary>
public class OwnerProvisioningJobTests
{
    private static (OwnerProvisioningJob job, AppDbContext db, IKeycloakAdminClient admin, IBackgroundJobClient bg)
        CreateSut()
    {
        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns((long?)null);

        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantSvc);

        var admin = Substitute.For<IKeycloakAdminClient>();
        admin.CreateUserAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
             .Returns("kc-user-id");

        var bg = Substitute.For<IBackgroundJobClient>();

        var job = new OwnerProvisioningJob(
            db, admin, bg, NullLogger<OwnerProvisioningJob>.Instance);

        return (job, db, admin, bg);
    }

    private static Tenant SeedTenant(AppDbContext db)
    {
        var tenant = new Tenant
        {
            Name       = "Olio & Sale",
            Slug       = "olio-and-sale",
            OwnerName  = "Jane Doe",
            OwnerEmail = "jane@example.com",
            IsActive   = true,
        };
        db.Tenants.Add(tenant);
        db.SaveChanges();
        return tenant;
    }

    [Fact]
    public async Task RunAsync_CreatesUserWithTemporaryPasswordAndUpdatePasswordAction()
    {
        var (job, db, admin, _) = CreateSut();
        var tenant = SeedTenant(db);

        await job.RunAsync(tenant.Id, "TempPass!23", CancellationToken.None);

        await admin.Received(1).CreateUserAsync(
            email:              "jane@example.com",
            firstName:          "Jane",
            lastName:           "Doe",
            tempPassword:       "TempPass!23",
            requiredActions:    Arg.Is<IReadOnlyList<string>>(a =>
                                    a.Count == 1 && a.Contains("UPDATE_PASSWORD")),
            temporaryPassword:  true,
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_AssignsManagerRealmRoleNotOwner()
    {
        var (job, db, admin, _) = CreateSut();
        var tenant = SeedTenant(db);

        await job.RunAsync(tenant.Id, "TempPass!23", CancellationToken.None);

        await admin.Received(1).AssignRealmRoleAsync(
            "kc-user-id", "Manager", Arg.Any<CancellationToken>());
        await admin.DidNotReceive().AssignRealmRoleAsync(
            Arg.Any<string>(), "Owner", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_PersistsKeycloakUserIdAndEnqueuesWelcomeEmail()
    {
        var (job, db, _, bg) = CreateSut();
        var tenant = SeedTenant(db);

        await job.RunAsync(tenant.Id, "TempPass!23", CancellationToken.None);

        var refreshed = await db.Tenants.IgnoreQueryFilters().FirstAsync(t => t.Id == tenant.Id);
        Assert.Equal("kc-user-id", refreshed.KeycloakUserId);

        bg.Received(1).Create(
            Arg.Any<Hangfire.Common.Job>(),
            Arg.Any<Hangfire.States.IState>());
    }

    [Fact]
    public async Task RunAsync_SkipsWhenTenantAlreadyHasKeycloakUserId()
    {
        var (job, db, admin, _) = CreateSut();
        var tenant = SeedTenant(db);
        tenant.KeycloakUserId = "pre-existing";
        await db.SaveChangesAsync();

        await job.RunAsync(tenant.Id, "TempPass!23", CancellationToken.None);

        await admin.DidNotReceive().CreateUserAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await admin.DidNotReceive().AssignRealmRoleAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
