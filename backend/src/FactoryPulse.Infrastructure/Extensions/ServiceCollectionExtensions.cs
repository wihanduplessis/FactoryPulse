using FactoryPulse.Application.Interfaces;
using FactoryPulse.Infrastructure.Persistence;
using FactoryPulse.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryPulse.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("FactoryPulseDatabase");

        services.AddDbContext<FactoryPulseDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IMachineRepository, MachineRepository>();
        services.AddScoped<IProductionOrderRepository, ProductionOrderRepository>();

        return services;
    }
}
