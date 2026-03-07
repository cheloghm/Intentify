namespace Intentify.Modules.Engage.Api;

public sealed record WidgetBootstrapResponse(string SiteId, string Domain, string DisplayName, string BotName);

public sealed record EngageChatSendRequest(string WidgetKey, string? SessionId, string Message, string? CollectorSessionId = null);

public sealed record EngageCitationResponse(string SourceId, string ChunkId, int ChunkIndex);

public sealed record EngageChatSendResponse(
    string SessionId,
    string Response,
    decimal Confidence,
    bool TicketCreated,
    IReadOnlyCollection<EngageCitationResponse> Sources,
    string? ResponseKind = null,
    string? PromoPublicKey = null,
    string? PromoTitle = null,
    string? PromoDescription = null);

public sealed record ConversationSummaryResponse(string SessionId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public sealed record ConversationMessageResponse(
    string MessageId,
    string Role,
    string Content,
    DateTime CreatedAtUtc,
    decimal? Confidence,
    IReadOnlyCollection<EngageCitationResponse> Citations);


public sealed record EngageBotResponse(string SiteId, string Name);

public sealed record UpdateEngageBotRequest(string Name);
