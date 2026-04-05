using Intentify.Shared.Validation;
using Intentify.Modules.Intelligence.Domain;

namespace Intentify.Modules.Intelligence.Application;

public sealed class QueryIntelligenceTrendsService(
    IIntelligenceTrendsRepository repository,
    IIntelligenceProfileRepository profileRepository)
{
    private const int    DashboardDefaultLimit = 25;
    private const int    DashboardMaxLimit     = 50;
    private const string DefaultCategory       = "general";
    private const string DefaultLocation       = "GB";
    private const string DefaultTimeWindow     = "7d";

    // ── Simple trends list ────────────────────────────────────────────────────

    public async Task<OperationResult<IntelligenceTrendsResponse>> HandleAsync(
        string tenantId, Guid siteId,
        string category, string location, string timeWindow,
        CancellationToken ct = default)
    {
        var errors = Validate(tenantId, siteId, category, location, timeWindow,
            out var nc, out var nl, out var ntw);
        if (errors.HasErrors)
            return OperationResult<IntelligenceTrendsResponse>.ValidationFailed(errors);

        var record = await repository.GetAsync(tenantId, siteId, nc!, nl!, ntw!, ct);
        if (record is null)
            return OperationResult<IntelligenceTrendsResponse>.NotFound();

        return OperationResult<IntelligenceTrendsResponse>.Success(new IntelligenceTrendsResponse(
            record.Provider, record.Category, record.Location, record.TimeWindow, record.RefreshedAtUtc,
            record.Items.Select(i => new IntelligenceTrendItemResponse(i.QueryOrTopic, i.Score, i.Rank, i.IsRising)).ToArray()));
    }

    // ── Full dashboard ────────────────────────────────────────────────────────

    public async Task<OperationResult<IntelligenceDashboardResponse>> HandleDashboardAsync(
        string tenantId,
        IntelligenceDashboardQuery query,
        CancellationToken ct = default)
    {
        var profile = await profileRepository.GetAsync(tenantId, query.SiteId, ct);

        var errors = ValidateDashboard(tenantId, query, profile,
            out var normCategory, out var normLocation, out var normTimeWindow,
            out var normProvider, out var normKeyword, out var normAudienceType,
            out var normAgeRange, out var normSearchType, out var normSubRegion, out var limit);

        if (errors.HasErrors)
            return OperationResult<IntelligenceDashboardResponse>.ValidationFailed(errors);

        var record = await repository.GetAsync(tenantId, query.SiteId, normCategory!, normLocation!, normTimeWindow!, ct);

        if (record is null)
            return OperationResult<IntelligenceDashboardResponse>.Success(
                Empty(query.SiteId, normCategory!, normLocation!, normTimeWindow!,
                    normAudienceType, normProvider, normAgeRange, normSearchType, normSubRegion));

        if (!string.IsNullOrWhiteSpace(normProvider)
            && !record.Provider.Equals(normProvider, StringComparison.OrdinalIgnoreCase))
            return OperationResult<IntelligenceDashboardResponse>.Success(
                Empty(query.SiteId, record.Category, record.Location, record.TimeWindow,
                    normAudienceType, normProvider, normAgeRange, normSearchType, normSubRegion, record.RefreshedAtUtc));

        // Filter by keyword
        var filtered = record.Items.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(normKeyword))
            filtered = filtered.Where(i => i.QueryOrTopic.Contains(normKeyword, StringComparison.OrdinalIgnoreCase));

        var matching = filtered.ToArray();
        var topItems = matching
            .OrderByDescending(i => i.Score).ThenBy(i => i.Rank ?? int.MaxValue).Take(limit)
            .Select(i => new IntelligenceDashboardTrendItemResponse(i.QueryOrTopic, i.Score, i.Rank, record.Provider, i.IsRising, record.Category))
            .ToArray();

        var relatedItems = record.RelatedQueries
            .OrderByDescending(i => i.Score).Take(15)
            .Select(i => new IntelligenceDashboardTrendItemResponse(i.QueryOrTopic, i.Score, i.Rank, record.Provider, false, record.Category))
            .ToArray();

        var risingItems = record.RisingQueries
            .OrderByDescending(i => i.Score).Take(15)
            .Select(i => new IntelligenceDashboardTrendItemResponse(i.QueryOrTopic, i.Score, i.Rank, record.Provider, true, record.Category))
            .ToArray();

        var avg    = matching.Length == 0 ? 0d : matching.Average(i => i.Score);
        var max    = matching.Length == 0 ? 0d : matching.Max(i => i.Score);
        var ranked = matching.Count(i => i.Rank is not null);
        var top    = topItems.FirstOrDefault()?.QueryOrTopic;

        return OperationResult<IntelligenceDashboardResponse>.Success(new IntelligenceDashboardResponse(
            query.SiteId, record.Category, record.Location, record.TimeWindow,
            normAudienceType, record.Provider, normAgeRange, normSearchType, normSubRegion,
            record.RefreshedAtUtc, matching.Length,
            new IntelligenceDashboardSummaryResponse(matching.Length, avg, max, ranked, top),
            topItems, relatedItems, risingItems));
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private static ValidationErrors Validate(
        string tenantId, Guid siteId,
        string category, string location, string timeWindow,
        out string? nc, out string? nl, out string? ntw)
    {
        var errors = new ValidationErrors();
        nc = nl = ntw = null;

        if (string.IsNullOrWhiteSpace(tenantId) || !Guid.TryParse(tenantId, out _))
            errors.Add("tenantId", "Tenant id is invalid.");
        if (siteId == Guid.Empty) errors.Add("siteId", "Site id is required.");
        if (string.IsNullOrWhiteSpace(category))   errors.Add("category",   "Category is required.");
        else nc = category.Trim();
        if (string.IsNullOrWhiteSpace(location))   errors.Add("location",   "Location is required.");
        else nl = location.Trim();
        if (string.IsNullOrWhiteSpace(timeWindow)) errors.Add("timeWindow", "Time window is required.");
        else ntw = timeWindow.Trim();
        return errors;
    }

    private static ValidationErrors ValidateDashboard(
        string tenantId,
        IntelligenceDashboardQuery query,
        IntelligenceProfile? profile,
        out string? normCategory,
        out string? normLocation,
        out string? normTimeWindow,
        out string? normProvider,
        out string? normKeyword,
        out string? normAudienceType,
        out string? normAgeRange,
        out string? normSearchType,
        out string? normSubRegion,
        out int limit)
    {
        var errors = new ValidationErrors();

        normCategory   = !string.IsNullOrWhiteSpace(query.Category)   ? query.Category.Trim()
            : !string.IsNullOrWhiteSpace(profile?.IndustryCategory)   ? profile.IndustryCategory.Trim()
            : DefaultCategory;

        normLocation   = !string.IsNullOrWhiteSpace(query.Location)   ? query.Location.Trim()
            : profile?.TargetLocations.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim()
            ?? DefaultLocation;

        normTimeWindow = !string.IsNullOrWhiteSpace(query.TimeWindow) ? query.TimeWindow.Trim() : DefaultTimeWindow;

        if (string.IsNullOrWhiteSpace(tenantId) || !Guid.TryParse(tenantId, out _))
            errors.Add("tenantId", "Tenant id is invalid.");
        if (query.SiteId == Guid.Empty)
            errors.Add("siteId", "Site id is required.");

        normProvider    = query.Provider?.Trim();
        normKeyword     = query.Keyword?.Trim();
        normAgeRange    = query.AgeRange?.Trim();
        normSearchType  = query.SearchType?.Trim().ToLowerInvariant();
        normSubRegion   = query.SubRegion?.Trim();
        normAudienceType = null;
        limit = query.Limit ?? DashboardDefaultLimit;

        var audienceSrc = !string.IsNullOrWhiteSpace(query.AudienceType)
            ? query.AudienceType : profile?.PrimaryAudienceType;
        if (!string.IsNullOrWhiteSpace(audienceSrc))
        {
            var a = audienceSrc.Trim();
            if (!a.Equals("B2B", StringComparison.OrdinalIgnoreCase)
                && !a.Equals("B2C", StringComparison.OrdinalIgnoreCase))
                errors.Add("audienceType", "Audience type must be B2B or B2C.");
            else
                normAudienceType = a.ToUpperInvariant();
        }

        if (limit <= 0 || limit > DashboardMaxLimit)
            errors.Add("limit", $"Limit must be between 1 and {DashboardMaxLimit}.");

        return errors;
    }

    private static IntelligenceDashboardResponse Empty(
        Guid siteId, string category, string location, string timeWindow,
        string? audienceType, string? provider, string? ageRange,
        string? searchType, string? subRegion, DateTime? refreshedAt = null) =>
        new(siteId, category, location, timeWindow, audienceType, provider,
            ageRange, searchType, subRegion, refreshedAt, 0,
            new IntelligenceDashboardSummaryResponse(0, 0d, 0d, 0, null),
            [], [], []);
}
