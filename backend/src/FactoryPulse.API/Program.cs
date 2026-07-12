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
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.HttpOverrides;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

var keyVaultUri = builder.Configuration["KeyVaultUri"];

if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
}

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
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("Version", builder.Configuration.GetValue("BuildSha", defaultValue: "local")));

if (!string.IsNullOrWhiteSpace(builder.Configuration["ApplicationInsights:ConnectionString"]))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

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

var loginPermitLimit = builder.Configuration.GetValue("RateLimiting:Login:PermitLimit", defaultValue: 5);
var loginWindowSeconds = builder.Configuration.GetValue("RateLimiting:Login:WindowSeconds", defaultValue: 60);

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", httpContext =>
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = loginPermitLimit,
                Window = TimeSpan.FromSeconds(loginWindowSeconds),
                QueueLimit = 0
            });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests",
            Detail = "Too many sign-in attempts. Try again shortly."
        }, cancellationToken);
    };
});

var useForwardedHeaders = builder.Configuration.GetValue("UseForwardedHeaders", defaultValue: false);

if (useForwardedHeaders)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

var app = builder.Build();

if (useForwardedHeaders)
{
    app.UseForwardedHeaders();
}

if (builder.Configuration.GetValue("ApplyMigrationsOnStartup", defaultValue: false))
{
    using var scope = app.Services.CreateScope();

    var dbContext = scope.ServiceProvider.GetRequiredService<FactoryPulseDbContext>();
    await dbContext.Database.MigrateAsync();
    app.Logger.LogInformation("Database migration complete.");
}

if (builder.Configuration.GetValue("SeedIdentityOnStartup", defaultValue: false))
{
    using var scope = app.Services.CreateScope();

    var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
    await seeder.SeedAsync();
    app.Logger.LogInformation("Identity seeding complete.");
}

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (builder.Configuration.GetValue("EnableSwagger", defaultValue: app.Environment.IsDevelopment()))
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

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

// Exposes the implicit Program class so integration tests can reference it
// via WebApplicationFactory<Program>.
public partial class Program
{
}
