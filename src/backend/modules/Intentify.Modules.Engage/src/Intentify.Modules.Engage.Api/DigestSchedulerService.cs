using Intentify.Modules.Engage.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Intentify.Modules.Engage.Api;

/// <summary>
/// Background service that fires digest generation for every bot that has
/// digest email enabled.  Runs once per hour; each bot's configured frequency
/// (daily / weekly) gates whether the digest is actually sent on any given run.
/// </summary>
internal sealed class DigestSchedulerService(
    IServiceProvider serviceProvider,
    IEngageBotRepository botRepository,
    ILogger<DigestSchedulerService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DigestSchedulerService started.");

        // Stagger the first run slightly so it doesn't compete with startup I/O.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken);
            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        IReadOnlyList<EngageBotDigestInfo> bots;
        try
        {
            bots = await botRepository.ListDigestEnabledBotsAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DigestSchedulerService: failed to list digest-enabled bots.");
            return;
        }

        if (bots.Count == 0)
        {
            return;
        }

        logger.LogInformation("DigestSchedulerService: running digest cycle for {Count} bot(s).", bots.Count);

        foreach (var bot in bots)
        {
            if (ct.IsCancellationRequested) break;
            await SendDigestForBotAsync(bot, ct);
        }
    }

    private async Task SendDigestForBotAsync(EngageBotDigestInfo bot, CancellationToken ct)
    {
        try
        {
            // GenerateDigestHandler is registered as Singleton but depends on
            // Scoped repositories — resolve it through a fresh scope per invocation.
            await using var scope = serviceProvider.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GenerateDigestHandler>();

            var result = await handler.HandleAsync(
                new GenerateDigestQuery(bot.TenantId, bot.SiteId), ct);

            if (result.NewLeadsCount == 0 && result.NewTicketsCount == 0 && result.ConversationsCount == 0)
            {
                logger.LogDebug(
                    "DigestSchedulerService: no activity for site {SiteId} — skipping email.",
                    bot.SiteId);
                return;
            }

            logger.LogInformation(
                "DigestSchedulerService: digest generated for site {SiteId} " +
                "({Leads} leads, {Tickets} tickets, {Conversations} conversations).",
                bot.SiteId, result.NewLeadsCount, result.NewTicketsCount, result.ConversationsCount);

            // TODO: plug in email delivery (SMTP / SendGrid / etc.) using
            //       bot.DigestEmailRecipients and the DigestResult above.
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "DigestSchedulerService: error generating digest for site {SiteId}.", bot.SiteId);
        }
    }
}
