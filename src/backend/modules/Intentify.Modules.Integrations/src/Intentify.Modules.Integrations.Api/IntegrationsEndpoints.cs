using System.Security.Claims;
using Intentify.Modules.Integrations.Application;
using Intentify.Modules.Integrations.Domain;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.Integrations.Api;

internal static class IntegrationsEndpoints
{
    internal static async Task<IResult> ListWebhooksAsync(
        HttpContext context,
        IWebhookRepository repository,
        [Microsoft.AspNetCore.Mvc.FromQuery] Guid siteId)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        var items = await repository.ListAsync(tenantId.Value, siteId, context.RequestAborted);
        return Results.Ok(items.Select(ToResult).ToArray());
    }

    internal static async Task<IResult> CreateWebhookAsync(
        HttpContext context,
        IWebhookRepository repository,
        CreateWebhookRequest request)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Url) || !Uri.TryCreate(request.Url, UriKind.Absolute, out _))
            return Results.BadRequest(new { error = "Invalid URL." });

        if (string.IsNullOrWhiteSpace(request.Label))
            return Results.BadRequest(new { error = "Label is required." });

        var endpoint = new WebhookEndpoint
        {
            TenantId     = tenantId.Value,
            SiteId       = request.SiteId,
            Url          = request.Url.Trim(),
            Label        = request.Label.Trim(),
            Type         = string.Equals(request.Type, "slack", StringComparison.OrdinalIgnoreCase) ? "slack" : "generic",
            Events       = string.Join(",", (request.Events ?? Array.Empty<string>())
                               .Where(e => !string.IsNullOrWhiteSpace(e))
                               .Select(e => e.Trim().ToLowerInvariant())
                               .Distinct()),
            IsActive     = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        await repository.InsertAsync(endpoint, context.RequestAborted);
        return Results.Ok(ToResult(endpoint));
    }

    internal static async Task<IResult> DeleteWebhookAsync(
        HttpContext context,
        IWebhookRepository repository,
        Guid id)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        var existing = await repository.GetAsync(tenantId.Value, id, context.RequestAborted);
        if (existing is null) return Results.NotFound();

        await repository.DeleteAsync(tenantId.Value, id, context.RequestAborted);
        return Results.Ok();
    }

    internal static async Task<IResult> TestWebhookAsync(
        HttpContext context,
        IWebhookRepository repository,
        IWebhookDispatcher dispatcher,
        Guid id)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        var endpoint = await repository.GetAsync(tenantId.Value, id, context.RequestAborted);
        if (endpoint is null) return Results.NotFound();

        var payload = new WebhookDispatchPayload(
            Event: "test",
            TenantId: tenantId.Value,
            SiteId: endpoint.SiteId,
            OccurredAtUtc: DateTime.UtcNow,
            Data: new Dictionary<string, object?> { ["message"] = "This is a test webhook from Intentify." });

        _ = dispatcher.DispatchAsync(payload, CancellationToken.None);
        return Results.Ok(new { queued = true });
    }

    private static Guid? TryGetTenantId(ClaimsPrincipal user)
    {
        var tenantId = user.FindFirstValue("tenantId");
        return Guid.TryParse(tenantId, out var parsed) ? parsed : null;
    }

    private static WebhookEndpointResult ToResult(WebhookEndpoint e) =>
        new(e.Id, e.SiteId, e.Url, e.Label, e.Type,
            e.Events.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            e.IsActive, e.CreatedAtUtc);
}

internal sealed record CreateWebhookRequest(
    Guid SiteId,
    string Url,
    string Label,
    string? Type,
    IReadOnlyCollection<string>? Events);
