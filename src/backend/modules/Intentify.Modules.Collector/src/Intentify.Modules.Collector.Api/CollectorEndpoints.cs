using System.Text.Json;
using Intentify.Modules.Collector.Application;
using Intentify.Shared.Validation;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.Collector.Api;

internal static class CollectorEndpoints
{
    private const int MaxContentLengthBytes = 32 * 1024;
    private const int MaxSessionIdLength = 128;
    private const int MaxSiteKeyLength = 256;
    private const int MaxTypeLength = 64;
    private const int MaxUrlLength = 2048;
    private const int MaxReferrerLength = 2048;
    private const int MaxDataKeys = 32;
    private const int MaxDataKeyLength = 64;
    private const int MaxDataStringLength = 256;
    private const string TrackerResourceName = "Intentify.Modules.Collector.Api.assets.tracker.js";

    public static async Task<IResult> GetTrackerAsync()
    {
        var assembly = typeof(CollectorModule).Assembly;
        await using var stream = assembly.GetManifestResourceStream(TrackerResourceName);
        if (stream is null)
        {
            return Results.NotFound();
        }

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        return Results.Text(content, "application/javascript; charset=utf-8");
    }

    public static async Task<IResult> CollectEventAsync(
        CollectorEventRequest? request,
        string? widgetKey,
        HttpContext context,
        IngestCollectorEventHandler handler)
    {
        _ = widgetKey;

        if (context.Request.ContentLength is > MaxContentLengthBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var requestErrors = ValidateRequest(request);
        if (requestErrors.Count > 0)
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(requestErrors));
        }

        var result = await handler.HandleAsync(new CollectEventCommand(
            request!.SiteKey,
            request.Type,
            request.Url,
            request.Referrer,
            request.TsUtc,
            TryResolveOrigin(context.Request),
            request.SessionId,
            request.Data),
            context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.Success => Results.Ok(),
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors?.Errors ?? new Dictionary<string, string[]>())),
            OperationStatus.NotFound => Results.NotFound(),
            OperationStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private static Dictionary<string, string[]> ValidateRequest(CollectorEventRequest? request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (request is null)
        {
            errors["body"] = ["Request body is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.SiteKey))
        {
            errors["siteKey"] = ["Site key is required."];
        }
        else if (request.SiteKey.Trim().Length > MaxSiteKeyLength)
        {
            errors["siteKey"] = ["Site key is too long."];
        }

        if (string.IsNullOrWhiteSpace(request.Type))
        {
            errors["type"] = ["Event type is required."];
        }
        else if (request.Type.Trim().Length > MaxTypeLength)
        {
            errors["type"] = ["Event type is too long."];
        }

        if (string.IsNullOrWhiteSpace(request.Url))
        {
            errors["url"] = ["Url is required."];
        }
        else if (request.Url.Trim().Length > MaxUrlLength)
        {
            errors["url"] = ["Url is too long."];
        }
        else if (!TryNormalizeAbsoluteUrl(request.Url, out _))
        {
            errors["url"] = ["Url must be an absolute HTTP/HTTPS URL."];
        }

        if (!string.IsNullOrWhiteSpace(request.Referrer))
        {
            if (request.Referrer.Trim().Length > MaxReferrerLength)
            {
                errors["referrer"] = ["Referrer is too long."];
            }
            else if (!TryNormalizeAbsoluteUrl(request.Referrer, out _))
            {
                errors["referrer"] = ["Referrer must be an absolute HTTP/HTTPS URL."];
            }
        }

        if (!string.IsNullOrWhiteSpace(request.SessionId) && request.SessionId.Trim().Length > MaxSessionIdLength)
        {
            errors["sessionId"] = ["Session id is too long."];
        }

        if (request.Data is { } data && data.ValueKind != JsonValueKind.Null && data.ValueKind != JsonValueKind.Undefined)
        {
            if (data.ValueKind != JsonValueKind.Object)
            {
                errors["data"] = ["Data must be an object."];
            }
            else
            {
                var keyCount = 0;
                foreach (var property in data.EnumerateObject())
                {
                    keyCount++;
                    if (keyCount > MaxDataKeys)
                    {
                        errors["data"] = [$"Data cannot contain more than {MaxDataKeys} keys."];
                        break;
                    }

                    if (property.Name.Length > MaxDataKeyLength)
                    {
                        errors["data"] = [$"Data keys cannot exceed {MaxDataKeyLength} characters."];
                        break;
                    }

                    if (property.Value.ValueKind == JsonValueKind.String
                        && property.Value.GetString() is { } value
                        && value.Length > MaxDataStringLength)
                    {
                        errors["data"] = [$"Data string values cannot exceed {MaxDataStringLength} characters."];
                        break;
                    }
                }
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

    private static string? TryResolveOrigin(HttpRequest request)
    {
        if (request.Headers.TryGetValue("Origin", out var originValues))
        {
            return originValues.ToString();
        }

        if (request.Headers.TryGetValue("Referer", out var refererValues))
        {
            var referer = refererValues.ToString();
            if (Uri.TryCreate(referer, UriKind.Absolute, out var uri))
            {
                return uri.GetLeftPart(UriPartial.Authority);
            }
        }

        return null;
    }
}
