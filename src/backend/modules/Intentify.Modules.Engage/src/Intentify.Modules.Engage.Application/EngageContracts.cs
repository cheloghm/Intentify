namespace Intentify.Modules.Engage.Application;

public sealed record WidgetBootstrapQuery(string WidgetKey);

public sealed record WidgetBootstrapResult(Guid SiteId, string Domain);

public sealed record ChatSendCommand(string WidgetKey, Guid? SessionId, string Message);

public sealed record EngageCitationResult(Guid SourceId, Guid ChunkId, int ChunkIndex);

public sealed record ChatSendResult(Guid SessionId, string Response, decimal Confidence, bool TicketCreated, IReadOnlyCollection<EngageCitationResult> Sources);

public sealed record ListConversationsQuery(Guid TenantId, Guid SiteId);

public sealed record ConversationSummaryResult(Guid SessionId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public sealed record GetConversationMessagesQuery(Guid TenantId, Guid SiteId, Guid SessionId);

public sealed record ConversationMessageResult(Guid MessageId, string Role, string Content, DateTime CreatedAtUtc, decimal? Confidence, IReadOnlyCollection<EngageCitationResult> Citations);
