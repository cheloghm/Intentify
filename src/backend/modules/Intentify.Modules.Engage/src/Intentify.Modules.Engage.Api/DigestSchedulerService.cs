using Intentify.Modules.Engage.Application;
using Intentify.Modules.Flows.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Intentify.Modules.Engage.Api;

/// <summary>
/// Background service that sends the weekly digest email for every bot that
/// has DigestEmailEnabled = true and DigestEmailRecipients configured.
///
/// Schedule: checks every hour; only sends on Sunday 19:00–20:00 UTC
/// (arrives Monday morning for European/US timezones).
///
/// Force a test send: set Intentify__Engage__DigestForceRun=true in .env.
/// Email delivery: uses ResendEmailService registered in FlowsModule.
///
/// Required .env keys for email:
///   Intentify__Email__Resend__ApiKey=re_YOUR_KEY
///   Intentify__Email__Resend__FromAddress=hello@hven.io
///   Intentify__Email__Resend__FromName=Hven
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

        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken);
            try { await Task.Delay(CheckInterval, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }

        logger.LogInformation("DigestSchedulerService stopped.");
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        var now       = DateTime.UtcNow;
        var isSendDay = now.DayOfWeek == DayOfWeek.Sunday && now.Hour == 19;

        if (!isSendDay)
        {
            // Allow force-run via config flag — use indexer (no Binder extension needed)
            try
            {
                await using var cfgScope = serviceProvider.CreateAsyncScope();
                var cfg = cfgScope.ServiceProvider
                    .GetService<Microsoft.Extensions.Configuration.IConfiguration>();
                var flag = cfg?["Intentify:Engage:DigestForceRun"];
                var forceRun = string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);
                if (!forceRun) return;

                logger.LogInformation("DigestSchedulerService: force-run flag set — running digest now.");
            }
            catch { return; }
        }

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
            logger.LogDebug("DigestSchedulerService: no bots with digest enabled.");
            return;
        }

        logger.LogInformation("DigestSchedulerService: starting weekly digest for {Count} bot(s).", bots.Count);

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
            await using var scope = serviceProvider.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GenerateDigestHandler>();
            var email   = scope.ServiceProvider.GetService<ResendEmailService>();

            var result = await handler.HandleAsync(new GenerateDigestQuery(bot.TenantId, bot.SiteId), ct);

            if (result.NewLeadsCount == 0 && result.NewTicketsCount == 0 && result.ConversationsCount == 0)
            {
                logger.LogDebug("DigestSchedulerService: no activity for site {SiteId} — skipping.", bot.SiteId);
                return;
            }

            if (email is null || !email.IsConfigured)
            {
                logger.LogWarning(
                    "DigestSchedulerService: digest ready for site {SiteId} but Resend is not configured. " +
                    "Add Intentify__Email__Resend__ApiKey and Intentify__Email__Resend__FromAddress to .env.",
                    bot.SiteId);
                return;
            }

            var recipients = (bot.DigestEmailRecipients ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (recipients.Length == 0)
            {
                logger.LogWarning("DigestSchedulerService: site {SiteId} has no recipients.", bot.SiteId);
                return;
            }

            var subject = $"📊 Your Weekly Hven Report — {DateTime.UtcNow:MMMM d, yyyy}";
            var html    = BuildDigestHtml(bot.Name ?? bot.DisplayName ?? "Intentify", result);

            foreach (var recipient in recipients)
            {
                var (success, error) = await email.SendAsync(recipient, subject, html, ct);
                if (success)
                    logger.LogInformation("Digest sent to {Email} for site {SiteId}.", recipient, bot.SiteId);
                else
                    logger.LogWarning("Digest failed for {Email} / {SiteId}: {Error}", recipient, bot.SiteId, error);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DigestSchedulerService: error for site {SiteId}.", bot.SiteId);
        }
    }

    private static string BuildDigestHtml(string botName, DigestResult d)
    {
        var aiNarrativeHtml = string.IsNullOrWhiteSpace(d.AiNarrative) ? "" :
            $"<div style='background:linear-gradient(135deg,#0f172a,#1e293b);border-radius:12px;padding:20px 24px;margin-bottom:24px'>" +
            $"<div style='color:#818cf8;font-size:10px;font-weight:700;letter-spacing:.1em;text-transform:uppercase;margin-bottom:8px'>🤖 AI Weekly Summary</div>" +
            $"<div style='color:#f1f5f9;font-size:14px;line-height:1.7'>{Esc(d.AiNarrative)}</div>" +
            $"</div>";

        var leadRows = d.NewLeads.Any()
            ? string.Join("", d.NewLeads.Take(5).Select(l =>
                $"<tr>" +
                $"<td style='padding:8px 12px;border-bottom:1px solid #f1f5f9;font-weight:600;color:#1e293b'>{Esc(l.Name ?? "Anonymous")}</td>" +
                $"<td style='padding:8px 12px;border-bottom:1px solid #f1f5f9;color:#64748b;font-family:monospace;font-size:12px'>{Esc(l.Email ?? "—")}</td>" +
                $"<td style='padding:8px 12px;border-bottom:1px solid #f1f5f9;color:#6366f1;font-weight:500'>{Esc(l.OpportunityLabel ?? "—")}</td>" +
                $"</tr>"))
            : "<tr><td colspan='3' style='padding:16px;text-align:center;color:#94a3b8;font-style:italic'>No new leads this week</td></tr>";

        var topOppHtml = d.TopOpportunity is { } top
            ? $"<div style='background:#eef2ff;border-left:3px solid #6366f1;border-radius:0 8px 8px 0;padding:12px 16px;margin:16px 0;font-size:13px;color:#334155;line-height:1.6'>" +
              $"🌟 <strong>Top opportunity:</strong> {Esc(top.Name ?? top.Email ?? "—")} — {Esc(top.OpportunityLabel ?? "—")}" +
              (top.IntentScore.HasValue ? $" <span style='color:#6366f1;font-weight:600'>(score: {top.IntentScore})</span>" : "") +
              $"</div>"
            : "";

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',system-ui,sans-serif;color:#1e293b;max-width:600px;margin:0 auto;padding:24px;background:#f8fafc">
              <div style="background:linear-gradient(135deg,#0f172a,#1e293b);border-radius:12px;padding:24px 28px;margin-bottom:20px">
                <h1 style="color:#f8fafc;margin:0 0 4px;font-size:18px;font-weight:700;letter-spacing:-.01em">📊 Weekly Report</h1>
                <p style="color:#64748b;margin:0;font-size:13px">{Esc(botName)} · {DateTime.UtcNow:MMMM d, yyyy}</p>
              </div>
              {aiNarrativeHtml}
              <table width="100%" style="border-collapse:collapse;margin-bottom:20px"><tr>
                {StatCard("⭐","New Leads",d.NewLeadsCount.ToString())}
                {StatCard("🎫","New Tickets",d.NewTicketsCount.ToString())}
                {StatCard("💬","Conversations",d.ConversationsCount.ToString())}
              </tr></table>
              {topOppHtml}
              <div style="background:#fff;border:1px solid #e2e8f0;border-radius:12px;overflow:hidden;margin-bottom:20px">
                <div style="padding:10px 14px;border-bottom:1px solid #f1f5f9;font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:#94a3b8">New Leads This Week</div>
                <table width="100%" style="border-collapse:collapse;font-size:13px">
                  <thead><tr style="background:#f8fafc">
                    <th style="padding:8px 12px;text-align:left;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:#94a3b8;border-bottom:1px solid #e2e8f0">Name</th>
                    <th style="padding:8px 12px;text-align:left;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:#94a3b8;border-bottom:1px solid #e2e8f0">Email</th>
                    <th style="padding:8px 12px;text-align:left;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:#94a3b8;border-bottom:1px solid #e2e8f0">Opportunity</th>
                  </tr></thead>
                  <tbody>{leadRows}</tbody>
                </table>
              </div>
              <div style="text-align:center;margin-bottom:24px">
                <a href="https://www.hven.io/#/dashboard" style="display:inline-block;background:#6366f1;color:#fff;text-decoration:none;padding:12px 28px;border-radius:8px;font-size:13px;font-weight:600">View Full Dashboard →</a>
              </div>
              <p style="font-size:11px;color:#94a3b8;text-align:center;margin:0;line-height:1.6">
                You're receiving this because digest email is enabled for your Hven site.<br>
                Manage in <strong>Engage → Bot Config → Digest Email</strong>.
              </p>
            </body></html>
            """;
    }

    private static string StatCard(string icon, string label, string value) =>
        $"<td style='width:33.3%;padding:4px'><div style='background:#fff;border:1px solid #e2e8f0;border-radius:10px;padding:14px;text-align:center'>" +
        $"<div style='font-size:22px;margin-bottom:4px'>{icon}</div>" +
        $"<div style='font-family:monospace;font-size:24px;font-weight:700;color:#0f172a;line-height:1'>{value}</div>" +
        $"<div style='font-size:10px;color:#94a3b8;text-transform:uppercase;letter-spacing:.06em;font-weight:700;margin-top:4px'>{label}</div>" +
        $"</div></td>";

    private static string Esc(string s) =>
        s.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;").Replace("\"","&quot;");
}
