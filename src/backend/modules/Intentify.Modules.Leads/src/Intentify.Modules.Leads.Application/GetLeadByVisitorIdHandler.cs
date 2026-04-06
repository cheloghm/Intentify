namespace Intentify.Modules.Leads.Application;

// Append to Handlers.cs — or place in its own file

public sealed class GetLeadByVisitorIdHandler(ILeadRepository repository)
{
    public async Task<Intentify.Modules.Leads.Domain.Lead?> HandleAsync(
        GetLeadByVisitorIdQuery query,
        CancellationToken cancellationToken = default)
    {
        return await repository.GetByLinkedVisitorIdAsync(
            query.TenantId, query.SiteId, query.VisitorId, cancellationToken);
    }
}
