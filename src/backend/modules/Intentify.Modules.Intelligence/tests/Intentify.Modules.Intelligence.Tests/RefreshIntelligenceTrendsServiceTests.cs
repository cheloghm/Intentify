using Intentify.Modules.Intelligence.Application;
using Intentify.Modules.Intelligence.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Intelligence.Tests;

public sealed class RefreshIntelligenceTrendsServiceTests
{
    [Fact]
    public async Task HandleAsync_AfterSuccessfulUpsert_DispatchesObservers()
    {
        var provider = new FakeProvider(OperationResult<ExternalSearchResult>.Success(
            new ExternalSearchResult(
                [new ExternalSearchItem("lead generation", 99, 1)],
                "Google",
                new DateTime(2026, 01, 01, 12, 0, 0, DateTimeKind.Utc))));

        var repository = new FakeTrendsRepository();
        var observer = new RecordingObserver();
        var service = new RefreshIntelligenceTrendsService(provider, repository, [observer]);
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var result = await service.HandleAsync(
            tenantId.ToString(),
            new RefreshIntelligenceRequest(siteId, "Marketing", "US", "7d", 10));

        Assert.True(result.IsSuccess);
        Assert.NotNull(repository.LastUpserted);
        var notification = Assert.Single(observer.Notifications);
        Assert.Equal(tenantId.ToString(), notification.TenantId);
        Assert.Equal(siteId, notification.SiteId);
        Assert.Equal("Marketing", notification.Category);
        Assert.Equal("US", notification.Location);
        Assert.Equal("7d", notification.TimeWindow);
    }

    [Fact]
    public async Task HandleAsync_WhenProviderFails_DoesNotDispatchObservers()
    {
        var provider = new FakeProvider(OperationResult<ExternalSearchResult>.Error());
        var repository = new FakeTrendsRepository();
        var observer = new RecordingObserver();
        var service = new RefreshIntelligenceTrendsService(provider, repository, [observer]);

        var result = await service.HandleAsync(
            Guid.NewGuid().ToString(),
            new RefreshIntelligenceRequest(Guid.NewGuid(), "Marketing", "US", "7d", 10));

        Assert.False(result.IsSuccess);
        Assert.Null(repository.LastUpserted);
        Assert.Empty(observer.Notifications);
    }

    private sealed class FakeProvider(OperationResult<ExternalSearchResult> result) : IExternalSearchProvider
    {
        public Task<OperationResult<ExternalSearchResult>> SearchAsync(string tenantId, Guid siteId, ExternalSearchQuery query, CancellationToken ct)
            => Task.FromResult(result);
    }

    private sealed class FakeTrendsRepository : IIntelligenceTrendsRepository
    {
        public IntelligenceTrendRecord? LastUpserted { get; private set; }

        public Task UpsertAsync(IntelligenceTrendRecord record, CancellationToken ct = default)
        {
            LastUpserted = record;
            return Task.CompletedTask;
        }

        public Task<IntelligenceTrendRecord?> GetAsync(string tenantId, Guid siteId, string category, string location, string timeWindow, CancellationToken ct = default)
            => Task.FromResult<IntelligenceTrendRecord?>(null);

        public Task<IntelligenceStatusResponse?> GetStatusAsync(string tenantId, Guid siteId, string category, string location, string timeWindow, CancellationToken ct = default)
            => Task.FromResult<IntelligenceStatusResponse?>(null);
    }

    private sealed class RecordingObserver : IIntelligenceObserver
    {
        public List<IntelligenceTrendsUpdatedNotification> Notifications { get; } = [];

        public Task OnTrendsUpdated(IntelligenceTrendsUpdatedNotification notification, CancellationToken ct)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }
    }
}
