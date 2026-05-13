using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Testcontainers.Keycloak;

namespace DineOS.Tests.Fixtures;

public sealed class KeycloakContainerFixture : IAsyncLifetime
{
    private readonly KeycloakContainer _container = BuildContainer();

    public string Realm => "dineos";

    public string BaseUrl => _container.GetBaseAddress().TrimEnd('/');

    public string Authority => $"{BaseUrl}/realms/{Realm}";

    public string TokenEndpoint => $"{Authority}/protocol/openid-connect/token";

    private static KeycloakContainer BuildContainer()
    {
        var realmFile = Path.Combine(
            AppContext.BaseDirectory, "keycloak", "realm-export.json");

        // Use 26.x: kc.sh injects --profile=dev which is a recognized Keycloak 26.x runtime option.
        // On Keycloak 24.x the runtime picocli CLI rejected --profile, causing exit code 2.
        return new KeycloakBuilder("quay.io/keycloak/keycloak:26.1")
            .WithEnvironment("KEYCLOAK_ADMIN", "admin")
            .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
            .WithEnvironment("KC_HOSTNAME_STRICT", "false")
            .WithEnvironment("KC_HTTP_ENABLED", "true")
            .WithRealm(realmFile)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .AddCustomWaitStrategy(
                        new HttpWaitStrategy()
                            .ForPath("/realms/dineos/.well-known/openid-configuration")
                            .ForPort(8080),
                        s => s.WithTimeout(TimeSpan.FromSeconds(120))))
            .Build();
    }

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
