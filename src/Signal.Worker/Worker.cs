using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Signal.Application.Services;

namespace Signal.Worker;

public class Worker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<Worker> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromMinutes(30);

    public Worker(IServiceProvider serviceProvider, ILogger<Worker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Signal Ingestion Worker starting. Polling interval: {Interval}", _pollingInterval);

        // Allow app host to stabilize
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Executing scheduled source ingestion pass at: {Time}", DateTimeOffset.UtcNow);

                using (var scope = _serviceProvider.CreateScope())
                {
                    var ingestionService = scope.ServiceProvider.GetRequiredService<SourceIngestionService>();
                    var summary = await ingestionService.IngestAllActiveSourcesAsync(stoppingToken);

                    _logger.LogInformation(
                        "Ingestion complete. Checked: {Checked}, Discovered: {Discovered}, New: {New}, Duplicates: {Dupes}",
                        summary.SourcesChecked,
                        summary.ItemsDiscovered,
                        summary.NewItemsSaved,
                        summary.DuplicatesFiltered);

                    if (summary.Errors.Count > 0)
                    {
                        foreach (var err in summary.Errors)
                        {
                            _logger.LogWarning("Ingestion warning/error: {Error}", err);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in Signal ingestion worker loop.");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("Signal Ingestion Worker stopped gracefully.");
    }
}
