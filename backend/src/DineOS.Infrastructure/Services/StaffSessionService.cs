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
/// short-lived, role-scoped staff-session JWT (HS256, backend-signed) plus a
/// longer-lived refresh token. The access token carries <c>tenant_id</c> (so
/// <see cref="ITenantService"/> resolves the tenant identically to a Keycloak
/// token), the staff member's operational <c>role</c>, and a <c>jti</c> for
/// revocation. Refresh exchanges the refresh token for a new access token; end
/// blacklists both ids in Redis (<see cref="ITokenBlacklistService"/>).
/// </summary>
public sealed class StaffSessionService(
    AppDbContext db,
    ITenantService tenantService,
    IPinHasher pinHasher,
    ITokenBlacklistService blacklist,
    IValidator<StartStaffSessionRequest> validator,
    IOptions<StaffSessionOptions> options,
    ILogger<StaffSessionService> logger) : IStaffSessionService
{
    /// <summary>Redis key namespace for revoked staff-token ids (keeps them clear of Keycloak jtis).</summary>
    public const string BlacklistKeyPrefix = "staff-jti:";

    private const string AccessTokenUse = "staff_session";
    private const string RefreshTokenUse = "staff_refresh";

    // Single message for "no such staff member", "inactive", and "wrong PIN"
    // so the endpoint never reveals which staff ids exist or are active.
    private const string InvalidCredentials = "Invalid staff member or PIN.";
    private const string InvalidSession = "Invalid or expired staff session.";
    private const string NotConfigured = "Staff sessions are not configured.";

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

        if (!IsSigningKeyUsable())
            return Result<StaffSessionResponse>.Failure(NotConfigured);

        logger.LogInformation(
            "Staff session started. TenantId={TenantId} StaffMemberId={StaffMemberId} Role={Role}",
            tenantId, staff.Id, staff.Role);

        return Result<StaffSessionResponse>.Success(IssueSession(staff, DateTimeOffset.UtcNow));
    }

    public async Task<Result<StaffSessionResponse>> RefreshAsync(
        string refreshToken,
        CancellationToken ct = default)
    {
        if (!IsSigningKeyUsable())
            return Result<StaffSessionResponse>.Failure(NotConfigured);

        var now = DateTimeOffset.UtcNow;
        var token = TryReadToken(refreshToken, RefreshTokenUse, now);
        if (token is null)
            return Result<StaffSessionResponse>.Failure(InvalidSession);

        if (await blacklist.IsBlacklistedAsync(BlacklistKeyPrefix + token.Jti))
        {
            logger.LogWarning("Staff refresh rejected — revoked token. StaffMemberId={StaffMemberId}", token.StaffMemberId);
            return Result<StaffSessionResponse>.Failure(InvalidSession);
        }

        // Anonymous endpoint → no tenant context; scope explicitly by the
        // token's tenant_id (ignore the global filter, which is keyed on the
        // absent ambient tenant).
        var staff = await db.StaffMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                s => s.Id == token.StaffMemberId
                  && s.TenantId == token.TenantId
                  && s.DeletedAt == null, ct);

        if (staff is null || !staff.IsActive)
        {
            logger.LogWarning(
                "Staff refresh rejected — staff missing/inactive. StaffMemberId={StaffMemberId}", token.StaffMemberId);
            return Result<StaffSessionResponse>.Failure("Staff session is no longer valid.");
        }

        // Non-rotating: issue a fresh access token, echo the existing refresh
        // token with its remaining lifetime. (Refresh rotation is a possible
        // future hardening — see docs/backend/staff-pin-auth.md.)
        var access = IssueToken(staff, now, AccessLifetime, AccessTokenUse);
        var refreshRemaining = Math.Max(0, (int)(token.ExpiresAt - now.ToUnixTimeSeconds()));

        return Result<StaffSessionResponse>.Success(new StaffSessionResponse(
            access.Token,
            (int)AccessLifetime.TotalSeconds,
            staff.Id,
            staff.FullName,
            staff.Role,
            refreshToken,
            refreshRemaining));
    }

    public async Task EndAsync(
        string? accessJti,
        long? accessExpiresAtUnix,
        string? refreshToken,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        if (!string.IsNullOrEmpty(accessJti))
            await blacklist.BlacklistAsync(BlacklistKeyPrefix + accessJti, RemainingTtl(accessExpiresAtUnix, now));

        if (!string.IsNullOrEmpty(refreshToken))
        {
            var token = TryReadToken(refreshToken, RefreshTokenUse, now);
            if (token is not null)
                await blacklist.BlacklistAsync(BlacklistKeyPrefix + token.Jti, RemainingTtl(token.ExpiresAt, now));
        }

        logger.LogInformation("Staff session ended (tokens revoked). AccessJti={AccessJti}", accessJti);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    private TimeSpan AccessLifetime =>
        TimeSpan.FromMinutes(_options.TokenLifetimeMinutes <= 0 ? 60 : _options.TokenLifetimeMinutes);

    private TimeSpan RefreshLifetime =>
        TimeSpan.FromMinutes(_options.RefreshTokenLifetimeMinutes <= 0 ? 720 : _options.RefreshTokenLifetimeMinutes);

    private bool IsSigningKeyUsable()
    {
        if (Encoding.UTF8.GetByteCount(_options.SigningKey) >= 32)
            return true;
        logger.LogError("StaffSession:SigningKey is missing or shorter than 32 bytes — cannot issue tokens.");
        return false;
    }

    private StaffSessionResponse IssueSession(StaffMember staff, DateTimeOffset now)
    {
        var access = IssueToken(staff, now, AccessLifetime, AccessTokenUse);
        var refresh = IssueToken(staff, now, RefreshLifetime, RefreshTokenUse);
        return new StaffSessionResponse(
            access.Token,
            (int)AccessLifetime.TotalSeconds,
            staff.Id,
            staff.FullName,
            staff.Role,
            refresh.Token,
            (int)RefreshLifetime.TotalSeconds);
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

    private (string Token, string Jti) IssueToken(
        StaffMember staff, DateTimeOffset now, TimeSpan lifetime, string tokenUse)
    {
        var jti = Guid.NewGuid().ToString("N");

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
            ["tenant_id"]       = staff.TenantId,
            ["staff_member_id"] = staff.Id,
            ["jti"]             = jti,
            ["token_use"]       = tokenUse,
            ["iat"]             = now.ToUnixTimeSeconds(),
            ["nbf"]             = now.ToUnixTimeSeconds(),
            ["exp"]             = now.Add(lifetime).ToUnixTimeSeconds(),
        };

        // Only the access token carries the operational role + display name.
        if (tokenUse == AccessTokenUse)
        {
            payload["name"] = staff.FullName;
            payload["role"] = staff.Role;
        }

        var headerSeg  = Base64Url(JsonSerializer.SerializeToUtf8Bytes(header));
        var payloadSeg = Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{headerSeg}.{payloadSeg}";
        var signature = Base64Url(SignHmac(signingInput));

        return ($"{signingInput}.{signature}", jti);
    }

    /// <summary>
    /// Verifies an HS256 staff token's signature, issuer/audience, expiry and
    /// <c>token_use</c>, returning its key claims. Returns null on any failure.
    /// </summary>
    private TokenData? TryReadToken(string token, string expectedTokenUse, DateTimeOffset now)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
            return null;

        var expectedSig = Base64Url(SignHmac($"{parts[0]}.{parts[1]}"));
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSig), Encoding.UTF8.GetBytes(parts[2])))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[1])));
            var root = doc.RootElement;

            if (GetString(root, "iss") != _options.Issuer) return null;
            if (GetString(root, "aud") != _options.Audience) return null;
            if (GetString(root, "token_use") != expectedTokenUse) return null;

            var exp = GetLong(root, "exp");
            if (exp is null || now.ToUnixTimeSeconds() >= exp.Value) return null;

            var jti = GetString(root, "jti");
            var staffId = GetLong(root, "staff_member_id");
            var tenantId = GetLong(root, "tenant_id");
            if (jti is null || staffId is null || tenantId is null) return null;

            return new TokenData(jti, staffId.Value, tenantId.Value, exp.Value);
        }
        catch (JsonException) { return null; }
        catch (FormatException) { return null; }
    }

    private byte[] SignHmac(string input)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SigningKey));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
    }

    private static TimeSpan RemainingTtl(long? expiresAtUnix, DateTimeOffset now)
    {
        if (expiresAtUnix is null) return TimeSpan.Zero;
        var ttl = DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix.Value) - now;
        return ttl < TimeSpan.Zero ? TimeSpan.Zero : ttl;
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static long? GetLong(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el)) return null;
        return el.ValueKind switch
        {
            JsonValueKind.Number when el.TryGetInt64(out var v) => v,
            JsonValueKind.String when long.TryParse(el.GetString(), out var v) => v,
            _ => null,
        };
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
        return Convert.FromBase64String(s);
    }

    private sealed record TokenData(string Jti, long StaffMemberId, long TenantId, long ExpiresAt);
}
