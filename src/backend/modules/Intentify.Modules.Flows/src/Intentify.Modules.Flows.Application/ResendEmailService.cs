using System.Net.Http.Json;
using System.Text.Json;

namespace Intentify.Modules.Flows.Application;

/// <summary>
/// Sends transactional email via the Resend API (https://resend.com).
/// Free tier: 3,000 emails/month, 100/day. No SDK needed — pure HTTP.
///
/// .env configuration:
///   Intentify__Email__Provider=Resend
///   Intentify__Email__Resend__ApiKey=re_YOUR_KEY
///   Intentify__Email__Resend__FromAddress=notify@yourdomain.com
///   Intentify__Email__Resend__FromName=Intentify
/// </summary>
public sealed class ResendEmailService(IHttpClientFactory httpClientFactory, ResendEmailOptions options)
{
    public const string ClientName = "resend-email";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(options.ApiKey) &&
        !string.IsNullOrWhiteSpace(options.FromAddress);

    public async Task<(bool Success, string? Error)> SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
            return (false, "Email is not configured. Add Intentify__Email__Resend__ApiKey and Intentify__Email__Resend__FromAddress to your .env file.");

        try
        {
            using var client = httpClientFactory.CreateClient(ClientName);

            var fromDisplay = string.IsNullOrWhiteSpace(options.FromName)
                ? options.FromAddress
                : $"{options.FromName} <{options.FromAddress}>";

            var payload = new
            {
                from    = fromDisplay,
                to      = new[] { to },
                subject = subject,
                html    = htmlBody
            };

            var response = await client.PostAsJsonAsync("emails", payload, ct);

            if (response.IsSuccessStatusCode)
                return (true, null);

            var body = await response.Content.ReadAsStringAsync(ct);
            return (false, $"Resend returned {(int)response.StatusCode}: {body[..Math.Min(200, body.Length)]}");
        }
        catch (Exception ex)
        {
            return (false, $"Email send failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends a plain lead-capture notification to the business owner.
    /// Called automatically by the Flows engine when a lead is captured.
    /// </summary>
    public Task<(bool Success, string? Error)> SendLeadNotificationAsync(
        string ownerEmail,
        string? leadName,
        string? leadEmail,
        string? opportunityLabel,
        string? conversationSummary,
        string siteDomain,
        CancellationToken ct = default)
    {
        var name    = leadName  ?? "Anonymous";
        var email   = leadEmail ?? "—";
        var opp     = opportunityLabel ?? "—";
        var summary = conversationSummary ?? "No summary available.";

        var html = $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:sans-serif;color:#1e293b;max-width:560px;margin:0 auto;padding:24px">
              <div style="background:#6366f1;border-radius:10px;padding:20px 24px;margin-bottom:24px">
                <h1 style="color:#fff;margin:0;font-size:18px">⭐ New Lead Captured</h1>
                <p style="color:#c7d2fe;margin:6px 0 0;font-size:13px">{siteDomain}</p>
              </div>
              <table style="width:100%;border-collapse:collapse;font-size:14px">
                <tr><td style="padding:8px 0;color:#94a3b8;width:120px">Name</td><td style="padding:8px 0;font-weight:600">{name}</td></tr>
                <tr><td style="padding:8px 0;color:#94a3b8">Email</td><td style="padding:8px 0;font-weight:600">{email}</td></tr>
                <tr><td style="padding:8px 0;color:#94a3b8">Intent</td><td style="padding:8px 0">{opp}</td></tr>
              </table>
              <div style="background:#f8fafc;border-radius:8px;padding:16px;margin:20px 0;font-size:13px;color:#475569;line-height:1.6">
                <strong style="display:block;margin-bottom:6px;color:#1e293b">Conversation Summary</strong>
                {summary}
              </div>
              <a href="https://app.intentify.io/leads" style="display:inline-block;background:#6366f1;color:#fff;text-decoration:none;padding:10px 20px;border-radius:8px;font-size:13px;font-weight:600">
                View Lead in Intentify →
              </a>
              <p style="font-size:11px;color:#94a3b8;margin-top:24px">Intentify · Visitor Intelligence Platform</p>
            </body>
            </html>
            """;

        return SendAsync(ownerEmail, $"New lead: {name} ({email})", html, ct);
    }
}

public sealed class ResendEmailOptions
{
    public const string ConfigurationSection = "Intentify:Email:Resend";
    public string? ApiKey       { get; set; }
    public string? FromAddress  { get; set; }
    public string? FromName     { get; set; } = "Intentify";
}

public sealed class EmailProviderOptions
{
    public const string ConfigurationSection = "Intentify:Email";
    // "Resend" | "None"
    public string Provider { get; set; } = "None";
}
