using Intentify.Modules.Promos.Domain;

namespace Intentify.Modules.Promos.Application;

public sealed record CreatePromoCommand(
    Guid TenantId,
    Guid SiteId,
    string Name,
    string? Description,
    bool IsActive,
    string? FlyerFileName,
    string? FlyerContentType,
    byte[]? FlyerBytes,
    IReadOnlyCollection<PromoQuestion>? Questions);

public sealed record ListPromosQuery(Guid TenantId, Guid? SiteId);
public sealed record ListPromoEntriesQuery(Guid TenantId, Guid PromoId, int Page, int PageSize);
public sealed record GetPromoDetailQuery(Guid TenantId, Guid PromoId, int EntryPage, int EntryPageSize);

public sealed record PromoDetailResult(Promo Promo, IReadOnlyCollection<PromoEntry> Entries);

public sealed record CreatePublicPromoEntryCommand(
    string PromoKey,
    string? VisitorId,
    string? FirstPartyId,
    string? SessionId,
    string? Email,
    string? Name,
    bool ConsentGiven,
    string ConsentStatement,
    IReadOnlyDictionary<string, string>? Answers);

public interface IPromoRepository
{
    Task InsertAsync(Promo promo, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Promo>> ListAsync(ListPromosQuery query, CancellationToken cancellationToken = default);
    Task<Promo?> GetActiveByPublicKeyAsync(string publicKey, CancellationToken cancellationToken = default);
    Task<Promo?> GetByIdAsync(Guid tenantId, Guid promoId, CancellationToken cancellationToken = default);
}

public interface IPromoEntryRepository
{
    Task InsertAsync(PromoEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PromoEntry>> ListByPromoAsync(ListPromoEntriesQuery query, CancellationToken cancellationToken = default);
}

public interface IPromoConsentLogRepository
{
    Task InsertAsync(PromoConsentLog consentLog, CancellationToken cancellationToken = default);
}

public interface IPromoVisitorLookup
{
    Task<Guid?> ResolveVisitorIdAsync(Guid tenantId, Guid siteId, Guid? visitorId, string? firstPartyId, string? sessionId, CancellationToken cancellationToken = default);
}
