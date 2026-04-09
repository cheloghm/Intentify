using System.Text.Json;

namespace Intentify.Modules.Collector.Application;

public sealed record CollectEventCommand(
    string? SiteKey,
    string? SnippetId,
    string? Type,
    string? Url,
    string? Referrer,
    DateTime? TsUtc,
    string? Origin,
    string? SessionId,
    string? VisitorId,
    string? Fingerprint,
    JsonElement? Data,
    string? IpAddress = null);

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
    string? Platform,
    string? Country = null,
    string? City = null,
    string? Region = null,
    string? ProductName = null,
    string? ProductCategory = null,
    string? ProductPrice = null,
    string? ProductBrand = null,
    int? ScrollDepthPct = null,
    int? TimeOnPageSeconds = null);

public interface ICollectorEventObserver
{
    Task OnCollectorEventIngestedAsync(CollectorEventIngestedNotification notification, CancellationToken cancellationToken = default);
}
