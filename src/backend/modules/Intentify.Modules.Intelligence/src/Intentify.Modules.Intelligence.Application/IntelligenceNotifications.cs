namespace Intentify.Modules.Intelligence.Application;

public sealed record IntelligenceTrendsUpdatedNotification(
    string TenantId,
    Guid SiteId,
    string Category,
    string Location,
    string TimeWindow,
    DateTime RefreshedAtUtc);

public interface IIntelligenceObserver
{
    Task OnTrendsUpdated(IntelligenceTrendsUpdatedNotification notification, CancellationToken ct);
}
