using Intentify.Shared.AI;
using Intentify.Shared.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Intentify.Modules.Intelligence.Application;

/// <summary>
/// Uses the existing SerpApi/Google Trends infrastructure to surface what people
/// in a given industry and geography are actively searching for.
/// Results are cached in-memory for 4 hours to minimise SerpApi quota usage.
/// </summary>
public sealed class CompetitorSignalsService(
    IExternalSearchProvider searchProvider,
    IServiceProvider serviceProvider) : ICompetitorSignalsService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(4);

    private static readonly string[] TransactionalSignals =
        ["buy", "price", "cost", "purchase", "hire", "get ", "near me", "for sale",
         "order", "delivery", "cheap", "discount", "quote", "book", "subscribe"];

    private static readonly string[] CommercialSignals =
        ["best", "top ", "review", "compare", " vs ", "alternative", "rated",
         "recommended", "pros", "cons", "worth", "ranking", "list"];

    private readonly Dictionary<string, (CompetitorSignalsResult result, DateTime expires)> _cache
        = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _cacheLock = new();

    public async Task<CompetitorSignalsResult> GetCompetitorSignalsAsync(
        CompetitorSignalsQuery query,
        CancellationToken cancellationToken = default)
    {
        var industry   = query.Industry.Trim();
        var location   = string.IsNullOrWhiteSpace(query.Location) ? "GB" : query.Location.Trim();
        var timeWindow = string.IsNullOrWhiteSpace(query.TimeWindow) ? "7d" : query.TimeWindow.Trim();
        var limit      = Math.Clamp(query.MaxResults, 1, 25);

        var cacheKey = $"{industry}|{location}|{timeWindow}".ToLowerInvariant();

        lock (_cacheLock)
        {
            if (_cache.TryGetValue(cacheKey, out var cached) && cached.expires > DateTime.UtcNow)
                return cached.result with { IsFromCache = true };
        }

        var searchQuery = new ExternalSearchQuery(
            Category:   industry,
            Location:   location,
            TimeWindow: timeWindow,
            Limit:      limit,
            Keyword:    industry);

        var searchResult = await searchProvider.SearchAsync(
            tenantId: "system",
            siteId:   Guid.Empty,
            query:    searchQuery,
            ct:       cancellationToken);

        if (searchResult.Status != OperationStatus.Success)
            return new CompetitorSignalsResult([], [], null, DateTime.UtcNow, IsFromCache: false);

        var data = searchResult.Value!;

        var trending = (data.RelatedQueries ?? [])
            .Take(limit)
            .Select(i => new CompetitorKeywordSignal(
                i.QueryOrTopic,
                (int)Math.Round(Math.Clamp(i.Score, 0, 100)),
                ClassifyIntent(i.QueryOrTopic),
                IsRising: false))
            .ToArray();

        var rising = (data.RisingQueries ?? [])
            .Take(limit)
            .Select(i => new CompetitorKeywordSignal(
                i.QueryOrTopic,
                (int)Math.Round(Math.Clamp(i.Score, 0, 100)),
                ClassifyIntent(i.QueryOrTopic),
                IsRising: true))
            .ToArray();

        string? aiSummary = null;
        var aiClient = serviceProvider.GetService<IChatCompletionClient>();
        if (aiClient is not null && (trending.Length > 0 || rising.Length > 0))
        {
            var topKeywords = trending.Concat(rising)
                .OrderByDescending(k => k.Score)
                .Take(5)
                .Select(k => $"{k.Keyword} ({k.Score}/100, {k.Intent})")
                .ToArray();

            if (topKeywords.Length > 0)
            {
                var userPrompt =
                    $"Based on these trending searches in the {industry} industry in {location}: " +
                    string.Join(", ", topKeywords) +
                    ". Write 2 sentences explaining what this means for a business in this " +
                    "industry and what they should focus on.";

                var aiResult = await aiClient.CompleteAsync(
                    "You are a competitive intelligence analyst.",
                    userPrompt,
                    cancellationToken);

                if (aiResult.IsSuccess && !string.IsNullOrWhiteSpace(aiResult.Value))
                    aiSummary = aiResult.Value.Trim();
            }
        }

        var result = new CompetitorSignalsResult(trending, rising, aiSummary, DateTime.UtcNow, IsFromCache: false);

        lock (_cacheLock)
        {
            _cache[cacheKey] = (result, DateTime.UtcNow.Add(CacheTtl));
        }

        return result;
    }

    private static string ClassifyIntent(string keyword)
    {
        var lower = keyword.ToLowerInvariant();
        if (TransactionalSignals.Any(s => lower.Contains(s))) return "Transactional";
        if (CommercialSignals.Any(s => lower.Contains(s)))    return "Commercial";
        return "Informational";
    }
}
