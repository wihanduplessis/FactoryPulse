using Swashbuckle.AspNetCore.SwaggerUI;
using FactoryPulse.Infrastructure.Extensions;
using FactoryPulse.Application.Extensions;
using FactoryPulse.API.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "FactoryPulse API");
    });
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
