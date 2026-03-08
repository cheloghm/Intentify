using Intentify.Modules.Intelligence.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Intentify.Modules.Intelligence.Api;

public sealed class RecurringIntelligenceRefreshWorker(
    RecurringIntelligenceRefreshOrchestrator orchestrator,
    ILogger<RecurringIntelligenceRefreshWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!orchestrator.IsEnabled)
        {
            logger.LogInformation("Recurring intelligence refresh worker is disabled.");
            return;
        }

        using var timer = new PeriodicTimer(orchestrator.PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await orchestrator.RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Recurring intelligence refresh worker failed during execution.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }
}
