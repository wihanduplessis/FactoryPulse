using FactoryPulse.Application.Interfaces;
using FactoryPulse.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryPulse.Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IMachineService, MachineService>();
        return services;
    }
}
