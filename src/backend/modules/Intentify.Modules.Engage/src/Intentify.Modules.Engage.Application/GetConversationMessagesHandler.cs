using Intentify.Shared.Validation;

namespace Intentify.Modules.Engage.Application;

public sealed class GetConversationMessagesHandler
{
    private readonly IEngageChatSessionRepository _sessionRepository;
    private readonly IEngageChatMessageRepository _messageRepository;

    public GetConversationMessagesHandler(IEngageChatSessionRepository sessionRepository, IEngageChatMessageRepository messageRepository)
    {
        _sessionRepository = sessionRepository;
        _messageRepository = messageRepository;
    }

    public async Task<OperationResult<IReadOnlyCollection<ConversationMessageResult>>> HandleAsync(GetConversationMessagesQuery query, CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(query.SessionId, cancellationToken);
        if (session is null || session.TenantId != query.TenantId)
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
