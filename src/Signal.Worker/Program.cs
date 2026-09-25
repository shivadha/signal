using Signal.Infrastructure;
using Signal.Infrastructure.Data;
using Signal.Infrastructure.Persistence;
using Signal.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Add Infrastructure & Application services
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Ensure DB exists and seeded
using (var scope = host.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = services.GetRequiredService<SignalDbContext>();
        await db.Database.EnsureCreatedAsync();
        await SourceSeeder.SeedAsync(db, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to initialize SQLite database in Worker.");
    }
}

host.Run();
