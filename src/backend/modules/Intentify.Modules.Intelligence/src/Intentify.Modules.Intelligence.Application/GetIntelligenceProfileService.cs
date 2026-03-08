using Intentify.Modules.Intelligence.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Intelligence.Application;

public sealed class GetIntelligenceProfileService(IIntelligenceProfileRepository repository)
{
    public async Task<OperationResult<IntelligenceProfileResponse>> HandleAsync(
        string tenantId,
        Guid siteId,
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

        if (errors.HasErrors)
        {
            return OperationResult<IntelligenceProfileResponse>.ValidationFailed(errors);
        }

        var profile = await repository.GetAsync(tenantId, siteId, ct);
        if (profile is null)
        {
            return OperationResult<IntelligenceProfileResponse>.NotFound();
        }

        return OperationResult<IntelligenceProfileResponse>.Success(ToResponse(profile));
    }

    private static IntelligenceProfileResponse ToResponse(IntelligenceProfile profile)
        => new(
            profile.SiteId,
            profile.ProfileName,
            profile.IndustryCategory,
            profile.PrimaryAudienceType,
            profile.TargetLocations,
            profile.PrimaryProductsOrServices,
            profile.WatchTopics,
            profile.SeasonalPriorities,
            profile.IsActive,
            profile.RefreshIntervalMinutes,
            profile.CreatedAtUtc,
            profile.UpdatedAtUtc);
}
