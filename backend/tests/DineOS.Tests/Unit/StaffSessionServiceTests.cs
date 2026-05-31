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

    private static (StaffSessionService svc, AppDbContext db) CreateSut(long? tenantId = 1L)
    {
        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns(tenantId);

        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantSvc);

        var options = Options.Create(new StaffSessionOptions
        {
            SigningKey = "test-staff-session-signing-key-0123456789abcdef",
            Issuer = "dineos-staff-session",
            Audience = "dineos-api",
            TokenLifetimeMinutes = 60,
        });

        var svc = new StaffSessionService(
            db,
            tenantSvc,
            new PinHasher(),
            new StartStaffSessionRequestValidator(),
            options,
            NullLogger<StaffSessionService>.Instance);

        return (svc, db);
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
        var (svc, db) = CreateSut();
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
        var (svc, db) = CreateSut(tenantId: 7L);
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
        var (svc, db) = CreateSut();
        var staff = SeedStaff(db);

        var result = await svc.StartAsync(new StartStaffSessionRequest { StaffMemberId = staff.Id, Pin = "9999" });

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid staff member or PIN.", result.Error);
    }

    [Fact]
    public async Task StartAsync_InactiveStaff_Fails()
    {
        var (svc, db) = CreateSut();
        var staff = SeedStaff(db, active: false);

        var result = await svc.StartAsync(new StartStaffSessionRequest { StaffMemberId = staff.Id, Pin = Pin });

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid staff member or PIN.", result.Error);
    }

    [Fact]
    public async Task StartAsync_NoTenantContext_Fails()
    {
        var (svc, _) = CreateSut(tenantId: null);

        var result = await svc.StartAsync(new StartStaffSessionRequest { StaffMemberId = 1, Pin = Pin });

        Assert.False(result.IsSuccess);
        Assert.Equal("Tenant context is required.", result.Error);
    }

    [Fact]
    public async Task StartAsync_UnknownStaffId_Fails()
    {
        var (svc, db) = CreateSut();
        SeedStaff(db);

        var result = await svc.StartAsync(new StartStaffSessionRequest { StaffMemberId = 999, Pin = Pin });

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid staff member or PIN.", result.Error);
    }
}
