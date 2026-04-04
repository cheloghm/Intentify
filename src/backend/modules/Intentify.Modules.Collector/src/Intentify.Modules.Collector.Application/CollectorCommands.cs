using System.Text.Json;

namespace Intentify.Modules.Collector.Application;

public sealed record CollectEventCommand(
    string? SiteKey,
    string? Type,
    string? Url,
    string? Referrer,
    DateTime? TsUtc,
    string? Origin,
    string? SessionId,
    string? VisitorId,
    string? Fingerprint,
    JsonElement? Data);

public sealed record CollectorEventIngestedNotification(
    Guid SiteId,
    Guid TenantId,
    DateTime OccurredAtUtc,
    string Type,
    string Url,
    string? Referrer,
    string? SessionId,
    string? FirstPartyId,
    string? VisitorId,
    string? Fingerprint,
    string? UserAgent,
    string? Language,
    string? Platform);

public interface ICollectorEventObserver
{
    Task OnCollectorEventIngestedAsync(CollectorEventIngestedNotification notification, CancellationToken cancellationToken = default);
}
