using System.Security.Claims;
using Intentify.Modules.Auth.Application;
using Intentify.Modules.Engage.Application;
using Intentify.Modules.Sites.Application;
using Intentify.Shared.AI;
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

    public static async Task<IResult> WidgetBootstrapAsync(string widgetKey, WidgetBootstrapHandler handler, IEngageBotRepository botRepository, ISiteRepository siteRepository, ITenantRepository tenantRepository, HttpContext context, IHostEnvironment environment, IConfiguration configuration)
    {
        var result = await handler.HandleAsync(new WidgetBootstrapQuery(widgetKey), context.RequestAborted);
        return result.Status switch
        {
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound => Results.NotFound(),
            _ => await BuildWidgetBootstrapOkAsync(result.Value!, widgetKey, botRepository, siteRepository, tenantRepository, context, environment, configuration)
        };
    }

    private static async Task<IResult> BuildWidgetBootstrapOkAsync(
        WidgetBootstrapResult result,
        string widgetKey,
        IEngageBotRepository botRepository,
        ISiteRepository siteRepository,
        ITenantRepository tenantRepository,
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
        string? autoTriggerRulesJson = null;
        var hideBranding = false;
        string? customBrandingText = null;
        string? openingMessage = null;
        string? abTestVariant = null;
        var surveyEnabled = false;
        string? surveyQuestion = null;
        string? surveyOptions = null;
        var exitIntentEnabled = false;
        string? exitIntentMessage = null;

        if (site is not null)
        {
            var bot = await botRepository.GetOrCreateForSiteAsync(site.TenantId, site.Id, context.RequestAborted);
            surveyEnabled = bot.SurveyEnabled;
            surveyQuestion = bot.SurveyQuestion;
            surveyOptions = bot.SurveyOptions;
            exitIntentEnabled = bot.ExitIntentEnabled;
            exitIntentMessage = bot.ExitIntentMessage;
            var resolvedName = string.IsNullOrWhiteSpace(bot.Name) ? bot.DisplayName : bot.Name;
            if (!string.IsNullOrWhiteSpace(resolvedName))
            {
                displayName = resolvedName;
                botName = resolvedName;
            }

            primaryColor = bot.PrimaryColor;
            launcherVisible = bot.LauncherVisible;
            autoTriggerRulesJson = bot.AutoTriggerRulesJson;

            // Gate white-label features by plan
            var tenant = await tenantRepository.GetByIdAsync(site.TenantId, context.RequestAborted);
            var planLimits = PlanLimits.Get(tenant?.Plan);
            hideBranding = planLimits.AllowWhiteLabel && bot.HideBranding;
            customBrandingText = planLimits.AllowWhiteLabel ? bot.CustomBrandingText : null;

            if (bot.AbTestEnabled
                && !string.IsNullOrWhiteSpace(bot.OpeningMessageA)
                && !string.IsNullOrWhiteSpace(bot.OpeningMessageB))
            {
                abTestVariant = Random.Shared.Next(2) == 0 ? "A" : "B";
                openingMessage = abTestVariant == "A" ? bot.OpeningMessageA : bot.OpeningMessageB;
                _ = botRepository.IncrementAbTestImpressionAsync(site.TenantId, site.Id, abTestVariant, CancellationToken.None);
            }
            else if (!string.IsNullOrWhiteSpace(bot.OpeningMessageA))
            {
                openingMessage = bot.OpeningMessageA;
            }
        }

        return Results.Ok(new WidgetBootstrapResponse(result.SiteId.ToString("N"), result.Domain, displayName, botName, primaryColor, launcherVisible, autoTriggerRulesJson, hideBranding, customBrandingText, openingMessage, abTestVariant, SurveyEnabled: surveyEnabled, SurveyQuestion: surveyQuestion, SurveyOptions: surveyOptions, ExitIntentEnabled: exitIntentEnabled, ExitIntentMessage: exitIntentMessage));
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

        var resolvedVisitorId = NormalizeOptional(request.VisitorId);

        var result = await handler.HandleAsync(new ChatSendCommand(
            resolvedWidgetKey,
            sessionId,
            request.Message,
            resolvedCollectorSessionId,
            resolvedVisitorId,
            NormalizeOptional(request.CurrentPageUrl),
            NormalizeOptional(request.CurrentPageTitle),
            NormalizeOptional(request.ProductName),
            NormalizeOptional(request.ProductPrice),
            NormalizeOptional(request.ProductBrand),
            NormalizeOptional(request.ProductCategory),
            NormalizeOptional(request.ProductCurrency),
            request.ProductAvailable,
            NormalizeOptional(request.AbTestVariant),
            NormalizeOptional(request.SurveyAnswer)), context.RequestAborted);
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
                result.Value.SecondaryResponse,
                ToStage7DecisionResponse(result.Value.Stage7Decision),
                result.Value.OpportunityLabel,
                result.Value.IntentScore,
                result.Value.ConversationSummary,
                result.Value.SuggestedFollowUp,
                result.Value.PreferredContactMethod,
                result.Value.FollowUpEmailDraft,
                result.Value.NextBestAction))
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
            _ => Results.Ok(new EngageBotResponse(result.Value!.BotId.ToString("N"), result.Value.Name, result.Value.PrimaryColor, result.Value.LauncherVisible, result.Value.Tone, result.Value.Verbosity, result.Value.FallbackStyle, result.Value.BusinessDescription, result.Value.Industry, result.Value.ServicesDescription, result.Value.GeoFocus, result.Value.PersonalityDescriptor, result.Value.DigestEmailEnabled, result.Value.DigestEmailRecipients, result.Value.DigestEmailFrequency, result.Value.HideBranding, result.Value.CustomBrandingText, result.Value.AbTestEnabled, result.Value.OpeningMessageA, result.Value.OpeningMessageB, result.Value.AbTestImpressionCountA, result.Value.AbTestImpressionCountB, result.Value.AbTestConversionCountA, result.Value.AbTestConversionCountB, SurveyEnabled: result.Value.SurveyEnabled, SurveyQuestion: result.Value.SurveyQuestion, SurveyOptions: result.Value.SurveyOptions, ExitIntentEnabled: result.Value.ExitIntentEnabled, ExitIntentMessage: result.Value.ExitIntentMessage))
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

        var result = await handler.HandleAsync(new UpdateEngageBotCommand(tenantId.Value, parsedSiteId, request.Name, request.PrimaryColor, request.LauncherVisible, request.Tone, request.Verbosity, request.FallbackStyle, request.BusinessDescription, request.Industry, request.ServicesDescription, request.GeoFocus, request.PersonalityDescriptor, request.DigestEmailEnabled, request.DigestEmailRecipients, request.DigestEmailFrequency, HideBranding: request.HideBranding, CustomBrandingText: request.CustomBrandingText, AbTestEnabled: request.AbTestEnabled, OpeningMessageA: request.OpeningMessageA, OpeningMessageB: request.OpeningMessageB, SurveyEnabled: request.SurveyEnabled, SurveyQuestion: request.SurveyQuestion, SurveyOptions: request.SurveyOptions, ExitIntentEnabled: request.ExitIntentEnabled, ExitIntentMessage: request.ExitIntentMessage), context.RequestAborted);
        return result.Status switch
        {
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(new EngageBotResponse(result.Value!.BotId.ToString("N"), result.Value.Name, result.Value.PrimaryColor, result.Value.LauncherVisible, result.Value.Tone, result.Value.Verbosity, result.Value.FallbackStyle, result.Value.BusinessDescription, result.Value.Industry, result.Value.ServicesDescription, result.Value.GeoFocus, result.Value.PersonalityDescriptor, result.Value.DigestEmailEnabled, result.Value.DigestEmailRecipients, result.Value.DigestEmailFrequency, result.Value.HideBranding, result.Value.CustomBrandingText, result.Value.AbTestEnabled, result.Value.OpeningMessageA, result.Value.OpeningMessageB, result.Value.AbTestImpressionCountA, result.Value.AbTestImpressionCountB, result.Value.AbTestConversionCountA, result.Value.AbTestConversionCountB, SurveyEnabled: result.Value.SurveyEnabled, SurveyQuestion: result.Value.SurveyQuestion, SurveyOptions: result.Value.SurveyOptions, ExitIntentEnabled: result.Value.ExitIntentEnabled, ExitIntentMessage: result.Value.ExitIntentMessage))
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
        return Results.Ok(results.Select(item => new ConversationSummaryResponse(item.SessionId.ToString("N"), item.CreatedAtUtc, item.UpdatedAtUtc, item.HasLead, item.HasTicket)).ToArray());
    }

    public static async Task<IResult> GetOpportunityAnalyticsAsync(string? siteId, HttpContext context, GetOpportunityAnalyticsHandler handler)
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

        var result = await handler.HandleAsync(tenantId.Value, parsedSiteId, context.RequestAborted);
        return Results.Ok(new OpportunityAnalyticsResponse(
            result.TotalCommercialOpportunities,
            result.CommercialCount,
            result.SupportCount,
            result.GeneralCount,
            result.HighIntentCount,
            new OpportunityContactMethodBreakdownResponse(
                result.PreferredContactMethodDistribution.Email,
                result.PreferredContactMethodDistribution.Phone,
                result.PreferredContactMethodDistribution.Unknown),
            result.OpportunitiesOverTime.Select(item => new OpportunityDailyPointResponse(item.DateUtc, item.Count)).ToArray()));
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



    public static async Task<IResult> DigestSendAsync(DigestSendRequest request, HttpContext context, GenerateDigestHandler handler)
    {
        if (string.IsNullOrWhiteSpace(request.SiteId) || !Guid.TryParse(request.SiteId, out var parsedSiteId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is required and must be a valid GUID."]
            }));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(new GenerateDigestQuery(tenantId.Value, parsedSiteId), context.RequestAborted);
        return Results.Ok(result);
    }

    public static async Task<IResult> GetAbTestResultsAsync(string? siteId, HttpContext context, IEngageBotRepository botRepository)
    {
        if (string.IsNullOrWhiteSpace(siteId) || !Guid.TryParse(siteId, out var parsedSiteId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is required and must be a valid GUID."]
            }));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        var bot = await botRepository.GetBySiteAsync(tenantId.Value, parsedSiteId, context.RequestAborted);
        if (bot is null) return Results.NotFound();

        var rateA = bot.AbTestImpressionCountA > 0
            ? (double)bot.AbTestConversionCountA / bot.AbTestImpressionCountA
            : 0.0;
        var rateB = bot.AbTestImpressionCountB > 0
            ? (double)bot.AbTestConversionCountB / bot.AbTestImpressionCountB
            : 0.0;

        var winner = rateA > rateB ? "A" : rateB > rateA ? "B" : "tie";

        return Results.Ok(new
        {
            abTestEnabled = bot.AbTestEnabled,
            openingMessageA = bot.OpeningMessageA,
            openingMessageB = bot.OpeningMessageB,
            impressionsA = bot.AbTestImpressionCountA,
            impressionsB = bot.AbTestImpressionCountB,
            conversionsA = bot.AbTestConversionCountA,
            conversionsB = bot.AbTestConversionCountB,
            conversionRateA = rateA,
            conversionRateB = rateB,
            winner
        });
    }

    public static async Task<IResult> GetSurveyResultsAsync(string? siteId, HttpContext context, IEngageChatSessionRepository sessionRepository)
    {
        if (string.IsNullOrWhiteSpace(siteId) || !Guid.TryParse(siteId, out var parsedSiteId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is required and must be a valid GUID."]
            }));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        var answers = await sessionRepository.ListSurveyAnswersAsync(tenantId.Value, parsedSiteId, context.RequestAborted);
        var total = answers.Count;
        var breakdown = answers
            .GroupBy(a => a, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => new SurveyOptionBreakdownResponse(g.Key, g.Count(), total > 0 ? Math.Round((double)g.Count() / total * 100, 1) : 0))
            .ToArray();

        return Results.Ok(new SurveyResultsResponse(total, breakdown));
    }

    public static async Task<IResult> ResetAbTestAsync(string? siteId, HttpContext context, IEngageBotRepository botRepository)
    {
        if (string.IsNullOrWhiteSpace(siteId) || !Guid.TryParse(siteId, out var parsedSiteId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is required and must be a valid GUID."]
            }));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        await botRepository.ResetAbTestCountersAsync(tenantId.Value, parsedSiteId, context.RequestAborted);
        return Results.NoContent();
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

    public static async Task<IResult> GenerateFollowUpAsync(
        GenerateFollowUpRequest request,
        HttpContext context,
        IChatCompletionClient ai)
    {
        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null) return Results.Unauthorized();

        var visitorName  = string.IsNullOrWhiteSpace(request.VisitorName)  ? "a visitor"           : request.VisitorName;
        var visitorEmail = string.IsNullOrWhiteSpace(request.VisitorEmail) ? "not captured yet"    : request.VisitorEmail;
        var summary      = string.IsNullOrWhiteSpace(request.ConversationSummary) ? "No summary available." : request.ConversationSummary;

        const string systemPrompt = "You are a sales professional writing a personalised follow-up email.";
        var userPrompt = $"""
            Write a short, warm, personalised follow-up email for a lead from our website.
            Lead name: {visitorName}
            Their email: {visitorEmail}
            What they discussed: {summary}
            Keep it under 150 words. Friendly but professional. No subject line — just the email body.
            """;

        var result = await ai.CompleteAsync(systemPrompt, userPrompt, context.RequestAborted);
        if (!result.IsSuccess)
            return Results.Problem("AI generation failed. Check AI configuration.");

        return Results.Ok(new { emailBody = result.Value });
    }

    private static Guid? TryGetTenantId(ClaimsPrincipal user)
    {
        var tenantIdValue = user.FindFirstValue("tenantId");
        return Guid.TryParse(tenantIdValue, out var tenantId) ? tenantId : null;
    }
}

public sealed record GenerateFollowUpRequest(
    string? LeadId,
    string? ConversationSummary,
    string? VisitorName,
    string? VisitorEmail,
    string? SiteId);
