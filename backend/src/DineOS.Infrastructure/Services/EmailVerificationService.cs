using System.Security.Cryptography;
using System.Text;
using DineOS.Application.Common;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Application.Restaurants;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineOS.Infrastructure.Services;

public sealed class EmailVerificationService(
    AppDbContext db,
    IOptions<EmailVerificationOptions> options,
    IValidator<ConfirmEmailVerificationRequest> confirmValidator,
    ILogger<EmailVerificationService> logger) : IEmailVerificationService
{
    private readonly EmailVerificationOptions _opts = options.Value;

    public async Task<string> IssueAccountVerificationCodeAsync(long tenantId, CancellationToken ct = default)
    {
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.DeletedAt == null, ct);

        if (tenant is null)
            throw new InvalidOperationException($"Tenant {tenantId} not found while issuing verification code.");

        var email = NormalizeEmail(tenant.OwnerEmail);
        await ExpirePendingCodesAsync(email, EmailVerificationPurpose.AccountVerification, ct);

        var code = GenerateCode();
        var entry = new EmailVerificationCode
        {
            Email     = email,
            Purpose   = EmailVerificationPurpose.AccountVerification,
            CodeHash  = HashCode(code),
            ExpiresAt = DateTime.UtcNow.AddMinutes(_opts.CodeTtlMinutes),
            TenantId  = tenantId,
        };

        db.EmailVerificationCodes.Add(entry);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Issued account verification code: TenantId={TenantId} Email={Email} ExpiresAt={ExpiresAt}",
            tenantId, email, entry.ExpiresAt);

        return code;
    }

    public async Task<ServiceResult<bool>> ConfirmAccountVerificationCodeAsync(
        long tenantId,
        ConfirmEmailVerificationRequest request,
        CancellationToken ct = default)
    {
        var validation = await confirmValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ServiceResult<bool>.ValidationFailed(
                "Validation failed.",
                validation.Errors.Select(e => e.ErrorMessage).ToList());

        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.DeletedAt == null, ct);

        if (tenant is null)
            return ServiceResult<bool>.NotFound($"Restaurant {tenantId} not found.");

        if (tenant.OwnerEmailVerified)
            return ServiceResult<bool>.Ok(true, "Email already verified.");

        var email = NormalizeEmail(tenant.OwnerEmail);
        var entry = await db.EmailVerificationCodes
            .Where(c => c.Email == email
                     && c.Purpose == EmailVerificationPurpose.AccountVerification
                     && c.ConsumedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (entry is null || entry.ExpiresAt < DateTime.UtcNow)
            return ServiceResult<bool>.ValidationFailed(
                "Verification code is invalid or expired.",
                new List<string> { "Verification code is invalid or expired." });

        if (entry.FailedAttempts >= _opts.MaxAttemptsPerCode)
            return ServiceResult<bool>.ValidationFailed(
                "Too many attempts; request a new code.",
                new List<string> { "Too many attempts; request a new code." });

        if (!FixedTimeEquals(entry.CodeHash, HashCode(request.Code)))
        {
            entry.FailedAttempts++;
            await db.SaveChangesAsync(ct);
            return ServiceResult<bool>.ValidationFailed(
                "Verification code is invalid or expired.",
                new List<string> { "Verification code is invalid or expired." });
        }

        entry.ConsumedAt            = DateTime.UtcNow;
        tenant.OwnerEmailVerified   = true;
        tenant.OwnerEmailVerifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Verified owner email: TenantId={TenantId} Email={Email}",
            tenantId, email);

        return ServiceResult<bool>.Ok(true, "Email verified.");
    }

    private async Task ExpirePendingCodesAsync(
        string email,
        EmailVerificationPurpose purpose,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var pending = await db.EmailVerificationCodes
            .Where(c => c.Email == email && c.Purpose == purpose && c.ConsumedAt == null)
            .ToListAsync(ct);

        foreach (var p in pending)
            p.ConsumedAt = now;

        if (pending.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string GenerateCode()
    {
        // Cryptographically random 6-digit code (100000–999999).
        var value = RandomNumberGenerator.GetInt32(100_000, 1_000_000);
        return value.ToString();
    }

    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b));
}
