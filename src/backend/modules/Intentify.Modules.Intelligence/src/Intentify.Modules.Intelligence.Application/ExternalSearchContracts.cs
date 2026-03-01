using Intentify.Shared.Validation;

namespace Intentify.Modules.Intelligence.Application;

public sealed record ExternalSearchQuery(string Category, string Location, string TimeWindow, int Limit);

public sealed record ExternalSearchItem(string QueryOrTopic, double Score, int? Rank);

public sealed record ExternalSearchResult(IReadOnlyList<ExternalSearchItem> Items, string Provider, DateTime RetrievedAtUtc);

public interface IExternalSearchProvider
{
    Task<OperationResult<ExternalSearchResult>> SearchAsync(string tenantId, Guid siteId, ExternalSearchQuery query, CancellationToken ct);
}
