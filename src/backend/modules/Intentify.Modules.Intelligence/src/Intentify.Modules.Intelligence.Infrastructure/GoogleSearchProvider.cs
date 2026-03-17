using System.Net.Http.Json;
using System.Text.Json;
using Intentify.Modules.Intelligence.Application;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Intelligence.Infrastructure;

public sealed class GoogleSearchProvider(HttpClient httpClient, GoogleSearchOptions options) : IExternalSearchProvider
{
    public const string ClientName = "intelligence-google-search";

    public async Task<OperationResult<ExternalSearchResult>> SearchAsync(string tenantId, Guid siteId, ExternalSearchQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl) || string.IsNullOrWhiteSpace(options.ApiKey))
        {
            var errors = new ValidationErrors();
            errors.Add("provider", "Google search provider is not configured.");
            return OperationResult<ExternalSearchResult>.ValidationFailed(errors);
        }

        try
        {
            using var request = BuildRequest(tenantId, siteId, query);
            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errors = new ValidationErrors();
                errors.Add("provider", "Google search request failed.");
                return OperationResult<ExternalSearchResult>.ValidationFailed(errors);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!document.RootElement.TryGetProperty("items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
            {
                var errors = new ValidationErrors();
                errors.Add("provider", "Google search response is invalid.");
                return OperationResult<ExternalSearchResult>.ValidationFailed(errors);
            }

            var items = new List<ExternalSearchItem>();
            foreach (var item in itemsElement.EnumerateArray())
            {
                var queryOrTopic = item.TryGetProperty("queryOrTopic", out var queryElement)
                    ? queryElement.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(queryOrTopic))
                {
                    continue;
                }

                var score = item.TryGetProperty("score", out var scoreElement) && scoreElement.TryGetDouble(out var parsedScore)
                    ? parsedScore
                    : 0d;

                int? rank = item.TryGetProperty("rank", out var rankElement) && rankElement.TryGetInt32(out var parsedRank)
                    ? parsedRank
                    : null;

                items.Add(new ExternalSearchItem(queryOrTopic.Trim(), score, rank));
            }

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
                string.IsNullOrWhiteSpace(provider) ? "Google" : provider,
                retrievedAtUtc));
        }
        catch (JsonException)
        {
            var errors = new ValidationErrors();
            errors.Add("provider", "Google search response is invalid JSON.");
            return OperationResult<ExternalSearchResult>.ValidationFailed(errors);
        }
        catch (HttpRequestException)
        {
            var errors = new ValidationErrors();
            errors.Add("provider", "Google search provider is unavailable.");
            return OperationResult<ExternalSearchResult>.ValidationFailed(errors);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            var errors = new ValidationErrors();
            errors.Add("provider", "Google search provider request timed out.");
            return OperationResult<ExternalSearchResult>.ValidationFailed(errors);
        }
    }

    private HttpRequestMessage BuildRequest(string tenantId, Guid siteId, ExternalSearchQuery query)
    {
        var endpoint = $"search?tenantId={Uri.EscapeDataString(tenantId)}&siteId={siteId:D}&category={Uri.EscapeDataString(query.Category)}&location={Uri.EscapeDataString(query.Location)}&timeWindow={Uri.EscapeDataString(query.TimeWindow)}&limit={query.Limit}";

        var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Add("X-Api-Key", options.ApiKey);
        return request;
    }
}
