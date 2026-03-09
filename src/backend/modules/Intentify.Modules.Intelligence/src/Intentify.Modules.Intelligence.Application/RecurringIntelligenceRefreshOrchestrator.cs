using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Intentify.Modules.Intelligence.Application;

public interface IRecurringIntelligenceRefreshExecutor
{
    Task ExecuteAsync(string tenantId, RefreshIntelligenceRequest request, CancellationToken ct = default);
}

public sealed class RecurringIntelligenceRefreshExecutor(RefreshIntelligenceTrendsService service) : IRecurringIntelligenceRefreshExecutor
{
    public async Task ExecuteAsync(string tenantId, RefreshIntelligenceRequest request, CancellationToken ct = default)
    {
        await service.HandleAsync(tenantId, request, ct);
    }
}

public sealed class RecurringIntelligenceRefreshOrchestrator(
    IIntelligenceProfileRepository profileRepository,
    IIntelligenceTrendsRepository trendsRepository,
    IRecurringIntelligenceRefreshExecutor refreshExecutor,
    IOptions<RecurringIntelligenceRefreshOptions> options,
    TimeProvider timeProvider,
    ILogger<RecurringIntelligenceRefreshOrchestrator> logger)
{
    private const int MinProfileRefreshIntervalMinutes = 5;

    public bool IsEnabled => options.Value.Enabled;

    public TimeSpan PollInterval => TimeSpan.FromSeconds(Math.Clamp(options.Value.PollIntervalSeconds, 30, 3600));

    public async Task RunOnceAsync(CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        var maxProfilesPerCycle = Math.Clamp(options.Value.MaxProfilesPerCycle, 1, 500);
        var profiles = await profileRepository.ListActiveAsync(ct);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var refreshedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in profiles.Take(maxProfilesPerCycle))
        {
            ct.ThrowIfCancellationRequested();

            if (profile.RefreshIntervalMinutes is null || profile.RefreshIntervalMinutes < MinProfileRefreshIntervalMinutes)
            {
                continue;
            }

            var tenantId = profile.TenantId.ToString();
            var locations = profile.TargetLocations.Count == 0
                ? ["global"]
                : profile.TargetLocations.Where(static x => !string.IsNullOrWhiteSpace(x)).Select(static x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var location in locations)
            {
                var timeWindow = string.IsNullOrWhiteSpace(options.Value.TimeWindow) ? "7d" : options.Value.TimeWindow.Trim();
                var key = $"{tenantId}:{profile.SiteId}:{profile.IndustryCategory}:{location}:{timeWindow}";
                if (!refreshedKeys.Add(key))
                {
                    continue;
                }

                try
                {
                    var status = await trendsRepository.GetStatusAsync(tenantId, profile.SiteId, profile.IndustryCategory, location, timeWindow, ct);
                    var dueAtUtc = status?.RefreshedAtUtc.AddMinutes(profile.RefreshIntervalMinutes.Value);
                    if (dueAtUtc is not null && dueAtUtc.Value > now)
                    {
                        continue;
                    }

                    await refreshExecutor.ExecuteAsync(
                        tenantId,
                        new RefreshIntelligenceRequest(profile.SiteId, profile.IndustryCategory, location, timeWindow, Limit: 10),
                        ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(
                        ex,
                        "Recurring intelligence refresh failed for tenant {TenantId} site {SiteId} location {Location}.",
                        tenantId,
                        profile.SiteId,
                        location);
                }
            }
        }
    }
}
