using Intentify.Modules.Collector.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Collector.Application;

public sealed class IngestCollectorEventHandler
{
    private const int MaxSiteKeyLength = 256;
    private const int MaxTypeLength = 64;
    private const int MaxUrlLength = 2048;
    private const int MaxReferrerLength = 2048;

    private readonly ISiteLookupRepository _sites;
    private readonly ICollectorEventRepository _events;

    public IngestCollectorEventHandler(ISiteLookupRepository sites, ICollectorEventRepository events)
    {
        _sites = sites;
        _events = events;
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

        var siteKey = command.SiteKey!.Trim();
        var site = await _sites.GetBySiteKeyAsync(siteKey, cancellationToken);
        if (site is null)
        {
            return OperationResult<bool>.NotFound();
        }

        if (!site.AllowedOrigins.Contains(normalizedOrigin, StringComparer.OrdinalIgnoreCase))
        {
            return OperationResult<bool>.Forbidden();
        }

        var now = DateTime.UtcNow;
        var occurredAt = command.TsUtc?.ToUniversalTime() ?? now;
        var collectorEvent = new CollectorEvent
        {
            SiteId = site.Id,
            TenantId = site.TenantId,
            Type = command.Type!.Trim(),
            Url = normalizedUrl!,
            Referrer = normalizedReferrer,
            OccurredAtUtc = occurredAt,
            ReceivedAtUtc = now,
            Origin = normalizedOrigin
        };

        await _events.InsertAsync(collectorEvent, cancellationToken);

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

        if (command.SiteKey is null)
        {
            errors.Add("body", "Request body is required.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(command.SiteKey))
        {
            errors.Add("siteKey", "Site key is required.");
        }
        else if (command.SiteKey.Trim().Length > MaxSiteKeyLength)
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
}
