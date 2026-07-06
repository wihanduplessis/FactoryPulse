using Swashbuckle.AspNetCore.SwaggerUI;
using FactoryPulse.Infrastructure.Extensions;
using FactoryPulse.Application.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

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
