using Intentify.Modules.Collector.Application;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.Collector.Api;

internal static class CollectorEndpoints
{
    private const int MaxContentLengthBytes = 32 * 1024;
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
        HttpContext context,
        IngestCollectorEventHandler handler)
    {
        if (context.Request.ContentLength is > MaxContentLengthBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var origin = TryResolveOrigin(context.Request);
        var command = new CollectEventCommand(
            request?.SiteKey,
            request?.Type,
            request?.Url,
            request?.Referrer,
            request?.TsUtc,
            origin);

        var result = await handler.HandleAsync(command);

        return result.Status switch
        {
            Intentify.Shared.Validation.OperationStatus.ValidationFailed => Results.BadRequest(
                ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            Intentify.Shared.Validation.OperationStatus.NotFound => Results.NotFound(),
            Intentify.Shared.Validation.OperationStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
            _ => Results.Ok()
        };
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
