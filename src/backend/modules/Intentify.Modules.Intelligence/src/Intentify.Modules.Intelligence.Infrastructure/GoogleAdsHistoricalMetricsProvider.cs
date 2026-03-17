using System.Text;
using System.Text.Json;
using Intentify.Modules.Intelligence.Application;
using Intentify.Modules.Intelligence.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Intelligence.Infrastructure;

public sealed class GoogleAdsHistoricalMetricsProvider(
    HttpClient httpClient,
    GoogleAdsOptions options,
    IIntelligenceProfileRepository profileRepository) : IExternalSearchProvider
{
    public const string ClientName = "intelligence-google-ads";

    public async Task<OperationResult<ExternalSearchResult>> SearchAsync(
        string tenantId,
        Guid siteId,
        ExternalSearchQuery query,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl)
            || string.IsNullOrWhiteSpace(options.DeveloperToken)
            || string.IsNullOrWhiteSpace(options.ClientId)
            || string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            var errors = new ValidationErrors();
            errors.Add("provider", "Google Ads provider is not configured.");
            return OperationResult<ExternalSearchResult>.ValidationFailed(errors);
        }

        var profile = await profileRepository.GetAsync(tenantId, siteId, ct);
        var keywords = ResolveKeywords(profile, query.Category, query.Limit);

        try
        {
            using var request = BuildRequest(query, keywords);
            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errors = new ValidationErrors();
                errors.Add("provider", "Google Ads request failed.");
                return OperationResult<ExternalSearchResult>.ValidationFailed(errors);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var items = ParseItems(document.RootElement, query.Limit);

            var provider = document.RootElement.TryGetProperty("provider", out var providerElement)
                ? providerElement.GetString()
                : null;

            var retrievedAtUtc = document.RootElement.TryGetProperty("retrievedAtUtc", out var retrievedAtElement)
                && retrievedAtElement.ValueKind == JsonValueKind.String
                && DateTime.TryParse(retrievedAtElement.GetString(), out var parsedRetrievedAtUtc)
                    ? DateTime.SpecifyKind(parsedRetrievedAtUtc, DateTimeKind.Utc)
                    : DateTime.UtcNow;

            return OperationResult<ExternalSearchResult>.Success(new ExternalSearchResult(
                items,
                string.IsNullOrWhiteSpace(provider) ? "GoogleAds" : provider,
                retrievedAtUtc));
        }
        catch (JsonException)
        {
            var errors = new ValidationErrors();
            errors.Add("provider", "Google Ads response is invalid JSON.");
            return OperationResult<ExternalSearchResult>.ValidationFailed(errors);
        }
        catch (HttpRequestException)
        {
            var errors = new ValidationErrors();
            errors.Add("provider", "Google Ads provider is unavailable.");
            return OperationResult<ExternalSearchResult>.ValidationFailed(errors);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            var errors = new ValidationErrors();
            errors.Add("provider", "Google Ads provider request timed out.");
            return OperationResult<ExternalSearchResult>.ValidationFailed(errors);
        }
    }

    private HttpRequestMessage BuildRequest(ExternalSearchQuery query, IReadOnlyCollection<string> keywords)
    {
        var payload = new
        {
            keywords,
            location = query.Location,
            timeWindow = query.TimeWindow,
            limit = query.Limit,
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "historical-metrics")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        request.Headers.Add("X-Developer-Token", options.DeveloperToken);
        request.Headers.Add("X-Client-Id", options.ClientId);
        request.Headers.Add("X-Client-Secret", options.ClientSecret);

        return request;
    }

    private static IReadOnlyCollection<string> ResolveKeywords(IntelligenceProfile? profile, string category, int limit)
    {
        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (profile is not null)
        {
            foreach (var keyword in profile.PrimaryProductsOrServices)
            {
                Add(results, seen, keyword, limit);
            }

            foreach (var keyword in profile.WatchTopics)
            {
                Add(results, seen, keyword, limit);
            }
        }

        if (results.Count == 0)
        {
            Add(results, seen, category, limit);
        }

        return results;
    }

    private static void Add(List<string> list, HashSet<string> seen, string? value, int limit)
    {
        if (string.IsNullOrWhiteSpace(value) || list.Count >= limit)
        {
            return;
        }

        var normalized = value.Trim();
        if (seen.Add(normalized))
        {
            list.Add(normalized);
        }
    }

    private static IReadOnlyList<ExternalSearchItem> ParseItems(JsonElement root, int limit)
    {
        if (!root.TryGetProperty("items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<ExternalSearchItem>();
        foreach (var item in itemsElement.EnumerateArray())
        {
            var keyword = item.TryGetProperty("keyword", out var keywordElement)
                ? keywordElement.GetString()
                : item.TryGetProperty("queryOrTopic", out var queryElement)
                    ? queryElement.GetString()
                    : null;

            if (string.IsNullOrWhiteSpace(keyword))
            {
                continue;
            }

            var score = item.TryGetProperty("avgMonthlySearches", out var volumeElement) && volumeElement.TryGetDouble(out var avgMonthlySearches)
                ? avgMonthlySearches
                : item.TryGetProperty("score", out var scoreElement) && scoreElement.TryGetDouble(out var parsedScore)
                    ? parsedScore
                    : 0d;

            var rank = item.TryGetProperty("rank", out var rankElement) && rankElement.TryGetInt32(out var parsedRank)
                ? parsedRank
                : (int?)null;

            items.Add(new ExternalSearchItem(keyword.Trim(), score, rank));
        }

        return items
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Rank ?? int.MaxValue)
            .Take(limit)
            .Select((item, index) => item with { Rank = item.Rank ?? index + 1 })
            .ToArray();
    }
}
