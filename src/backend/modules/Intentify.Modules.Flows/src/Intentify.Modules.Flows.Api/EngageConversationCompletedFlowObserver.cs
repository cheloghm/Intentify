using Intentify.Modules.Engage.Application;
using Intentify.Modules.Flows.Application;

namespace Intentify.Modules.Flows.Api;

internal sealed class EngageConversationCompletedFlowObserver(ExecuteFlowsForTriggerService executeFlowsForTriggerService) : IEngageConversationObserver
{
    private const string TriggerType = "engage_conversation_completed";

    public async Task OnConversationCompletedAsync(ConversationCompletedNotification notification, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sessionId"] = notification.SessionId.ToString(),
            ["completedAtUtc"] = notification.CompletedAtUtc.ToString("O")
        };

        var triggerFilters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sessionId"] = notification.SessionId.ToString(),
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
