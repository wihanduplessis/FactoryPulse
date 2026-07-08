using FactoryPulse.Application.Interfaces;
using FactoryPulse.Application.Services;
using FactoryPulse.Application.Validators;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryPulse.Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IMachineService, MachineService>();
        services.AddScoped<IProductionOrderService, ProductionOrderService>();
        services.AddValidatorsFromAssemblyContaining<CreateMachineRequestValidator>();
        return services;
    }
}
