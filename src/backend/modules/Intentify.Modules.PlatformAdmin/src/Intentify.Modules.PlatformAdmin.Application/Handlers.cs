namespace Intentify.Modules.PlatformAdmin.Application;

public sealed class GetPlatformSummaryHandler(IPlatformAdminReadRepository repository)
{
    public Task<PlatformSummaryResult> HandleAsync(CancellationToken cancellationToken = default)
        => repository.GetPlatformSummaryAsync(cancellationToken);
}

public sealed class ListPlatformTenantsHandler(IPlatformAdminReadRepository repository)
{
    public Task<PlatformTenantListResult> HandleAsync(ListPlatformTenantsQuery query, CancellationToken cancellationToken = default)
        => repository.ListTenantsAsync(query, cancellationToken);
}

public sealed class GetPlatformTenantDetailHandler(IPlatformAdminReadRepository repository)
{
    public Task<PlatformTenantDetailResult?> HandleAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => repository.GetTenantDetailAsync(tenantId, cancellationToken);
}

public sealed class GetPlatformOperationalSummaryHandler(IPlatformAdminReadRepository repository)
{
    public Task<PlatformOperationalSummaryResult> HandleAsync(CancellationToken cancellationToken = default)
        => repository.GetOperationalSummaryAsync(cancellationToken);
}

public sealed class GetPlatformDashboardHandler(IPlatformAdminReadRepository repository)
{
    public Task<PlatformDashboardResult> HandleAsync(CancellationToken cancellationToken = default)
        => repository.GetPlatformDashboardAsync(cancellationToken);
}
