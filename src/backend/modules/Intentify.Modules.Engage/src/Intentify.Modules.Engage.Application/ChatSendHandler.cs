using Intentify.Modules.Engage.Domain;
using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Sites.Application;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Engage.Application;

public sealed class ChatSendHandler
{
    public const decimal LowConfidenceThreshold = 0.50m;
    public const int TopChunkScoreThreshold = 2;

    private readonly ISiteRepository _siteRepository;
    private readonly IEngageChatSessionRepository _sessionRepository;
    private readonly IEngageBotRepository _botRepository;
    private readonly IEngageChatMessageRepository _messageRepository;
    private readonly IEngageHandoffTicketRepository _ticketRepository;
    private readonly RetrieveTopChunksHandler _retrieveTopChunksHandler;

    public ChatSendHandler(
        ISiteRepository siteRepository,
        IEngageChatSessionRepository sessionRepository,
        IEngageBotRepository botRepository,
        IEngageChatMessageRepository messageRepository,
        IEngageHandoffTicketRepository ticketRepository,
        RetrieveTopChunksHandler retrieveTopChunksHandler)
    {
        _siteRepository = siteRepository;
        _sessionRepository = sessionRepository;
        _botRepository = botRepository;
        _messageRepository = messageRepository;
        _ticketRepository = ticketRepository;
        _retrieveTopChunksHandler = retrieveTopChunksHandler;
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

        var retrieved = await _retrieveTopChunksHandler.HandleAsync(
            new RetrieveTopChunksQuery(site.TenantId, site.Id, command.Message, 3, bot.BotId),
            cancellationToken);

        var topScore = retrieved.Count == 0 ? 0 : retrieved.Max(item => item.Score);
        var confidence = ComputeConfidence(retrieved.Count > 0, topScore);

        var isLowConfidence = retrieved.Count == 0 || topScore < TopChunkScoreThreshold || confidence < LowConfidenceThreshold;
        if (isLowConfidence)
        {
            var fallbackResponse = "Thanks — we’ll get back to you shortly.";
            await _ticketRepository.InsertAsync(new EngageHandoffTicket
            {
                TenantId = site.TenantId,
                SiteId = site.Id,
                SessionId = session.Id,
                UserMessage = command.Message,
                Reason = "LowConfidence",
                CreatedAtUtc = now
            }, cancellationToken);

            await _messageRepository.InsertAsync(new EngageChatMessage
            {
                SessionId = session.Id,
                Role = "assistant",
                Content = fallbackResponse,
                CreatedAtUtc = now,
                Confidence = confidence
            }, cancellationToken);

            await _sessionRepository.TouchAsync(session.Id, now, cancellationToken);

            return OperationResult<ChatSendResult>.Success(new ChatSendResult(session.Id, fallbackResponse, confidence, true, []));
        }

        var citations = retrieved
            .Select(item => new EngageCitationResult(item.SourceId, item.ChunkId, item.ChunkIndex))
            .ToArray();

        var response = BuildGroundedResponse(retrieved);
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

    private static string BuildGroundedResponse(IReadOnlyCollection<RetrievedChunkResult> chunks)
    {
        var excerpts = chunks
            .Take(2)
            .Select(item => item.Content.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item));

        return $"Based on your knowledge base: {string.Join(" ", excerpts)}";
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
                return existing;
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
