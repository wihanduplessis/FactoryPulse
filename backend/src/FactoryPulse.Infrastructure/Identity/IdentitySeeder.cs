using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FactoryPulse.Infrastructure.Identity;

public class IdentitySeeder
{
    private static readonly string[] RoleNames = ["Admin", "Manager", "Viewer"];

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IdentitySeeder> _logger;

    public IdentitySeeder(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        ILogger<IdentitySeeder> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _logger = logger;
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

        var adminEmail = _configuration["SeedAdmin:Email"];
        var adminPassword = _configuration["SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            adminEmail = "admin@factorypulse.local";
        }

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            _logger.LogWarning("SeedAdmin:Password is not configured - skipping admin user seeding.");
            return;
        }

        if (await _userManager.FindByEmailAsync(adminEmail) is not null)
        {
            return;
        }

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
            _logger.LogInformation("Seeded admin user {Email}.", adminEmail);
        }
        else
        {
            _logger.LogError(
                "Failed to seed admin user: {Errors}",
                string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}
