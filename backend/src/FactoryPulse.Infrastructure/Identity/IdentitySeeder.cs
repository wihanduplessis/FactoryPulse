using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace FactoryPulse.Infrastructure.Identity;

public class IdentitySeeder
{
    private static readonly string[] RoleNames = ["Admin", "Manager", "Viewer"];

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;

    public IdentitySeeder(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
    }

    public async Task SeedAsync()
    {
        foreach (var roleName in RoleNames)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        var adminEmail = _configuration["SeedAdmin:Email"] ?? "admin@factorypulse.local";
        var adminPassword = _configuration["SeedAdmin:Password"];

        if (adminPassword is not null && await _userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "System Administrator",
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(admin, "Admin");
            }
        }
    }
}
