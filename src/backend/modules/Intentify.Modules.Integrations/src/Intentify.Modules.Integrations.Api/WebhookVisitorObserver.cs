using Intentify.Modules.Integrations.Application;
using Intentify.Modules.Visitors.Application;

namespace Intentify.Modules.Integrations.Api;

/// <summary>
/// Dispatches a "visitor.identified" webhook when a visitor makes their first recorded session.
/// Uses SessionsCount == 1 as a first-visit signal.
/// </summary>
internal sealed class WebhookVisitorObserver(IWebhookDispatcher dispatcher) : IVisitorEventObserver
{
    public Task OnPageViewAsync(VisitorPageViewNotification notification, CancellationToken ct = default)
    {
        if (notification.SessionsCount != 1) return Task.CompletedTask;

        var payload = new WebhookDispatchPayload(
            Event: "visitor.identified",
            TenantId: notification.TenantId,
            SiteId: notification.SiteId,
            OccurredAtUtc: notification.OccurredAtUtc,
            Data: new Dictionary<string, object?>
            {
                ["visitorId"] = notification.VisitorId.ToString(),
                ["pageUrl"]   = notification.PageUrl
            });

        _ = dispatcher.DispatchAsync(payload, CancellationToken.None);
        return Task.CompletedTask;
    }
}
