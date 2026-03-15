using System.Security.Claims;
using Intentify.Modules.Engage.Application;
using Intentify.Modules.Sites.Application;
using Intentify.Shared.Validation;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

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

    public static async Task<IResult> WidgetBootstrapAsync(string widgetKey, WidgetBootstrapHandler handler, IEngageBotRepository botRepository, ISiteRepository siteRepository, HttpContext context, IHostEnvironment environment, IConfiguration configuration)
    {
        var result = await handler.HandleAsync(new WidgetBootstrapQuery(widgetKey), context.RequestAborted);
        return result.Status switch
        {
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound => Results.NotFound(),
            _ => await BuildWidgetBootstrapOkAsync(result.Value!, widgetKey, botRepository, siteRepository, context, environment, configuration)
        };
    }

    private static async Task<IResult> BuildWidgetBootstrapOkAsync(
        WidgetBootstrapResult result,
        string widgetKey,
        IEngageBotRepository botRepository,
        ISiteRepository siteRepository,
        HttpContext context,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        var site = await siteRepository.GetByWidgetKeyAsync(widgetKey, context.RequestAborted);
        var originCheck = EnsurePublicOriginAllowed(site, context, environment, configuration);
        if (originCheck is not null)
        {
            return originCheck;
        }
        var displayName = "Assistant";
        var botName = "Assistant";
        string? primaryColor = null;
        bool? launcherVisible = null;

        if (site is not null)
        {
            var bot = await botRepository.GetOrCreateForSiteAsync(site.TenantId, site.Id, context.RequestAborted);
            var resolvedName = string.IsNullOrWhiteSpace(bot.Name) ? bot.DisplayName : bot.Name;
            if (!string.IsNullOrWhiteSpace(resolvedName))
            {
                displayName = resolvedName;
                botName = resolvedName;
            }

            primaryColor = bot.PrimaryColor;
            launcherVisible = bot.LauncherVisible;
        }

        return Results.Ok(new WidgetBootstrapResponse(result.SiteId.ToString("N"), result.Domain, displayName, botName, primaryColor, launcherVisible));
    }

    public static async Task<IResult> ChatSendAsync(
        EngageChatSendRequest request,
        string? widgetKey,
        ChatSendHandler handler,
        ISiteRepository siteRepository,
        HttpContext context,
        IHostEnvironment environment,
        IConfiguration configuration)
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

        if (!string.IsNullOrWhiteSpace(resolvedWidgetKey))
        {
            var site = await siteRepository.GetByWidgetKeyAsync(resolvedWidgetKey, context.RequestAborted);
            var originCheck = EnsurePublicOriginAllowed(site, context, environment, configuration);
            if (originCheck is not null)
            {
                return originCheck;
            }
        }

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
            _ => Results.Ok(new EngageBotResponse(result.Value!.BotId.ToString("N"), result.Value.Name, result.Value.PrimaryColor, result.Value.LauncherVisible, result.Value.Tone, result.Value.Verbosity, result.Value.FallbackStyle))
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

        var result = await handler.HandleAsync(new UpdateEngageBotCommand(tenantId.Value, parsedSiteId, request.Name, request.PrimaryColor, request.LauncherVisible, request.Tone, request.Verbosity, request.FallbackStyle), context.RequestAborted);
        return result.Status switch
        {
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(new EngageBotResponse(result.Value!.BotId.ToString("N"), result.Value.Name, result.Value.PrimaryColor, result.Value.LauncherVisible, result.Value.Tone, result.Value.Verbosity, result.Value.FallbackStyle))
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


    public static async Task<IResult> GetWidgetConversationMessagesAsync(string sessionId, string? widgetKey, HttpContext context, GetWidgetConversationMessagesHandler handler, ISiteRepository siteRepository, IHostEnvironment environment, IConfiguration configuration)
    {
        if (!Guid.TryParse(sessionId, out var parsedSessionId))
        {
            return Results.NotFound();
        }

        if (!string.IsNullOrWhiteSpace(widgetKey))
        {
            var site = await siteRepository.GetByWidgetKeyAsync(widgetKey, context.RequestAborted);
            var originCheck = EnsurePublicOriginAllowed(site, context, environment, configuration);
            if (originCheck is not null)
            {
                return originCheck;
            }
        }

        var result = await handler.HandleAsync(new GetWidgetConversationMessagesQuery(widgetKey ?? string.Empty, parsedSessionId), context.RequestAborted);
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



    private static IResult? EnsurePublicOriginAllowed(
        Intentify.Modules.Sites.Domain.Site? site,
        HttpContext context,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        if (site is null)
        {
            return Results.NotFound();
        }

        if (!OriginNormalizer.TryNormalize(TryResolveOrigin(context.Request), out var normalizedOrigin))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["origin"] = ["Origin or Referer header is required to determine the request origin."]
            }));
        }

        if (!site.AllowedOrigins.Contains(normalizedOrigin, StringComparer.OrdinalIgnoreCase)
            && !CanBypassOriginValidation(normalizedOrigin, environment, configuration))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        return null;
    }

    private static bool CanBypassOriginValidation(string normalizedOrigin, IHostEnvironment environment, IConfiguration configuration)
    {
        if (!environment.IsDevelopment())
        {
            var allowLocalhost = configuration.GetValue<bool>("Intentify:Sites:AllowLocalhostInstallStatus");
            if (!allowLocalhost)
            {
                return false;
            }
        }

        if (!Uri.TryCreate(normalizedOrigin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.Host == "127.0.0.1";
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
