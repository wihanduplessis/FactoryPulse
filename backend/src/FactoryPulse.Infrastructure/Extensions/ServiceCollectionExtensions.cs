using FactoryPulse.Application.Interfaces;
using FactoryPulse.Infrastructure.Identity;
using FactoryPulse.Infrastructure.Persistence;
using FactoryPulse.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
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
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<FactoryPulseDbContext>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
