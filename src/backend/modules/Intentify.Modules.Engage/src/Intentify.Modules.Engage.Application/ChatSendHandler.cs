using System.Text.RegularExpressions;
using Intentify.Modules.Engage.Domain;
using Intentify.Modules.Leads.Application;
using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Sites.Application;
using Intentify.Modules.Sites.Domain;
using Intentify.Modules.Tickets.Application;
using Intentify.Shared.AI;
using Intentify.Shared.Validation;
using Microsoft.Extensions.Logging;

namespace Intentify.Modules.Engage.Application;

public sealed class ChatSendHandler
{
    public const decimal LowConfidenceThreshold = 0.50m;
    public const int TopChunkScoreThreshold = 2;
    private const int PromptReplayMessageLimit = 12;
    private const int PromptDistilledUserTurnsLimit = 3;
    private const int HandoffTranscriptLineLimit = 8;
    private static readonly string[] HumanHelpPhrases =
    [
        "contact form",
        "form isn't working",
        "form is not working",
        "can't submit",
        "cannot submit",
        "doesn't submit"
    ];
    private static readonly string[] HumanHelpRequestPhrases =
    [
        "help me",
        "need help",
        "someone help",
        "talk to",
        "speak to",
        "human",
        "agent",
        "representative",
        "support"
    ];
    private static readonly string[] HumanHelpProblemTerms =
    [
        "broken",
        "error",
        "failed",
        "not working",
        "doesn't work",
        "checkout",
        "payment",
        "refund",
        "complaint",
        "issue",
        "problem"
    ];

    private const string AskForContactDetailsResponse = "Sorry about that — I’ll get someone to help. What’s your name and best email?";
    private const string ContactDetailsReceivedResponse = "Thanks — I’ve got your details. Our team will contact you shortly.";
    private const string GreetingResponse = "Hi! How can I help you today?";
    private const string AckResponse = "Got it — what would you like to know or do next?";
    private const string LowConfidenceClarificationResponse = "I don’t have that information in our knowledge base yet. If you tell me a bit more, I can help refine the question — or I can create a ticket for our team to follow up.";
    private const string ShortPromptClarificationResponse = "Happy to help — are you looking for hours, location, contact details, or services?";
    private const string ContactFallbackResponse = "I don’t see a verified contact detail in the knowledge base yet. If you share how you’d like to be reached, I can pass it to our team.";
    private const string LocationFallbackResponse = "I don’t see a confirmed location in the knowledge base yet. Share your city or area and I can help narrow it down or ask the team to follow up.";
    private const string HoursFallbackResponse = "I don’t see confirmed business hours in the knowledge base yet. If you want, I can open a ticket so the team can reply with exact hours.";
    private const string ServicesFallbackResponse = "I can help with services or menu questions. Tell me the specific service or item you’re looking for, and I’ll narrow it down.";
    private const string SoftFallbackResponse = "I don’t have a reliable answer yet, but I can help refine the question. Share a little more detail and I’ll try again.";
    private const string EscalationFallbackResponse = "Thanks — I can connect you with our team. Please share your name and best email.";
    private const string PromoCommandPrefix = "/promo";
    private const string PromoResponseText = "Please complete this short promo form.";
    private const string ContactDetailsNamePrefix = "my name is";
    private static readonly IReadOnlyDictionary<ChatIntent, BusinessResponseTemplate> BusinessResponseTemplates = new Dictionary<ChatIntent, BusinessResponseTemplate>
    {
        [ChatIntent.Contact] = new("ContactFallback", ContactFallbackResponse),
        [ChatIntent.Location] = new("LocationFallback", LocationFallbackResponse),
        [ChatIntent.Hours] = new("HoursFallback", HoursFallbackResponse),
        [ChatIntent.Services] = new("ServicesFallback", ServicesFallbackResponse)
    };

    private readonly ISiteRepository _siteRepository;
    private readonly IEngageChatSessionRepository _sessionRepository;
    private readonly IEngageBotRepository _botRepository;
    private readonly IEngageChatMessageRepository _messageRepository;
    private readonly IEngageHandoffTicketRepository _ticketRepository;
    private readonly CreateTicketHandler _createTicketHandler;
    private readonly ILeadVisitorLinker _leadVisitorLinker;
    private readonly UpsertLeadFromPromoEntryHandler _upsertLeadFromPromoEntryHandler;
    private readonly RetrieveTopChunksHandler _retrieveTopChunksHandler;
    private readonly IChatCompletionClient _chatCompletionClient;
    private readonly VisitorContextBundleHandler _stage7VisitorContextBundleHandler;
    private readonly AiDecisionGenerationService _stage7AiDecisionGenerationService;
    private readonly TimeSpan _sessionTimeout;
    private readonly ILogger<ChatSendHandler> _logger;

