using Intentify.Modules.Integrations.Application;
using Intentify.Modules.Leads.Application;

namespace Intentify.Modules.Integrations.Api;

/// <summary>Dispatches a "lead.created" webhook when a lead is captured.</summary>
internal sealed class WebhookLeadObserver(IWebhookDispatcher dispatcher) : ILeadEventObserver
{
    public Task OnLeadCapturedAsync(LeadCapturedNotification notification, CancellationToken ct = default)
    {
        if (!notification.IsNew) return Task.CompletedTask;

        var payload = new WebhookDispatchPayload(
            Event: "lead.created",
            TenantId: notification.TenantId,
            SiteId: notification.SiteId,
            OccurredAtUtc: notification.OccurredAtUtc,
            Data: new Dictionary<string, object?>
            {
                ["leadId"] = notification.LeadId.ToString(),
                ["name"]   = notification.Name,
                ["email"]  = notification.Email
            });

        _ = dispatcher.DispatchAsync(payload, CancellationToken.None);
        return Task.CompletedTask;
    }
}
