using System.Security.Claims;
using Intentify.Modules.Sites.Application;
using Intentify.Shared.Validation;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.Sites.Api;

internal static class ApiKeyEndpoints
{
    public static async Task<IResult> GenerateApiKeyAsync(
        string siteId,
        GenerateApiKeyRequest request,
        HttpContext context,
        GenerateApiKeyHandler handler)
    {
        if (!Guid.TryParse(siteId, out var siteGuid))
            return Results.BadRequest(BadSiteId());

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Label))
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(
                new Dictionary<string, string[]> { ["label"] = ["Label is required."] }));

        var result = await handler.HandleAsync(
            new GenerateApiKeyCommand(tenantId.Value, siteGuid, request.Label.Trim()),
            context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.NotFound         => Results.NotFound(),
            OperationStatus.ValidationFailed => Results.BadRequest(
                ProblemDetailsHelpers.CreateValidationProblemDetails(
                    result.Errors?.Errors ?? new Dictionary<string, string[]>())),
            _ => Results.Ok(new GenerateApiKeyResponse(
                result.Value!.KeyId,
                result.Value.Label,
                result.Value.RawSecret,
                result.Value.Hint,
                result.Value.CreatedAtUtc))
        };
    }

    public static async Task<IResult> ListApiKeysAsync(
        string siteId,
        HttpContext context,
        ListApiKeysHandler handler)
    {
        if (!Guid.TryParse(siteId, out var siteGuid))
            return Results.BadRequest(BadSiteId());

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        var result = await handler.HandleAsync(
            new ListApiKeysCommand(tenantId.Value, siteGuid),
            context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(result.Value!.Select(k => new ApiKeyResponse(
                k.KeyId, k.Label, k.Hint, k.CreatedAtUtc, k.RevokedAtUtc, k.IsActive)))
        };
    }

    public static async Task<IResult> RevokeApiKeyAsync(
        string siteId,
        string keyId,
        HttpContext context,
        RevokeApiKeyHandler handler)
    {
        if (!Guid.TryParse(siteId, out var siteGuid))
            return Results.BadRequest(BadSiteId());

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(keyId))
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(
                new Dictionary<string, string[]> { ["keyId"] = ["Key id is required."] }));

        var result = await handler.HandleAsync(
            new RevokeApiKeyCommand(tenantId.Value, siteGuid, keyId),
            context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.NotFound         => Results.NotFound(),
            OperationStatus.ValidationFailed => Results.BadRequest(
                ProblemDetailsHelpers.CreateValidationProblemDetails(
                    result.Errors?.Errors ?? new Dictionary<string, string[]>())),
            _ => Results.Ok(new { revoked = result.Value })
        };
    }

    private static Guid? TryGetTenantId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("tenantId");
        return Guid.TryParse(value, out var g) ? g : null;
    }

    private static object BadSiteId() =>
        ProblemDetailsHelpers.CreateValidationProblemDetails(
            new Dictionary<string, string[]> { ["siteId"] = ["Site id is invalid."] });
}
