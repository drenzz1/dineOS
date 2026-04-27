using Microsoft.Extensions.DependencyInjection;

namespace DineOS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
