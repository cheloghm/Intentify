using Intentify.Shared.Validation;

namespace Intentify.Modules.Intelligence.Application;

public sealed class QueryIntelligenceTrendsService(IIntelligenceTrendsRepository repository)
{
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
}
