using System.Net.Http.Json;
using System.Text.Json;
using Intentify.Modules.Integrations.Application;
using Intentify.Modules.Integrations.Domain;
using Microsoft.Extensions.Logging;

namespace Intentify.Modules.Integrations.Infrastructure;

public sealed class WebhookDispatcherImpl(
    IWebhookRepository repository,
    IHttpClientFactory httpClientFactory,
    ILogger<WebhookDispatcherImpl> logger) : IWebhookDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task DispatchAsync(WebhookDispatchPayload payload, CancellationToken ct = default)
    {
        IReadOnlyCollection<WebhookEndpoint> endpoints;
        try
        {
            endpoints = await repository.ListByEventAsync(payload.TenantId, payload.SiteId, payload.Event, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WebhookDispatcher: failed to list webhooks for event {Event}.", payload.Event);
            return;
        }

        if (endpoints.Count == 0) return;

        foreach (var endpoint in endpoints)
        {
            _ = SendAsync(endpoint, payload, CancellationToken.None);
        }
    }

    private async Task SendAsync(WebhookEndpoint endpoint, WebhookDispatchPayload payload, CancellationToken ct)
    {
        try
        {
            using var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(10);

            if (string.Equals(endpoint.Type, "slack", StringComparison.OrdinalIgnoreCase))
            {
                var slackBody = BuildSlackMessage(payload);
                using var slackContent = JsonContent.Create(slackBody, options: JsonOptions);
                var slackResponse = await http.PostAsync(endpoint.Url, slackContent, ct);
                if (!slackResponse.IsSuccessStatusCode)
                    logger.LogWarning("WebhookDispatcher: Slack webhook {Id} returned {Status}.", endpoint.Id, slackResponse.StatusCode);
                else
                    logger.LogDebug("WebhookDispatcher: Slack event {Event} dispatched to {Id}.", payload.Event, endpoint.Id);
            }
            else
            {
                using var genericContent = JsonContent.Create(payload, options: JsonOptions);
                var response = await http.PostAsync(endpoint.Url, genericContent, ct);
                if (!response.IsSuccessStatusCode)
                    logger.LogWarning("WebhookDispatcher: webhook {Id} returned {Status}.", endpoint.Id, response.StatusCode);
                else
                    logger.LogDebug("WebhookDispatcher: event {Event} dispatched to {Id}.", payload.Event, endpoint.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WebhookDispatcher: error sending to endpoint {Id}.", endpoint.Id);
        }
    }

    private static object BuildSlackMessage(WebhookDispatchPayload payload)
    {
        var icon = payload.Event switch
        {
            "lead.created"        => ":star:",
            "visitor.identified"  => ":eyes:",
            _                     => ":bell:"
        };

        var lines = new List<string>
        {
            $"{icon} *{FormatEventName(payload.Event)}*",
            $"Site: `{payload.SiteId}`",
        };

        foreach (var (key, value) in payload.Data)
        {
            if (value is not null)
                lines.Add($"{Capitalize(key)}: {value}");
        }

        return new
        {
            text = string.Join("\n", lines),
            blocks = new[]
            {
                new
                {
                    type = "section",
                    text = new { type = "mrkdwn", text = string.Join("\n", lines) }
                }
            }
        };
    }

    private static string FormatEventName(string ev) =>
        ev.Replace(".", " ").Replace("-", " ")
          .Split(' ')
          .Select(w => w.Length > 0 ? char.ToUpperInvariant(w[0]) + w[1..] : w)
          .Aggregate((a, b) => $"{a} {b}");

    private static string Capitalize(string s) =>
        s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
