namespace Intentify.Modules.Intelligence.Api;

public sealed record RefreshIntelligenceRequest(string SiteId, string Category, string Location, string TimeWindow, int? Limit);
