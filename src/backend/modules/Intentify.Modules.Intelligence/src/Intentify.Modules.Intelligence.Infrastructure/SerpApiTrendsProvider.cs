using System.Text.Json;
using Intentify.Modules.Intelligence.Application;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Intelligence.Infrastructure;

/// <summary>
/// Fetches Google Trends data via SerpApi (https://serpapi.com/google-trends-api).
///
/// .env configuration:
///   Intentify__Intelligence__SerpApi__BaseUrl=https://serpapi.com/
///   Intentify__Intelligence__SerpApi__ApiKey=YOUR_SERPAPI_KEY
///   Intentify__Intelligence__SerpApi__TimeoutSeconds=15
///   Intentify__Intelligence__Search__Provider=SerpApi
///
/// SerpApi returns three useful collections from Google Trends:
///   interest_over_time  → top items (scored 0-100)
///   related_queries.top → conceptually related searches
///   related_queries.rising → searches surging right now ("Breakout" or +N%)
/// </summary>
public sealed class SerpApiTrendsProvider(HttpClient httpClient, SerpApiTrendsOptions options)
    : IExternalSearchProvider
{
    public const string ClientName = "intelligence-serpapi-trends";

    public async Task<OperationResult<ExternalSearchResult>> SearchAsync(
        string tenantId,
        Guid siteId,
        ExternalSearchQuery query,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl) || string.IsNullOrWhiteSpace(options.ApiKey))
        {
            var errors = new ValidationErrors();
            errors.Add("provider",
                "SerpApi is not configured. Add " +
                "Intentify__Intelligence__SerpApi__BaseUrl and " +
                "Intentify__Intelligence__SerpApi__ApiKey to your .env file.");
            return OperationResult<ExternalSearchResult>.ValidationFailed(errors);
        }

        try
        {
            using var request = BuildRequest(query);
            using var response = await httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                var errors = new ValidationErrors();
                errors.Add("provider",
                    $"SerpApi returned HTTP {(int)response.StatusCode}: " +
                    body[..Math.Min(300, body.Length)]);
                return OperationResult<ExternalSearchResult>.ValidationFailed(errors);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (doc.RootElement.TryGetProperty("error", out var errEl))
            {
                var errors = new ValidationErrors();
                errors.Add("provider", $"SerpApi error: {errEl.GetString()}");
                return OperationResult<ExternalSearchResult>.ValidationFailed(errors);
            }

            var items          = ParseTopItems(doc.RootElement, query.Limit);
            var relatedQueries = ParseRelatedTop(doc.RootElement, query.Limit);
            var risingQueries  = ParseRelatedRising(doc.RootElement, query.Limit);

            return OperationResult<ExternalSearchResult>.Success(new ExternalSearchResult(
                items,
                "GoogleTrends",   // the data source is Google Trends even though SerpApi is the proxy
                DateTime.UtcNow,
                relatedQueries,
                risingQueries));
        }
        catch (JsonException ex)
        {
            var errors = new ValidationErrors();
            errors.Add("provider", $"SerpApi response JSON is invalid: {ex.Message}");
            return OperationResult<ExternalSearchResult>.ValidationFailed(errors);
        }
        catch (HttpRequestException ex)
        {
            var errors = new ValidationErrors();
            errors.Add("provider", $"SerpApi unreachable: {ex.Message}");
            return OperationResult<ExternalSearchResult>.ValidationFailed(errors);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            var errors = new ValidationErrors();
            errors.Add("provider", "SerpApi request timed out. Increase SerpApi:TimeoutSeconds if needed.");
            return OperationResult<ExternalSearchResult>.ValidationFailed(errors);
        }
    }

    // ── Request builder ────────────────────────────────────────────────────────
    // SerpApi Google Trends endpoint reference:
    // GET /search.json?engine=google_trends&q=TERM&geo=GB&date=now+7-d&data_type=TIMESERIES&api_key=KEY
    //
    // Extra filter params added based on query:
    //   cat     — Google Trends category ID (0 = all, mapped from our CategoryId field)
    //   gprop   — search type: web (default), images, news, froogle (shopping), youtube
    //   hl      — language code, defaults to en

    private HttpRequestMessage BuildRequest(ExternalSearchQuery query)
    {
        var searchTerm = !string.IsNullOrWhiteSpace(query.Keyword)
            ? query.Keyword.Trim()
            : query.Category.Trim();

        var geo  = ResolveGeo(query.Location);
        var date = MapTimeWindow(query.TimeWindow);

        var sb = new System.Text.StringBuilder("search.json?");
        sb.Append("engine=google_trends");
        sb.Append("&q=").Append(Uri.EscapeDataString(searchTerm));
        sb.Append("&geo=").Append(Uri.EscapeDataString(geo));
        sb.Append("&date=").Append(Uri.EscapeDataString(date));
        sb.Append("&data_type=TIMESERIES");
        sb.Append("&hl=en");

        // Google Trends category filter (0 = all categories)
        if (query.CategoryId.HasValue && query.CategoryId.Value != 0)
            sb.Append("&cat=").Append(query.CategoryId.Value);

        // Search type (web/images/news/shopping/youtube)
        if (!string.IsNullOrWhiteSpace(query.SearchType) && query.SearchType != "web")
            sb.Append("&gprop=").Append(Uri.EscapeDataString(MapSearchType(query.SearchType)));

        // Comparison terms (up to 4 additional, comma-separated in the q param)
        if (query.ComparisonTerms?.Count > 0)
        {
            var allTerms = new List<string> { searchTerm };
            allTerms.AddRange(query.ComparisonTerms.Take(4));
            // SerpApi accepts comma-separated terms for comparison
            sb.Replace($"&q={Uri.EscapeDataString(searchTerm)}",
                $"&q={Uri.EscapeDataString(string.Join(",", allTerms))}");
        }

        sb.Append("&api_key=").Append(Uri.EscapeDataString(options.ApiKey!));

        return new HttpRequestMessage(HttpMethod.Get, sb.ToString());
    }

    // ── Response parsers ───────────────────────────────────────────────────────

    private static IReadOnlyList<ExternalSearchItem> ParseTopItems(JsonElement root, int limit)
    {
        var items = new List<ExternalSearchItem>();

        if (root.TryGetProperty("interest_over_time", out var iot)
            && iot.TryGetProperty("averages", out var avgArr)
            && avgArr.ValueKind == JsonValueKind.Array)
        {
            int rank = 1;
            foreach (var avg in avgArr.EnumerateArray())
            {
                var q = avg.TryGetProperty("query", out var qEl) ? qEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(q)) continue;
                var score = avg.TryGetProperty("value", out var vEl) && vEl.TryGetDouble(out var v) ? v : 0d;
                items.Add(new ExternalSearchItem(q.Trim(), score, rank++));
            }
        }

        // Fallback: parse timeline_data peak values
        if (!items.Any()
            && root.TryGetProperty("interest_over_time", out var iot2)
            && iot2.TryGetProperty("timeline_data", out var timeline)
            && timeline.ValueKind == JsonValueKind.Array)
        {
            var peaks = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var point in timeline.EnumerateArray())
            {
                if (!point.TryGetProperty("values", out var vals)) continue;
                foreach (var val in vals.EnumerateArray())
                {
                    var q = val.TryGetProperty("query", out var qEl) ? qEl.GetString() : null;
                    if (string.IsNullOrWhiteSpace(q)) continue;
                    var score = val.TryGetProperty("extracted_value", out var eEl) && eEl.TryGetDouble(out var ev) ? ev
                        : val.TryGetProperty("value", out var vEl) && double.TryParse(vEl.GetString(), out var sv) ? sv : 0d;
                    if (!peaks.TryGetValue(q, out var cur) || score > cur) peaks[q] = score;
                }
            }
            int rank = 1;
            foreach (var kv in peaks.OrderByDescending(x => x.Value).Take(limit))
                items.Add(new ExternalSearchItem(kv.Key.Trim(), kv.Value, rank++));
        }

        return items.Take(limit).ToArray();
    }

    private static IReadOnlyList<ExternalSearchItem> ParseRelatedTop(JsonElement root, int limit)
    {
        if (!root.TryGetProperty("related_queries", out var rq)) return [];
        if (!rq.TryGetProperty("top", out var arr) || arr.ValueKind != JsonValueKind.Array) return [];

        var items = new List<ExternalSearchItem>();
        int rank = 1;
        foreach (var item in arr.EnumerateArray())
        {
            var q = item.TryGetProperty("query", out var qEl) ? qEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(q)) continue;
            var score = item.TryGetProperty("value", out var vEl)
                ? (vEl.ValueKind == JsonValueKind.Number && vEl.TryGetDouble(out var v) ? v
                  : double.TryParse(vEl.GetString(), out var sv) ? sv : 0d)
                : 0d;
            items.Add(new ExternalSearchItem(q.Trim(), score, rank++, IsRising: false));
        }
        return items.Take(limit).ToArray();
    }

    private static IReadOnlyList<ExternalSearchItem> ParseRelatedRising(JsonElement root, int limit)
    {
        if (!root.TryGetProperty("related_queries", out var rq)) return [];
        if (!rq.TryGetProperty("rising", out var arr) || arr.ValueKind != JsonValueKind.Array) return [];

        var items = new List<ExternalSearchItem>();
        int rank = 1;
        foreach (var item in arr.EnumerateArray())
        {
            var q = item.TryGetProperty("query", out var qEl) ? qEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(q)) continue;

            var rawVal = item.TryGetProperty("value", out var vEl) ? vEl.GetString() : null;
            double score = rawVal switch
            {
                null => 0d,
                var s when s.Equals("Breakout", StringComparison.OrdinalIgnoreCase) => 100d,
                var s => double.TryParse(s.TrimStart('+').TrimEnd('%'), out var pct)
                    ? Math.Min(100d, pct / 10d) : 0d
            };

            items.Add(new ExternalSearchItem(q.Trim(), score, rank++, IsRising: true));
        }
        return items.Take(limit).ToArray();
    }

    // ── Mapping helpers ────────────────────────────────────────────────────────

    private static string ResolveGeo(string location)
    {
        if (string.IsNullOrWhiteSpace(location)) return "GB";
        return location.Trim().ToUpperInvariant() switch
        {
            "GB" or "UK" or "UNITED KINGDOM" or "ENGLAND" or "BRITAIN" => "GB",
            "IE" or "IRELAND" or "REPUBLIC OF IRELAND"                  => "IE",
            "BELFAST" or "NORTHERN IRELAND" or "NI"                     => "GB-NIR",
            "LONDON"                                                     => "GB-ENG",
            "SCOTLAND"                                                   => "GB-SCT",
            "WALES"                                                      => "GB-WLS",
            "US" or "USA" or "UNITED STATES" or "AMERICA"               => "US",
            "CA" or "CANADA"                                             => "CA",
            "AU" or "AUSTRALIA"                                          => "AU",
            "DE" or "GERMANY"                                            => "DE",
            "FR" or "FRANCE"                                             => "FR",
            var s when s.Length <= 6 => s,   // assume already a geo code
            _                                => "GB"
        };
    }

    private static string MapTimeWindow(string tw) =>
        (tw ?? "7d").ToLowerInvariant() switch
        {
            "24h" or "1d" => "now 1-d",
            "7d"          => "now 7-d",
            "30d"         => "today 1-m",
            "90d"         => "today 3-m",
            "12m"         => "today 12-m",
            _             => "now 7-d",
        };

    private static string MapSearchType(string t) =>
        t.ToLowerInvariant() switch
        {
            "images"   => "images",
            "news"     => "news",
            "shopping" => "froogle",
            "youtube"  => "youtube",
            _          => ""   // empty string = web (default)
        };
}
