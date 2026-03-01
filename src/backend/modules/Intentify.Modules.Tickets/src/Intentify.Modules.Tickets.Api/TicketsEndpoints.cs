using System.Security.Claims;
using Intentify.Modules.Tickets.Application;
using Intentify.Shared.Validation;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.Tickets.Api;

internal static class TicketsEndpoints
{
    public static async Task<IResult> CreateAsync(HttpContext context, CreateTicketRequest request, CreateTicketHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(
            new CreateTicketCommand(tenantId.Value, request.SiteId, request.VisitorId, request.EngageSessionId, request.Subject, request.Description, request.AssignedToUserId),
            context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            _ => Results.Ok(result.Value)
        };
    }

    public static async Task<IResult> GetAsync(HttpContext context, string ticketId, GetTicketHandler handler)
    {
        var parsed = ParseTicketAndTenantId(context, ticketId, out var tenantId, out var parsedTicketId);
        if (parsed is not null)
        {
            return parsed;
        }

        var result = await handler.HandleAsync(new GetTicketQuery(tenantId!.Value, parsedTicketId), context.RequestAborted);
        return result.Status == OperationStatus.NotFound ? Results.NotFound() : Results.Ok(result.Value);
    }

    public static async Task<IResult> ListAsync(
        HttpContext context,
        string? siteId,
        string? visitorId,
        string? engageSessionId,
        int page,
        int pageSize,
        ListTicketsHandler handler)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var parsedSiteId = TryParseGuid(siteId, "siteId", errors);
        var parsedVisitorId = TryParseGuid(visitorId, "visitorId", errors);
        var parsedSessionId = TryParseGuid(engageSessionId, "engageSessionId", errors);

        if (errors.Count > 0)
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(errors));
        }

        page = page <= 0 ? 1 : page;
        pageSize = pageSize is <= 0 or > 200 ? 50 : pageSize;

        var items = await handler.HandleAsync(
            new ListTicketsQuery(tenantId.Value, parsedSiteId, parsedVisitorId, parsedSessionId, page, pageSize),
            context.RequestAborted);

        return Results.Ok(items);
    }

    public static async Task<IResult> UpdateAsync(HttpContext context, string ticketId, UpdateTicketRequest request, UpdateTicketHandler handler)
    {
        var parsed = ParseTicketAndTenantId(context, ticketId, out var tenantId, out var parsedTicketId);
        if (parsed is not null)
        {
            return parsed;
        }

        var result = await handler.HandleAsync(
            new UpdateTicketCommand(tenantId!.Value, parsedTicketId, request.Subject, request.Description),
            context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.NotFound => Results.NotFound(),
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            _ => Results.Ok(result.Value)
        };
    }

    public static async Task<IResult> SetAssignmentAsync(HttpContext context, string ticketId, SetTicketAssignmentRequest request, SetTicketAssignmentHandler handler)
    {
        var parsed = ParseTicketAndTenantId(context, ticketId, out var tenantId, out var parsedTicketId);
        if (parsed is not null)
        {
            return parsed;
        }

        var result = await handler.HandleAsync(new SetTicketAssignmentCommand(tenantId!.Value, parsedTicketId, request.AssignedToUserId), context.RequestAborted);
        return result.Status == OperationStatus.NotFound ? Results.NotFound() : Results.Ok(result.Value);
    }

    public static async Task<IResult> AddNoteAsync(HttpContext context, string ticketId, AddTicketNoteRequest request, AddTicketNoteHandler handler)
    {
        var parsed = ParseTicketAndTenantId(context, ticketId, out var tenantId, out var parsedTicketId);
        if (parsed is not null)
        {
            return parsed;
        }

        var authorUserId = TryGetUserId(context.User);
        if (authorUserId is null)
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(
            new AddTicketNoteCommand(tenantId!.Value, parsedTicketId, authorUserId.Value, request.Content),
            context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.NotFound => Results.NotFound(),
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            _ => Results.Ok(result.Value)
        };
    }

    public static async Task<IResult> ListNotesAsync(HttpContext context, string ticketId, int? page, int? pageSize, ListTicketNotesHandler handler)
    {
        var parsed = ParseTicketAndTenantId(context, ticketId, out var tenantId, out var parsedTicketId);
        if (parsed is not null)
        {
            return parsed;
        }

        var resolvedPage = page.GetValueOrDefault();
        resolvedPage = resolvedPage <= 0 ? 1 : resolvedPage;

        var resolvedPageSize = pageSize.GetValueOrDefault();
        resolvedPageSize = resolvedPageSize is <= 0 or > 200 ? 50 : resolvedPageSize;

        var result = await handler.HandleAsync(new ListTicketNotesQuery(tenantId!.Value, parsedTicketId, resolvedPage, resolvedPageSize), context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(result.Value)
        };
    }

    public static async Task<IResult> TransitionStatusAsync(HttpContext context, string ticketId, TransitionTicketStatusRequest request, TransitionTicketStatusHandler handler)
    {
        var parsed = ParseTicketAndTenantId(context, ticketId, out var tenantId, out var parsedTicketId);
        if (parsed is not null)
        {
            return parsed;
        }

        var result = await handler.HandleAsync(new TransitionTicketStatusCommand(tenantId!.Value, parsedTicketId, request.Status), context.RequestAborted);

        return result.Status switch
        {
            OperationStatus.NotFound => Results.NotFound(),
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            _ => Results.Ok(result.Value)
        };
    }

    private static IResult? ParseTicketAndTenantId(HttpContext context, string ticketId, out Guid? tenantId, out Guid parsedTicketId)
    {
        tenantId = TryGetTenantId(context.User);
        parsedTicketId = Guid.Empty;

        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        if (!Guid.TryParse(ticketId, out parsedTicketId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["ticketId"] = ["Ticket id is invalid."]
            }));
        }

        return null;
    }

    private static Guid? TryParseGuid(string? value, string fieldName, IDictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Guid.TryParse(value, out var parsed))
        {
            errors[fieldName] = [$"{fieldName} is invalid."];
            return null;
        }

        return parsed;
    }

    private static Guid? TryGetTenantId(ClaimsPrincipal user)
    {
        var tenantIdValue = user.FindFirstValue("tenantId");
        return Guid.TryParse(tenantIdValue, out var tenantId) ? tenantId : null;
    }

    private static Guid? TryGetUserId(ClaimsPrincipal user)
    {
        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(userIdValue, out var userId) ? userId : null;
    }
}
