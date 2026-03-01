namespace Intentify.Modules.Intelligence.Domain;

public static class IntelligenceMongoCollections
{
    public const string Trends = "intelligence_trends";
}

public sealed class IntelligenceTrendRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid TenantId { get; init; }

    public Guid SiteId { get; init; }

    public string Category { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public string TimeWindow { get; init; } = string.Empty;

    public string Provider { get; init; } = "Google";

    public IReadOnlyCollection<IntelligenceTrendItem> Items { get; init; } = [];

    public DateTime RefreshedAtUtc { get; init; }
}

public sealed record IntelligenceTrendItem(string QueryOrTopic, double Score, int? Rank);
