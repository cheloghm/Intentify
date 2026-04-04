using Intentify.Modules.Flows.Application;
using Intentify.Modules.Visitors.Application;

namespace Intentify.Modules.Flows.Api;

internal sealed class VisitorReturnFlowObserver(ExecuteFlowsForTriggerService executeFlowsForTriggerService) : IVisitorEventObserver
{
    private const string TriggerType = "visitor_return";

    public async Task OnPageViewAsync(VisitorPageViewNotification notification, CancellationToken ct = default)
    {
        if (notification.SessionsCount <= 1)
        {
            return;
        }

        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["visitorId"] = notification.VisitorId.ToString(),
            ["sessionsCount"] = notification.SessionsCount.ToString(),
            ["pageUrl"] = notification.PageUrl ?? string.Empty
        };

        var triggerFilters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["visitorId"] = notification.VisitorId.ToString(),
            ["siteId"] = notification.SiteId.ToString()
        };

        _ = await executeFlowsForTriggerService.HandleAsync(new ExecuteFlowsTriggerCommand(
            notification.TenantId,
            notification.SiteId,
            TriggerType,
            triggerFilters,
            payload), ct);
    }
}
