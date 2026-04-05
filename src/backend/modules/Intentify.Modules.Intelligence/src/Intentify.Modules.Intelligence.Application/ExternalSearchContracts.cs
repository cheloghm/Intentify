using Intentify.Shared.Validation;

namespace Intentify.Modules.Intelligence.Application;

/// <summary>
/// Query sent to SerpApi. All optional fields are null by default —
/// the provider only adds them to the request URL when present.
/// </summary>
public sealed record ExternalSearchQuery(
    string Category,
    string Location,
    string TimeWindow,
    int Limit,
    string? Keyword          = null,
    string? AgeRange         = null,
    int? CategoryId          = null,
    string? SearchType       = null,
    IReadOnlyList<string>? ComparisonTerms = null,
    string? SubRegion        = null);

/// <summary>Single trend data point returned by the provider.</summary>
public sealed record ExternalSearchItem(
    string QueryOrTopic,
    double Score,
    int? Rank,
    bool IsRising = false);

/// <summary>
/// Full result envelope from SerpApi — top items plus related/rising collections.
/// </summary>
public sealed record ExternalSearchResult(
    IReadOnlyList<ExternalSearchItem> Items,
    string Provider,
    DateTime RetrievedAtUtc,
    IReadOnlyList<ExternalSearchItem>? RelatedQueries = null,
    IReadOnlyList<ExternalSearchItem>? RisingQueries  = null);

public interface IExternalSearchProvider
{
    Task<OperationResult<ExternalSearchResult>> SearchAsync(
        string tenantId,
        Guid siteId,
        ExternalSearchQuery query,
        CancellationToken ct);
}