    public ChatSendHandler(
        ISiteRepository siteRepository,
        IEngageChatSessionRepository sessionRepository,
        IEngageBotRepository botRepository,
        IEngageChatMessageRepository messageRepository,
        IEngageHandoffTicketRepository ticketRepository,
        CreateTicketHandler createTicketHandler,
        ILeadVisitorLinker leadVisitorLinker,
        UpsertLeadFromPromoEntryHandler upsertLeadFromPromoEntryHandler,
        RetrieveTopChunksHandler retrieveTopChunksHandler,
        IChatCompletionClient chatCompletionClient,
        VisitorContextBundleHandler stage7VisitorContextBundleHandler,
        AiDecisionGenerationService stage7AiDecisionGenerationService,
        int sessionTimeoutMinutes,
        ILogger<ChatSendHandler> logger)
    {
        _siteRepository = siteRepository;
        _sessionRepository = sessionRepository;
        _botRepository = botRepository;
        _messageRepository = messageRepository;
        _ticketRepository = ticketRepository;
        _createTicketHandler = createTicketHandler;
        _leadVisitorLinker = leadVisitorLinker;
        _upsertLeadFromPromoEntryHandler = upsertLeadFromPromoEntryHandler;
        _retrieveTopChunksHandler = retrieveTopChunksHandler;
        _chatCompletionClient = chatCompletionClient;
        _stage7VisitorContextBundleHandler = stage7VisitorContextBundleHandler;
        _stage7AiDecisionGenerationService = stage7AiDecisionGenerationService;
        _sessionTimeout = TimeSpan.FromMinutes(sessionTimeoutMinutes > 0 ? sessionTimeoutMinutes : 30);
        _logger = logger;
    }

