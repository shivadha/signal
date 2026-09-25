using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Signal.Application.Common.Interfaces;
using Signal.Application.Services;
using Signal.Infrastructure.Persistence;
using Signal.Infrastructure.Providers;
using Signal.Infrastructure.Providers.AI;
using Signal.Infrastructure.Providers.Telegram;

namespace Signal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? configuration["DATABASE_CONNECTION_STRING"]
                               ?? "Data Source=data/signal.db";

        // Ensure data directory exists if relative path is used
        if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            var pathPart = connectionString.Split("Data Source=")[1].Split(';')[0].Trim();
            var dir = Path.GetDirectoryName(pathPart);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        services.AddDbContext<SignalDbContext>(options =>
        {
            options.UseSqlite(connectionString);
        });

        services.AddScoped<ISignalDbContext>(sp => sp.GetRequiredService<SignalDbContext>());

        // HTTP Client for RSS ingestion
        services.AddHttpClient<RssSourceProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        services.AddScoped<ISourceProvider, RssSourceProvider>();

        // AI Provider registration based on configuration
        var aiProviderName = (configuration["AI_PROVIDER"] ?? "none").ToLowerInvariant();
        switch (aiProviderName)
        {
            case "none":
            default:
                services.AddScoped<IAIProvider, NoneAiProvider>();
                break;
        }

        // Telegram Bot Provider
        services.AddHttpClient<TelegramBotService>();
        services.AddScoped<ITelegramProvider, TelegramBotService>();
        services.AddScoped<TelegramBotService>();

        // Application services
        services.AddSingleton<IContentNormalizer, ContentNormalizationService>();
        services.AddScoped<IDuplicateDetector, DuplicateDetectionService>();
        services.AddScoped<SourceIngestionService>();

        return services;
    }
}
