namespace Intentify.Modules.Visitors.Application;

// ── IDashboardAnalyticsReader lives here in Application so the handler
// does not need to reference the Infrastructure assembly. ──────────────────────

public interface IDashboardAnalyticsReader
{
    Task<DashboardAnalyticsResult> GetDashboardAsync(DashboardAnalyticsQuery query, CancellationToken ct = default);
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetDashboardAnalyticsHandler(IDashboardAnalyticsReader reader)
{
    public Task<DashboardAnalyticsResult> HandleAsync(DashboardAnalyticsQuery query, CancellationToken ct = default)
        => reader.GetDashboardAsync(query, ct);
}
