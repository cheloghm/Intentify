using Intentify.Modules.Intelligence.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Intelligence.Application;

public sealed class RefreshIntelligenceTrendsService(
    IExternalSearchProvider provider,
    IIntelligenceTrendsRepository repository,
    IEnumerable<IIntelligenceObserver> observers)
{
    private const int DefaultLimit = 25;
    private const int MaxLimit     = 50;

    public async Task<OperationResult<RefreshIntelligenceResult>> HandleAsync(
        string tenantId,
        RefreshIntelligenceRequest request,
        CancellationToken ct = default)
    {
        var errors = ValidateRequest(tenantId, request,
            out var normCategory, out var normLocation, out var normTimeWindow, out var limit);

        if (errors.HasErrors)
            return OperationResult<RefreshIntelligenceResult>.ValidationFailed(errors);

        // Build the external query — pass every filter the request carries
        var searchQuery = new ExternalSearchQuery(
            normCategory!,
            normLocation!,
            normTimeWindow!,
            limit,
            Keyword:         request.Keyword?.Trim(),
            AgeRange:        request.AgeRange?.Trim(),
            CategoryId:      request.CategoryId,
            SearchType:      request.SearchType?.Trim(),
            ComparisonTerms: request.ComparisonTerms,
            SubRegion:       request.SubRegion?.Trim());

        var providerResult = await provider.SearchAsync(tenantId, request.SiteId, searchQuery, ct);

        if (!providerResult.IsSuccess || providerResult.Value is null)
        {
            return providerResult.Status == OperationStatus.ValidationFailed && providerResult.Errors is not null
                ? OperationResult<RefreshIntelligenceResult>.ValidationFailed(providerResult.Errors)
                : OperationResult<RefreshIntelligenceResult>.Error();
        }

        if (!Guid.TryParse(tenantId, out var tenantGuid))
        {
            errors.Add("tenantId", "Tenant id is invalid.");
            return OperationResult<RefreshIntelligenceResult>.ValidationFailed(errors);
        }

        var resp = providerResult.Value;

        var record = new IntelligenceTrendRecord
        {
            TenantId       = tenantGuid,
            SiteId         = request.SiteId,
            Category       = normCategory!,
            Location       = normLocation!,
            TimeWindow     = normTimeWindow!,
            AgeRange       = request.AgeRange?.Trim(),
            Provider       = string.IsNullOrWhiteSpace(resp.Provider) ? "GoogleTrends" : resp.Provider,
            RefreshedAtUtc = resp.RetrievedAtUtc,
            Items = resp.Items
                .Select(i => new IntelligenceTrendItem(i.QueryOrTopic.Trim(), i.Score, i.Rank, i.IsRising))
                .ToArray(),
            RelatedQueries = (resp.RelatedQueries ?? [])
                .Select(i => new IntelligenceTrendItem(i.QueryOrTopic.Trim(), i.Score, i.Rank, false))
                .ToArray(),
            RisingQueries = (resp.RisingQueries ?? [])
                .Select(i => new IntelligenceTrendItem(i.QueryOrTopic.Trim(), i.Score, i.Rank, true))
                .ToArray(),
        };

        await repository.UpsertAsync(record, ct);

        var notification = new IntelligenceTrendsUpdatedNotification(
            tenantGuid.ToString(),
            record.SiteId,
            record.Category,
            record.Location,
            record.TimeWindow,
            record.RefreshedAtUtc);

        foreach (var observer in observers)
            await observer.OnTrendsUpdated(notification, ct);

        return OperationResult<RefreshIntelligenceResult>.Success(new RefreshIntelligenceResult(
            record.Provider,
            record.RefreshedAtUtc,
            record.Items.Count,
            record.RelatedQueries.Count,
            record.RisingQueries.Count));
    }

    private static ValidationErrors ValidateRequest(
        string tenantId,
        RefreshIntelligenceRequest request,
        out string? category,
        out string? location,
        out string? timeWindow,
        out int limit)
    {
        var errors = new ValidationErrors();
        category   = null;
        location   = null;
        timeWindow = null;
        limit = request.Limit ?? DefaultLimit;

        if (string.IsNullOrWhiteSpace(tenantId) || !Guid.TryParse(tenantId, out _))
            errors.Add("tenantId", "Tenant id is invalid.");

        if (request.SiteId == Guid.Empty)
            errors.Add("siteId", "Site id is required.");

        if (string.IsNullOrWhiteSpace(request.Category))
            errors.Add("category", "Category is required.");
        else
            category = request.Category.Trim();

        if (string.IsNullOrWhiteSpace(request.Location))
            errors.Add("location", "Location is required.");
        else
            location = request.Location.Trim();

        if (string.IsNullOrWhiteSpace(request.TimeWindow))
            errors.Add("timeWindow", "Time window is required.");
        else
            timeWindow = request.TimeWindow.Trim();

        if (limit <= 0 || limit > MaxLimit)
            errors.Add("limit", $"Limit must be between 1 and {MaxLimit}.");

        return errors;
    }
}
