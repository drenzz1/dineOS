using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text.Json;

namespace DineOS.Api.Auth;

/// <summary>
/// Maps Keycloak realm_access.roles into ClaimTypes.Role so policy-based
/// authorization (RequireRole) works without custom token validation config.
/// </summary>
public class KeycloakRolesTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var realmAccess = principal.FindFirst("realm_access");
        if (realmAccess is null)
            return Task.FromResult(principal);

        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(realmAccess.Value);
            root = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return Task.FromResult(principal);
        }

        if (!root.TryGetProperty("roles", out var roles))
            return Task.FromResult(principal);

        var identity = (ClaimsIdentity)principal.Identity!;
        foreach (var role in roles.EnumerateArray())
        {
            var name = role.GetString();
            if (name is not null && !identity.HasClaim(ClaimTypes.Role, name))
                identity.AddClaim(new Claim(ClaimTypes.Role, name));
        }

        return Task.FromResult(principal);
    }
}
