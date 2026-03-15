using Intentify.Modules.Sites.Application;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Engage.Application;

public sealed class GetWidgetConversationMessagesHandler
{
    private readonly ISiteRepository _siteRepository;
    private readonly IEngageBotRepository _botRepository;
    private readonly IEngageChatSessionRepository _sessionRepository;
    private readonly IEngageChatMessageRepository _messageRepository;
    private readonly TimeSpan _sessionTimeout;

    public GetWidgetConversationMessagesHandler(
        ISiteRepository siteRepository,
        IEngageBotRepository botRepository,
        IEngageChatSessionRepository sessionRepository,
        IEngageChatMessageRepository messageRepository,
        int sessionTimeoutMinutes)
    {
        _siteRepository = siteRepository;
        _botRepository = botRepository;
        _sessionRepository = sessionRepository;
        _messageRepository = messageRepository;
        _sessionTimeout = TimeSpan.FromMinutes(sessionTimeoutMinutes > 0 ? sessionTimeoutMinutes : 30);
    }

    public async Task<OperationResult<IReadOnlyCollection<ConversationMessageResult>>> HandleAsync(GetWidgetConversationMessagesQuery query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.WidgetKey))
        {
            return OperationResult<IReadOnlyCollection<ConversationMessageResult>>.NotFound();
        }

        var site = await _siteRepository.GetByWidgetKeyAsync(query.WidgetKey.Trim(), cancellationToken);
        if (site is null)
        {
            return OperationResult<IReadOnlyCollection<ConversationMessageResult>>.NotFound();
        }

        var bot = await _botRepository.GetOrCreateForSiteAsync(site.TenantId, site.Id, cancellationToken);
        var session = await _sessionRepository.GetByIdAsync(query.SessionId, cancellationToken);
        if (session is null)
        {
            return OperationResult<IReadOnlyCollection<ConversationMessageResult>>.NotFound();
        }

        var active = DateTime.UtcNow - session.UpdatedAtUtc <= _sessionTimeout;
        if (!active
            || session.TenantId != site.TenantId
            || session.SiteId != site.Id
            || session.BotId != bot.BotId
            || !string.Equals(session.WidgetKey, query.WidgetKey.Trim(), StringComparison.Ordinal))
        {
            return OperationResult<IReadOnlyCollection<ConversationMessageResult>>.NotFound();
        }

        var messages = await _messageRepository.ListBySessionAsync(query.SessionId, cancellationToken);
        return OperationResult<IReadOnlyCollection<ConversationMessageResult>>.Success(messages
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => new ConversationMessageResult(
                item.Id,
                item.Role,
                item.Content,
                item.CreatedAtUtc,
                item.Confidence,
                item.Citations?.Select(c => new EngageCitationResult(c.SourceId, c.ChunkId, c.ChunkIndex)).ToArray() ?? []))
            .ToArray());
    }
}
