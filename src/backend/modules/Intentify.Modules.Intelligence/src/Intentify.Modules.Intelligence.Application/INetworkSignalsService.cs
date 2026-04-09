namespace Intentify.Modules.Intelligence.Application;

public interface INetworkSignalsService
{
    Task<NetworkSignalsResult> GetNetworkSignalsAsync(
        NetworkSignalsQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record NetworkSignalsQuery(
    string? Country = null,
    string? ProductCategory = null,
    string? Industry = null,
    int DaysBack = 7);

public sealed record NetworkSignalsResult(
    IReadOnlyList<TrendingTopicSignal> TrendingTopics,
    IReadOnlyList<CategoryIntentSignal> CategoryIntents,
    IReadOnlyList<CountryIntentSignal> CountryIntents,
    IReadOnlyList<ProductTrendSignal> ProductTrends,
    int TotalSitesContributing,
    int TotalVisitorsContributing,
    DateTime GeneratedAtUtc);

public sealed record TrendingTopicSignal(
    string Topic,
    int SignalCount,
    double TrendScore,
    string? Category);

public sealed record CategoryIntentSignal(
    string Category,
    int VisitorCount,
    int SiteCount,
    double IntentScore);

public sealed record CountryIntentSignal(
    string Country,
    int VisitorCount,
    double AverageEngagement);

public sealed record ProductTrendSignal(
    string ProductName,
    int ViewCount,
    string? Category,
    string? PriceRange);