    public async Task<OperationResult<ChatSendResult>> HandleAsync(ChatSendCommand command, CancellationToken cancellationToken = default)
    {
        var validationErrors = new ValidationErrors();

        if (string.IsNullOrWhiteSpace(command.WidgetKey))
        {
            validationErrors.Add("widgetKey", "Widget key is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Message))
        {
            validationErrors.Add("message", "Message is required.");
        }

        if (validationErrors.HasErrors)
        {
            return OperationResult<ChatSendResult>.ValidationFailed(validationErrors);
        }

        var site = await _siteRepository.GetByWidgetKeyAsync(command.WidgetKey, cancellationToken);
        if (site is null)
        {
            _logger.LogWarning("Engage chat send failed: widgetKey {WidgetKey} did not resolve to a site.", command.WidgetKey);
            return OperationResult<ChatSendResult>.NotFound();
        }

        _logger.LogInformation("Engage chat send received for widgetKey {WidgetKey}, tenant {TenantId}, site {SiteId}.", command.WidgetKey, site.TenantId, site.Id);

        var now = DateTime.UtcNow;
        var bot = await _botRepository.GetOrCreateForSiteAsync(site.TenantId, site.Id, cancellationToken);
        var session = await ResolveSessionAsync(site.TenantId, site.Id, bot.BotId, command.WidgetKey, command.SessionId, command.CollectorSessionId, now, cancellationToken);

        await _messageRepository.InsertAsync(new EngageChatMessage
        {
            SessionId = session.Id,
            Role = "user",
            Content = command.Message,
            CreatedAtUtc = now
        }, cancellationToken);

        await _sessionRepository.TouchAsync(session.Id, now, cancellationToken);

        var recentMessages = await _messageRepository.ListBySessionAsync(session.Id, cancellationToken);

        if (TryResolveManualPromo(command.Message, out var promoPublicKey))
        {
            await _messageRepository.InsertAsync(new EngageChatMessage
            {
                SessionId = session.Id,
                Role = "assistant",
                Content = PromoResponseText,
                CreatedAtUtc = now,
                Confidence = 1m
            }, cancellationToken);

            await _sessionRepository.TouchAsync(session.Id, now, cancellationToken);

            LogChatQualitySignal(session.Id, "Promo", 1m, false, 0, "ManualPromo", null);

            return OperationResult<ChatSendResult>.Success(new ChatSendResult(
                session.Id,
                PromoResponseText,
                1m,
                false,
                [],
                "promo",
                promoPublicKey));
        }

        if (TryBuildSmalltalkResponse(command.Message, recentMessages, out var smalltalkResponse))
        {
            await _messageRepository.InsertAsync(new EngageChatMessage
            {
                SessionId = session.Id,
                Role = "assistant",
                Content = smalltalkResponse,
                CreatedAtUtc = now,
                Confidence = 1m
            }, cancellationToken);

            await _sessionRepository.TouchAsync(session.Id, now, cancellationToken);

            LogChatQualitySignal(session.Id, "Smalltalk", 1m, false, 0, null, null);

            return OperationResult<ChatSendResult>.Success(new ChatSendResult(session.Id, smalltalkResponse, 1m, false, []));
        }

        if (await IsAwaitingContactDetailsAsync(session.Id, cancellationToken) && ContainsEmail(command.Message))
        {
            return await CaptureContactDetailsAsync(site, session, command.Message, now, cancellationToken);
        }

        if (NeedsHumanHelp(command.Message))
        {
            return await CreateHumanHelpResponseAsync(site, session, command.Message, now, cancellationToken);
        }

        var intent = DetectIntent(command.Message);
        if (intent == ChatIntent.EscalationHelp)
        {
            return await CreateHumanHelpResponseAsync(site, session, command.Message, now, cancellationToken);
        }

        if (intent == ChatIntent.AmbiguousShortPrompt)
        {
            return await CreateAssistantResponseAsync(session.Id, now, ShortPromptClarificationResponse, 0.35m, false, "Clarification", "AmbiguousShortPrompt", cancellationToken);
        }

        var retrieved = await _retrieveTopChunksHandler.HandleAsync(
            new RetrieveTopChunksQuery(site.TenantId, site.Id, command.Message, 3, bot.BotId),
            cancellationToken);

        var topScore = retrieved.Count == 0 ? 0 : retrieved.Max(item => item.Score);
        var confidence = ComputeConfidence(retrieved.Count > 0, topScore);

        _logger.LogInformation("Engage retrieval summary for session {SessionId}: hits={Hits}, topScore={TopScore}, confidence={Confidence}.", session.Id, retrieved.Count, topScore, confidence);

        var isLowConfidence = retrieved.Count == 0 || topScore < TopChunkScoreThreshold || confidence < LowConfidenceThreshold;
        if (isLowConfidence)
        {
            _logger.LogInformation("Engage chat decision: layered fallback path for session {SessionId}.", session.Id);
            return await CreateLayeredFallbackResponseAsync(site, bot, session, command.Message, intent, now, "LowConfidence", cancellationToken);
        }

        var citations = retrieved
            .Select(item => new EngageCitationResult(item.SourceId, item.ChunkId, item.ChunkIndex))
            .ToArray();

        var completion = await _chatCompletionClient.CompleteAsync(BuildPrompt(bot, command.Message, retrieved, recentMessages), cancellationToken);
        if (!completion.IsSuccess || string.IsNullOrWhiteSpace(completion.Value))
        {
            _logger.LogWarning("Engage AI completion unavailable for session {SessionId}.", session.Id);
            return await CreateLayeredFallbackResponseAsync(site, bot, session, command.Message, intent, now, "AiUnavailable", cancellationToken);
        }

        _logger.LogInformation("Engage chat decision: grounded answer path for session {SessionId}.", session.Id);

        var response = NormalizeAiResponse(completion.Value);
        await _messageRepository.InsertAsync(new EngageChatMessage
        {
            SessionId = session.Id,
            Role = "assistant",
            Content = response,
            CreatedAtUtc = now,
            Confidence = confidence,
            Citations = citations.Select(item => new EngageCitation
            {
                SourceId = item.SourceId,
                ChunkId = item.ChunkId,
                ChunkIndex = item.ChunkIndex
            }).ToArray()
        }, cancellationToken);

        await _sessionRepository.TouchAsync(session.Id, now, cancellationToken);

        LogChatQualitySignal(session.Id, "Grounded", confidence, false, citations.Length, null, intent.ToString());

        var stage7Decision = await TryGenerateStage7DecisionAsync(
            site.TenantId,
            site.Id,
            session,
            command.Message,
            cancellationToken);

        return OperationResult<ChatSendResult>.Success(new ChatSendResult(
            session.Id,
            response,
            confidence,
            false,
            citations,
            Stage7Decision: stage7Decision));
    }


    private async Task<AiDecisionContract?> TryGenerateStage7DecisionAsync(
    Guid tenantId,
    Guid siteId,
    EngageChatSession session,
    string message,
    CancellationToken cancellationToken)
    {
        try
        {
            var visitorId = await ResolveVisitorIdAsync(tenantId, siteId, session.CollectorSessionId, cancellationToken);
            var bundleResult = await _stage7VisitorContextBundleHandler.HandleAsync(
                new BuildVisitorContextBundleQuery(
                    tenantId,
                    siteId,
                    visitorId,
                    session.Id,
                    message),
                cancellationToken);

            if (!bundleResult.IsSuccess || bundleResult.Value is null)
            {
                return null;
            }

            var decision = await _stage7AiDecisionGenerationService.GenerateAsync(bundleResult.Value, cancellationToken);
            return decision.ValidationStatus == AiDecisionValidationStatus.Valid
                ? decision
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stage7 recommendation generation failed for session {SessionId}.", session.Id);
            return null;
        }
    }

    private static decimal ComputeConfidence(bool hasChunks, int topScore)
    {
        if (!hasChunks)
        {
            return 0m;
        }

        return Math.Min(1m, topScore / 4m);
    }

    private static string BuildPrompt(EngageBot bot, string message, IReadOnlyCollection<RetrievedChunkResult> chunks, IReadOnlyCollection<EngageChatMessage> messages)
    {
        var dedupedChunks = chunks
            .Select(item => item.Content.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();

        var context = string.Join("\n", dedupedChunks
            .Select((item, index) => $"[{index + 1}] {item}"));

        var transcript = string.Join("\n", messages
            .TakeLast(PromptReplayMessageLimit)
            .Select(item => $"{(string.Equals(item.Role, "assistant", StringComparison.OrdinalIgnoreCase) ? "Assistant" : "User")}: {item.Content.Trim()}"));

        var distilledContext = BuildDistilledContext(messages, message);
        var tone = ResolvePromptTone(bot.Tone);
        var verbosity = ResolvePromptVerbosity(bot.Verbosity);

        return $"""
You are an Engage support assistant.

Use only the retrieved knowledge context to answer the user's question in plain English.
- Keep a {tone} tone.
- Keep verbosity {verbosity}.
- Keep the answer concise and practical.
- Rephrase the content naturally; do not copy/paste raw website text.
- Do not use markdown lists, bullets, or asterisks unless the user explicitly asks for a list.
- Do not sound robotic.
- Do not start with: Based on your knowledge base:
- If the answer is not in the retrieved context, reply exactly: I don’t have that information in our knowledge base yet.

Retrieved knowledge context (deduped):
{context}

Conversation transcript (oldest to newest):
{transcript}

Distilled prior user context:
{distilledContext}

User question:
{message}
""";
    }

    private static bool TryBuildSmalltalkResponse(string message, IReadOnlyCollection<EngageChatMessage> messages, out string response)
    {
        var normalized = message.Trim().ToLowerInvariant();
        var isGreeting = normalized is "hi" or "hello" or "hey";
        var isAcknowledgement = normalized is "yes" or "no" or "ok" or "okay" or "thanks" or "thank you" or "sure";
        var isVeryShortNonQuestion = normalized.Length > 0 && normalized.Length <= 5 && !normalized.Contains('?');
        var priorAssistantAskedQuestion = messages
            .Reverse()
            .Skip(1)
            .FirstOrDefault(item => string.Equals(item.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            ?.Content.TrimEnd().EndsWith("?", StringComparison.Ordinal) == true;

        if (priorAssistantAskedQuestion && isAcknowledgement)
        {
            response = string.Empty;
            return false;
        }

        if (!isGreeting && !isAcknowledgement && !isVeryShortNonQuestion)
        {
            response = string.Empty;
            return false;
        }

        response = isGreeting ? GreetingResponse : AckResponse;
        return true;
    }

    private static bool IsRealQuestion(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = message.Trim().ToLowerInvariant();
        if (normalized.EndsWith("?", StringComparison.Ordinal))
        {
            return true;
        }

        return normalized.StartsWith("who ", StringComparison.Ordinal)
            || normalized.StartsWith("what ", StringComparison.Ordinal)
            || normalized.StartsWith("when ", StringComparison.Ordinal)
            || normalized.StartsWith("where ", StringComparison.Ordinal)
            || normalized.StartsWith("why ", StringComparison.Ordinal)
            || normalized.StartsWith("how ", StringComparison.Ordinal)
            || normalized.StartsWith("can ", StringComparison.Ordinal)
            || normalized.StartsWith("could ", StringComparison.Ordinal)
            || normalized.StartsWith("do ", StringComparison.Ordinal)
            || normalized.StartsWith("does ", StringComparison.Ordinal)
            || normalized.StartsWith("is ", StringComparison.Ordinal)
            || normalized.StartsWith("are ", StringComparison.Ordinal)
            || normalized.StartsWith("did ", StringComparison.Ordinal)
            || normalized.StartsWith("will ", StringComparison.Ordinal)
            || normalized.StartsWith("would ", StringComparison.Ordinal);
    }

    private static string NormalizeAiResponse(string response)
    {
        var normalized = response.Trim();
        const string prohibitedPrefix = "Based on your knowledge base:";

        if (normalized.StartsWith(prohibitedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[prohibitedPrefix.Length..].TrimStart();
        }

        return normalized;
    }

    private async Task<OperationResult<ChatSendResult>> CreateFallbackResponseAsync(
        Site site,
        EngageChatSession session,
        string userMessage,
        DateTime now,
        string reason,
        CancellationToken cancellationToken,
        string? responseOverride = null)
    {
        const decimal fallbackConfidence = 0m;
        var fallbackResponse = responseOverride ?? "Thanks — we’ll get back to you shortly.";

        var existingHandoffs = await _ticketRepository.ListBySessionAsync(session.Id, cancellationToken);
        var createdTicket = existingHandoffs.Count == 0;
        var handoffPackage = await BuildHandoffPackageAsync(session.Id, userMessage, cancellationToken);

        if (createdTicket)
        {
            await _ticketRepository.InsertAsync(new EngageHandoffTicket
            {
                TenantId = site.TenantId,
                SiteId = site.Id,
                SessionId = session.Id,
                UserMessage = userMessage,
                Reason = reason,
                LastAssistantMessage = handoffPackage.LastAssistantMessage,
                TranscriptExcerpt = handoffPackage.TranscriptExcerpt,
                CitationCount = handoffPackage.CitationCount,
                CreatedAtUtc = now
            }, cancellationToken);

            var visitorId = await ResolveVisitorIdAsync(site.TenantId, site.Id, session.CollectorSessionId, cancellationToken);

            await _createTicketHandler.HandleAsync(
                new CreateTicketCommand(
                    site.TenantId,
                    site.Id,
                    visitorId,
                    session.Id,
                    $"Engage handoff: {reason}",
                    handoffPackage.TicketDescription,
                    null),
                cancellationToken);
        }

        await _messageRepository.InsertAsync(new EngageChatMessage
        {
            SessionId = session.Id,
            Role = "assistant",
            Content = fallbackResponse,
            CreatedAtUtc = now,
            Confidence = fallbackConfidence
        }, cancellationToken);

        await _sessionRepository.TouchAsync(session.Id, now, cancellationToken);

        LogChatQualitySignal(session.Id, "EscalationFallback", fallbackConfidence, createdTicket, handoffPackage.CitationCount, reason, null);

        return OperationResult<ChatSendResult>.Success(new ChatSendResult(session.Id, fallbackResponse, fallbackConfidence, createdTicket, []));
    }

    private async Task<OperationResult<ChatSendResult>> CreateLayeredFallbackResponseAsync(
        Site site,
        EngageBot bot,
        EngageChatSession session,
        string userMessage,
        ChatIntent intent,
        DateTime now,
        string reason,
        CancellationToken cancellationToken)
    {
        if (intent == ChatIntent.AmbiguousShortPrompt)
        {
            return await CreateAssistantResponseAsync(session.Id, now, ShortPromptClarificationResponse, 0.35m, false, "Clarification", "AmbiguousShortPrompt", cancellationToken);
        }

        if (TryBuildBusinessAwareFallback(intent, out var businessTemplate))
        {
            return await CreateAssistantResponseAsync(session.Id, now, businessTemplate.Response, 0.3m, false, businessTemplate.Path, null, cancellationToken);
        }

        var shouldEscalate = ShouldEscalateFallback(bot, intent, userMessage, reason);
        if (!shouldEscalate)
        {
            var fallback = IsRealQuestion(userMessage)
                ? LowConfidenceClarificationResponse
                : BuildSoftFallbackResponse(bot);
            return await CreateAssistantResponseAsync(session.Id, now, fallback, 0.2m, false, "Fallback", reason, cancellationToken);
        }

        return await CreateFallbackResponseAsync(site, session, userMessage, now, reason, cancellationToken, EscalationFallbackResponse);
    }

    private static bool NeedsHumanHelp(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        if (HumanHelpPhrases.Any(phrase => message.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var normalized = message.Trim().ToLowerInvariant();
        var requestedHumanHelp = HumanHelpRequestPhrases.Any(phrase => normalized.Contains(phrase, StringComparison.Ordinal));
        if (!requestedHumanHelp)
        {
            return false;
        }

        return HumanHelpProblemTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
    }

    private static ChatIntent DetectIntent(string message)
    {
        var normalized = message.Trim().ToLowerInvariant();

        if (normalized.Length <= 3 || normalized is "help" or "info" or "details" or "price")
        {
            return ChatIntent.AmbiguousShortPrompt;
        }

        if (normalized.Contains("human", StringComparison.Ordinal)
            || normalized.Contains("agent", StringComparison.Ordinal)
            || normalized.Contains("representative", StringComparison.Ordinal)
            || normalized.Contains("someone", StringComparison.Ordinal)
            || normalized.Contains("support", StringComparison.Ordinal))
        {
            return ChatIntent.EscalationHelp;
        }

        if (normalized.Contains("contact", StringComparison.Ordinal)
            || normalized.Contains("phone", StringComparison.Ordinal)
            || normalized.Contains("email", StringComparison.Ordinal)
            || normalized.Contains("call", StringComparison.Ordinal))
        {
            return ChatIntent.Contact;
        }

        if (normalized.Contains("location", StringComparison.Ordinal)
            || normalized.Contains("address", StringComparison.Ordinal)
            || normalized.Contains("where", StringComparison.Ordinal)
            || normalized.Contains("located", StringComparison.Ordinal))
        {
            return ChatIntent.Location;
        }

        if (normalized.Contains("hours", StringComparison.Ordinal)
            || normalized.Contains("open", StringComparison.Ordinal)
            || normalized.Contains("close", StringComparison.Ordinal)
            || normalized.Contains("time", StringComparison.Ordinal))
        {
            return ChatIntent.Hours;
        }

        if (normalized.Contains("service", StringComparison.Ordinal)
            || normalized.Contains("menu", StringComparison.Ordinal)
            || normalized.Contains("offer", StringComparison.Ordinal)
            || normalized.Contains("pricing", StringComparison.Ordinal))
        {
            return ChatIntent.Services;
        }

        return ChatIntent.General;
    }

    private static bool TryBuildBusinessAwareFallback(ChatIntent intent, out BusinessResponseTemplate response)
    {
        return BusinessResponseTemplates.TryGetValue(intent, out response!);
    }

    private static bool ShouldEscalateFallback(EngageBot bot, ChatIntent intent, string userMessage, string reason)
    {
        var isActionableHelpIntent = intent == ChatIntent.EscalationHelp || NeedsHumanHelp(userMessage);
        var isRealQuestion = IsRealQuestion(userMessage);

        if (string.Equals(NormalizeOptional(bot.FallbackStyle), "handoff", StringComparison.OrdinalIgnoreCase)
            && (isActionableHelpIntent || isRealQuestion))
        {
            return true;
        }

        if (reason == "AiUnavailable" && isRealQuestion)
        {
            return true;
        }

        return isActionableHelpIntent;
    }

    private static string BuildSoftFallbackResponse(EngageBot bot)
    {
        var tone = NormalizeOptional(bot.Tone)?.ToLowerInvariant();
        return tone switch
        {
            "professional" => "I don’t have a reliable answer yet, but I can help refine your question. Share a bit more detail and I’ll try again.",
            "casual" => "I don’t have a solid answer yet, but we can figure it out together. Share a little more detail and I’ll try again.",
            _ => SoftFallbackResponse
        };
    }

    private static string ResolvePromptTone(string? tone)
    {
        var normalized = NormalizeOptional(tone)?.ToLowerInvariant();
        return normalized is "warm" or "professional" or "casual"
            ? normalized
            : "warm";
    }

    private static string ResolvePromptVerbosity(string? verbosity)
    {
        var normalized = NormalizeOptional(verbosity)?.ToLowerInvariant();
        return normalized is "brief" or "balanced" or "detailed"
            ? normalized
            : "balanced";
    }

    private async Task<OperationResult<ChatSendResult>> CreateAssistantResponseAsync(
        Guid sessionId,
        DateTime now,
        string response,
        decimal confidence,
        bool ticketCreated,
        string qualityPath,
        string? qualityReason,
        CancellationToken cancellationToken)
    {
        await _messageRepository.InsertAsync(new EngageChatMessage
        {
            SessionId = sessionId,
            Role = "assistant",
            Content = response,
            CreatedAtUtc = now,
            Confidence = confidence
        }, cancellationToken);

        await _sessionRepository.TouchAsync(sessionId, now, cancellationToken);
        LogChatQualitySignal(sessionId, qualityPath, confidence, ticketCreated, 0, qualityReason, null);
        return OperationResult<ChatSendResult>.Success(new ChatSendResult(sessionId, response, confidence, ticketCreated, []));
    }

    private static bool ContainsEmail(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return Regex.IsMatch(message, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase);
    }

    private async Task<bool> IsAwaitingContactDetailsAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var handoffs = await _ticketRepository.ListBySessionAsync(sessionId, cancellationToken);
        if (handoffs.Count == 0)
        {
            return false;
        }

        var messages = await _messageRepository.ListBySessionAsync(sessionId, cancellationToken);
        var lastAssistantMessage = messages
            .Where(item => string.Equals(item.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => item.Content)
            .FirstOrDefault();

        return string.Equals(lastAssistantMessage, AskForContactDetailsResponse, StringComparison.Ordinal);
    }

    private async Task<OperationResult<ChatSendResult>> CreateHumanHelpResponseAsync(
        Site site,
        EngageChatSession session,
        string userMessage,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var handoffPackage = await BuildHandoffPackageAsync(session.Id, userMessage, cancellationToken);

        await _ticketRepository.InsertAsync(new EngageHandoffTicket
        {
            TenantId = site.TenantId,
            SiteId = site.Id,
            SessionId = session.Id,
            UserMessage = userMessage,
            Reason = "NeedsHumanHelp",
            LastAssistantMessage = handoffPackage.LastAssistantMessage,
            TranscriptExcerpt = handoffPackage.TranscriptExcerpt,
            CitationCount = handoffPackage.CitationCount,
            CreatedAtUtc = now
        }, cancellationToken);

        var visitorId = await ResolveVisitorIdAsync(site.TenantId, site.Id, session.CollectorSessionId, cancellationToken);

        await _createTicketHandler.HandleAsync(
            new CreateTicketCommand(
                site.TenantId,
                site.Id,
                visitorId,
                session.Id,
                "Engage handoff: NeedsHumanHelp",
                handoffPackage.TicketDescription,
                null),
            cancellationToken);

        await _messageRepository.InsertAsync(new EngageChatMessage
        {
            SessionId = session.Id,
            Role = "assistant",
            Content = AskForContactDetailsResponse,
            CreatedAtUtc = now,
            Confidence = 0m
        }, cancellationToken);

        await _sessionRepository.TouchAsync(session.Id, now, cancellationToken);
        LogChatQualitySignal(session.Id, "HumanHelp", 0m, true, 0, "NeedsHumanHelp", null);
        return OperationResult<ChatSendResult>.Success(new ChatSendResult(session.Id, AskForContactDetailsResponse, 0m, true, []));
    }

    private async Task<OperationResult<ChatSendResult>> CaptureContactDetailsAsync(
        Site site,
        EngageChatSession session,
        string userMessage,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var visitorId = await ResolveVisitorIdAsync(site.TenantId, site.Id, session.CollectorSessionId, cancellationToken);
        var parsedEmail = TryExtractEmail(userMessage);
        var parsedName = TryExtractName(userMessage, parsedEmail);

        await _createTicketHandler.HandleAsync(
            new CreateTicketCommand(
                site.TenantId,
                site.Id,
                visitorId,
                session.Id,
                "Engage handoff: ContactDetails",
                $"Contact details provided by visitor: {userMessage}",
                null),
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(parsedEmail))
        {
            await _upsertLeadFromPromoEntryHandler.HandleAsync(
                new UpsertLeadFromPromoEntryCommand(
                    site.TenantId,
                    site.Id,
                    Guid.NewGuid(),
                    visitorId,
                    null,
                    session.CollectorSessionId,
                    parsedEmail,
                    parsedName,
                    true),
                cancellationToken);
        }

        await _messageRepository.InsertAsync(new EngageChatMessage
        {
            SessionId = session.Id,
            Role = "assistant",
            Content = ContactDetailsReceivedResponse,
            CreatedAtUtc = now,
            Confidence = 0m
        }, cancellationToken);

        await _sessionRepository.TouchAsync(session.Id, now, cancellationToken);
        LogChatQualitySignal(session.Id, "ContactCapture", 0m, true, 0, "ContactDetails", null);
        return OperationResult<ChatSendResult>.Success(new ChatSendResult(session.Id, ContactDetailsReceivedResponse, 0m, true, []));
    }

    private static string BuildDistilledContext(IReadOnlyCollection<EngageChatMessage> messages, string currentUserMessage)
    {
        var userMessages = messages
            .Where(item => string.Equals(item.Role, "user", StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.CreatedAtUtc)
            .ToList();

        if (userMessages.Count > 0)
        {
            userMessages.RemoveAt(userMessages.Count - 1);
        }

        var currentUserMessageNormalized = currentUserMessage.Trim();

        var distilled = userMessages
            .Select(item => item.Content.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Where(item => !string.Equals(item, currentUserMessageNormalized, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .TakeLast(PromptDistilledUserTurnsLimit)
            .ToArray();

        if (distilled.Length == 0)
        {
            return "none";
        }

        return string.Join("\n", distilled.Select((item, index) => $"- priorUser{index + 1}: {item}"));
    }

    private async Task<(string TicketDescription, string? LastAssistantMessage, string TranscriptExcerpt, int CitationCount)> BuildHandoffPackageAsync(
        Guid sessionId,
        string userMessage,
        CancellationToken cancellationToken)
    {
        var messages = await _messageRepository.ListBySessionAsync(sessionId, cancellationToken);
        var latestMessages = messages
            .OrderBy(item => item.CreatedAtUtc)
            .TakeLast(HandoffTranscriptLineLimit)
            .Select(item => $"{(string.Equals(item.Role, "assistant", StringComparison.OrdinalIgnoreCase) ? "Assistant" : "User")}: {item.Content.Trim()}")
            .ToArray();

        var transcriptExcerpt = latestMessages.Length == 0
            ? $"User: {userMessage}"
            : string.Join("\n", latestMessages);

        var lastAssistantMessage = messages
            .Where(item => string.Equals(item.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => item.Content.Trim())
            .FirstOrDefault();

        var citationCount = messages
            .Where(item => string.Equals(item.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            .Sum(item => item.Citations?.Count ?? 0);

        var ticketDescription = $"{userMessage}\n\n[Engage handoff package]\nRecent transcript:\n{transcriptExcerpt}\nGrounding citations observed in session: {citationCount}";
        return (ticketDescription, lastAssistantMessage, transcriptExcerpt, citationCount);
    }

    private void LogChatQualitySignal(
        Guid sessionId,
        string path,
        decimal confidence,
        bool ticketCreated,
        int citationCount,
        string? reason,
        string? intent)
    {
        var confidenceBucket = confidence switch
        {
            >= 0.8m => "high",
            >= 0.5m => "medium",
            > 0m => "low",
            _ => "none"
        };

        _logger.LogInformation(
            "Engage chat quality signal for session {SessionId}: path={Path}, confidence={Confidence}, confidenceBucket={ConfidenceBucket}, ticketCreated={TicketCreated}, citationCount={CitationCount}, reason={Reason}, intent={Intent}.",
            sessionId,
            path,
            confidence,
            confidenceBucket,
            ticketCreated,
            citationCount,
            reason ?? string.Empty,
            intent ?? string.Empty);
    }

    private static string? TryExtractEmail(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var match = Regex.Match(message, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase);
        return match.Success ? match.Value.Trim() : null;
    }

    private static string? TryExtractName(string message, string? email)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var withoutEmail = !string.IsNullOrWhiteSpace(email)
            ? message.Replace(email, string.Empty, StringComparison.OrdinalIgnoreCase)
            : message;

        var normalized = withoutEmail.Trim(' ', ',', '.', ';', ':', '-', '_');
        if (normalized.StartsWith(ContactDetailsNamePrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[ContactDetailsNamePrefix.Length..].Trim(' ', ',', '.', ';', ':', '-', '_');
        }

        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized.Length <= 200 ? normalized : normalized[..200];
    }


    private async Task<Guid?> ResolveVisitorIdAsync(Guid tenantId, Guid siteId, string? collectorSessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(collectorSessionId))
        {
            return null;
        }

        return await _leadVisitorLinker.ResolveVisitorIdAsync(tenantId, siteId, null, null, collectorSessionId, cancellationToken);
    }

    private async Task<EngageChatSession> ResolveSessionAsync(
        Guid tenantId,
        Guid siteId,
        Guid botId,
        string widgetKey,
        Guid? sessionId,
        string? collectorSessionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var normalizedCollectorSessionId = NormalizeOptional(collectorSessionId);

        if (sessionId.HasValue)
        {
            var existing = await _sessionRepository.GetByIdAsync(sessionId.Value, cancellationToken);
            if (existing is not null && existing.TenantId == tenantId && existing.SiteId == siteId && existing.BotId == botId)
            {
                var idleFor = now - existing.UpdatedAtUtc;
                if (idleFor <= _sessionTimeout)
                {
                    if (!string.IsNullOrWhiteSpace(normalizedCollectorSessionId)
                        && string.IsNullOrWhiteSpace(existing.CollectorSessionId))
                    {
                        await _sessionRepository.SetCollectorSessionIdIfEmptyAsync(existing.Id, normalizedCollectorSessionId, cancellationToken);
                    }

                    return existing;
                }
            }
        }

        if (!sessionId.HasValue && !string.IsNullOrWhiteSpace(normalizedCollectorSessionId))
        {
            var byCollectorSession = await _sessionRepository.ListBySiteAsync(tenantId, siteId, normalizedCollectorSessionId, cancellationToken);
            var resumed = byCollectorSession
                .Where(item => item.BotId == botId && string.Equals(item.WidgetKey, widgetKey, StringComparison.Ordinal))
                .OrderByDescending(item => item.UpdatedAtUtc)
                .FirstOrDefault(item => now - item.UpdatedAtUtc <= _sessionTimeout);

            if (resumed is not null)
            {
                return resumed;
            }
        }

        var created = new EngageChatSession
        {
            TenantId = tenantId,
            SiteId = siteId,
            BotId = botId,
            WidgetKey = widgetKey,
            CollectorSessionId = normalizedCollectorSessionId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _sessionRepository.InsertAsync(created, cancellationToken);
        return created;
    }

    private static bool TryResolveManualPromo(string message, out string promoPublicKey)
    {
        promoPublicKey = string.Empty;

        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var trimmed = message.Trim();
        if (!trimmed.StartsWith(PromoCommandPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = trimmed.Length == PromoCommandPrefix.Length
            ? string.Empty
            : trimmed[PromoCommandPrefix.Length..].Trim();

        if (string.IsNullOrWhiteSpace(remainder))
        {
            return false;
        }

        promoPublicKey = remainder.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return !string.IsNullOrWhiteSpace(promoPublicKey);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private sealed record BusinessResponseTemplate(string Path, string Response);

    private enum ChatIntent
    {
        General,
        Contact,
        Location,
        Hours,
        Services,
        EscalationHelp,
        AmbiguousShortPrompt
    }
}
