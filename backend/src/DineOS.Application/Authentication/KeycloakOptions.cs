namespace DineOS.Application.Authentication;

public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    public string? Realm { get; set; }
    public string? Authority { get; set; }
    public string? MetadataAddress { get; set; }
    public string? Audience { get; set; }
    public string? AuthServerUrl { get; set; }
    public string? PublicAuthServerUrl { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string GrantType { get; set; } = "password";
    public bool RequireHttpsMetadata { get; set; }
    public string? AdminClientId { get; set; }
    public string? AdminClientSecret { get; set; }

    public string? GetIssuerAuthority()
    {
        if (!string.IsNullOrWhiteSpace(Authority))
            return TrimTrailingSlash(Authority);

        if (!string.IsNullOrWhiteSpace(PublicAuthServerUrl) && !string.IsNullOrWhiteSpace(Realm))
            return $"{TrimTrailingSlash(PublicAuthServerUrl)}/realms/{Realm}";

        return GetBackchannelAuthority();
    }

    public string? GetBackchannelAuthority()
    {
        if (!string.IsNullOrWhiteSpace(AuthServerUrl) && !string.IsNullOrWhiteSpace(Realm))
            return $"{TrimTrailingSlash(AuthServerUrl)}/realms/{Realm}";

        if (!string.IsNullOrWhiteSpace(Authority))
            return TrimTrailingSlash(Authority);

        return null;
    }

    public string? GetClientId() =>
        !string.IsNullOrWhiteSpace(ClientId) ? ClientId : Audience;

    private static string TrimTrailingSlash(string value) => value.Trim().TrimEnd('/');
}
