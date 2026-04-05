namespace Intentify.Modules.Intelligence.Domain;

public static class IntelligenceMongoCollections
{
    public const string Trends   = "intelligence_trends";
    public const string Profiles = "intelligence_profiles";
}

// ── Trend record stored in MongoDB ───────────────────────────────────────────

public sealed class IntelligenceTrendRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid TenantId { get; init; }

    public Guid SiteId { get; init; }

    public string Category { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public string TimeWindow { get; init; } = string.Empty;

    // Optional: stored for display but not used in repository lookups
    public string? AgeRange { get; init; }

    public string Provider { get; init; } = "GoogleTrends";

    public IReadOnlyCollection<IntelligenceTrendItem> Items { get; init; } = [];

    // Google Trends also returns "related queries" — conceptually similar searches
    public IReadOnlyCollection<IntelligenceTrendItem> RelatedQueries { get; init; } = [];

    // Rising queries are searches surging rapidly in the period
    public IReadOnlyCollection<IntelligenceTrendItem> RisingQueries { get; init; } = [];

    public DateTime RefreshedAtUtc { get; init; }
}

public sealed record IntelligenceTrendItem(
    string QueryOrTopic,
    double Score,
    int? Rank,
    bool IsRising = false);