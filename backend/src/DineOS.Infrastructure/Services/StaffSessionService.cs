using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Domain.Entities;
using DineOS.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineOS.Infrastructure.Services;

/// <summary>
/// Verifies a staff PIN within the business's tenant context and mints a
/// short-lived, role-scoped staff-session JWT (HS256, backend-signed). The
/// token carries <c>tenant_id</c> (so <see cref="ITenantService"/> resolves the
/// tenant identically to a Keycloak token) and the staff member's operational
/// role as a <c>role</c> claim, which the API's StaffSession bearer scheme maps
/// to the role claim that <c>RequireRole(...)</c> policies check.
/// </summary>
public sealed class StaffSessionService(
    AppDbContext db,
    ITenantService tenantService,
    IPinHasher pinHasher,
    IValidator<StartStaffSessionRequest> validator,
    IOptions<StaffSessionOptions> options,
    ILogger<StaffSessionService> logger) : IStaffSessionService
{
    // Single message for "no such staff member", "inactive", and "wrong PIN"
    // so the endpoint never reveals which staff ids exist or are active.
    private const string InvalidCredentials = "Invalid staff member or PIN.";

    private readonly StaffSessionOptions _options = options.Value;

    public async Task<Result<StaffSessionResponse>> StartAsync(
        StartStaffSessionRequest request,
        CancellationToken ct = default)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<StaffSessionResponse>.Failure(
                "Validation failed.",
                validation.Errors.Select(e => e.ErrorMessage).ToList());

        if (tenantService.TenantId is not { } tenantId)
            return Result<StaffSessionResponse>.Failure("Tenant context is required.");

        // Tenant filter is explicit (belt-and-suspenders alongside the global
        // query filter) so a staff id from another tenant can never match.
        var staff = await db.StaffMembers
            .FirstOrDefaultAsync(
                s => s.Id == request.StaffMemberId && s.TenantId == tenantId, ct);

        if (staff is null || !staff.IsActive || !VerifyPin(request.Pin, staff.PinHash))
        {
            logger.LogWarning(
                "Staff session denied. TenantId={TenantId} StaffMemberId={StaffMemberId} Reason={Reason}",
                tenantId,
                request.StaffMemberId,
                staff is null ? "not-found" : !staff.IsActive ? "inactive" : "bad-pin");
            return Result<StaffSessionResponse>.Failure(InvalidCredentials);
        }

        if (Encoding.UTF8.GetByteCount(_options.SigningKey) < 32)
        {
            logger.LogError("StaffSession:SigningKey is missing or shorter than 32 bytes — cannot issue tokens.");
            return Result<StaffSessionResponse>.Failure("Staff sessions are not configured.");
        }

        var now = DateTimeOffset.UtcNow;
        var lifetime = TimeSpan.FromMinutes(_options.TokenLifetimeMinutes <= 0 ? 720 : _options.TokenLifetimeMinutes);
        var token = IssueToken(staff, now, lifetime);

        logger.LogInformation(
            "Staff session started. TenantId={TenantId} StaffMemberId={StaffMemberId} Role={Role}",
            tenantId, staff.Id, staff.Role);

        return Result<StaffSessionResponse>.Success(new StaffSessionResponse(
            token,
            (int)lifetime.TotalSeconds,
            staff.Id,
            staff.FullName,
            staff.Role));
    }

    // BCrypt.Verify throws on a malformed stored hash (e.g. the demo seeder's
    // "demo-pin-hash" placeholder). Treat any such failure as a non-match so a
    // bad/legacy PinHash yields a clean 401, never a 500.
    private bool VerifyPin(string pin, string hash)
    {
        try
        {
            return pinHasher.Verify(pin, hash);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PIN verification failed due to a malformed stored hash.");
            return false;
        }
    }

    private string IssueToken(StaffMember staff, DateTimeOffset now, TimeSpan lifetime)
    {
        var header = new Dictionary<string, object>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT",
        };

        var payload = new Dictionary<string, object>
        {
            ["iss"]             = _options.Issuer,
            ["aud"]             = _options.Audience,
            ["sub"]             = $"staff:{staff.Id}",
            ["name"]            = staff.FullName,
            ["role"]            = staff.Role,
            ["tenant_id"]       = staff.TenantId,
            ["staff_member_id"] = staff.Id,
            ["token_use"]       = "staff_session",
            ["iat"]             = now.ToUnixTimeSeconds(),
            ["nbf"]             = now.ToUnixTimeSeconds(),
            ["exp"]             = now.Add(lifetime).ToUnixTimeSeconds(),
        };

        var headerSeg  = Base64Url(JsonSerializer.SerializeToUtf8Bytes(header));
        var payloadSeg = Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{headerSeg}.{payloadSeg}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SigningKey));
        var signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput));

        return $"{signingInput}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
