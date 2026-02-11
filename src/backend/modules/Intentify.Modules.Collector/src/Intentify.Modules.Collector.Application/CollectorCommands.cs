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
    JsonElement? Data);
