using System.Text.Json;

namespace Intentify.Modules.Collector.Api;

public sealed record CollectorEventRequest(
    string SiteKey,
    string Type,
    string Url,
    string? Referrer,
    DateTime? TsUtc,
    string? SessionId = null,
    string? VisitorId = null,
    string? Fingerprint = null,
    JsonElement? Data = null);
