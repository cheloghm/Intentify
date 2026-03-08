using Intentify.Modules.Intelligence.Application;
using Intentify.Modules.Intelligence.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Intentify.Modules.Intelligence.Tests;

public sealed class RecurringIntelligenceRefreshOrchestratorTests
{
    [Fact]
    public async Task RunOnceAsync_WhenDisabled_IsNoOp()
    {
        var profileRepository = new FakeProfileRepository([
            CreateProfile(Guid.NewGuid(), Guid.NewGuid(), 60, ["US"])
        ]);
        var trendsRepository = new FakeTrendsRepository();
        var executor = new FakeExecutor();
        var orchestrator = CreateOrchestrator(profileRepository, trendsRepository, executor, new RecurringIntelligenceRefreshOptions
        {
            Enabled = false
        });

        await orchestrator.RunOnceAsync();

        Assert.Empty(executor.Invocations);
    }

    [Fact]
    public async Task RunOnceAsync_RefreshesOnlyDueProfiles()
    {
        var now = new DateTime(2026, 01, 01, 12, 0, 0, DateTimeKind.Utc);
        var dueProfile = CreateProfile(Guid.NewGuid(), Guid.NewGuid(), 60, ["US"]);
        var notDueProfile = CreateProfile(Guid.NewGuid(), Guid.NewGuid(), 60, ["US"]);

        var profileRepository = new FakeProfileRepository([dueProfile, notDueProfile]);
        var trendsRepository = new FakeTrendsRepository
        {
            StatusMap =
            {
                [Key(notDueProfile, "US")] = new IntelligenceStatusResponse("Google", notDueProfile.IndustryCategory, "US", "7d", now.AddMinutes(-5), 10),
                [Key(dueProfile, "US")] = new IntelligenceStatusResponse("Google", dueProfile.IndustryCategory, "US", "7d", now.AddHours(-2), 10)
            }
        };

        var executor = new FakeExecutor();
        var orchestrator = CreateOrchestrator(profileRepository, trendsRepository, executor, new RecurringIntelligenceRefreshOptions
        {
            Enabled = true,
            TimeWindow = "7d"
        }, now);

        await orchestrator.RunOnceAsync();

        Assert.Single(executor.Invocations);
        Assert.Equal(dueProfile.SiteId, executor.Invocations[0].Request.SiteId);
    }

    [Fact]
    public async Task RunOnceAsync_UsesRefreshExecutorPath()
    {
        var profile = CreateProfile(Guid.NewGuid(), Guid.NewGuid(), 60, ["US"]);
        var executor = new FakeExecutor();
        var orchestrator = CreateOrchestrator(
            new FakeProfileRepository([profile]),
            new FakeTrendsRepository(),
            executor,
            new RecurringIntelligenceRefreshOptions { Enabled = true });

        await orchestrator.RunOnceAsync();

        Assert.Single(executor.Invocations);
    }

    [Fact]
    public async Task RunOnceAsync_InvokesRefreshWithProfileTenantAndSiteScope()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var profile = CreateProfile(tenantId, siteId, 60, ["US"]);
        var executor = new FakeExecutor();

        var orchestrator = CreateOrchestrator(
            new FakeProfileRepository([profile]),
            new FakeTrendsRepository(),
            executor,
            new RecurringIntelligenceRefreshOptions { Enabled = true });

        await orchestrator.RunOnceAsync();

        var invocation = Assert.Single(executor.Invocations);
        Assert.Equal(tenantId.ToString(), invocation.TenantId);
        Assert.Equal(siteId, invocation.Request.SiteId);
    }

    [Fact]
    public async Task RunOnceAsync_WhenProviderFails_ContinuesProcessing()
    {
        var failed = CreateProfile(Guid.NewGuid(), Guid.NewGuid(), 60, ["US"]);
        var success = CreateProfile(Guid.NewGuid(), Guid.NewGuid(), 60, ["CA"]);
        var executor = new FakeExecutor
        {
            FailSiteId = failed.SiteId
        };

        var orchestrator = CreateOrchestrator(
            new FakeProfileRepository([failed, success]),
            new FakeTrendsRepository(),
            executor,
            new RecurringIntelligenceRefreshOptions { Enabled = true });

        var exception = await Record.ExceptionAsync(() => orchestrator.RunOnceAsync());

        Assert.Null(exception);
        Assert.Equal(2, executor.Invocations.Count);
    }

    private static RecurringIntelligenceRefreshOrchestrator CreateOrchestrator(
        IIntelligenceProfileRepository profileRepository,
        IIntelligenceTrendsRepository trendsRepository,
        FakeExecutor executor,
        RecurringIntelligenceRefreshOptions options,
        DateTime? now = null)
        => new(
            profileRepository,
            trendsRepository,
            executor,
            Options.Create(options),
            new FakeTimeProvider(now ?? new DateTime(2026, 01, 01, 12, 0, 0, DateTimeKind.Utc)),
            NullLogger<RecurringIntelligenceRefreshOrchestrator>.Instance);

    private static IntelligenceProfile CreateProfile(Guid tenantId, Guid siteId, int intervalMinutes, IReadOnlyCollection<string> locations)
        => new()
        {
            TenantId = tenantId,
            SiteId = siteId,
            IndustryCategory = "Retail",
            ProfileName = "Profile",
            PrimaryAudienceType = "B2C",
            TargetLocations = locations,
            PrimaryProductsOrServices = ["Service"],
            IsActive = true,
            RefreshIntervalMinutes = intervalMinutes,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

    private static string Key(IntelligenceProfile profile, string location)
        => $"{profile.TenantId}:{profile.SiteId}:{profile.IndustryCategory}:{location}:7d";

    private sealed class FakeProfileRepository(IReadOnlyList<IntelligenceProfile> profiles) : IIntelligenceProfileRepository
    {
        public Task UpsertAsync(IntelligenceProfile profile, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IntelligenceProfile?> GetAsync(string tenantId, Guid siteId, CancellationToken ct = default) => Task.FromResult<IntelligenceProfile?>(null);
        public Task<IReadOnlyList<IntelligenceProfile>> ListActiveAsync(CancellationToken ct = default) => Task.FromResult(profiles);
    }

    private sealed class FakeTrendsRepository : IIntelligenceTrendsRepository
    {
        public Dictionary<string, IntelligenceStatusResponse> StatusMap { get; } = [];

        public Task UpsertAsync(IntelligenceTrendRecord record, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IntelligenceTrendRecord?> GetAsync(string tenantId, Guid siteId, string category, string location, string timeWindow, CancellationToken ct = default)
            => Task.FromResult<IntelligenceTrendRecord?>(null);

        public Task<IntelligenceStatusResponse?> GetStatusAsync(string tenantId, Guid siteId, string category, string location, string timeWindow, CancellationToken ct = default)
        {
            StatusMap.TryGetValue($"{tenantId}:{siteId}:{category}:{location}:{timeWindow}", out var status);
            return Task.FromResult<IntelligenceStatusResponse?>(status);
        }
    }

    private sealed class FakeExecutor : IRecurringIntelligenceRefreshExecutor
    {
        public Guid? FailSiteId { get; init; }

        public List<(string TenantId, RefreshIntelligenceRequest Request)> Invocations { get; } = [];

        public Task ExecuteAsync(string tenantId, RefreshIntelligenceRequest request, CancellationToken ct = default)
        {
            Invocations.Add((tenantId, request));
            if (FailSiteId == request.SiteId)
            {
                throw new InvalidOperationException("provider failed");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
