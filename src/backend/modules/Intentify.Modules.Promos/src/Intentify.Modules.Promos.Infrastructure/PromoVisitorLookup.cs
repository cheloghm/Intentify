using Intentify.Modules.Promos.Application;
using Intentify.Modules.Visitors.Domain;
using MongoDB.Driver;

namespace Intentify.Modules.Promos.Infrastructure;

public sealed class PromoVisitorLookup : IPromoVisitorLookup
{
    private readonly IMongoCollection<Visitor> _visitors;

    public PromoVisitorLookup(IMongoDatabase database)
    {
        _visitors = database.GetCollection<Visitor>(VisitorsMongoCollections.Visitors);
    }

    public async Task<Guid?> ResolveVisitorIdAsync(Guid tenantId, Guid siteId, Guid? visitorId, string? firstPartyId, string? sessionId, CancellationToken cancellationToken = default)
    {
        if (visitorId is { } explicitId)
        {
            var matched = await _visitors.Find(item => item.Id == explicitId && item.TenantId == tenantId && item.SiteId == siteId)
                .Project(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (matched != Guid.Empty) return matched;
        }

        if (!string.IsNullOrWhiteSpace(firstPartyId))
        {
            var normalized = firstPartyId.Trim();
            var matched = await _visitors.Find(item => item.TenantId == tenantId && item.SiteId == siteId && item.FirstPartyId == normalized)
                .Project(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (matched != Guid.Empty) return matched;
        }

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var normalized = sessionId.Trim();
            var matched = await _visitors.Find(item => item.TenantId == tenantId && item.SiteId == siteId && item.Sessions.Any(s => s.SessionId == normalized))
                .Project(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (matched != Guid.Empty) return matched;
        }

        return null;
    }
}
