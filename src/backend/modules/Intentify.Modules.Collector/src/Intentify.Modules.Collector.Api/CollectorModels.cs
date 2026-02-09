namespace Intentify.Modules.Collector.Api;

public sealed record CollectorEventRequest(
    string SiteKey,
    string Type,
    string Url,
    string? Referrer,
    DateTime? TsUtc);
