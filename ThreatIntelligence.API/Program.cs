using Scalar.AspNetCore;
using ThreatIntelligence.Infrastructure;
using ThreatIntelligence.Application;
using DotNetEnv;

var builder = WebApplication.CreateBuilder(args);

var envPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".env");
Env.Load(envPath);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

using (var scope = builder.Services.BuildServiceProvider().CreateScope())
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();