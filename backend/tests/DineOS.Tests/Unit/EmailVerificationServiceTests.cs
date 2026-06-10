using System.Security.Cryptography;
using System.Text;
using DineOS.Application.Common;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Application.Restaurants;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DineOS.Tests.Unit;

public class EmailVerificationServiceTests
{
    private static (EmailVerificationService svc, AppDbContext db) CreateSut(EmailVerificationOptions? options = null)
    {
        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns((long?)null);

        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantSvc);

        var svc = new EmailVerificationService(
            db,
            Options.Create(options ?? new EmailVerificationOptions()),
            new ConfirmEmailVerificationRequestValidator(),
            NullLogger<EmailVerificationService>.Instance);

        return (svc, db);
    }

    private static ConfirmEmailVerificationRequest Req(string code) =>
        new() { Code = code };

    private static async Task<Tenant> SeedTenantAsync(AppDbContext db, string email = "owner@example.com")
    {
        var tenant = new Tenant
        {
            Name = "Pasta Co", Slug = "pasta-co",
            OwnerName = "Owner", OwnerEmail = email,
            Phone = "1", City = "C", IsActive = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    [Fact]
    public async Task IssueAccountVerificationCodeAsync_StoresHashOnly()
    {
        var (svc, db) = CreateSut();
        var tenant = await SeedTenantAsync(db);

        var code = await svc.IssueAccountVerificationCodeAsync(tenant.Id);

        Assert.Matches("^[0-9]{6}$", code);

        var saved = await db.EmailVerificationCodes.SingleAsync();
        Assert.NotEqual(code, saved.CodeHash);
        Assert.Equal(64, saved.CodeHash.Length); // SHA-256 hex = 64 chars
        Assert.Equal(EmailVerificationPurpose.AccountVerification, saved.Purpose);
        Assert.Equal(tenant.Id, saved.TenantId);
    }

    [Fact]
    public async Task IssueAccountVerificationCodeAsync_ExpiresOlderPendingCodes()
    {
        var (svc, db) = CreateSut();
        var tenant = await SeedTenantAsync(db);

        await svc.IssueAccountVerificationCodeAsync(tenant.Id);
        await svc.IssueAccountVerificationCodeAsync(tenant.Id);

        var rows = await db.EmailVerificationCodes
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        // Two rows total; the first must now have ConsumedAt set.
        Assert.Equal(2, rows.Count);
        Assert.NotNull(rows[0].ConsumedAt);
        Assert.Null(rows[1].ConsumedAt);
    }

    [Fact]
    public async Task ConfirmAccountVerificationCodeAsync_SuccessMarksTenantVerified()
    {
        var (svc, db) = CreateSut();
        var tenant = await SeedTenantAsync(db);

        var code = await svc.IssueAccountVerificationCodeAsync(tenant.Id);
        var result = await svc.ConfirmAccountVerificationCodeAsync(tenant.Id, Req(code));

        Assert.True(result.IsSuccess);

        var reloaded = await db.Tenants.FirstAsync(t => t.Id == tenant.Id);
        Assert.True(reloaded.OwnerEmailVerified);
        Assert.NotNull(reloaded.OwnerEmailVerifiedAt);
    }

    [Fact]
    public async Task ConfirmAccountVerificationCodeAsync_WrongCode_FailsAndIncrementsAttempts()
    {
        var (svc, db) = CreateSut();
        var tenant = await SeedTenantAsync(db);
        await svc.IssueAccountVerificationCodeAsync(tenant.Id);

        var result = await svc.ConfirmAccountVerificationCodeAsync(tenant.Id, Req("000000"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.ValidationFailed, result.Error);

        var entry = await db.EmailVerificationCodes.SingleAsync();
        Assert.Equal(1, entry.FailedAttempts);
        Assert.Null(entry.ConsumedAt);
    }

    [Fact]
    public async Task ConfirmAccountVerificationCodeAsync_ExpiredCode_Fails()
    {
        var (svc, db) = CreateSut(new EmailVerificationOptions { CodeTtlMinutes = 15 });
        var tenant = await SeedTenantAsync(db);
        var code = await svc.IssueAccountVerificationCodeAsync(tenant.Id);

        // Backdate the row so it's already expired.
        var entry = await db.EmailVerificationCodes.SingleAsync();
        entry.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var result = await svc.ConfirmAccountVerificationCodeAsync(tenant.Id, Req(code));

        Assert.False(result.IsSuccess);
    }

    // ── Password reset codes (forgot-password flow) ───────────────────────────

    [Fact]
    public async Task IssuePasswordResetCodeAsync_StoresNormalizedEmailAndHashOnly()
    {
        var (svc, db) = CreateSut();

        var code = await svc.IssuePasswordResetCodeAsync("Owner@Example.com ");

        Assert.Matches("^[0-9]{6}$", code);

        var saved = await db.EmailVerificationCodes.SingleAsync();
        Assert.Equal("owner@example.com", saved.Email);
        Assert.Equal(EmailVerificationPurpose.PasswordReset, saved.Purpose);
        Assert.NotEqual(code, saved.CodeHash);
        Assert.Null(saved.TenantId);
    }

    [Fact]
    public async Task ConsumePasswordResetCodeAsync_WithCorrectCode_ConsumesItOnce()
    {
        var (svc, db) = CreateSut();
        var code = await svc.IssuePasswordResetCodeAsync("owner@example.com");

        // Submitted email casing must not matter — codes are stored normalized.
        var first = await svc.ConsumePasswordResetCodeAsync("Owner@Example.com", code);
        Assert.True(first.IsSuccess);

        var entry = await db.EmailVerificationCodes.SingleAsync();
        Assert.NotNull(entry.ConsumedAt);

        // A consumed code cannot be replayed.
        var second = await svc.ConsumePasswordResetCodeAsync("owner@example.com", code);
        Assert.False(second.IsSuccess);
    }

    [Fact]
    public async Task ConsumePasswordResetCodeAsync_WrongCode_FailsAndIncrementsAttempts()
    {
        var (svc, db) = CreateSut();
        await svc.IssuePasswordResetCodeAsync("owner@example.com");

        var result = await svc.ConsumePasswordResetCodeAsync("owner@example.com", "000000");

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.ValidationFailed, result.Error);

        var entry = await db.EmailVerificationCodes.SingleAsync();
        Assert.Equal(1, entry.FailedAttempts);
        Assert.Null(entry.ConsumedAt);
    }

    [Fact]
    public async Task ConsumePasswordResetCodeAsync_RejectsAccountVerificationCodes()
    {
        // Purpose isolation: a code issued to verify the account email must
        // not be redeemable to reset the password.
        var (svc, db) = CreateSut();
        var tenant = await SeedTenantAsync(db);
        var accountCode = await svc.IssueAccountVerificationCodeAsync(tenant.Id);

        var result = await svc.ConsumePasswordResetCodeAsync(tenant.OwnerEmail, accountCode);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ConsumePasswordResetCodeAsync_ExpiredCode_Fails()
    {
        var (svc, db) = CreateSut();
        var code = await svc.IssuePasswordResetCodeAsync("owner@example.com");

        var entry = await db.EmailVerificationCodes.SingleAsync();
        entry.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var result = await svc.ConsumePasswordResetCodeAsync("owner@example.com", code);

        Assert.False(result.IsSuccess);
    }
}
