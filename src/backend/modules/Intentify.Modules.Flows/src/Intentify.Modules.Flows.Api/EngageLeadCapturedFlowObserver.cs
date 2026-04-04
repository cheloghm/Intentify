using Intentify.Modules.Flows.Application;
using Intentify.Modules.Leads.Application;

namespace Intentify.Modules.Flows.Api;

internal sealed class EngageLeadCapturedFlowObserver(ExecuteFlowsForTriggerService executeFlowsForTriggerService) : ILeadEventObserver
{
    private const string TriggerType = "engage_lead_captured";

    public async Task OnLeadCapturedAsync(LeadCapturedNotification notification, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["email"] = notification.Email ?? string.Empty,
            ["name"] = notification.Name ?? string.Empty,
            ["leadId"] = notification.LeadId.ToString(),
            ["isNew"] = notification.IsNew.ToString()
        };

        var triggerFilters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["email"] = notification.Email ?? string.Empty,
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
