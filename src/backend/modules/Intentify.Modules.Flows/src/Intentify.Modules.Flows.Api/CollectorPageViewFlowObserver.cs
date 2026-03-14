using Intentify.Modules.Collector.Application;
using Intentify.Modules.Flows.Application;
using Microsoft.Extensions.Configuration;

namespace Intentify.Modules.Flows.Api;

internal sealed class CollectorPageViewFlowObserver(
    ExecuteFlowsForTriggerService executeFlowsForTriggerService,
    IConfiguration configuration) : ICollectorEventObserver
{
    private const string TriggerType = "CollectorPageView";

    public async Task OnCollectorEventIngestedAsync(CollectorEventIngestedNotification notification, CancellationToken cancellationToken = default)
    {
        var enabled = configuration.GetValue<bool>("Intentify:Flows:EnableCollectorPageViewTrigger", true);
        if (!enabled)
        {
            return;
        }

        if (!string.Equals(notification.Type, "pageview", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["eventType"] = notification.Type,
            ["url"] = notification.Url,
            ["occurredAtUtc"] = notification.OccurredAtUtc.ToString("O")
        };

        if (!string.IsNullOrWhiteSpace(notification.Referrer))
        {
            payload["referrer"] = notification.Referrer;
        }

        if (!string.IsNullOrWhiteSpace(notification.SessionId))
        {
            payload["sessionId"] = notification.SessionId;
        }

        _ = await executeFlowsForTriggerService.HandleAsync(new ExecuteFlowsTriggerCommand(
            notification.TenantId,
            notification.SiteId,
            TriggerType,
            TriggerFilters: null,
            Payload: payload), cancellationToken);
    }
}
