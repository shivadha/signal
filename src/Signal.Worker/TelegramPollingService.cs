using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Signal.Application.Common.Interfaces;
using Signal.Infrastructure.Providers.Telegram;

namespace Signal.Worker;

public class TelegramPollingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelegramPollingService> _logger;
    private long _offset = 0;

    public TelegramPollingService(IServiceProvider serviceProvider, ILogger<TelegramPollingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telegram Polling Service starting...");

        // Wait brief delay for host startup
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var botService = scope.ServiceProvider.GetRequiredService<TelegramBotService>();
                var db = scope.ServiceProvider.GetRequiredService<ISignalDbContext>();
                var sheets = scope.ServiceProvider.GetRequiredService<IGoogleSheetsProvider>();

                var updates = await botService.GetUpdatesAsync(_offset, stoppingToken);

                foreach (var update in updates)
                {
                    _offset = update.UpdateId + 1;

                    // Handle text messages and commands
                    if (update.Message?.Text is { } text && update.Message.Chat != null)
                    {
                        var chatId = update.Message.Chat.Id.ToString();
                        _logger.LogInformation("Received Telegram command '{Command}' from Chat {ChatId}", text, chatId);

                        var reply = await botService.HandleCommandAsync(text, db, stoppingToken);
                        await botService.SendMessageAsync(chatId, reply.Text, reply.Markup, stoppingToken);
                    }

                    // Handle inline button callbacks
                    if (update.CallbackQuery is { } cb && cb.Message?.Chat != null && !string.IsNullOrEmpty(cb.Data))
                    {
                        var chatId = cb.Message.Chat.Id.ToString();
                        _logger.LogInformation("Received Telegram callback '{Data}' from Chat {ChatId}", cb.Data, chatId);

                        var result = await botService.HandleCallbackAsync(cb.Data, db, sheets, stoppingToken);
                        await botService.AnswerCallbackQueryAsync(cb.Id, "Processed", stoppingToken);
                        await botService.SendMessageAsync(chatId, result.Text, result.Markup, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error occurred in Telegram polling loop. Retrying in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }

            // Small delay to prevent tight loop if zero updates
            await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
        }

        _logger.LogInformation("Telegram Polling Service stopped.");
    }
}
