using DineOS.Application.Authentication;

namespace DineOS.Tests.Unit;

public class KeycloakOptionsTests
{
    [Fact]
    public void GetAuthorizationEndpoint_UsesConfiguredAuthority()
    {
        var options = new KeycloakOptions
        {
            Authority = "http://localhost:8080/realms/dineos/"
        };

        Assert.Equal(
            "http://localhost:8080/realms/dineos/protocol/openid-connect/auth",
            options.GetAuthorizationEndpoint());
    }

    [Fact]
    public void GetBackchannelTokenEndpoint_UsesInternalAuthServerWhenConfigured()
    {
        var options = new KeycloakOptions
        {
            Realm = "dineos",
            Authority = "http://localhost:8080/realms/dineos",
            AuthServerUrl = "http://keycloak:8080/"
        };

        Assert.Equal(
            "http://keycloak:8080/realms/dineos/protocol/openid-connect/token",
            options.GetBackchannelTokenEndpoint());
    }
}
