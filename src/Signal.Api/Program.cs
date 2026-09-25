using Microsoft.EntityFrameworkCore;
using Signal.Application.Common.Interfaces;
using Signal.Application.Common.Models;
using Signal.Application.Services;
using Signal.Infrastructure;
using Signal.Infrastructure.Data;
using Signal.Infrastructure.Persistence;
using Signal.Infrastructure.Providers.Telegram;

var builder = WebApplication.CreateBuilder(args);

// Add Infrastructure & Application Services
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Auto-migrate / Ensure SQLite Database & Seed Default Sources
using (var scope = app.Services.CreateScope())
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
        logger.LogError(ex, "An error occurred while initializing the database.");
    }
}

app.UseHttpsRedirection();

// Root & Health
app.MapGet("/", () => Results.Ok(new
{
    Name = "Signal API",
    Version = "1.0.0",
    Status = "Healthy",
    Tagline = "Your personal information firewall. Signal over noise."
}));

app.MapGet("/health", () => Results.Ok(new
{
    Status = "UP",
    Timestamp = DateTimeOffset.UtcNow,
    Database = "SQLite (Active)"
}));

// Sources Endpoints
app.MapGet("/api/sources", async (ISignalDbContext db) =>
{
    var sources = await db.Sources.AsNoTracking().ToListAsync();
    return Results.Ok(sources);
});

// Feed Endpoints (ContentItems)
app.MapGet("/api/feed", async (ISignalDbContext db, int? limit) =>
{
    var take = Math.Clamp(limit ?? 20, 1, 100);
    var items = await db.ContentItems
        .AsNoTracking()
        .Include(c => c.Source)
        .OrderByDescending(c => c.PublishedAt)
        .Take(take)
        .Select(c => new
        {
            c.Id,
            c.Title,
            c.Url,
            c.CanonicalUrl,
            c.Author,
            c.PublishedAt,
            c.DiscoveredAt,
            c.Language,
            c.Category,
            c.Summary,
            SourceName = c.Source.Name,
            c.Source.TrustTier
        })
        .ToListAsync();

    return Results.Ok(items);
});

// Trigger Ingestion Pipeline
app.MapPost("/api/ingest", async (SourceIngestionService ingestionService, CancellationToken ct) =>
{
    var summary = await ingestionService.IngestAllActiveSourcesAsync(ct);
    return Results.Ok(summary);
});

// Phase 2: Check URL Endpoint
app.MapPost("/api/check", async (
    CheckUrlRequest request,
    TelegramBotService botService,
    ISignalDbContext db,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Url))
        return Results.BadRequest(new { Error = "URL is required" });

    var analysis = await botService.HandleCommandAsync($"/check {request.Url}", db, ct);
    return Results.Ok(new { Response = analysis });
});

// Phase 2: Offers Endpoint
app.MapGet("/api/offers", async (ISignalDbContext db) =>
{
    var offers = await db.Opportunities
        .AsNoTracking()
        .Include(o => o.ContentItem)
        .OrderByDescending(o => o.CreatedAt)
        .Take(50)
        .ToListAsync();
    return Results.Ok(offers);
});

// Telegram Webhook Endpoint
app.MapPost("/api/telegram/webhook", async (
    TelegramUpdate update,
    TelegramBotService botService,
    ISignalDbContext db,
    CancellationToken ct) =>
{
    if (update.Message?.Text is { } text)
    {
        var reply = await botService.HandleCommandAsync(text, db, ct);
        return Results.Ok(new { Handled = true, Reply = reply });
    }

    return Results.Ok(new { Handled = false });
});

app.Run();

public record CheckUrlRequest(string Url);

// Required for WebApplicationFactory in integration tests
public partial class Program { }

