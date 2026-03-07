using Intentify.Shared.Validation;

namespace Intentify.Modules.Intelligence.Application;

public sealed class QueryIntelligenceTrendsService(IIntelligenceTrendsRepository repository)
{
    private const int DashboardDefaultLimit = 10;
    private const int DashboardMaxLimit = 50;

    public async Task<OperationResult<IntelligenceTrendsResponse>> HandleAsync(
        string tenantId,
        Guid siteId,
        string category,
        string location,
        string timeWindow,
        CancellationToken ct = default)
    {
        var errors = Validate(tenantId, siteId, category, location, timeWindow, out var normalizedCategory, out var normalizedLocation, out var normalizedTimeWindow);
        if (errors.HasErrors)
        {
            return OperationResult<IntelligenceTrendsResponse>.ValidationFailed(errors);
        }

        var record = await repository.GetAsync(tenantId, siteId, normalizedCategory!, normalizedLocation!, normalizedTimeWindow!, ct);
        if (record is null)
        {
            return OperationResult<IntelligenceTrendsResponse>.NotFound();
        }

        return OperationResult<IntelligenceTrendsResponse>.Success(new IntelligenceTrendsResponse(
            record.Provider,
            record.Category,
            record.Location,
            record.TimeWindow,
            record.RefreshedAtUtc,
            record.Items.Select(item => new IntelligenceTrendItemResponse(item.QueryOrTopic, item.Score, item.Rank)).ToArray()));
    }

    public async Task<OperationResult<IntelligenceDashboardResponse>> HandleDashboardAsync(
        string tenantId,
        IntelligenceDashboardQuery query,
        CancellationToken ct = default)
    {
        var errors = ValidateDashboardQuery(
            tenantId,
            query,
            out var normalizedCategory,
            out var normalizedLocation,
            out var normalizedTimeWindow,
            out var normalizedProvider,
            out var normalizedKeyword,
            out var normalizedAudienceType,
            out var limit);

        if (errors.HasErrors)
        {
            return OperationResult<IntelligenceDashboardResponse>.ValidationFailed(errors);
        }

        var record = await repository.GetAsync(
            tenantId,
            query.SiteId,
            normalizedCategory!,
            normalizedLocation!,
            normalizedTimeWindow!,
            ct);

        if (record is null)
        {
            return OperationResult<IntelligenceDashboardResponse>.Success(CreateEmptyDashboardResponse(
                query.SiteId,
                normalizedCategory!,
                normalizedLocation!,
                normalizedTimeWindow!,
                normalizedAudienceType,
                normalizedProvider));
        }

        if (!string.IsNullOrWhiteSpace(normalizedProvider)
            && !record.Provider.Equals(normalizedProvider, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<IntelligenceDashboardResponse>.Success(CreateEmptyDashboardResponse(
                query.SiteId,
                record.Category,
                record.Location,
                record.TimeWindow,
                normalizedAudienceType,
                normalizedProvider,
                record.RefreshedAtUtc));
        }

        var filteredItems = record.Items.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            filteredItems = filteredItems.Where(item =>
                item.QueryOrTopic.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase));
        }

        var matchingItems = filteredItems.ToArray();
        var topItems = matchingItems
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Rank ?? int.MaxValue)
            .Take(limit)
            .Select(item => new IntelligenceDashboardTrendItemResponse(item.QueryOrTopic, item.Score, item.Rank, record.Provider))
            .ToArray();

        var averageScore = matchingItems.Length == 0 ? 0d : matchingItems.Average(item => item.Score);
        var maxScore = matchingItems.Length == 0 ? 0d : matchingItems.Max(item => item.Score);

        return OperationResult<IntelligenceDashboardResponse>.Success(new IntelligenceDashboardResponse(
            query.SiteId,
            record.Category,
            record.Location,
            record.TimeWindow,
            normalizedAudienceType,
            record.Provider,
            record.RefreshedAtUtc,
            matchingItems.Length,
            new IntelligenceDashboardSummaryResponse(matchingItems.Length, averageScore, maxScore),
            topItems));
    }

    private static ValidationErrors Validate(string tenantId, Guid siteId, string category, string location, string timeWindow, out string? normalizedCategory, out string? normalizedLocation, out string? normalizedTimeWindow)
    {
        var errors = new ValidationErrors();
        normalizedCategory = null;
        normalizedLocation = null;
        normalizedTimeWindow = null;

        if (string.IsNullOrWhiteSpace(tenantId) || !Guid.TryParse(tenantId, out _))
        {
            errors.Add("tenantId", "Tenant id is invalid.");
        }

        if (siteId == Guid.Empty)
        {
            errors.Add("siteId", "Site id is required.");
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            errors.Add("category", "Category is required.");
        }
        else
        {
            normalizedCategory = category.Trim();
        }

        if (string.IsNullOrWhiteSpace(location))
        {
            errors.Add("location", "Location is required.");
        }
        else
        {
            normalizedLocation = location.Trim();
        }

        if (string.IsNullOrWhiteSpace(timeWindow))
        {
            errors.Add("timeWindow", "Time window is required.");
        }
        else
        {
            normalizedTimeWindow = timeWindow.Trim();
        }

        return errors;
    }

    private static ValidationErrors ValidateDashboardQuery(
        string tenantId,
        IntelligenceDashboardQuery query,
        out string? normalizedCategory,
        out string? normalizedLocation,
        out string? normalizedTimeWindow,
        out string? normalizedProvider,
        out string? normalizedKeyword,
        out string? normalizedAudienceType,
        out int limit)
    {
        var errors = Validate(tenantId, query.SiteId, query.Category, query.Location, query.TimeWindow, out normalizedCategory, out normalizedLocation, out normalizedTimeWindow);
        normalizedProvider = null;
        normalizedKeyword = null;
        normalizedAudienceType = null;
        limit = query.Limit ?? DashboardDefaultLimit;

        if (!string.IsNullOrWhiteSpace(query.Provider))
        {
            normalizedProvider = query.Provider.Trim();
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            normalizedKeyword = query.Keyword.Trim();
        }

        if (!string.IsNullOrWhiteSpace(query.AudienceType))
        {
            var audienceType = query.AudienceType.Trim();
            if (!audienceType.Equals("B2B", StringComparison.OrdinalIgnoreCase)
                && !audienceType.Equals("B2C", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("audienceType", "Audience type must be either B2B or B2C.");
            }
            else
            {
                normalizedAudienceType = audienceType.ToUpperInvariant();
            }
        }

        if (limit <= 0 || limit > DashboardMaxLimit)
        {
            errors.Add("limit", $"Limit must be between 1 and {DashboardMaxLimit}.");
        }

        return errors;
    }

    private static IntelligenceDashboardResponse CreateEmptyDashboardResponse(
        Guid siteId,
        string category,
        string location,
        string timeWindow,
        string? audienceType,
        string? provider,
        DateTime? refreshedAtUtc = null)
    {
        return new IntelligenceDashboardResponse(
            siteId,
            category,
            location,
            timeWindow,
            audienceType,
            provider,
            refreshedAtUtc,
            0,
            new IntelligenceDashboardSummaryResponse(0, 0d, 0d),
            []);
    }
}
