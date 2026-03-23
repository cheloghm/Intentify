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
    private static readonly EngageConversationPolicy ConversationPolicy = new();
    public const decimal LowConfidenceThreshold = 0.50m;
    public const int TopChunkScoreThreshold = 2;
    private const int PromptReplayMessageLimit = 12;
    private const int PromptDistilledUserTurnsLimit = 3;
    private const int HandoffTranscriptLineLimit = 8;

    private const string AskForContactDetailsResponse = "Sorry about that — I’ll get someone to help. What’s your name and best email?";
    private const string ContactDetailsReceivedResponse = "Thanks — I’ve got your details. Our team will contact you shortly.";
    private const string CommercialContactDetailsPrefix = "Thanks — it sounds like you’re looking for";
    private const string GreetingResponse = "Hi! How can I help you today?";
    private const string AckResponse = "Got it.";
    private const string NeutralClarificationResponse = "Happy to help — could you share a bit more about what you need?";
    private const string SoftFallbackResponse = "I can help with that — what would you like to sort out first?";
    private const string EscalationFallbackResponse = "Thanks — I can connect you with our team. Please share your name and best email.";
    private const string PromoCommandPrefix = "/promo";
    private const string PromoResponseText = "Please complete this short promo form.";
    private const string StateGreeting = "Greeting";
    private const string StateInform = "Inform";
    private const string StateDiscover = "Discover";
    private const string StateCaptureLead = "CaptureLead";
    private const string StateSupportTriage = "SupportTriage";
    private const string StateConfirmHandoff = "ConfirmHandoff";
    private const string StateClarify = "Clarify";
    private const string StateCloseIdle = "CloseIdle";
    private const string CaptureModeLead = "Lead";
    private const string CaptureModeSupport = "Support";
    private const string PreferredContactMethodEmail = "Email";
    private const string PreferredContactMethodPhone = "Phone";
    private const string AskPreferredContactMethodResponse = "What’s the best way to reach you — email or phone?";
    private const string AskForEmailResponse = "Thanks — what’s your best email?";
    private const string AskForPhoneResponse = "Thanks — what’s your best phone number?";
    private static readonly string[] VerboseRequestTerms =
    [
        "detail",
        "detailed",
        "step by step",
        "list",
        "all",
        "everything"
    ];
    private static readonly string[] FillerPhrases =
    [
        "If you want, I can also",
        "If you'd like",
        "If you’d like",
        "What would you like to do next?"
    ];
    private const string SupportTroubleshootPrompt = "Sorry you’re running into that — what happens when you try it (any error text or the exact step where it fails)?";
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
        var sessionHandoffs = await _ticketRepository.ListBySessionAsync(session.Id, cancellationToken);
        var normalizedMessage = ConversationPolicy.NormalizeUserMessage(command.Message);
        var userAskedForDetail = UserRequestedDetail(command.Message);
        MergeDiscoverySlots(session, command.Message);

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

        var priorAssistantAskedQuestion = TryGetLastAssistantDirectQuestion(recentMessages, out _);
        if (ConversationPolicy.TryBuildSmalltalkResponse(command.Message, priorAssistantAskedQuestion, GreetingResponse, AckResponse, out var smalltalkResponse))
        {
            session.ConversationState = smalltalkResponse == GreetingResponse ? StateGreeting : StateCloseIdle;
            return await CreateAssistantResponseAsync(session, now, ShapeAssistantResponse(smalltalkResponse, userAskedForDetail), 1m, false, "Smalltalk", null, cancellationToken);
        }

        if (IsCaptureMode(session, CaptureModeSupport) || IsCaptureMode(session, CaptureModeLead))
        {
            var capture = await ContinueProgressiveCaptureAsync(site, session, command.Message, now, sessionHandoffs, cancellationToken);
            if (capture is not null)
            {
                return capture;
            }
        }

        var intent = ConversationPolicy.DetectIntent(normalizedMessage);
        var hasCommercialIntent = ConversationPolicy.TryBuildCommercialIntentContactPrompt(command.Message, CommercialContactDetailsPrefix, out var commercialPrompt)
            || ConversationPolicy.IsStrongCommercialIntent(command.Message);
        var explicitCommercialContactRequest = ConversationPolicy.IsExplicitCommercialContactRequest(command.Message);
        var isRecommendationIntent = ConversationPolicy.IsRecommendationIntent(normalizedMessage);
        if (isRecommendationIntent && !hasCommercialIntent)
        {
            var recommendationResponse = ConversationPolicy.BuildRecommendationResponse(session, command.Message);
            session.ConversationState = StateDiscover;
            return await CreateAssistantResponseAsync(session, now, recommendationResponse, 0.45m, false, "Recommendation", ConversationPolicy.HasSufficientDiscoveryContext(session) ? "Direct" : "Clarify", cancellationToken);
        }

        if (hasCommercialIntent && (explicitCommercialContactRequest || ConversationPolicy.HasSufficientDiscoveryContext(session)))
        {
            var commercialResponse = explicitCommercialContactRequest && !string.IsNullOrWhiteSpace(commercialPrompt)
                ? commercialPrompt
                : ConversationPolicy.BuildNextDiscoveryQuestion(session);
            return await CreateCommercialLeadCapturePromptAsync(site, session, command.Message, commercialResponse, now, sessionHandoffs, recentMessages, cancellationToken);
        }

        if (ConversationPolicy.NeedsHumanHelp(command.Message))
        {
            if (ConversationPolicy.ShouldAttemptSupportTroubleshoot(session, command.Message, IsCaptureMode(session, CaptureModeSupport)))
            {
                session.ConversationState = StateSupportTriage;
                return await CreateAssistantResponseAsync(session, now, SupportTroubleshootPrompt, 0.3m, false, "SupportTriage", "TroubleshootFirst", cancellationToken);
            }

            return await CreateHumanHelpResponseAsync(site, session, command.Message, now, sessionHandoffs, recentMessages, cancellationToken);
        }

        var hasDirectQuestionContext = priorAssistantAskedQuestion;
        var isContinuationReply = ConversationPolicy.IsContinuationReply(command.Message);
        if (intent == ChatIntent.EscalationHelp)
        {
            return await CreateHumanHelpResponseAsync(site, session, command.Message, now, sessionHandoffs, recentMessages, cancellationToken);
        }

        if (intent == ChatIntent.AmbiguousShortPrompt)
        {
            return await CreateLayeredFallbackResponseAsync(
                site,
                bot,
                session,
                command.Message,
                normalizedMessage,
                intent,
                recentMessages,
                [],
                sessionHandoffs,
                now,
                "AmbiguousShortPrompt",
                hasDirectQuestionContext,
                isContinuationReply,
                cancellationToken);
        }

        var retrieved = await _retrieveTopChunksHandler.HandleAsync(
            new RetrieveTopChunksQuery(site.TenantId, site.Id, normalizedMessage, 3, bot.BotId),
            cancellationToken);

        var topScore = retrieved.Count == 0 ? 0 : retrieved.Max(item => item.Score);
        var confidence = ComputeConfidence(retrieved.Count > 0, topScore);

        _logger.LogInformation("Engage retrieval summary for session {SessionId}: hits={Hits}, topScore={TopScore}, confidence={Confidence}.", session.Id, retrieved.Count, topScore, confidence);

        var isLowConfidence = retrieved.Count == 0 || topScore < TopChunkScoreThreshold || confidence < LowConfidenceThreshold;
        if (isLowConfidence)
        {
            if (hasCommercialIntent && !explicitCommercialContactRequest)
            {
                var nextDiscoveryQuestion = ConversationPolicy.BuildNextDiscoveryQuestion(session);
                var priorDiscoveryQuestions = CountDiscoveryQuestionsAsked(recentMessages);
                if (priorDiscoveryQuestions >= 2 || TryGetLastAssistantDirectQuestion(recentMessages, out var lastQuestion) && string.Equals(lastQuestion, nextDiscoveryQuestion, StringComparison.Ordinal))
                {
                    var steadyCommercialResponse = "Thanks — that helps. Based on what you’ve shared, I can tailor a recommendation with one more detail: what matters most right now, timeline, budget, or scope?";
                    session.ConversationState = StateDiscover;
                    return await CreateAssistantResponseAsync(
                        session,
                        now,
                        ShapeAssistantResponse(steadyCommercialResponse, false),
                        0.4m,
                        false,
                        "Discover",
                        "CommercialPacing",
                        cancellationToken);
                }

                session.ConversationState = StateDiscover;
                return await CreateAssistantResponseAsync(
                    session,
                    now,
                    ShapeAssistantResponse(nextDiscoveryQuestion, false, allowMultipleQuestions: true),
                    0.4m,
                    false,
                    "Discover",
                    "CommercialExploreFirst",
                    cancellationToken);
            }

            _logger.LogInformation("Engage chat decision: layered fallback path for session {SessionId}.", session.Id);
            return await CreateLayeredFallbackResponseAsync(site, bot, session, command.Message, normalizedMessage, intent, recentMessages, retrieved, sessionHandoffs, now, "LowConfidence", hasDirectQuestionContext, isContinuationReply, cancellationToken);
        }

        var citations = retrieved
            .Select(item => new EngageCitationResult(item.SourceId, item.ChunkId, item.ChunkIndex))
            .ToArray();

        var completion = await _chatCompletionClient.CompleteAsync(BuildPrompt(bot, command.Message, normalizedMessage, retrieved, recentMessages), cancellationToken);
        if (!completion.IsSuccess || string.IsNullOrWhiteSpace(completion.Value))
        {
            _logger.LogWarning("Engage AI completion unavailable for session {SessionId}.", session.Id);
            return await CreateLayeredFallbackResponseAsync(site, bot, session, command.Message, normalizedMessage, intent, recentMessages, retrieved, sessionHandoffs, now, "AiUnavailable", hasDirectQuestionContext, isContinuationReply, cancellationToken);
        }

        _logger.LogInformation("Engage chat decision: grounded answer path for session {SessionId}.", session.Id);

        var assistantResponse = ShapeAssistantResponse(NormalizeAiResponse(completion.Value), userAskedForDetail);
        session.ConversationState = StateInform;
        await _messageRepository.InsertAsync(new EngageChatMessage
        {
            SessionId = session.Id,
            Role = "assistant",
            Content = assistantResponse,
            CreatedAtUtc = now,
            Confidence = confidence,
            Citations = citations.Select(item => new EngageCitation
            {
                SourceId = item.SourceId,
                ChunkId = item.ChunkId,
                ChunkIndex = item.ChunkIndex
            }).ToArray()
        }, cancellationToken);

        session.UpdatedAtUtc = now;
        await _sessionRepository.UpdateStateAsync(session, cancellationToken);

        LogChatQualitySignal(session.Id, "Grounded", confidence, false, citations.Length, null, intent.ToString());

        return OperationResult<ChatSendResult>.Success(new ChatSendResult(
            session.Id,
            assistantResponse,
            confidence,
            false,
            citations,
            null));
    }

    private static decimal ComputeConfidence(bool hasChunks, int topScore)
    {
        if (!hasChunks)
        {
            return 0m;
        }

        return Math.Min(1m, topScore / 4m);
    }

    private static string BuildPrompt(EngageBot bot, string message, string normalizedMessage, IReadOnlyCollection<RetrievedChunkResult> chunks, IReadOnlyCollection<EngageChatMessage> messages)
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
You are a site-aware business representative for this website.

