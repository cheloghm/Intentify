namespace Intentify.Modules.Engage.Application;

public sealed record WidgetBootstrapQuery(string WidgetKey);

public sealed record WidgetBootstrapResult(Guid SiteId, string Domain);

public sealed record ChatSendCommand(string WidgetKey, Guid? SessionId, string Message, string? CollectorSessionId, string? VisitorId = null);

public sealed record EngageCitationResult(Guid SourceId, Guid ChunkId, int ChunkIndex);

public sealed record ChatSendResult(
    Guid SessionId,
    string Response,
    decimal Confidence,
    bool TicketCreated,
    IReadOnlyCollection<EngageCitationResult> Sources,
    string? ResponseKind = null,
    string? PromoPublicKey = null,
    string? PromoTitle = null,
    string? PromoDescription = null,
    string? SecondaryResponse = null,
    AiDecisionContract? Stage7Decision = null,
    string? OpportunityLabel = null,
    int? IntentScore = null,
    string? ConversationSummary = null,
    string? SuggestedFollowUp = null,
    string? PreferredContactMethod = null,
    string? FollowUpEmailDraft = null,
    string? NextBestAction = null);

public sealed record ListConversationsQuery(Guid TenantId, Guid SiteId, string? CollectorSessionId);

public sealed record ConversationSummaryResult(Guid SessionId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, bool HasLead = false, bool HasTicket = false);

public sealed record OpportunityContactMethodBreakdownResult(int Email, int Phone, int Unknown);

public sealed record OpportunityDailyPointResult(DateTime DateUtc, int Count);

public sealed record OpportunityAnalyticsResult(
    int TotalCommercialOpportunities,
    int CommercialCount,
    int SupportCount,
    int GeneralCount,
    int HighIntentCount,
    OpportunityContactMethodBreakdownResult PreferredContactMethodDistribution,
    IReadOnlyCollection<OpportunityDailyPointResult> OpportunitiesOverTime);

public sealed record GetConversationMessagesQuery(Guid TenantId, Guid SiteId, Guid SessionId);

public sealed record GetWidgetConversationMessagesQuery(string WidgetKey, Guid SessionId);

public sealed record ConversationMessageResult(Guid MessageId, string Role, string Content, DateTime CreatedAtUtc, decimal? Confidence, IReadOnlyCollection<EngageCitationResult> Citations);

public sealed record GetEngageBotQuery(Guid TenantId, Guid SiteId);

public sealed record EngageBotResult(
    Guid BotId,
    string Name,
    string? PrimaryColor = null,
    bool? LauncherVisible = null,
    string? Tone = null,
    string? Verbosity = null,
    string? FallbackStyle = null,
    string? BusinessDescription = null,
    string? Industry = null,
    string? ServicesDescription = null,
    string? GeoFocus = null,
    string? PersonalityDescriptor = null,
    bool DigestEmailEnabled = false,
    string? DigestEmailRecipients = null,
    string? DigestEmailFrequency = null,
    string? WidgetPosition = null,
    string? GreetingMessage = null,
    string? LauncherIcon = null,
    string? AutoTriggerRulesJson = null);

public sealed record UpdateEngageBotCommand(
    Guid TenantId,
    Guid SiteId,
    string Name,
    string? PrimaryColor = null,
    bool? LauncherVisible = null,
    string? Tone = null,
    string? Verbosity = null,
    string? FallbackStyle = null,
    string? BusinessDescription = null,
    string? Industry = null,
    string? ServicesDescription = null,
    string? GeoFocus = null,
    string? PersonalityDescriptor = null,
    bool DigestEmailEnabled = false,
    string? DigestEmailRecipients = null,
    string? DigestEmailFrequency = null,
    string? WidgetPosition = null,
    string? GreetingMessage = null,
    string? LauncherIcon = null,
    string? AutoTriggerRulesJson = null);

public sealed record GenerateDigestQuery(Guid TenantId, Guid SiteId);

public sealed record ConversationCompletedNotification(
    Guid TenantId,
    Guid SiteId,
    Guid SessionId,
    DateTime CompletedAtUtc);

public interface IEngageConversationObserver
{
    Task OnConversationCompletedAsync(ConversationCompletedNotification notification, CancellationToken ct = default);
}

public sealed record DigestLeadEntry(string? Name, string? Email, string? OpportunityLabel, int? IntentScore);

public sealed record DigestTicketEntry(string Subject, string Status);

public sealed record EngageBotDigestInfo(
    Guid TenantId,
    Guid SiteId,
    string? Name,
    string? DisplayName,
    string? DigestEmailRecipients);

public sealed record DigestResult(
    Guid SiteId,
    DateTime GeneratedAtUtc,
    int NewLeadsCount,
    IReadOnlyCollection<DigestLeadEntry> NewLeads,
    int NewTicketsCount,
    IReadOnlyCollection<DigestTicketEntry> NewTickets,
    int ConversationsCount,
    DigestLeadEntry? TopOpportunity);
