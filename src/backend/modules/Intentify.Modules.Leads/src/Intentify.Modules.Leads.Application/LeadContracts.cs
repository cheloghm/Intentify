using Intentify.Modules.Leads.Domain;

namespace Intentify.Modules.Leads.Application;

public sealed record UpsertLeadFromPromoEntryCommand(
    Guid TenantId,
    Guid SiteId,
    Guid? VisitorId,
    string? FirstPartyId,
    string? SessionId,
    string? Email,
    string? Name,
    bool ConsentGiven,
    string? Phone = null,
    string? PreferredContactMethod = null,
    string? PreferredContactDetail = null,
    string? OpportunityLabel = null,
    int? IntentScore = null,
    string? ConversationSummary = null,
    string? SuggestedFollowUp = null);

public sealed record ListLeadsQuery(Guid TenantId, Guid? SiteId, int Page, int PageSize);
public sealed record GetLeadQuery(Guid TenantId, Guid LeadId);
public sealed record GetLeadByVisitorIdQuery(Guid TenantId, Guid SiteId, Guid VisitorId);

public interface ILeadRepository
{
    Task<Lead?> GetByEmailAsync(Guid tenantId, Guid siteId, string email, CancellationToken cancellationToken = default);
    Task<Lead?> GetByFirstPartyIdAsync(Guid tenantId, Guid siteId, string firstPartyId, CancellationToken cancellationToken = default);
    Task<Lead?> GetByIdAsync(Guid tenantId, Guid leadId, CancellationToken cancellationToken = default);
    Task<Lead?> GetByLinkedVisitorIdAsync(Guid tenantId, Guid siteId, Guid visitorId, CancellationToken cancellationToken = default);
    Task InsertAsync(Lead lead, CancellationToken cancellationToken = default);
    Task ReplaceAsync(Lead lead, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Lead>> ListAsync(ListLeadsQuery query, CancellationToken cancellationToken = default);
}

public sealed record LeadCapturedNotification(
    Guid TenantId,
    Guid SiteId,
    Guid LeadId,
    string? Email,
    string? Name,
    DateTime OccurredAtUtc,
    bool IsNew);

public interface ILeadEventObserver
{
    Task OnLeadCapturedAsync(LeadCapturedNotification notification, CancellationToken ct = default);
}

public interface ILeadVisitorLinker
{
    Task<Guid?> ResolveVisitorIdAsync(Guid tenantId, Guid siteId, Guid? visitorId, string? firstPartyId, string? sessionId, CancellationToken cancellationToken = default);
    Task EnrichVisitorIfPermittedAsync(Guid tenantId, Guid siteId, Guid? visitorId, bool consentGiven, string? email, string? displayName, string? phone, CancellationToken cancellationToken = default);
}
