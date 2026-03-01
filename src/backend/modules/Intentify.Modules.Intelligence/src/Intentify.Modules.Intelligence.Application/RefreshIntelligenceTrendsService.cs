using Intentify.Modules.Intelligence.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Intelligence.Application;

public sealed class RefreshIntelligenceTrendsService(
    IExternalSearchProvider provider,
    IIntelligenceTrendsRepository repository,
    IEnumerable<IIntelligenceObserver> observers)
{
    private const int DefaultLimit = 10;
    private const int MaxLimit = 50;

    public async Task<OperationResult<RefreshIntelligenceResult>> HandleAsync(string tenantId, RefreshIntelligenceRequest request, CancellationToken ct = default)
    {
        var errors = ValidateRequest(tenantId, request, out var normalizedCategory, out var normalizedLocation, out var normalizedTimeWindow, out var limit);
        if (errors.HasErrors)
        {
            return OperationResult<RefreshIntelligenceResult>.ValidationFailed(errors);
        }

        var providerResult = await provider.SearchAsync(
            tenantId,
            request.SiteId,
            new ExternalSearchQuery(normalizedCategory!, normalizedLocation!, normalizedTimeWindow!, limit),
            ct);

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

        var response = providerResult.Value;
        var record = new IntelligenceTrendRecord
        {
            TenantId = tenantGuid,
            SiteId = request.SiteId,
            Category = normalizedCategory!,
            Location = normalizedLocation!,
            TimeWindow = normalizedTimeWindow!,
            Provider = string.IsNullOrWhiteSpace(response.Provider) ? "Google" : response.Provider,
            RefreshedAtUtc = response.RetrievedAtUtc,
            Items = response.Items
                .Select(item => new IntelligenceTrendItem(item.QueryOrTopic.Trim(), item.Score, item.Rank))
                .ToArray()
        };

        await repository.UpsertAsync(record, ct);

        var notification = new IntelligenceTrendsUpdatedNotification(
            tenantId,
            record.SiteId,
            record.Category,
            record.Location,
            record.TimeWindow,
            record.RefreshedAtUtc);

        foreach (var observer in observers)
        {
            await observer.OnTrendsUpdated(notification, ct);
        }

        return OperationResult<RefreshIntelligenceResult>.Success(
            new RefreshIntelligenceResult(record.Provider, record.RefreshedAtUtc, record.Items.Count));
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
        category = null;
        location = null;
        timeWindow = null;
        limit = request.Limit ?? DefaultLimit;

        if (string.IsNullOrWhiteSpace(tenantId) || !Guid.TryParse(tenantId, out _))
        {
            errors.Add("tenantId", "Tenant id is invalid.");
        }

        if (request.SiteId == Guid.Empty)
        {
            errors.Add("siteId", "Site id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            errors.Add("category", "Category is required.");
        }
        else
        {
            category = request.Category.Trim();
        }

        if (string.IsNullOrWhiteSpace(request.Location))
        {
            errors.Add("location", "Location is required.");
        }
        else
        {
            location = request.Location.Trim();
        }

        if (string.IsNullOrWhiteSpace(request.TimeWindow))
        {
            errors.Add("timeWindow", "Time window is required.");
        }
        else
        {
            timeWindow = request.TimeWindow.Trim();
        }

        if (limit <= 0 || limit > MaxLimit)
        {
            errors.Add("limit", $"Limit must be between 1 and {MaxLimit}.");
        }

        return errors;
    }
}
