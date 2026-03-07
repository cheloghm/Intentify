using Intentify.Modules.Leads.Application;
using Intentify.Modules.Promos.Application;

namespace Intentify.Modules.Promos.Infrastructure;

public sealed class PromoVisitorLookup : IPromoVisitorLookup
{
    private readonly ILeadVisitorLinker _leadVisitorLinker;

    public PromoVisitorLookup(ILeadVisitorLinker leadVisitorLinker)
    {
        _leadVisitorLinker = leadVisitorLinker;
    }

    public Task<Guid?> ResolveVisitorIdAsync(Guid tenantId, Guid siteId, Guid? visitorId, string? firstPartyId, string? sessionId, CancellationToken cancellationToken = default)
    {
        return _leadVisitorLinker.ResolveVisitorIdAsync(tenantId, siteId, visitorId, firstPartyId, sessionId, cancellationToken);
    }
}
