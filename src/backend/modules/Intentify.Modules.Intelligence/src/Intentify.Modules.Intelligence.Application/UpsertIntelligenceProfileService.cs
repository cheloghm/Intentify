using Intentify.Modules.Intelligence.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Intelligence.Application;

public sealed class UpsertIntelligenceProfileService(IIntelligenceProfileRepository repository)
{
    private const int MaxTargetLocations = 10;
    private const int MaxProductsOrServices = 20;
    private const int MaxWatchTopics = 20;
    private const int MaxSeasonalPriorities = 12;
    private const int MinRefreshIntervalMinutes = 15;
    private const int MaxRefreshIntervalMinutes = 7 * 24 * 60;

    public async Task<OperationResult<IntelligenceProfileResponse>> HandleAsync(
        string tenantId,
        UpsertIntelligenceProfileRequest request,
        CancellationToken ct = default)
    {
        var errors = ValidateRequest(
            tenantId,
            request,
            out var normalizedProfileName,
            out var normalizedIndustryCategory,
            out var normalizedAudienceType,
            out var normalizedLocations,
            out var normalizedProducts,
            out var normalizedWatchTopics,
            out var normalizedSeasonalPriorities);

        if (errors.HasErrors)
        {
            return OperationResult<IntelligenceProfileResponse>.ValidationFailed(errors);
        }

        var existing = await repository.GetAsync(tenantId, request.SiteId, ct);
        var now = DateTime.UtcNow;
        var profile = new IntelligenceProfile
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            TenantId = existing?.TenantId ?? Guid.Parse(tenantId),
            SiteId = request.SiteId,
            ProfileName = normalizedProfileName!,
            IndustryCategory = normalizedIndustryCategory!,
            PrimaryAudienceType = normalizedAudienceType!,
            TargetLocations = normalizedLocations!,
            PrimaryProductsOrServices = normalizedProducts!,
            WatchTopics = normalizedWatchTopics!,
            SeasonalPriorities = normalizedSeasonalPriorities!,
            IsActive = request.IsActive,
            RefreshIntervalMinutes = request.RefreshIntervalMinutes,
            CreatedAtUtc = existing?.CreatedAtUtc ?? now,
            UpdatedAtUtc = now
        };

        await repository.UpsertAsync(profile, ct);
        return OperationResult<IntelligenceProfileResponse>.Success(ToResponse(profile));
    }

    private static ValidationErrors ValidateRequest(
        string tenantId,
        UpsertIntelligenceProfileRequest request,
        out string? profileName,
        out string? industryCategory,
        out string? audienceType,
        out IReadOnlyCollection<string>? targetLocations,
        out IReadOnlyCollection<string>? primaryProductsOrServices,
        out IReadOnlyCollection<string>? watchTopics,
        out IReadOnlyCollection<string>? seasonalPriorities)
    {
        var errors = new ValidationErrors();
        profileName = null;
        industryCategory = null;
        audienceType = null;
        targetLocations = null;
        primaryProductsOrServices = null;
        watchTopics = null;
        seasonalPriorities = null;

        if (string.IsNullOrWhiteSpace(tenantId) || !Guid.TryParse(tenantId, out _))
        {
            errors.Add("tenantId", "Tenant id is invalid.");
        }

        if (request.SiteId == Guid.Empty)
        {
            errors.Add("siteId", "Site id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ProfileName))
        {
            errors.Add("profileName", "Profile name is required.");
        }
        else
        {
            profileName = request.ProfileName.Trim();
        }

        if (string.IsNullOrWhiteSpace(request.IndustryCategory))
        {
            errors.Add("industryCategory", "Industry category is required.");
        }
        else
        {
            industryCategory = request.IndustryCategory.Trim();
        }

        if (string.IsNullOrWhiteSpace(request.PrimaryAudienceType))
        {
            errors.Add("primaryAudienceType", "Primary audience type is required.");
        }
        else
        {
            var normalizedAudience = request.PrimaryAudienceType.Trim();
            if (!normalizedAudience.Equals("B2B", StringComparison.OrdinalIgnoreCase)
                && !normalizedAudience.Equals("B2C", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("primaryAudienceType", "Primary audience type must be either B2B or B2C.");
            }
            else
            {
                audienceType = normalizedAudience.ToUpperInvariant();
            }
        }

        targetLocations = NormalizeCollection(request.TargetLocations, MaxTargetLocations);
        if (targetLocations.Count == 0)
        {
            errors.Add("targetLocations", "At least one target location is required.");
        }

        primaryProductsOrServices = NormalizeCollection(request.PrimaryProductsOrServices, MaxProductsOrServices);
        if (primaryProductsOrServices.Count == 0)
        {
            errors.Add("primaryProductsOrServices", "At least one product or service is required.");
        }

        watchTopics = NormalizeCollection(request.WatchTopics, MaxWatchTopics);
        seasonalPriorities = NormalizeCollection(request.SeasonalPriorities, MaxSeasonalPriorities);

        if (request.RefreshIntervalMinutes.HasValue
            && (request.RefreshIntervalMinutes.Value < MinRefreshIntervalMinutes
                || request.RefreshIntervalMinutes.Value > MaxRefreshIntervalMinutes))
        {
            errors.Add(
                "refreshIntervalMinutes",
                $"Refresh interval must be between {MinRefreshIntervalMinutes} and {MaxRefreshIntervalMinutes} minutes.");
        }

        return errors;
    }

    private static IReadOnlyCollection<string> NormalizeCollection(IReadOnlyCollection<string>? values, int maxCount)
    {
        if (values is null || values.Count == 0)
        {
            return [];
        }

        var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var normalized = value.Trim();
            if (distinct.Add(normalized))
            {
                result.Add(normalized);
            }

            if (result.Count >= maxCount)
            {
                break;
            }
        }

        return result;
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
