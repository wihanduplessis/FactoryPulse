using Microsoft.AspNetCore.Identity;

namespace FactoryPulse.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
}
