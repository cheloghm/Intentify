namespace Intentify.Modules.Engage.Api;

public sealed record WidgetBootstrapResponse(string SiteId, string Domain);

public sealed record EngageChatSendRequest(string WidgetKey, string? SessionId, string Message);

public sealed record EngageCitationResponse(string SourceId, string ChunkId, int ChunkIndex);

public sealed record EngageChatSendResponse(string SessionId, string Response, decimal Confidence, bool TicketCreated, IReadOnlyCollection<EngageCitationResponse> Sources);

public sealed record ConversationSummaryResponse(string SessionId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public sealed record ConversationMessageResponse(
    string MessageId,
    string Role,
    string Content,
    DateTime CreatedAtUtc,
    decimal? Confidence,
    IReadOnlyCollection<EngageCitationResponse> Citations);
