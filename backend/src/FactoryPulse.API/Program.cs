using System.Text;
using System.Text.Json.Serialization;
using FactoryPulse.API.Middleware;
using FactoryPulse.Application.Extensions;
using FactoryPulse.Infrastructure.Extensions;
using FactoryPulse.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerUI;
using FactoryPulse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;

if (string.IsNullOrWhiteSpace(jwtSettings.Key) || jwtSettings.Key.Length < 32)
{
    throw new InvalidOperationException(
        "JwtSettings:Key is missing or shorter than 32 characters. " +
        "Set it via user-secrets (local) or the JWT_KEY environment variable (container).");
}

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<FactoryPulse.API.OpenApi.BearerSecuritySchemeTransformer>();
});
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CanWrite", policy => policy.RequireRole("Manager", "Admin"))
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))
        };
    });

var app = builder.Build();

if (builder.Configuration.GetValue("ApplyMigrationsOnStartup", defaultValue: false))
{
    using var scope = app.Services.CreateScope();

    var dbContext = scope.ServiceProvider.GetRequiredService<FactoryPulseDbContext>();
    await dbContext.Database.MigrateAsync();
    app.Logger.LogInformation("Database migration complete.");

    var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
    await seeder.SeedAsync();
    app.Logger.LogInformation("Identity seeding complete.");
}

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "FactoryPulse API");
    });
}

if (builder.Configuration.GetValue("UseHttpsRedirection", defaultValue: true))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

// Exposes the implicit Program class so integration tests can reference it
// via WebApplicationFactory<Program>.
public partial class Program
{
}
