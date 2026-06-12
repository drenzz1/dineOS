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
    public string? AdminBaseUrl { get; set; }
    public string GoogleProviderAlias { get; set; } = "google";
    public string GoogleClientId { get; set; } = "dineos-google";
    public string? GoogleClientSecret { get; set; }
    public string? GoogleCallbackUrl { get; set; }
    public string? FrontendUrl { get; set; }

    public string GetAdminBaseUrl() =>
        TrimTrailingSlash(
            !string.IsNullOrWhiteSpace(AdminBaseUrl) ? AdminBaseUrl
            : !string.IsNullOrWhiteSpace(AuthServerUrl) ? AuthServerUrl
            : throw new InvalidOperationException("Keycloak:AdminBaseUrl or Keycloak:AuthServerUrl must be configured."));

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

    public string? GetAuthorizationEndpoint() =>
        BuildOpenIdConnectEndpoint(GetIssuerAuthority(), "auth");

    public string? GetTokenEndpoint() =>
        BuildOpenIdConnectEndpoint(GetIssuerAuthority(), "token");

    public string? GetBackchannelTokenEndpoint() =>
        BuildOpenIdConnectEndpoint(GetBackchannelAuthority(), "token");

    public string? GetBackchannelRevocationEndpoint() =>
        BuildOpenIdConnectEndpoint(GetBackchannelAuthority(), "revoke");

    public string GetGoogleCallbackUrl() =>
        GetRequiredAbsoluteUrl(GoogleCallbackUrl, "Keycloak:GoogleCallbackUrl");

    public string GetFrontendUrl() =>
        TrimTrailingSlash(GetRequiredAbsoluteUrl(FrontendUrl, "Keycloak:FrontendUrl"));

    private static string? BuildOpenIdConnectEndpoint(string? authority, string endpoint) =>
        string.IsNullOrWhiteSpace(authority)
            ? null
            : $"{TrimTrailingSlash(authority)}/protocol/openid-connect/{endpoint}";

    private static string GetRequiredAbsoluteUrl(string? value, string settingName) =>
        !string.IsNullOrWhiteSpace(value)
        && Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri.ToString()
            : throw new InvalidOperationException($"{settingName} must be configured as an absolute URL.");

    private static string TrimTrailingSlash(string value) => value.Trim().TrimEnd('/');
}
