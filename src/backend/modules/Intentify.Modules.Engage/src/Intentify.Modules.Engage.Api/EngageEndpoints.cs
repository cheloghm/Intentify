using System.Security.Claims;
using Intentify.Modules.Engage.Application;
using Intentify.Modules.Sites.Application;
using Intentify.Shared.Validation;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.Engage.Api;

internal static class EngageEndpoints
{
    private const string WidgetResourceName = "Intentify.Modules.Engage.Api.assets.widget.js";

    public static async Task<IResult> WidgetScriptAsync()
    {
        var assembly = typeof(EngageModule).Assembly;
        await using var stream = assembly.GetManifestResourceStream(WidgetResourceName);
        if (stream is null)
        {
            return Results.NotFound();
        }

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        return Results.Text(content, "application/javascript; charset=utf-8");
    }

    public static async Task<IResult> WidgetBootstrapAsync(string widgetKey, WidgetBootstrapHandler handler, IEngageBotRepository botRepository, ISiteRepository siteRepository, HttpContext context)
    {
        var result = await handler.HandleAsync(new WidgetBootstrapQuery(widgetKey), context.RequestAborted);
        return result.Status switch
        {
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound => Results.NotFound(),
            _ => await BuildWidgetBootstrapOkAsync(result.Value!, widgetKey, botRepository, siteRepository, context)
        };
    }

    private static async Task<IResult> BuildWidgetBootstrapOkAsync(
        WidgetBootstrapResult result,
        string widgetKey,
        IEngageBotRepository botRepository,
        ISiteRepository siteRepository,
        HttpContext context)
    {
        var site = await siteRepository.GetByWidgetKeyAsync(widgetKey, context.RequestAborted);
        var displayName = "Assistant";
        var botName = "Assistant";

        if (site is not null)
        {
            var bot = await botRepository.GetOrCreateForSiteAsync(site.TenantId, site.Id, context.RequestAborted);
            var resolvedName = string.IsNullOrWhiteSpace(bot.Name) ? bot.DisplayName : bot.Name;
            if (!string.IsNullOrWhiteSpace(resolvedName))
            {
                displayName = resolvedName;
                botName = resolvedName;
            }
        }

        return Results.Ok(new WidgetBootstrapResponse(result.SiteId.ToString("N"), result.Domain, displayName, botName));
    }

