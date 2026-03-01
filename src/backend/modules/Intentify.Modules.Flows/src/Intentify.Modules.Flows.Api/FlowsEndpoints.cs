using System.Security.Claims;
using Intentify.Modules.Flows.Application;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.Flows.Api;

internal static class FlowsEndpoints
{
    public static async Task<IResult> CreateAsync(CreateFlowRequest request, HttpContext context, CreateFlowService service)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        if (!Guid.TryParse(request.SiteId, out var siteId))
        {
            return Validation("siteId", "Site id is invalid.");
        }

        var result = await service.HandleAsync(new CreateFlowCommand(
            tenantId.Value,
            siteId,
            request.Name,
            request.Trigger.ToInput(),
            request.Conditions?.Select(x => x.ToInput()).ToArray(),
            request.Actions?.Select(x => x.ToInput()).ToArray()),
            context.RequestAborted);

        return MapResult(result);
    }

    public static async Task<IResult> UpdateAsync(string id, UpdateFlowRequest request, HttpContext context, UpdateFlowService service)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        if (!Guid.TryParse(id, out var flowId))
        {
            return Validation("id", "Flow id is invalid.");
        }

        var result = await service.HandleAsync(new UpdateFlowCommand(
            tenantId.Value,
            flowId,
            request.Name,
            request.Enabled,
            request.Trigger.ToInput(),
            request.Conditions?.Select(x => x.ToInput()).ToArray(),
            request.Actions?.Select(x => x.ToInput()).ToArray()),
            context.RequestAborted);

        return MapResult(result);
    }

    public static async Task<IResult> EnableAsync(string id, HttpContext context, SetFlowEnabledService service)
        => await SetEnabledAsync(id, true, context, service);

    public static async Task<IResult> DisableAsync(string id, HttpContext context, SetFlowEnabledService service)
        => await SetEnabledAsync(id, false, context, service);

    public static async Task<IResult> ListAsync(string siteId, HttpContext context, ListFlowsService service)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        if (!Guid.TryParse(siteId, out var siteGuid))
        {
            return Validation("siteId", "Site id is invalid.");
        }

        var result = await service.HandleAsync(new ListFlowsQuery(tenantId.Value, siteGuid), context.RequestAborted);
        return MapResult(result);
    }

    public static async Task<IResult> GetAsync(string id, HttpContext context, GetFlowService service)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        if (!Guid.TryParse(id, out var flowId))
        {
            return Validation("id", "Flow id is invalid.");
        }

        var result = await service.HandleAsync(new GetFlowQuery(tenantId.Value, flowId), context.RequestAborted);
        return MapResult(result);
    }

    public static async Task<IResult> ListRunsAsync(string id, int? limit, HttpContext context, ListFlowRunsService service)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        if (!Guid.TryParse(id, out var flowId))
        {
            return Validation("id", "Flow id is invalid.");
        }

        var result = await service.HandleAsync(new ListFlowRunsQuery(tenantId.Value, flowId, limit ?? 50), context.RequestAborted);
        return MapResult(result);
    }

    private static async Task<IResult> SetEnabledAsync(string id, bool enabled, HttpContext context, SetFlowEnabledService service)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        if (!Guid.TryParse(id, out var flowId))
        {
            return Validation("id", "Flow id is invalid.");
        }

        var result = await service.HandleAsync(new SetFlowEnabledCommand(tenantId.Value, flowId, enabled), context.RequestAborted);
        return MapResult(result);
    }

    private static Guid? TryGetTenantId(ClaimsPrincipal user)
    {
        var tenantId = user.FindFirstValue("tenantId");
        return Guid.TryParse(tenantId, out var parsed) ? parsed : null;
    }

    private static IResult Validation(string field, string message)
    {
        return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
        {
            [field] = [message]
        }));
    }

    private static IResult MapResult<T>(Intentify.Shared.Validation.OperationResult<T> result)
    {
        return result.Status switch
        {
            Intentify.Shared.Validation.OperationStatus.Success => Results.Ok(result.Value),
            Intentify.Shared.Validation.OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            Intentify.Shared.Validation.OperationStatus.NotFound => Results.NotFound(),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
