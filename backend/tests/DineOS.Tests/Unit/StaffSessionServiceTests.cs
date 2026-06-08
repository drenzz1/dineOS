using System.Text;
using System.Text.Json;
using DineOS.Application.Authorization;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Domain.Entities;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DineOS.Tests.Unit;

public class StaffSessionServiceTests
{
    private const string Pin = "1234";

    private static (StaffSessionService svc, AppDbContext db, ITokenBlacklistService blacklist) CreateSut(
        long? tenantId = 1L)
    {
        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns(tenantId);

        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantSvc);

        var blacklist = Substitute.For<ITokenBlacklistService>();
        blacklist.IsBlacklistedAsync(Arg.Any<string>()).Returns(false);

        var options = Options.Create(new StaffSessionOptions
        {
            SigningKey = "test-staff-session-signing-key-0123456789abcdef",
            Issuer = "dineos-staff-session",
            Audience = "dineos-api",
            TokenLifetimeMinutes = 60,
            RefreshTokenLifetimeMinutes = 720,
        });

        var svc = new StaffSessionService(
            db,
            tenantSvc,
            new PinHasher(),
            blacklist,
            new StartStaffSessionRequestValidator(),
            options,
            NullLogger<StaffSessionService>.Instance);

        return (svc, db, blacklist);
    }

    private static StaffMember SeedStaff(AppDbContext db, long tenantId = 1, bool active = true, string role = Roles.Manager)
    {
        var staff = new StaffMember
        {
            FullName = "Jane Doe",
            Email = "jane@demo.test",
            Role = role,
            PinHash = new PinHasher().Hash(Pin),
            IsActive = active,
            TenantId = tenantId,
        };
        db.StaffMembers.Add(staff);
        db.SaveChanges();
        return staff;
    }

    private static Dictionary<string, JsonElement> DecodePayload(string jwt)
    {
        var seg = jwt.Split('.')[1];
        seg = seg.Replace('-', '+').Replace('_', '/').PadRight(seg.Length + (4 - seg.Length % 4) % 4, '=');
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(seg));
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
    }

    [Fact]
    public async Task StartAsync_ValidPin_ReturnsRoleScopedToken()
    {
        var (svc, db, _) = CreateSut();
        var staff = SeedStaff(db, role: Roles.Cashier);

        var result = await svc.StartAsync(new StartStaffSessionRequest { StaffMemberId = staff.Id, Pin = Pin });

        Assert.True(result.IsSuccess);
        Assert.Equal(staff.Id, result.Value!.StaffMemberId);
        Assert.Equal("Jane Doe", result.Value.FullName);
        Assert.Equal(Roles.Cashier, result.Value.Role);
        Assert.Equal(3, result.Value.AccessToken.Split('.').Length);
        Assert.True(result.Value.ExpiresIn > 0);
    }

    [Fact]
    public async Task StartAsync_TokenCarriesTenantAndRoleClaims()
    {
        var (svc, db, _) = CreateSut(tenantId: 7L);
        var staff = SeedStaff(db, tenantId: 7, role: Roles.KitchenStaff);

        var result = await svc.StartAsync(new StartStaffSessionRequest { StaffMemberId = staff.Id, Pin = Pin });

        var payload = DecodePayload(result.Value!.AccessToken);
        Assert.Equal(7, payload["tenant_id"].GetInt64());
        Assert.Equal(Roles.KitchenStaff, payload["role"].GetString());
        Assert.Equal("staff_session", payload["token_use"].GetString());
    }

    [Fact]
    public async Task StartAsync_WrongPin_Fails()
    {
        var (svc, db, _) = CreateSut();
        var staff = SeedStaff(db);

        var result = await svc.StartAsync(new StartStaffSessionRequest { StaffMemberId = staff.Id, Pin = "9999" });

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid staff member or PIN.", result.Error);
    }

    [Fact]
    public async Task StartAsync_InactiveStaff_Fails()
    {
        var (svc, db, _) = CreateSut();
        var staff = SeedStaff(db, active: false);

        var result = await svc.StartAsync(new StartStaffSessionRequest { StaffMemberId = staff.Id, Pin = Pin });

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid staff member or PIN.", result.Error);
    }

    [Fact]
    public async Task StartAsync_NoTenantContext_Fails()
    {
        var (svc, _, _) = CreateSut(tenantId: null);

        var result = await svc.StartAsync(new StartStaffSessionRequest { StaffMemberId = 1, Pin = Pin });

        Assert.False(result.IsSuccess);
        Assert.Equal("Tenant context is required.", result.Error);
    }

    [Fact]
    public async Task StartAsync_MalformedStoredHash_FailsGracefully()
    {
        // The demo seeder stores a placeholder "demo-pin-hash" that is not a
        // valid BCrypt hash; verification must return a clean failure, not throw.
        var (svc, db, _) = CreateSut();
        var staff = new StaffMember
        {
            FullName = "Seeded Demo",
            Email = "seeded@demo.test",
            Role = Roles.Manager,
            PinHash = "demo-pin-hash",
            IsActive = true,
            TenantId = 1,
        };
        db.StaffMembers.Add(staff);
        db.SaveChanges();

        var result = await svc.StartAsync(new StartStaffSessionRequest { StaffMemberId = staff.Id, Pin = "1234" });

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid staff member or PIN.", result.Error);
    }

    [Fact]
    public async Task StartAsync_UnknownStaffId_Fails()
    {
        var (svc, db, _) = CreateSut();
        SeedStaff(db);

        var result = await svc.StartAsync(new StartStaffSessionRequest { StaffMemberId = 999, Pin = Pin });

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid staff member or PIN.", result.Error);
    }

    [Fact]
    public async Task StartAsync_IssuesAccessAndRefreshTokens()
    {
        var (svc, db, _) = CreateSut();
        var staff = SeedStaff(db);

        var result = await svc.StartAsync(new StartStaffSessionRequest { StaffMemberId = staff.Id, Pin = Pin });

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.RefreshToken.Split('.').Length);
        Assert.NotEqual(result.Value.AccessToken, result.Value.RefreshToken);
        Assert.Equal("staff_refresh", DecodePayload(result.Value.RefreshToken)["token_use"].GetString());
        Assert.Equal("staff_session", DecodePayload(result.Value.AccessToken)["token_use"].GetString());
        Assert.True(result.Value.RefreshExpiresIn > result.Value.ExpiresIn);
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_IssuesFreshAccessToken()
    {
        var (svc, db, _) = CreateSut();
        var staff = SeedStaff(db, role: Roles.Manager);
        var session = (await svc.StartAsync(new StartStaffSessionRequest { StaffMemberId = staff.Id, Pin = Pin })).Value!;

        var refreshed = await svc.RefreshAsync(session.RefreshToken);

        Assert.True(refreshed.IsSuccess);
        Assert.Equal(Roles.Manager, refreshed.Value!.Role);
        Assert.Equal("staff_session", DecodePayload(refreshed.Value.AccessToken)["token_use"].GetString());
        // The refresh token is echoed unchanged (non-rotating).
        Assert.Equal(session.RefreshToken, refreshed.Value.RefreshToken);
    }

    [Fact]
    public async Task RefreshAsync_AccessTokenAsRefresh_Fails()
    {
        var (svc, db, _) = CreateSut();
        var staff = SeedStaff(db);
        var session = (await svc.StartAsync(new StartStaffSessionRequest { StaffMemberId = staff.Id, Pin = Pin })).Value!;

        // Presenting the access token (token_use=staff_session) to refresh must fail.
        var refreshed = await svc.RefreshAsync(session.AccessToken);

        Assert.False(refreshed.IsSuccess);
    }

    [Fact]
    public async Task RefreshAsync_RevokedToken_Fails()
    {
        var (svc, db, blacklist) = CreateSut();
        var staff = SeedStaff(db);
        var session = (await svc.StartAsync(new StartStaffSessionRequest { StaffMemberId = staff.Id, Pin = Pin })).Value!;
        blacklist.IsBlacklistedAsync(Arg.Any<string>()).Returns(true);

        var refreshed = await svc.RefreshAsync(session.RefreshToken);

        Assert.False(refreshed.IsSuccess);
    }

    [Fact]
    public async Task RefreshAsync_InactiveStaff_Fails()
    {
        var (svc, db, _) = CreateSut();
        var staff = SeedStaff(db);
        var session = (await svc.StartAsync(new StartStaffSessionRequest { StaffMemberId = staff.Id, Pin = Pin })).Value!;

        staff.IsActive = false;
        db.SaveChanges();

        var refreshed = await svc.RefreshAsync(session.RefreshToken);

        Assert.False(refreshed.IsSuccess);
    }

    [Fact]
    public async Task RefreshAsync_GarbageToken_Fails()
    {
        var (svc, _, _) = CreateSut();

        var refreshed = await svc.RefreshAsync("not.a.jwt");

        Assert.False(refreshed.IsSuccess);
    }

    [Fact]
    public async Task EndAsync_BlacklistsBothTokenIds()
    {
        var (svc, db, blacklist) = CreateSut();
        var staff = SeedStaff(db);
        var session = (await svc.StartAsync(new StartStaffSessionRequest { StaffMemberId = staff.Id, Pin = Pin })).Value!;

        var accessJti = DecodePayload(session.AccessToken)["jti"].GetString();
        var refreshJti = DecodePayload(session.RefreshToken)["jti"].GetString();
        var accessExp = DecodePayload(session.AccessToken)["exp"].GetInt64();

        await svc.EndAsync(accessJti, accessExp, session.RefreshToken);

        await blacklist.Received(1).BlacklistAsync(
            $"{StaffSessionService.BlacklistKeyPrefix}{accessJti}", Arg.Any<TimeSpan>());
        await blacklist.Received(1).BlacklistAsync(
            $"{StaffSessionService.BlacklistKeyPrefix}{refreshJti}", Arg.Any<TimeSpan>());
    }
}