    public static async Task<IResult> ChatSendAsync(
        EngageChatSendRequest request,
        string? widgetKey,
        ChatSendHandler handler,
        HttpContext context)
    {
        var queryWidgetKey = NormalizeOptional(widgetKey);
        var bodyWidgetKey = NormalizeOptional(request.WidgetKey);

        if (!string.IsNullOrWhiteSpace(queryWidgetKey)
            && !string.IsNullOrWhiteSpace(bodyWidgetKey)
            && !string.Equals(queryWidgetKey, bodyWidgetKey, StringComparison.Ordinal))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["widgetKey"] = ["Widget key mismatch between query and request body."]
            }));
        }

        var resolvedWidgetKey = bodyWidgetKey ?? queryWidgetKey;

        Guid? sessionId = null;
        var normalizedSessionId = NormalizeOptional(request.SessionId);
        if (!string.IsNullOrWhiteSpace(normalizedSessionId))
        {
            if (!Guid.TryParse(normalizedSessionId, out var parsedSessionId))
            {
                return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["sessionId"] = ["Session id is invalid."]
                }));
            }

            sessionId = parsedSessionId;
        }

        var resolvedCollectorSessionId = NormalizeOptional(request.CollectorSessionId)
            ?? NormalizeOptional(context.Request.Cookies["intentify_sid"]);

        var result = await handler.HandleAsync(new ChatSendCommand(resolvedWidgetKey, sessionId, request.Message, resolvedCollectorSessionId), context.RequestAborted);
        return result.Status switch
        {
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(new EngageChatSendResponse(
                result.Value!.SessionId.ToString("N"),
                result.Value.Response,
                result.Value.Confidence,
                result.Value.TicketCreated,
                result.Value.Sources.Select(item => new EngageCitationResponse(item.SourceId.ToString("N"), item.ChunkId.ToString("N"), item.ChunkIndex)).ToArray(),
                result.Value.ResponseKind,
                result.Value.PromoPublicKey,
                result.Value.PromoTitle,
                result.Value.PromoDescription,
                ToStage7DecisionResponse(result.Value.Stage7Decision)))
        };
    }


    public static async Task<IResult> GetBotAsync(string? siteId, HttpContext context, GetEngageBotHandler handler)
    {
        if (string.IsNullOrWhiteSpace(siteId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is required."]
            }));
        }

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

        var result = await handler.HandleAsync(new GetEngageBotQuery(tenantId.Value, parsedSiteId), context.RequestAborted);
        return result.Status switch
        {
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(new EngageBotResponse(result.Value!.BotId.ToString("N"), result.Value.Name))
        };
    }

    public static async Task<IResult> UpdateBotAsync(string? siteId, UpdateEngageBotRequest request, HttpContext context, UpdateEngageBotHandler handler)
    {
        if (string.IsNullOrWhiteSpace(siteId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is required."]
            }));
        }

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

        var result = await handler.HandleAsync(new UpdateEngageBotCommand(tenantId.Value, parsedSiteId, request.Name), context.RequestAborted);
        return result.Status switch
        {
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(new EngageBotResponse(result.Value!.BotId.ToString("N"), result.Value.Name))
        };
    }

    public static async Task<IResult> ListConversationsAsync(string? siteId, string? collectorSessionId, HttpContext context, ListConversationsHandler handler)
    {
        if (string.IsNullOrWhiteSpace(siteId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is required."]
            }));
        }

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

        var results = await handler.HandleAsync(new ListConversationsQuery(tenantId.Value, parsedSiteId, collectorSessionId), context.RequestAborted);
        return Results.Ok(results.Select(item => new ConversationSummaryResponse(item.SessionId.ToString("N"), item.CreatedAtUtc, item.UpdatedAtUtc)).ToArray());
    }

    public static async Task<IResult> GetConversationMessagesAsync(string sessionId, string? siteId, HttpContext context, GetConversationMessagesHandler handler)
    {
        if (!Guid.TryParse(sessionId, out var parsedSessionId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["sessionId"] = ["Session id is invalid."]
            }));
        }

        if (string.IsNullOrWhiteSpace(siteId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is required."]
            }));
        }

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

        var result = await handler.HandleAsync(new GetConversationMessagesQuery(tenantId.Value, parsedSiteId, parsedSessionId), context.RequestAborted);
        return result.Status switch
        {
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(result.Value!.Select(item => new ConversationMessageResponse(
                item.MessageId.ToString("N"),
                item.Role,
                item.Content,
                item.CreatedAtUtc,
                item.Confidence,
                item.Citations.Select(c => new EngageCitationResponse(c.SourceId.ToString("N"), c.ChunkId.ToString("N"), c.ChunkIndex)).ToArray())).ToArray())
        };
    }



    private static EngageAiDecisionResponse? ToStage7DecisionResponse(AiDecisionContract? decision)
    {
        if (decision is null)
        {
            return null;
        }

        return new EngageAiDecisionResponse(
            decision.SchemaVersion,
            decision.DecisionId,
            decision.ContextRef is null
                ? null
                : new EngageAiDecisionContextRefResponse(
                    decision.ContextRef.TenantId.ToString("N"),
                    decision.ContextRef.SiteId.ToString("N"),
                    decision.ContextRef.VisitorId?.ToString("N"),
                    decision.ContextRef.EngageSessionId?.ToString("N")),
            decision.OverallConfidence,
            decision.Recommendations?.Select(item => new EngageAiRecommendationResponse(
                item.Type.ToString(),
                item.Confidence,
                item.Rationale,
                item.EvidenceRefs?.Select(evidence => new EngageAiEvidenceRefResponse(evidence.Source, evidence.ReferenceId, evidence.Detail)).ToArray(),
                item.TargetRefs is null
                    ? null
                    : new EngageAiTargetRefsResponse(
                        item.TargetRefs.PromoId?.ToString("N"),
                        item.TargetRefs.PromoPublicKey,
                        item.TargetRefs.KnowledgeSourceId?.ToString("N"),
                        item.TargetRefs.TicketId?.ToString("N"),
                        item.TargetRefs.VisitorId?.ToString("N")),
                item.RequiresApproval,
                item.ProposedCommand)).ToArray(),
            decision.ValidationStatus.ToString(),
            decision.ValidationErrors,
            decision.AllowlistedActions?.Select(item => item.ToString()).ToArray(),
            decision.ShouldFallback,
            decision.FallbackReason,
            decision.NoActionMessage);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static Guid? TryGetTenantId(ClaimsPrincipal user)
    {
        var tenantIdValue = user.FindFirstValue("tenantId");
        return Guid.TryParse(tenantIdValue, out var tenantId) ? tenantId : null;
    }
}
