using Intentify.Modules.Engage.Application;
using Intentify.Modules.Flows.Application;

namespace Intentify.Modules.Engage.Api;

/// <summary>
/// Sends a real-time "hot lead" alert email via Resend when a lead is
/// captured through the Engage widget.
/// Lives in Engage.Api so it can reference both Engage.Application
/// (for IEngageBotRepository) and Flows.Application (for ResendEmailService)
/// without introducing a circular project reference.
/// </summary>
internal sealed class ResendLeadNotificationService(
    IEngageBotRepository botRepository,
    ResendEmailService   emailService) : ILeadNotificationService
{
    public void NotifyHotLead(
        Guid    tenantId,
        Guid    siteId,
        string? capturedName,
        string? capturedEmail,
        string? userMessage,
        int?    intentScore,
        string? opportunityLabel)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (!emailService.IsConfigured) return;

                var bot = await botRepository.GetBySiteAsync(tenantId, siteId);
                if (bot is null) return;

                var recipients = (bot.DigestEmailRecipients ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (recipients.Length == 0) return;

                var name     = capturedName  ?? "Anonymous visitor";
                var email    = capturedEmail ?? "—";
                var asked    = userMessage   ?? "—";
                var siteName = bot.Name ?? bot.DisplayName ?? siteId.ToString();
                var subject  = $"🔥 Hot lead on {siteName}";
                var html     = BuildHtml(name, email, asked, intentScore, opportunityLabel, siteName);

                foreach (var recipient in recipients)
                    await emailService.SendAsync(recipient, subject, html, CancellationToken.None);
            }
            catch
            {
                // Fire-and-forget — swallow all exceptions
            }
        });
    }

    private static string BuildHtml(
        string name, string email, string asked,
        int? intentScore, string? opportunity, string siteName)
    {
        var intentRow = intentScore.HasValue
            ? $"<tr><td style='padding:8px 0;color:#94a3b8;width:120px'>Intent</td><td style='padding:8px 0;font-weight:600;color:#6366f1'>Score: {intentScore}</td></tr>"
            : "";
        var oppRow = opportunity is not null
            ? $"<tr><td style='padding:8px 0;color:#94a3b8'>Opportunity</td><td style='padding:8px 0'>{Esc(opportunity)}</td></tr>"
            : "";
        return $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:sans-serif;color:#1e293b;max-width:560px;margin:0 auto;padding:24px">
              <div style="background:linear-gradient(135deg,#0f172a,#1e293b);border-radius:10px;padding:20px 24px;margin-bottom:24px">
                <h1 style="color:#fff;margin:0;font-size:18px">🔥 Hot Lead Alert</h1>
                <p style="color:#c7d2fe;margin:6px 0 0;font-size:13px">{Esc(siteName)}</p>
              </div>
              <table style="width:100%;border-collapse:collapse;font-size:14px;margin-bottom:20px">
                <tr><td style="padding:8px 0;color:#94a3b8;width:120px">Name</td><td style="padding:8px 0;font-weight:600">{Esc(name)}</td></tr>
                <tr><td style="padding:8px 0;color:#94a3b8">Email</td><td style="padding:8px 0;font-weight:600">{Esc(email)}</td></tr>
                {oppRow}{intentRow}
              </table>
              <div style="background:#fef3c7;border-left:3px solid #f59e0b;border-radius:0 8px 8px 0;padding:12px 16px;margin-bottom:20px;font-size:13px;color:#92400e;line-height:1.6">
                <strong style="display:block;margin-bottom:6px">What they asked:</strong>
                {Esc(asked)}
              </div>
              <a href="https://app.hven.io/leads" style="display:inline-block;background:#6366f1;color:#fff;text-decoration:none;padding:10px 20px;border-radius:8px;font-size:13px;font-weight:600">View lead in Hven →</a>
              <p style="font-size:11px;color:#94a3b8;margin-top:24px">Hven · Visitor Intelligence Platform</p>
            </body>
            </html>
            """;
    }

    private static string Esc(string? s) =>
        (s ?? string.Empty)
            .Replace("&", "&amp;").Replace("<", "&lt;")
            .Replace(">", "&gt;").Replace("\"", "&quot;");
}
