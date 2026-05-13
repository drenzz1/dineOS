using DineOS.Application.Interfaces.Services;
using DineOS.Infrastructure;
using DineOS.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DineOS.Tests.Unit;

public class DatabaseMigratorRegistrationTests
{
    [Fact]
    public void AddInfrastructure_RegistersEfDatabaseMigrator()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=dineos_test;Username=postgres;Password=postgres",
                ["RabbitMq:Enabled"] = "false",
            })
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(IDatabaseMigrator));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(EfDatabaseMigrator), descriptor.ImplementationType);
    }
}
