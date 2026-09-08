using Scalar.AspNetCore;
using ThreatIntelligence.API.Middleware;
using ThreatIntelligence.Application;
using ThreatIntelligence.Infrastructure;
using ThreatIntelligence.Infrastructure.Services;
using DotNetEnv;

var builder = WebApplication.CreateBuilder(args);

var envPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".env");
Env.Load(envPath);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var health = scope.ServiceProvider.GetRequiredService<DatabaseHealthService>();
    try
    {
        if (!await health.CanConnectAsync())
        {
            Console.Error.WriteLine($"Database connection cannot be established to {health.GetConnectionTarget()}.");
            Environment.FailFast("Database connection cannot be established.");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Database connection cannot be established to {health.GetConnectionTarget()}. Error: {ex.Message}");
        Environment.FailFast("Database connection cannot be established.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseExceptionHandler();

app.UseAuthorization();

app.MapControllers();

app.Run();
