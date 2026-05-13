using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Infrastructure;
using DineOS.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DineOS.Tests.Unit;

public class AiProviderRegistrationTests
{
    [Theory]
    [InlineData(AiProviderOptions.Providers.Anthropic, typeof(AnthropicAiClient))]
    [InlineData(AiProviderOptions.Providers.OpenAI, typeof(OpenAiClient))]
    [InlineData(AiProviderOptions.Providers.Google, typeof(GoogleAiClient))]
    public void AddInfrastructure_ResolvesConfiguredAiProvider(string provider, Type expectedType)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=dineos_test;Username=postgres;Password=postgres",
                ["RabbitMq:Enabled"] = "false",
                ["Ai:Provider"] = provider,
                ["Anthropic:ApiKey"] = "anthropic-key",
                ["OpenAI:ApiKey"] = "openai-key",
                ["GoogleAI:ApiKey"] = "google-key",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        using var providerRoot = services.BuildServiceProvider();
        using var scope = providerRoot.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAiClient>();

        Assert.IsType(expectedType, client);
    }
}
