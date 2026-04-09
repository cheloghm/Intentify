using Intentify.Modules.Collector.Domain;
using Intentify.Modules.Sites.Domain;
using Intentify.Shared.Validation;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using System.Net.Http.Json;
using System.Text.Json;

namespace Intentify.Modules.Collector.Application;

public sealed class IngestCollectorEventHandler
{
    private const int MaxSiteKeyLength = 256;
    private const int MaxTypeLength = 64;
    private const int MaxUrlLength = 2048;
    private const int MaxReferrerLength = 2048;

    private readonly ISiteLookupRepository _sites;
    private readonly ICollectorEventRepository _events;
    private readonly IReadOnlyCollection<ICollectorEventObserver> _observers;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IngestCollectorEventHandler> _logger;

    public IngestCollectorEventHandler(
        ISiteLookupRepository sites,
        ICollectorEventRepository events,
        IEnumerable<ICollectorEventObserver> observers,
        IHttpClientFactory httpClientFactory,
        ILogger<IngestCollectorEventHandler> logger)
    {
        _sites = sites;
        _events = events;
        _observers = observers.ToArray();
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OperationResult<bool>> HandleAsync(CollectEventCommand command, CancellationToken cancellationToken = default)
    {
        var errors = Validate(command, out var normalizedUrl, out var normalizedReferrer);
        if (errors.HasErrors)
        {
            return OperationResult<bool>.ValidationFailed(errors);
        }

        if (!OriginNormalizer.TryNormalize(command.Origin, out var normalizedOrigin))
        {
            errors.Add("origin", "Origin or Referer header is required to determine the request origin.");
            return OperationResult<bool>.ValidationFailed(errors);
        }

        Site? site = null;
        if (!string.IsNullOrWhiteSpace(command.SiteKey))
        {
            site = await _sites.GetBySiteKeyAsync(command.SiteKey.Trim(), cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(command.SnippetId))
        {
            if (Guid.TryParse(command.SnippetId, out var snippetGuid))
            {
                site = await _sites.GetBySnippetIdAsync(snippetGuid, cancellationToken);
            }
            // Value is not a Guid — treat as a siteKey for backwards compatibility
            if (site is null)
            {
                site = await _sites.GetBySiteKeyAsync(command.SnippetId, cancellationToken);
            }
        }

        if (site is null)
        {
            return OperationResult<bool>.NotFound();
        }

        if (!site.AllowedOrigins.Contains(normalizedOrigin, StringComparer.OrdinalIgnoreCase))
        {
            return OperationResult<bool>.Forbidden();
        }

        var crossOriginEvent = false;
        if (Uri.TryCreate(normalizedOrigin, UriKind.Absolute, out var originUri))
        {
            var requestHost = originUri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                ? originUri.Host[4..]
                : originUri.Host;
            var siteHost = site.Domain.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                ? site.Domain[4..]
                : site.Domain;
            crossOriginEvent = !string.Equals(requestHost, siteHost, StringComparison.OrdinalIgnoreCase);
            if (crossOriginEvent)
            {
                _logger.LogWarning(
                    "Cross-origin event detected: snippet for site {SiteId} (domain: {SiteDomain}) received from origin {RequestOrigin}.",
                    site.Id, site.Domain, normalizedOrigin);
            }
        }

        var now = DateTime.UtcNow;
        var occurredAt = command.TsUtc?.ToUniversalTime() ?? now;
        var (geoCountry, geoCity, geoRegion) = await TryResolveGeoAsync(command.IpAddress);
        var collectorEvent = new CollectorEvent
        {
            SiteId = site.Id,
            TenantId = site.TenantId,
            Type = command.Type!.Trim(),
            Url = normalizedUrl!,
            Referrer = normalizedReferrer,
            OccurredAtUtc = occurredAt,
            ReceivedAtUtc = now,
            Origin = normalizedOrigin,
            SessionId = command.SessionId,
            Data = ToBsonDocument(command.Data),
            IpAddress = command.IpAddress,
            Country = geoCountry,
            City = geoCity,
            Region = geoRegion,
            CrossOriginEvent = crossOriginEvent
        };

        await _events.InsertAsync(collectorEvent, cancellationToken);

        var notification = new CollectorEventIngestedNotification(
            collectorEvent.SiteId,
            collectorEvent.TenantId,
            collectorEvent.OccurredAtUtc,
            collectorEvent.Type,
            collectorEvent.Url,
            collectorEvent.Referrer,
            collectorEvent.SessionId,
            command.VisitorId ?? TryGetString(command.Data, "firstPartyId") ?? TryGetString(command.Data, "fpid"),
            command.VisitorId,
            command.Fingerprint,
            TryGetString(command.Data, "userAgent"),
            TryGetString(command.Data, "language"),
            TryGetString(command.Data, "platform"),
            collectorEvent.Country,
            collectorEvent.City,
            collectorEvent.Region);

        foreach (var observer in _observers)
        {
            await observer.OnCollectorEventIngestedAsync(notification, cancellationToken);
        }

        if (site.FirstEventReceivedAtUtc is null)
        {
            await _sites.UpdateFirstEventReceivedAsync(site.Id, now, cancellationToken);
        }

        return OperationResult<bool>.Success(true);
    }

    private static ValidationErrors Validate(CollectEventCommand command, out string? normalizedUrl, out string? normalizedReferrer)
    {
        var errors = new ValidationErrors();
        normalizedUrl = null;
        normalizedReferrer = null;

        var hasSiteKey = !string.IsNullOrWhiteSpace(command.SiteKey);
        var hasSnippetId = !string.IsNullOrWhiteSpace(command.SnippetId);
        if (!hasSiteKey && !hasSnippetId)
        {
            errors.Add("siteKey", "Either siteKey or snippetId is required.");
        }
        else if (hasSiteKey && command.SiteKey!.Trim().Length > MaxSiteKeyLength)
        {
            errors.Add("siteKey", "Site key is too long.");
        }

        if (string.IsNullOrWhiteSpace(command.Type))
        {
            errors.Add("type", "Event type is required.");
        }
        else if (command.Type.Trim().Length > MaxTypeLength)
        {
            errors.Add("type", "Event type is too long.");
        }

        if (string.IsNullOrWhiteSpace(command.Url))
        {
            errors.Add("url", "Url is required.");
        }
        else if (command.Url.Trim().Length > MaxUrlLength)
        {
            errors.Add("url", "Url is too long.");
        }
        else if (!TryNormalizeAbsoluteUrl(command.Url, out normalizedUrl))
        {
            errors.Add("url", "Url must be an absolute HTTP/HTTPS URL.");
        }

        if (!string.IsNullOrWhiteSpace(command.Referrer))
        {
            if (command.Referrer.Trim().Length > MaxReferrerLength)
            {
                errors.Add("referrer", "Referrer is too long.");
            }
            else if (!TryNormalizeAbsoluteUrl(command.Referrer, out normalizedReferrer))
            {
                errors.Add("referrer", "Referrer must be an absolute HTTP/HTTPS URL.");
            }
        }

        return errors;
    }

    private static bool TryNormalizeAbsoluteUrl(string value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        normalized = uri.ToString();
        return true;
    }

    private static BsonDocument? ToBsonDocument(JsonElement? data)
    {
        if (data is null || data.Value.ValueKind == JsonValueKind.Null || data.Value.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        if (data.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return BsonSerializer.Deserialize<BsonDocument>(data.Value.GetRawText());
    }

    private static string? TryGetString(JsonElement? data, string key)
    {
        if (data is null || data.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!data.Value.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private async Task<(string? Country, string? City, string? Region)> TryResolveGeoAsync(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return (null, null, null);

        // Skip private/loopback addresses
        if (ip == "::1" || ip.StartsWith("127.") || ip.StartsWith("10.")
            || ip.StartsWith("192.168.") || ip.StartsWith("172."))
            return (null, null, null);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var client = _httpClientFactory.CreateClient("geo");
            var response = await client.GetFromJsonAsync<JsonElement>(
                $"http://ip-api.com/json/{ip}?fields=country,regionName,city", cts.Token);

            var country = response.TryGetProperty("country", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            var city    = response.TryGetProperty("city", out var ci) && ci.ValueKind == JsonValueKind.String ? ci.GetString() : null;
            var region  = response.TryGetProperty("regionName", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;

            return (country, city, region);
        }
        catch
        {
            return (null, null, null);
        }
    }
}