Use only the retrieved knowledge context to answer the user's question in plain English.
- Keep a {tone} tone.
- Keep verbosity {verbosity}.
- Keep the answer concise and practical.
- Rephrase the content naturally; do not copy/paste raw website text.
- Do not use markdown lists, bullets, or asterisks unless the user explicitly asks for a list.
- Do not sound robotic.
- Do not start with: Based on your knowledge base:
- If details are incomplete, do not mention internal systems or missing sources.
- If enough context is already present, provide a concise summary and a concrete next step instead of asking another follow-up question.
- Ask one brief, specific follow-up question only when it is necessary to move forward safely.
- Do not invent unsupported specifics.

Retrieved knowledge context (deduped):
{context}

Conversation transcript (oldest to newest):
{transcript}

Distilled prior user context:
{distilledContext}

User question:
{message}

Normalized user question (for typo recovery):
{normalizedMessage}
""";
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
        const string prohibitedPrefix = "Based on available information:";

        if (normalized.StartsWith(prohibitedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[prohibitedPrefix.Length..].TrimStart();
        }

        return normalized;
    }

    private static bool UserRequestedDetail(string message)
    {
        var normalized = message.Trim().ToLowerInvariant();
        return VerboseRequestTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
    }

    private static string ShapeAssistantResponse(string response, bool userAskedForDetail, bool allowMultipleQuestions = false)
    {
        var normalized = NormalizeAiResponse(response);
        foreach (var filler in FillerPhrases)
        {
            normalized = normalized.Replace(filler, string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        }

        normalized = Regex.Replace(normalized, @"\s{2,}", " ").Trim();
        if (!allowMultipleQuestions)
        {
            var firstQuestion = normalized.IndexOf('?', StringComparison.Ordinal);
            if (firstQuestion >= 0)
            {
                var laterQuestion = normalized.IndexOf('?', firstQuestion + 1);
                if (laterQuestion >= 0)
                {
                    normalized = normalized[..laterQuestion].TrimEnd();
                }
            }
        }

        if (userAskedForDetail)
        {
            return normalized;
        }

        var sentences = Regex.Split(normalized, @"(?<=[\.\?\!])\s+")
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Take(4)
            .ToArray();
        return sentences.Length == 0 ? normalized : string.Join(" ", sentences);
    }

    private static void MergeDiscoverySlots(EngageChatSession session, string message)
    {
        var normalized = message.Trim();
        if (string.IsNullOrWhiteSpace(session.CaptureGoal))
        {
            var goalMatch = Regex.Match(normalized, @"(?:need|want|looking for|looking to|trying to)\s+([^\.!\?,]+)", RegexOptions.IgnoreCase);
            if (goalMatch.Success)
            {
                session.CaptureGoal = goalMatch.Groups[1].Value.Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(session.CaptureLocation))
        {
            var locationMatch = Regex.Match(normalized, @"\b(?:in|near|around|at)\s+([A-Za-z0-9\s\-]{2,40})", RegexOptions.IgnoreCase);
            if (locationMatch.Success)
            {
                session.CaptureLocation = locationMatch.Groups[1].Value.Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(session.CaptureConstraints)
            && Regex.IsMatch(normalized, @"\b(budget|timeline|deadline|urgent|asap|cost|price)\b", RegexOptions.IgnoreCase))
        {
            session.CaptureConstraints = normalized.Length <= 200 ? normalized : normalized[..200];
        }

        if (string.IsNullOrWhiteSpace(session.CaptureType))
        {
            var typeMatch = Regex.Match(normalized, @"\bfor\s+([^\.!\?,]+)", RegexOptions.IgnoreCase);
            if (typeMatch.Success)
            {
                session.CaptureType = typeMatch.Groups[1].Value.Trim();
            }
        }

        session.CaptureContext ??= normalized.Length <= 200 ? normalized : normalized[..200];
    }

    private async Task<OperationResult<ChatSendResult>> CreateFallbackResponseAsync(
        Site site,
        EngageChatSession session,
        string userMessage,
        DateTime now,
        string reason,
        IReadOnlyCollection<EngageHandoffTicket> existingHandoffs,
        IReadOnlyCollection<EngageChatMessage> existingMessages,
        CancellationToken cancellationToken,
        string? responseOverride = null)
    {
        const decimal fallbackConfidence = 0m;
        var fallbackResponse = responseOverride ?? "Thanks — we’ll get back to you shortly.";

        var createdTicket = !existingHandoffs.Any(item => string.Equals(item.Reason, reason, StringComparison.Ordinal));
        var handoffPackage = BuildHandoffPackage(existingMessages, userMessage);

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

        session.ConversationState = StateConfirmHandoff;
        session.PendingCaptureMode = CaptureModeSupport;
        session.UpdatedAtUtc = now;
        await _sessionRepository.UpdateStateAsync(session, cancellationToken);

        LogChatQualitySignal(session.Id, "EscalationFallback", fallbackConfidence, createdTicket, handoffPackage.CitationCount, reason, null);

        return OperationResult<ChatSendResult>.Success(new ChatSendResult(session.Id, fallbackResponse, fallbackConfidence, createdTicket, []));
    }

    private async Task<OperationResult<ChatSendResult>> CreateLayeredFallbackResponseAsync(
        Site site,
        EngageBot bot,
        EngageChatSession session,
        string userMessage,
        string normalizedUserMessage,
        ChatIntent intent,
        IReadOnlyCollection<EngageChatMessage> messages,
        IReadOnlyCollection<RetrievedChunkResult> retrievedChunks,
        IReadOnlyCollection<EngageHandoffTicket> handoffs,
        DateTime now,
        string reason,
        bool hasDirectQuestionContext,
        bool isContinuationReply,
        CancellationToken cancellationToken)
    {
        var shouldEscalate = ConversationPolicy.ShouldEscalateFallback(bot, intent, normalizedUserMessage, reason, IsRealQuestion(userMessage));
        if (!shouldEscalate)
        {
            var fallback = IsRealQuestion(userMessage) || (hasDirectQuestionContext && isContinuationReply)
                ? reason == "LowConfidence" && retrievedChunks.Count == 0
                    ? BuildDeterministicClarificationResponse(intent)
                    : await BuildBusinessAwareClarificationResponseAsync(bot, userMessage, normalizedUserMessage, intent, messages, retrievedChunks, cancellationToken)
                : ConversationPolicy.BuildSoftFallbackResponse(bot, SoftFallbackResponse);
            session.ConversationState = StateClarify;
            return await CreateAssistantResponseAsync(session, now, ShapeAssistantResponse(fallback, UserRequestedDetail(userMessage)), 0.2m, false, "Fallback", reason, cancellationToken);
        }

        return await CreateFallbackResponseAsync(site, session, userMessage, now, reason, handoffs, messages, cancellationToken, EscalationFallbackResponse);
    }


    private async Task<string> BuildBusinessAwareClarificationResponseAsync(
        EngageBot bot,
        string userMessage,
        string normalizedUserMessage,
        ChatIntent intent,
        IReadOnlyCollection<EngageChatMessage> messages,
        IReadOnlyCollection<RetrievedChunkResult> retrievedChunks,
        CancellationToken cancellationToken)
    {
        var prompt = BuildClarificationPrompt(bot, userMessage, normalizedUserMessage, intent, messages, retrievedChunks);
        var completion = await _chatCompletionClient.CompleteAsync(prompt, cancellationToken);
        if (!completion.IsSuccess || string.IsNullOrWhiteSpace(completion.Value))
        {
            return NeutralClarificationResponse;
        }

        return ShapeAssistantResponse(completion.Value, false);
    }

    private static string BuildClarificationPrompt(
        EngageBot bot,
        string userMessage,
        string normalizedUserMessage,
        ChatIntent intent,
        IReadOnlyCollection<EngageChatMessage> messages,
        IReadOnlyCollection<RetrievedChunkResult> retrievedChunks)
    {
        var tone = ResolvePromptTone(bot.Tone);
        var recentTurns = string.Join("\n", messages
            .TakeLast(PromptReplayMessageLimit)
            .Select(item => $"{(string.Equals(item.Role, "assistant", StringComparison.OrdinalIgnoreCase) ? "Assistant" : "User")}: {item.Content.Trim()}"));
        var distilledContext = BuildDistilledContext(messages, userMessage);
        var chunkContext = string.Join("\n", retrievedChunks
            .Select(item => item.Content.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .Select((item, index) => $"[{index + 1}] {item}"));

        return $"""
