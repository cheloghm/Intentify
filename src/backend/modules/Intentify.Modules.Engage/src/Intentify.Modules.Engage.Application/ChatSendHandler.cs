using System.Text.RegularExpressions;
using Intentify.Modules.Engage.Domain;
using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Sites.Application;
using Intentify.Modules.Sites.Domain;
using Intentify.Modules.Tickets.Application;
using Intentify.Shared.AI;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Engage.Application;

public sealed class ChatSendHandler
{
    public const decimal LowConfidenceThreshold = 0.50m;
    public const int TopChunkScoreThreshold = 2;
    private static readonly string[] HumanHelpPhrases =
    [
        "contact form",
        "form isn't working",
        "form is not working",
        "can't submit",
        "cannot submit",
        "doesn't submit",
        "broken",
        "error",
        "failed",
        "not working",
        "doesn't work",
        "checkout",
        "payment",
        "refund",
        "complaint"
    ];

    private const string AskForContactDetailsResponse = "Sorry about that — I’ll get someone to help. What’s your name and best email?";
    private const string ContactDetailsReceivedResponse = "Thanks — I’ve got your details. Our team will contact you shortly.";
    private const string GreetingResponse = "Hi! How can I help you today?";
    private const string AckResponse = "Got it — what would you like to know or do next?";
    private const string LowConfidenceClarificationResponse = "I don’t have that information in our knowledge base yet. If you tell me a bit more, I can help refine the question — or I can create a ticket for our team to follow up.";

    private readonly ISiteRepository _siteRepository;
    private readonly IEngageChatSessionRepository _sessionRepository;
    private readonly IEngageBotRepository _botRepository;
    private readonly IEngageChatMessageRepository _messageRepository;
    private readonly IEngageHandoffTicketRepository _ticketRepository;
    private readonly CreateTicketHandler _createTicketHandler;
    private readonly RetrieveTopChunksHandler _retrieveTopChunksHandler;
    private readonly IChatCompletionClient _chatCompletionClient;
    private readonly TimeSpan _sessionTimeout;

    public ChatSendHandler(
        ISiteRepository siteRepository,
        IEngageChatSessionRepository sessionRepository,
        IEngageBotRepository botRepository,
        IEngageChatMessageRepository messageRepository,
        IEngageHandoffTicketRepository ticketRepository,
        CreateTicketHandler createTicketHandler,
        RetrieveTopChunksHandler retrieveTopChunksHandler,
        IChatCompletionClient chatCompletionClient,
        int sessionTimeoutMinutes)
    {
        _siteRepository = siteRepository;
        _sessionRepository = sessionRepository;
        _botRepository = botRepository;
        _messageRepository = messageRepository;
        _ticketRepository = ticketRepository;
        _createTicketHandler = createTicketHandler;
        _retrieveTopChunksHandler = retrieveTopChunksHandler;
        _chatCompletionClient = chatCompletionClient;
        _sessionTimeout = TimeSpan.FromMinutes(sessionTimeoutMinutes > 0 ? sessionTimeoutMinutes : 30);
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
            return OperationResult<ChatSendResult>.NotFound();
        }

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

