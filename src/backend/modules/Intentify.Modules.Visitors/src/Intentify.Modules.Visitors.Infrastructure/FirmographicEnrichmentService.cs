using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Intentify.Modules.Visitors.Infrastructure;

public interface IFirmographicEnrichmentService
{
    Task<FirmographicData?> EnrichAsync(string ip, CancellationToken cancellationToken = default);
}

public sealed record FirmographicData(
    string? CompanyName,
    string? CompanyDomain,
    string? CompanyIndustry,
    string? CompanySize,
    string? CompanyLinkedIn);

// IPInfo token: set Intentify__Enrichment__IPInfoToken in Railway environment variables
// Free tier: 50,000 lookups/month — https://ipinfo.io/signup
public sealed class FirmographicEnrichmentService : IFirmographicEnrichmentService
{
    private readonly IHttpClientFactory _http;
    private readonly string? _token;
    private readonly ILogger<FirmographicEnrichmentService> _logger;

    // In-memory cache: IP → (data, cachedAt). Max 10,000 entries, TTL 24h.
    private readonly ConcurrentDictionary<string, (FirmographicData? Data, DateTime CachedAt)> _cache = new();
    private const int MaxCacheEntries = 10_000;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private static readonly string[] PrivatePrefixes = ["127.", "10.", "192.168.", "172.", "::1", "localhost"];

    public FirmographicEnrichmentService(
        IHttpClientFactory http,
        IConfiguration configuration,
        ILogger<FirmographicEnrichmentService> logger)
    {
        _http    = http;
        _token   = configuration["Intentify:Enrichment:IPInfoToken"];
        _logger  = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_token);

    public async Task<FirmographicData?> EnrichAsync(string ip, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ip)) return null;
        if (IsPrivate(ip)) return null;
        if (!IsConfigured) return null;

        // Cache hit
        if (_cache.TryGetValue(ip, out var cached) && DateTime.UtcNow - cached.CachedAt < CacheTtl)
            return cached.Data;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var client = _http.CreateClient("ipinfo");
            var url    = $"https://ipinfo.io/{ip}/json?token={_token}";
            var resp   = await client.GetFromJsonAsync<JsonElement>(url, cts.Token);

            var data = Parse(resp);
            StoreCache(ip, data);
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "FirmographicEnrichmentService: lookup failed for IP {Ip}.", ip);
            StoreCache(ip, null); // cache miss so we don't hammer the API
            return null;
        }
    }

    private static FirmographicData? Parse(JsonElement root)
    {
        // Paid plan: root.company.{name, domain, type}
        // Free plan: root.org = "AS12345 Acme Corp"
        string? companyName    = null;
        string? companyDomain  = null;
        string? companyIndustry = null;
        string? companySize    = null;
        string? companyLinkedIn = null;

        if (root.TryGetProperty("company", out var company))
        {
            companyName    = TryStr(company, "name");
            companyDomain  = TryStr(company, "domain");
            companyIndustry = TryStr(company, "type"); // IPInfo uses "type" for sector
            companySize    = TryStr(company, "size");
            companyLinkedIn = TryStr(company, "linkedin");
        }

        // Free tier fallback: parse org field
        if (string.IsNullOrWhiteSpace(companyName) && root.TryGetProperty("org", out var org) && org.ValueKind == JsonValueKind.String)
        {
            var orgStr = org.GetString() ?? string.Empty;
            // org = "AS15169 Google LLC" — strip "AS##### " prefix
            var spaceIdx = orgStr.IndexOf(' ');
            if (spaceIdx > 0 && orgStr.StartsWith("AS", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(orgStr[2..spaceIdx], out _))
            {
                companyName = orgStr[(spaceIdx + 1)..].Trim();
            }
            else if (!string.IsNullOrWhiteSpace(orgStr))
            {
                companyName = orgStr.Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(companyName)) return null;

        return new FirmographicData(companyName, companyDomain, companyIndustry, companySize, companyLinkedIn);
    }

    private static string? TryStr(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool IsPrivate(string ip) =>
        PrivatePrefixes.Any(p => ip.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    private void StoreCache(string ip, FirmographicData? data)
    {
        if (_cache.Count >= MaxCacheEntries)
        {
            // Evict oldest ~10% to avoid unbounded growth
            var oldest = _cache
                .OrderBy(kv => kv.Value.CachedAt)
                .Take(MaxCacheEntries / 10)
                .Select(kv => kv.Key)
                .ToArray();
            foreach (var key in oldest)
                _cache.TryRemove(key, out _);
        }
        _cache[ip] = (data, DateTime.UtcNow);
    }
}
