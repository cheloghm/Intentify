namespace Intentify.Modules.Collector.Application;

public sealed record CollectEventCommand(
    string? SiteKey,
    string? Type,
    string? Url,
    string? Referrer,
    DateTime? TsUtc,
    string? Origin);
