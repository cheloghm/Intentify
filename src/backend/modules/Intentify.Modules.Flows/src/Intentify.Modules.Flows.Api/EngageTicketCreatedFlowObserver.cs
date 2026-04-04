using Intentify.Modules.Flows.Application;
using Intentify.Modules.Tickets.Application;

namespace Intentify.Modules.Flows.Api;

internal sealed class EngageTicketCreatedFlowObserver(ExecuteFlowsForTriggerService executeFlowsForTriggerService) : ITicketEventObserver
{
    private const string TriggerType = "engage_ticket_created";

    public async Task OnTicketCreatedAsync(TicketCreatedNotification notification, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["subject"] = notification.Subject,
            ["ticketId"] = notification.TicketId.ToString()
        };

        var triggerFilters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["subject"] = notification.Subject,
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
