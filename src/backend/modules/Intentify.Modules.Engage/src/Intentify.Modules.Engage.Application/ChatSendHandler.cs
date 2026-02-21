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
        var session = await ResolveSessionAsync(site.TenantId, site.Id, bot.BotId, command.WidgetKey, command.SessionId, now, cancellationToken);

        await _messageRepository.InsertAsync(new EngageChatMessage
        {
            SessionId = session.Id,
            Role = "user",
            Content = command.Message,
            CreatedAtUtc = now
        }, cancellationToken);

        await _sessionRepository.TouchAsync(session.Id, now, cancellationToken);

        var retrieved = await _retrieveTopChunksHandler.HandleAsync(
            new RetrieveTopChunksQuery(site.TenantId, site.Id, command.Message, 3, bot.BotId),
            cancellationToken);

        var topScore = retrieved.Count == 0 ? 0 : retrieved.Max(item => item.Score);
        var confidence = ComputeConfidence(retrieved.Count > 0, topScore);

        var isLowConfidence = retrieved.Count == 0 || topScore < TopChunkScoreThreshold || confidence < LowConfidenceThreshold;
        if (isLowConfidence)
        {
            return await CreateFallbackResponseAsync(site, session, command.Message, now, "LowConfidence", cancellationToken);
        }

        var citations = retrieved
            .Select(item => new EngageCitationResult(item.SourceId, item.ChunkId, item.ChunkIndex))
            .ToArray();

        var completion = await _chatCompletionClient.CompleteAsync(BuildPrompt(command.Message, retrieved), cancellationToken);
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

    private static string BuildPrompt(string message, IReadOnlyCollection<RetrievedChunkResult> chunks)
    {
        var context = string.Join("\n", chunks
            .Take(3)
            .Select((item, index) => $"[{index + 1}] {item.Content.Trim()}"));

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

User question:
{message}
""";
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
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _sessionRepository.InsertAsync(created, cancellationToken);
        return created;
    }
}
