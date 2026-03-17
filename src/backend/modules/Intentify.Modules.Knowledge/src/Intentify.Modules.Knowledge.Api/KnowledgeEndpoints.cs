using System.Security.Claims;
using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Knowledge.Domain;
using Intentify.Shared.Validation;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.Knowledge.Api;

internal static class KnowledgeEndpoints
{
    public static async Task<IResult> CreateSourceAsync(CreateKnowledgeSourceRequest request, HttpContext context, CreateKnowledgeSourceHandler handler)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (!Guid.TryParse(request.SiteId, out var siteId))
        {
            errors["siteId"] = ["Site id is invalid."];
        }

        if (errors.Count > 0)
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(errors));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(new CreateKnowledgeSourceCommand(tenantId.Value, siteId, request.Type, request.Name, request.Url, request.Text), context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            _ => Results.Ok(new CreateKnowledgeSourceResponse(result.Value!.SourceId.ToString("N"), result.Value.Status))
        };
    }

    public static async Task<IResult> UploadPdfAsync(string sourceId, HttpContext context, UploadPdfHandler handler)
    {
        if (!Guid.TryParse(sourceId, out var parsedSourceId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["sourceId"] = ["Source id is invalid."]
            }));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["file"] = ["PDF file is required."]
            }));
        }

        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, context.RequestAborted);

        var result = await handler.HandleAsync(new UploadPdfCommand(tenantId.Value, parsedSourceId, memory.ToArray()), context.RequestAborted);
        return result.Status switch
        {
            OperationStatus.NotFound => Results.NotFound(),
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            _ => Results.Ok(new CreateKnowledgeSourceResponse(result.Value!.SourceId.ToString("N"), result.Value.Status))
        };
    }

    public static async Task<IResult> IndexSourceAsync(string sourceId, HttpContext context, IndexKnowledgeSourceHandler handler)
    {
        if (!Guid.TryParse(sourceId, out var parsedSourceId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["sourceId"] = ["Source id is invalid."]
            }));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(new IndexKnowledgeSourceCommand(tenantId.Value, parsedSourceId), context.RequestAborted);
        return result.Status switch
        {
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(new IndexKnowledgeSourceResponse(result.Value!.Status, result.Value.ChunkCount, result.Value.FailureReason))
        };
    }

    public static async Task<IResult> ListSourcesAsync(string siteId, HttpContext context, IKnowledgeSourceRepository repository)
    {
        if (!Guid.TryParse(siteId, out var parsedSiteId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is invalid."]
            }));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var sources = await repository.ListSourcesAsync(tenantId.Value, parsedSiteId, context.RequestAborted);
        return Results.Ok(sources.Select(ToResponse).ToArray());
    }

    public static async Task<IResult> DeleteSourceAsync(string sourceId, HttpContext context, DeleteKnowledgeSourceHandler handler)
    {
        if (!Guid.TryParse(sourceId, out var parsedSourceId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["sourceId"] = ["Source id is invalid."]
            }));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(new DeleteKnowledgeSourceCommand(tenantId.Value, parsedSourceId), context.RequestAborted);
        return result.Status == OperationStatus.NotFound ? Results.NotFound() : Results.NoContent();
    }

    public static async Task<IResult> RetrieveAsync(string siteId, string query, int top, HttpContext context, RetrieveTopChunksHandler handler)
    {
        if (!Guid.TryParse(siteId, out var parsedSiteId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is invalid."]
            }));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["query"] = ["Query is required."]
            }));
        }

        var topK = top <= 0 ? 5 : Math.Min(top, 50);
        var result = await handler.HandleAsync(new RetrieveTopChunksQuery(tenantId.Value, parsedSiteId, query, topK), context.RequestAborted);
        return Results.Ok(result.Select(item => new RetrieveChunkResponse(
            item.ChunkId.ToString("N"),
            item.SourceId.ToString("N"),
            item.ChunkIndex,
            item.Content,
            item.Score)).ToArray());
    }

    private static KnowledgeSourceSummaryResponse ToResponse(KnowledgeSource source)
    {
        return new KnowledgeSourceSummaryResponse(
            source.Id.ToString("N"),
            source.SiteId.ToString("N"),
            source.Type,
            source.Name,
            source.Url,
            source.Status.ToString(),
            source.FailureReason,
            source.ChunkCount,
            source.CreatedAtUtc,
            source.UpdatedAtUtc,
            source.IndexedAtUtc);
    }

    private static Guid? TryGetTenantId(ClaimsPrincipal user)
    {
        var tenantIdValue = user.FindFirstValue("tenantId");
        return Guid.TryParse(tenantIdValue, out var tenantId) ? tenantId : null;
    }
}