You are a human-sounding site representative for a business website chat.

Write exactly one short reply for a low-support situation.
- Keep a {tone} tone.
- Do not mention internal systems, confidence, reliability, or knowledge base details.
- Do not invent unsupported specifics.
- Use business cues only if supported by the conversation or context snippets.
- If context is already sufficient, give a brief summary and a concrete next step instead of asking another question.
- If context is weak or business type is unclear, ask one neutral clarification question.
- Keep it concise and practical.

Detected intent: {intent}

Retrieved business context snippets:
{(string.IsNullOrWhiteSpace(chunkContext) ? "none" : chunkContext)}

Conversation transcript (oldest to newest):
{(string.IsNullOrWhiteSpace(recentTurns) ? "none" : recentTurns)}

Distilled prior user context:
{distilledContext}

Current user message:
{userMessage}

Normalized user message:
{normalizedUserMessage}
""";
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
        EngageChatSession session,
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
            SessionId = session.Id,
            Role = "assistant",
            Content = response,
            CreatedAtUtc = now,
            Confidence = confidence
        }, cancellationToken);

        session.UpdatedAtUtc = now;
        await _sessionRepository.UpdateStateAsync(session, cancellationToken);
        LogChatQualitySignal(session.Id, qualityPath, confidence, ticketCreated, 0, qualityReason, null);
        return OperationResult<ChatSendResult>.Success(new ChatSendResult(session.Id, response, confidence, ticketCreated, []));
    }

    private static bool ContainsEmail(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return Regex.IsMatch(message, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase);
    }

    private static bool ContainsPhone(string message) => !string.IsNullOrWhiteSpace(ConversationPolicy.TryExtractPhone(message));

    private static bool IsCaptureMode(EngageChatSession session, string mode)
    {
        return string.Equals(session.PendingCaptureMode, mode, StringComparison.Ordinal);
    }

    private async Task<OperationResult<ChatSendResult>?> ContinueProgressiveCaptureAsync(
        Site site,
        EngageChatSession session,
        string userMessage,
        DateTime now,
        IReadOnlyCollection<EngageHandoffTicket> handoffs,
        CancellationToken cancellationToken)
    {
        var parsedEmail = ConversationPolicy.TryExtractEmail(userMessage);
        var parsedPhone = ConversationPolicy.TryExtractPhone(userMessage);
        var parsedName = ConversationPolicy.TryExtractName(userMessage, parsedEmail, parsedPhone);
        var parsedPreferredContactMethod = ConversationPolicy.TryExtractPreferredContactMethod(userMessage, parsedEmail, parsedPhone);

        session.CapturedEmail ??= parsedEmail;
        session.CapturedPhone ??= parsedPhone;
        session.CapturedName ??= parsedName;
        session.CapturedPreferredContactMethod ??= parsedPreferredContactMethod;

        if (!string.IsNullOrWhiteSpace(parsedEmail) || !string.IsNullOrWhiteSpace(parsedPhone))
        {
            session.CapturedEmail = parsedEmail ?? session.CapturedEmail;
            session.CapturedPhone = parsedPhone ?? session.CapturedPhone;
        }

        if (!string.IsNullOrWhiteSpace(parsedPreferredContactMethod))
        {
            session.CapturedPreferredContactMethod = parsedPreferredContactMethod;
        }

        if (string.IsNullOrWhiteSpace(session.CapturedPreferredContactMethod))
        {
            if (!string.IsNullOrWhiteSpace(session.CapturedEmail))
            {
                session.CapturedPreferredContactMethod = PreferredContactMethodEmail;
            }
            else if (!string.IsNullOrWhiteSpace(session.CapturedPhone))
            {
                session.CapturedPreferredContactMethod = PreferredContactMethodPhone;
            }
        }

        var hasFollowupMinimum = !string.IsNullOrWhiteSpace(session.CapturedName)
            && (string.Equals(session.CapturedPreferredContactMethod, PreferredContactMethodEmail, StringComparison.Ordinal)
                ? !string.IsNullOrWhiteSpace(session.CapturedEmail)
                : string.Equals(session.CapturedPreferredContactMethod, PreferredContactMethodPhone, StringComparison.Ordinal)
                    ? !string.IsNullOrWhiteSpace(session.CapturedPhone)
                    : false);

        if (!hasFollowupMinimum)
        {
            var question = string.IsNullOrWhiteSpace(session.CapturedName)
                ? "Please share your first name."
                : string.IsNullOrWhiteSpace(session.CapturedPreferredContactMethod)
                    ? AskPreferredContactMethodResponse
                    : string.Equals(session.CapturedPreferredContactMethod, PreferredContactMethodEmail, StringComparison.Ordinal)
                        ? AskForEmailResponse
                        : AskForPhoneResponse;
            session.ConversationState = StateCaptureLead;
            session.UpdatedAtUtc = now;
            await _sessionRepository.UpdateStateAsync(session, cancellationToken);
            return await CreateAssistantResponseAsync(session, now, question, 0.6m, false, "CaptureLead", "AwaitingContactFields", cancellationToken);
        }

        return await CaptureContactDetailsAsync(site, session, userMessage, now, false, handoffs, cancellationToken);
    }

    private async Task<OperationResult<ChatSendResult>> CreateCommercialLeadCapturePromptAsync(
        Site site,
        EngageChatSession session,
        string userMessage,
        string prompt,
        DateTime now,
        IReadOnlyCollection<EngageHandoffTicket> handoffs,
        IReadOnlyCollection<EngageChatMessage> messages,
        CancellationToken cancellationToken)
    {
        if (!handoffs.Any(item => string.Equals(item.Reason, "CommercialLeadCapture", StringComparison.Ordinal)))
        {
            var handoffPackage = BuildHandoffPackage(messages, userMessage);
            await _ticketRepository.InsertAsync(new EngageHandoffTicket
            {
                TenantId = site.TenantId,
                SiteId = site.Id,
                SessionId = session.Id,
                UserMessage = userMessage,
                Reason = "CommercialLeadCapture",
                LastAssistantMessage = handoffPackage.LastAssistantMessage,
                TranscriptExcerpt = handoffPackage.TranscriptExcerpt,
                CitationCount = handoffPackage.CitationCount,
                CreatedAtUtc = now
            }, cancellationToken);
        }

        session.ConversationState = StateCaptureLead;
        session.PendingCaptureMode = CaptureModeLead;
        return await CreateAssistantResponseAsync(session, now, ShapeAssistantResponse(prompt, false, allowMultipleQuestions: true), 0.8m, false, "CommercialLeadCapture", "CommercialIntent", cancellationToken);
    }

    private static string BuildDeterministicClarificationResponse(ChatIntent intent)
    {
        return intent switch
        {
            ChatIntent.Contact => "I can help with contact details — are you looking for a phone number, email, or contact form?",
            ChatIntent.Location => "Happy to help with location — are you looking for our address or service area?",
            ChatIntent.Hours => "I can help with hours — do you want weekday, weekend, or holiday times?",
            ChatIntent.Services => "I can help with services — which specific service do you want details about?",
            ChatIntent.Organization => "If you’re asking about our organization, I can help with the business name, contact details, hours, location, or services. Which one do you need?",
            _ => NeutralClarificationResponse
        };
    }

    private async Task<OperationResult<ChatSendResult>> CreateHumanHelpResponseAsync(
        Site site,
        EngageChatSession session,
        string userMessage,
        DateTime now,
        IReadOnlyCollection<EngageHandoffTicket> handoffs,
        IReadOnlyCollection<EngageChatMessage> messages,
        CancellationToken cancellationToken)
    {
        var handoffPackage = BuildHandoffPackage(messages, userMessage);
        var needsHumanTicketExists = handoffs.Any(item => string.Equals(item.Reason, "NeedsHumanHelp", StringComparison.Ordinal));
        if (!needsHumanTicketExists)
        {
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
            await _createTicketHandler.HandleAsync(new CreateTicketCommand(site.TenantId, site.Id, visitorId, session.Id, "Engage handoff: NeedsHumanHelp", handoffPackage.TicketDescription, null), cancellationToken);
        }

        await _messageRepository.InsertAsync(new EngageChatMessage
        {
            SessionId = session.Id,
            Role = "assistant",
            Content = AskForContactDetailsResponse,
            CreatedAtUtc = now,
            Confidence = 0m
        }, cancellationToken);

        session.ConversationState = StateSupportTriage;
        session.PendingCaptureMode = CaptureModeSupport;
        session.UpdatedAtUtc = now;
        await _sessionRepository.UpdateStateAsync(session, cancellationToken);
        LogChatQualitySignal(session.Id, "HumanHelp", 0m, !needsHumanTicketExists, 0, "NeedsHumanHelp", null);
        return OperationResult<ChatSendResult>.Success(new ChatSendResult(session.Id, AskForContactDetailsResponse, 0m, !needsHumanTicketExists, []));
    }

    private async Task<OperationResult<ChatSendResult>> CaptureContactDetailsAsync(
        Site site,
        EngageChatSession session,
        string userMessage,
        DateTime now,
        bool createTicket,
        IReadOnlyCollection<EngageHandoffTicket> handoffs,
        CancellationToken cancellationToken)
    {
        var visitorId = await ResolveVisitorIdAsync(site.TenantId, site.Id, session.CollectorSessionId, cancellationToken);
        var parsedEmail = ConversationPolicy.TryExtractEmail(userMessage) ?? session.CapturedEmail;
        var parsedPhone = ConversationPolicy.TryExtractPhone(userMessage) ?? session.CapturedPhone;
        var parsedPreferredContactMethod = ConversationPolicy.TryExtractPreferredContactMethod(userMessage, parsedEmail, parsedPhone) ?? session.CapturedPreferredContactMethod;
        var parsedName = ConversationPolicy.TryExtractName(userMessage, parsedEmail, parsedPhone) ?? session.CapturedName;
        session.CapturedEmail = parsedEmail;
        session.CapturedPhone = parsedPhone;
        session.CapturedPreferredContactMethod = parsedPreferredContactMethod;
        session.CapturedName = parsedName;

        if (createTicket && !handoffs.Any(item => string.Equals(item.Reason, "ContactDetails", StringComparison.Ordinal)))
        {
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
        }

        if (!string.IsNullOrWhiteSpace(parsedEmail) || !string.IsNullOrWhiteSpace(parsedPhone))
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
                    true,
                    parsedPhone,
                    session.CapturedPreferredContactMethod),
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

        session.PendingCaptureMode = null;
        session.ConversationState = StateConfirmHandoff;
        session.UpdatedAtUtc = now;
        await _sessionRepository.UpdateStateAsync(session, cancellationToken);
        LogChatQualitySignal(session.Id, "ContactCapture", 0m, createTicket, 0, "ContactDetails", null);
        return OperationResult<ChatSendResult>.Success(new ChatSendResult(session.Id, ContactDetailsReceivedResponse, 0m, createTicket, []));
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

    private static (string TicketDescription, string? LastAssistantMessage, string TranscriptExcerpt, int CitationCount) BuildHandoffPackage(
        IReadOnlyCollection<EngageChatMessage> messages,
        string userMessage)
    {
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

    private static bool IsContinuationReply(string message) => ConversationPolicy.IsContinuationReply(message);

    private static bool TryGetLastAssistantDirectQuestion(IReadOnlyCollection<EngageChatMessage> messages, out string question)
    {
        question = string.Empty;
        var lastAssistant = messages
            .Reverse()
            .Skip(1)
            .FirstOrDefault(item => string.Equals(item.Role, "assistant", StringComparison.OrdinalIgnoreCase));

        if (lastAssistant is null)
        {
            return false;
        }

        var content = lastAssistant.Content.Trim();
        if (!content.EndsWith("?", StringComparison.Ordinal))
        {
            return false;
        }

        question = content;
        return true;
    }

    private static int CountDiscoveryQuestionsAsked(IReadOnlyCollection<EngageChatMessage> messages)
    {
        return messages.Count(item =>
            string.Equals(item.Role, "assistant", StringComparison.OrdinalIgnoreCase)
            && (
                string.Equals(item.Content.Trim(), "What outcome are you trying to achieve?", StringComparison.Ordinal)
                || string.Equals(item.Content.Trim(), "What type of work or use case is this for?", StringComparison.Ordinal)
                || string.Equals(item.Content.Trim(), "What location should we plan for?", StringComparison.Ordinal)
                || string.Equals(item.Content.Trim(), "Any key constraints like budget or timeline?", StringComparison.Ordinal)));
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
            var existing = await _sessionRepository.GetByIdAsync(tenantId, siteId, sessionId.Value, cancellationToken);
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

}
