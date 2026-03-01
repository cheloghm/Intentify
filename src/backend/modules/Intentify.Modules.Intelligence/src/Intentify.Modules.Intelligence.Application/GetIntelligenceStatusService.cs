using Intentify.Shared.Validation;

namespace Intentify.Modules.Intelligence.Application;

public sealed class GetIntelligenceStatusService(IIntelligenceTrendsRepository repository)
{
    public async Task<OperationResult<IntelligenceStatusResponse>> HandleAsync(
        string tenantId,
        Guid siteId,
        string category,
        string location,
        string timeWindow,
        CancellationToken ct = default)
    {
        var errors = new ValidationErrors();

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

        if (string.IsNullOrWhiteSpace(location))
        {
            errors.Add("location", "Location is required.");
        }

        if (string.IsNullOrWhiteSpace(timeWindow))
        {
            errors.Add("timeWindow", "Time window is required.");
        }

        if (errors.HasErrors)
        {
            return OperationResult<IntelligenceStatusResponse>.ValidationFailed(errors);
        }

        var status = await repository.GetStatusAsync(tenantId, siteId, category.Trim(), location.Trim(), timeWindow.Trim(), ct);
        if (status is null)
        {
            return OperationResult<IntelligenceStatusResponse>.NotFound();
        }

        return OperationResult<IntelligenceStatusResponse>.Success(status);
    }
}
