namespace Intentify.Modules.Intelligence.Api;

public sealed record RefreshIntelligenceRequest(string SiteId, string Category, string Location, string TimeWindow, int? Limit);

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
