namespace Intentify.Modules.Intelligence.Api;

// ── API-layer request models ──────────────────────────────────────────────────
// Note: RefreshIntelligenceApiRequest is defined in IntelligenceEndpoints.cs
// to keep it co-located with the handler that uses it.
//
// UpsertIntelligenceProfileRequest is the API-layer shape (uses string fields
// rather than the application-layer record which has Guid SiteId).

public sealed record UpsertIntelligenceProfileRequest(
    string ProfileName,
    string IndustryCategory,
    string PrimaryAudienceType,
    IReadOnlyCollection<string> TargetLocations,
    IReadOnlyCollection<string> PrimaryProductsOrServices,
    IReadOnlyCollection<string>? WatchTopics,
    IReadOnlyCollection<string>? SeasonalPriorities,
    bool IsActive,
    int? RefreshIntervalMinutes);