            return OperationResult<ChatSendResult>.Success(new ChatSendResult(session.Id, smalltalkResponse, 1m, false, []));
        }

        var retrieved = await _retrieveTopChunksHandler.HandleAsync(
            new RetrieveTopChunksQuery(site.TenantId, site.Id, command.Message, 3, bot.BotId),
            cancellationToken);

        var topScore = retrieved.Count == 0 ? 0 : retrieved.Max(item => item.Score);
        var confidence = ComputeConfidence(retrieved.Count > 0, topScore);

        var isLowConfidence = retrieved.Count == 0 || topScore < TopChunkScoreThreshold || confidence < LowConfidenceThreshold;
        if (isLowConfidence)
        {
            var shouldCreateTicket = IsRealQuestion(command.Message);
            if (shouldCreateTicket)
            {
                return await CreateFallbackResponseAsync(site, session, command.Message, now, "LowConfidence", cancellationToken);
            }

            await _messageRepository.InsertAsync(new EngageChatMessage
            {
                SessionId = session.Id,
                Role = "assistant",
                Content = LowConfidenceClarificationResponse,
                CreatedAtUtc = now,
                Confidence = 0m
            }, cancellationToken);

            await _sessionRepository.TouchAsync(session.Id, now, cancellationToken);

            return OperationResult<ChatSendResult>.Success(new ChatSendResult(session.Id, LowConfidenceClarificationResponse, 0m, false, []));
        }

        var citations = retrieved
            .Select(item => new EngageCitationResult(item.SourceId, item.ChunkId, item.ChunkIndex))
            .ToArray();

        var completion = await _chatCompletionClient.CompleteAsync(BuildPrompt(command.Message, retrieved, recentMessages), cancellationToken);
        if (!completion.IsSuccess || string.IsNullOrWhiteSpace(completion.Value))
        {
            return await CreateFallbackResponseAsync(site, session, command.Message, now, "AiUnavailable", cancellationToken);
        }

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

        return OperationResult<ChatSendResult>.Success(new ChatSendResult(session.Id, response, confidence, false, citations));
    }

    private static decimal ComputeConfidence(bool hasChunks, int topScore)
    {
        if (!hasChunks)
        {
            return 0m;
        }

        return Math.Min(1m, topScore / 4m);
    }

    private static string BuildPrompt(string message, IReadOnlyCollection<RetrievedChunkResult> chunks, IReadOnlyCollection<EngageChatMessage> messages)
    {
        var context = string.Join("\n", chunks
            .Take(3)
            .Select((item, index) => $"[{index + 1}] {item.Content.Trim()}"));

        var transcript = string.Join("\n", messages
            .TakeLast(30)
            .Select(item => $"{(string.Equals(item.Role, "assistant", StringComparison.OrdinalIgnoreCase) ? "Assistant" : "User")}: {item.Content.Trim()}"));

        return $"""
You are an Engage support assistant.

Use only the knowledge context to answer the user's question in plain English.
- Keep the answer concise: 1-2 short paragraphs.
- Rephrase the content naturally; do not copy/paste raw website text.
- Do not use markdown lists, bullets, or asterisks unless the user explicitly asks for a list.
- Do not start with: Based on your knowledge base:
- If the knowledge context is not enough, ask exactly one clarifying question.

Knowledge context:
{context}

Conversation transcript (oldest to newest):
{transcript}

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
        CancellationToken cancellationToken)
    {
        const decimal fallbackConfidence = 0m;
        var fallbackResponse = "Thanks — we’ll get back to you shortly.";

        await _ticketRepository.InsertAsync(new EngageHandoffTicket
        {
            TenantId = site.TenantId,
            SiteId = site.Id,
            SessionId = session.Id,
            UserMessage = userMessage,
            Reason = reason,
            CreatedAtUtc = now
        }, cancellationToken);

        await _createTicketHandler.HandleAsync(
            new CreateTicketCommand(
                site.TenantId,
                site.Id,
                null,
                session.Id,
                $"Engage handoff: {reason}",
                userMessage,
                null),
            cancellationToken);

        await _messageRepository.InsertAsync(new EngageChatMessage
        {
            SessionId = session.Id,
            Role = "assistant",
            Content = fallbackResponse,
            CreatedAtUtc = now,
            Confidence = fallbackConfidence
        }, cancellationToken);

        await _sessionRepository.TouchAsync(session.Id, now, cancellationToken);

        return OperationResult<ChatSendResult>.Success(new ChatSendResult(session.Id, fallbackResponse, fallbackConfidence, true, []));
    }

    private static bool NeedsHumanHelp(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return HumanHelpPhrases.Any(phrase =>
            message.Contains(phrase, StringComparison.OrdinalIgnoreCase));
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
        await _ticketRepository.InsertAsync(new EngageHandoffTicket
        {
            TenantId = site.TenantId,
            SiteId = site.Id,
            SessionId = session.Id,
            UserMessage = userMessage,
            Reason = "NeedsHumanHelp",
            CreatedAtUtc = now
        }, cancellationToken);

        await _createTicketHandler.HandleAsync(
            new CreateTicketCommand(
                site.TenantId,
                site.Id,
                null,
                session.Id,
                "Engage handoff: NeedsHumanHelp",
                userMessage,
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
        return OperationResult<ChatSendResult>.Success(new ChatSendResult(session.Id, AskForContactDetailsResponse, 0m, true, []));
    }

    private async Task<OperationResult<ChatSendResult>> CaptureContactDetailsAsync(
        Site site,
        EngageChatSession session,
        string userMessage,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await _createTicketHandler.HandleAsync(
            new CreateTicketCommand(
                site.TenantId,
                site.Id,
                null,
                session.Id,
                "Engage handoff: ContactDetails",
                $"Contact details provided by visitor: {userMessage}",
                null),
            cancellationToken);

        await _messageRepository.InsertAsync(new EngageChatMessage
        {
            SessionId = session.Id,
            Role = "assistant",
            Content = ContactDetailsReceivedResponse,
            CreatedAtUtc = now,
            Confidence = 0m
        }, cancellationToken);

        await _sessionRepository.TouchAsync(session.Id, now, cancellationToken);
        return OperationResult<ChatSendResult>.Success(new ChatSendResult(session.Id, ContactDetailsReceivedResponse, 0m, true, []));
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
        if (sessionId.HasValue)
        {
            var existing = await _sessionRepository.GetByIdAsync(sessionId.Value, cancellationToken);
            if (existing is not null && existing.TenantId == tenantId && existing.SiteId == siteId && existing.BotId == botId)
            {
                var idleFor = now - existing.UpdatedAtUtc;
                if (idleFor <= _sessionTimeout)
                {
                    if (!string.IsNullOrWhiteSpace(collectorSessionId)
                        && string.IsNullOrWhiteSpace(existing.CollectorSessionId))
                    {
                        await _sessionRepository.SetCollectorSessionIdIfEmptyAsync(existing.Id, collectorSessionId, cancellationToken);
                    }

                    return existing;
                }
            }
        }

        var created = new EngageChatSession
        {
            TenantId = tenantId,
            SiteId = siteId,
            BotId = botId,
            WidgetKey = widgetKey,
            CollectorSessionId = string.IsNullOrWhiteSpace(collectorSessionId) ? null : collectorSessionId.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _sessionRepository.InsertAsync(created, cancellationToken);
        return created;
    }
}
