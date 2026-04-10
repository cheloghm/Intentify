using Intentify.Modules.Integrations.Domain;

namespace Intentify.Modules.Integrations.Application;

public interface IWebhookRepository
{
    Task<IReadOnlyCollection<WebhookEndpoint>> ListAsync(Guid tenantId, Guid siteId, CancellationToken ct = default);
    Task<WebhookEndpoint?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task InsertAsync(WebhookEndpoint webhook, CancellationToken ct = default);
    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>Returns all active webhooks subscribed to the given event for any site under the tenant.</summary>
    Task<IReadOnlyCollection<WebhookEndpoint>> ListByEventAsync(Guid tenantId, Guid siteId, string eventName, CancellationToken ct = default);
}
