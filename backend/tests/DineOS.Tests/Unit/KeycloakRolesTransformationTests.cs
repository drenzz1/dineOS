using DineOS.Api.Auth;
using DineOS.Application.Authorization;
using System.Security.Claims;

namespace DineOS.Tests.Unit;

public class KeycloakRolesTransformationTests
{
    private readonly KeycloakRolesTransformation _sut = new();

    private static ClaimsPrincipal PrincipalWith(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task TransformAsync_NoRealmAccessClaim_ReturnsPrincipalUnchanged()
    {
        var principal = PrincipalWith(new Claim("sub", "user-1"));

        var result = await _sut.TransformAsync(principal);

        Assert.Same(principal, result);
        Assert.Empty(result.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public async Task TransformAsync_MalformedRealmAccessJson_ReturnsPrincipalUnchanged()
    {
        var principal = PrincipalWith(new Claim("realm_access", "not-json"));

        var result = await _sut.TransformAsync(principal);

        Assert.Same(principal, result);
        Assert.Empty(result.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public async Task TransformAsync_RealmAccessWithoutRolesProperty_ReturnsPrincipalUnchanged()
    {
        var principal = PrincipalWith(new Claim("realm_access", """{"other":"value"}"""));

        var result = await _sut.TransformAsync(principal);

        Assert.Same(principal, result);
        Assert.Empty(result.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public async Task TransformAsync_ValidRoles_AddsClaimTypeRoleForEach()
    {
        var principal = PrincipalWith(
            new Claim("realm_access", """{"roles":["Manager","Cashier"]}"""));

        var result = await _sut.TransformAsync(principal);

        var roles = result.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.Contains(Roles.Manager, roles);
        Assert.Contains(Roles.Cashier, roles);
    }

    [Fact]
    public async Task TransformAsync_DuplicateRole_NotAddedTwice()
    {
        var identity = new ClaimsIdentity(
            [new Claim("realm_access", """{"roles":["Manager"]}"""),
             new Claim(ClaimTypes.Role, Roles.Manager)],
            "Test");
        var principal = new ClaimsPrincipal(identity);

        var result = await _sut.TransformAsync(principal);

        Assert.Single(result.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public async Task TransformAsync_EmptyRolesArray_NoRoleClaimsAdded()
    {
        var principal = PrincipalWith(new Claim("realm_access", """{"roles":[]}"""));

        var result = await _sut.TransformAsync(principal);

        Assert.Empty(result.FindAll(ClaimTypes.Role));
    }
}
