namespace Intentify.Modules.Integrations.Application;

/// <summary>
/// Sends webhook payloads to registered endpoints.
/// Implementations are fire-and-forget safe.
/// </summary>
public interface IWebhookDispatcher
{
    /// <summary>
    /// Dispatch an event to all active webhooks for the given site.
    /// Fire-and-forget: call with <c>_ = dispatcher.DispatchAsync(...);</c>
    /// </summary>
    Task DispatchAsync(WebhookDispatchPayload payload, CancellationToken ct = default);
}
