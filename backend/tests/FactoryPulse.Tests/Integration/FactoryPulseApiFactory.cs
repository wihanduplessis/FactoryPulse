using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace FactoryPulse.Tests.Integration;

/// <summary>
/// Boots the real API in-process against a real SQL Server running in a
/// throwaway Docker container. Migrations and identity seeding run on startup,
/// so each test run starts from a clean, fully-migrated database.
/// </summary>
public class FactoryPulseApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@integration.test";
    public const string AdminPassword = "Integration123!";

    private readonly MsSqlContainer _database =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = new SqlConnectionStringBuilder(_database.GetConnectionString())
        {
            InitialCatalog = "FactoryPulseIntegrationDb"
        }.ConnectionString;

        builder.UseEnvironment("Development");

        builder.UseSetting("ConnectionStrings:FactoryPulseDatabase", connectionString);
        builder.UseSetting("JwtSettings:Issuer", "FactoryPulse");
        builder.UseSetting("JwtSettings:Audience", "FactoryPulseClients");
        builder.UseSetting("JwtSettings:Key", "integration-tests-signing-key-at-least-32-chars");
        builder.UseSetting("JwtSettings:AccessTokenMinutes", "30");
        builder.UseSetting("SeedAdmin:Email", AdminEmail);
        builder.UseSetting("SeedAdmin:Password", AdminPassword);
        builder.UseSetting("ApplyMigrationsOnStartup", "true");
        builder.UseSetting("UseHttpsRedirection", "false");
    }

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        await base.DisposeAsync();
    }
}
