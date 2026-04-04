using Intentify.Modules.Collector.Application;
using Intentify.Modules.Flows.Application;

namespace Intentify.Modules.Flows.Api;

internal sealed class ExitIntentFlowObserver(ExecuteFlowsForTriggerService executeFlowsForTriggerService) : ICollectorEventObserver
{
    private const string TriggerType = "exit_intent";

    public async Task OnCollectorEventIngestedAsync(CollectorEventIngestedNotification notification, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(notification.Type, "exit_intent", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["pageUrl"] = notification.Url,
            ["sessionId"] = notification.SessionId ?? string.Empty
        };

        var triggerFilters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["pageUrl"] = notification.Url,
            ["siteId"] = notification.SiteId.ToString()
        };

        _ = await executeFlowsForTriggerService.HandleAsync(new ExecuteFlowsTriggerCommand(
            notification.TenantId,
            notification.SiteId,
            TriggerType,
            triggerFilters,
            payload), cancellationToken);
    }
}
