using Intentify.Modules.Leads.Application;
using Intentify.Modules.Visitors.Domain;
using MongoDB.Driver;

namespace Intentify.Modules.Leads.Infrastructure;

public sealed class LeadVisitorLinker : ILeadVisitorLinker
{
    private readonly IMongoCollection<Visitor> _visitors;

    public LeadVisitorLinker(IMongoDatabase database)
    {
        _visitors = database.GetCollection<Visitor>(VisitorsMongoCollections.Visitors);
    }

    public async Task<Guid?> ResolveVisitorIdAsync(Guid tenantId, Guid siteId, Guid? visitorId, string? firstPartyId, string? sessionId, CancellationToken cancellationToken = default)
    {
        if (visitorId is { } explicitId)
        {
            var found = await _visitors.Find(item => item.Id == explicitId && item.TenantId == tenantId && item.SiteId == siteId)
                .Project(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (found != Guid.Empty) return found;
        }

        if (!string.IsNullOrWhiteSpace(firstPartyId))
        {
            var fp = firstPartyId.Trim();
            var found = await _visitors.Find(item => item.TenantId == tenantId && item.SiteId == siteId && item.FirstPartyId == fp)
                .Project(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (found != Guid.Empty) return found;
        }

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var session = sessionId.Trim();
            var found = await _visitors.Find(item => item.TenantId == tenantId && item.SiteId == siteId && item.Sessions.Any(s => s.SessionId == session))
                .Project(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (found != Guid.Empty) return found;
        }

        return null;
    }

    public async Task EnrichVisitorIfPermittedAsync(Guid tenantId, Guid siteId, Guid? visitorId, bool consentGiven, string? email, string? displayName, string? phone, CancellationToken cancellationToken = default)
    {
        if (!consentGiven || visitorId is null)
        {
            return;
        }

        var visitor = await _visitors.Find(item => item.Id == visitorId.Value && item.TenantId == tenantId && item.SiteId == siteId)
            .FirstOrDefaultAsync(cancellationToken);

        if (visitor is null)
        {
            return;
        }

        var changed = false;

        if (visitor.PrimaryEmail is null && !string.IsNullOrWhiteSpace(email))
        {
            visitor.PrimaryEmail = email;
            changed = true;
        }

        if (visitor.DisplayName is null && !string.IsNullOrWhiteSpace(displayName))
        {
            visitor.DisplayName = displayName;
            changed = true;
        }

        if (visitor.Phone is null && !string.IsNullOrWhiteSpace(phone))
        {
            visitor.Phone = phone;
            changed = true;
        }

        if (changed)
        {
            visitor.LastIdentifiedAtUtc = DateTime.UtcNow;
            await _visitors.ReplaceOneAsync(item => item.Id == visitor.Id && item.TenantId == tenantId && item.SiteId == siteId, visitor, cancellationToken: cancellationToken);
        }
    }
}
