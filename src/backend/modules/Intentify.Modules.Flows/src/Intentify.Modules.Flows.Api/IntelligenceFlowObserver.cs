using Intentify.Modules.Flows.Application;
using Intentify.Modules.Intelligence.Application;

namespace Intentify.Modules.Flows.Api;

internal sealed class IntelligenceFlowObserver(ExecuteFlowsForTriggerService executeFlowsForTriggerService) : IIntelligenceObserver
{
    private const string IntelligenceTriggerType = "IntelligenceTrendsUpdated";

    public async Task OnTrendsUpdated(IntelligenceTrendsUpdatedNotification notification, CancellationToken ct)
    {
        if (!Guid.TryParse(notification.TenantId, out var tenantId))
        {
            return;
        }

        var triggerFilters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["category"] = notification.Category,
            ["location"] = notification.Location,
            ["timeWindow"] = notification.TimeWindow
        };

        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["category"] = notification.Category,
            ["location"] = notification.Location,
            ["timeWindow"] = notification.TimeWindow,
            ["refreshedAtUtc"] = notification.RefreshedAtUtc.ToString("O")
        };

        _ = await executeFlowsForTriggerService.HandleAsync(new ExecuteFlowsTriggerCommand(
            tenantId,
            notification.SiteId,
            IntelligenceTriggerType,
            triggerFilters,
            payload), ct);
    }
}
