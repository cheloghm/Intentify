using Intentify.Modules.Ads.Domain;
using Intentify.Modules.Sites.Application;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Ads.Application;

public sealed class CreateAdCampaignHandler(IAdCampaignRepository repository, ISiteRepository sites)
{
    public async Task<OperationResult<AdCampaign>> HandleAsync(CreateAdCampaignCommand command, CancellationToken cancellationToken = default)
    {
        var errors = AdsValidationHelpers.ValidateCampaign(command.SiteId, command.Name, command.Budget);
        if (errors.HasErrors) return OperationResult<AdCampaign>.ValidationFailed(errors);

        var site = await sites.GetByTenantAndIdAsync(command.TenantId, command.SiteId, cancellationToken);
        if (site is null) return OperationResult<AdCampaign>.NotFound();

        var now = DateTime.UtcNow;
        var campaign = new AdCampaign
        {
            TenantId = command.TenantId,
            SiteId = command.SiteId,
            Name = command.Name.Trim(),
            Objective = AdsValidationHelpers.TrimOrNull(command.Objective, 200),
            IsActive = command.IsActive,
            StartsAtUtc = command.StartsAtUtc,
            EndsAtUtc = command.EndsAtUtc,
            Budget = command.Budget,
            Placements = AdsValidationHelpers.NormalizePlacements(command.Placements).ToList(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await repository.InsertAsync(campaign, cancellationToken);
        return OperationResult<AdCampaign>.Success(campaign);
    }
}

public sealed class UpdateAdCampaignHandler(IAdCampaignRepository repository, ISiteRepository sites)
{
    public async Task<OperationResult<AdCampaign>> HandleAsync(UpdateAdCampaignCommand command, CancellationToken cancellationToken = default)
    {
        var errors = AdsValidationHelpers.ValidateCampaign(command.SiteId, command.Name, command.Budget);
        if (errors.HasErrors) return OperationResult<AdCampaign>.ValidationFailed(errors);

        var site = await sites.GetByTenantAndIdAsync(command.TenantId, command.SiteId, cancellationToken);
        if (site is null) return OperationResult<AdCampaign>.NotFound();

        var campaign = await repository.GetByIdAsync(command.TenantId, command.CampaignId, cancellationToken);
        if (campaign is null) return OperationResult<AdCampaign>.NotFound();

        campaign.SiteId = command.SiteId;
        campaign.Name = command.Name.Trim();
        campaign.Objective = AdsValidationHelpers.TrimOrNull(command.Objective, 200);
        campaign.IsActive = command.IsActive;
        campaign.StartsAtUtc = command.StartsAtUtc;
        campaign.EndsAtUtc = command.EndsAtUtc;
        campaign.Budget = command.Budget;
        campaign.UpdatedAtUtc = DateTime.UtcNow;

        await repository.UpdateAsync(campaign, cancellationToken);
        return OperationResult<AdCampaign>.Success(campaign);
    }
}

public sealed class GetAdCampaignHandler(IAdCampaignRepository repository)
{
    public async Task<OperationResult<AdCampaign>> HandleAsync(GetAdCampaignQuery query, CancellationToken cancellationToken = default)
    {
        var campaign = await repository.GetByIdAsync(query.TenantId, query.CampaignId, cancellationToken);
        return campaign is null ? OperationResult<AdCampaign>.NotFound() : OperationResult<AdCampaign>.Success(campaign);
    }
}

public sealed class ListAdCampaignsHandler(IAdCampaignRepository repository, ISiteRepository sites)
{
    public async Task<OperationResult<IReadOnlyCollection<AdCampaign>>> HandleAsync(ListAdCampaignsQuery query, CancellationToken cancellationToken = default)
    {
        if (query.SiteId is { } siteId)
        {
            var site = await sites.GetByTenantAndIdAsync(query.TenantId, siteId, cancellationToken);
            if (site is null) return OperationResult<IReadOnlyCollection<AdCampaign>>.NotFound();
        }

        var campaigns = await repository.ListAsync(query, cancellationToken);
        return OperationResult<IReadOnlyCollection<AdCampaign>>.Success(campaigns);
    }
}

public sealed class UpsertAdPlacementsHandler(IAdCampaignRepository repository)
{
    public async Task<OperationResult<AdCampaign>> HandleAsync(UpsertAdPlacementsCommand command, CancellationToken cancellationToken = default)
    {
        var placements = AdsValidationHelpers.NormalizePlacements(command.Placements);
        var updated = await repository.ReplacePlacementsAsync(command.TenantId, command.CampaignId, placements, DateTime.UtcNow, cancellationToken);
        return updated is null ? OperationResult<AdCampaign>.NotFound() : OperationResult<AdCampaign>.Success(updated);
    }
}

public sealed class SetAdCampaignActiveHandler(IAdCampaignRepository repository)
{
    public async Task<OperationResult<AdCampaign>> HandleAsync(SetAdCampaignActiveCommand command, CancellationToken cancellationToken = default)
    {
        var campaign = await repository.GetByIdAsync(command.TenantId, command.CampaignId, cancellationToken);
        if (campaign is null) return OperationResult<AdCampaign>.NotFound();

        campaign.IsActive = command.IsActive;
        campaign.UpdatedAtUtc = DateTime.UtcNow;
        await repository.UpdateAsync(campaign, cancellationToken);
        return OperationResult<AdCampaign>.Success(campaign);
    }
}

public sealed class GetAdCampaignReportHandler(IAdCampaignRepository repository)
{
    public async Task<OperationResult<AdCampaignReportResponse>> HandleAsync(GetAdCampaignReportQuery query, CancellationToken cancellationToken = default)
    {
        var campaign = await repository.GetByIdAsync(query.TenantId, query.CampaignId, cancellationToken);
        if (campaign is null) return OperationResult<AdCampaignReportResponse>.NotFound();

        var byPlacement = campaign.Placements
            .OrderBy(item => item.Order)
            .Select(item => new AdCampaignPlacementMetrics(item.Id, item.SlotKey, 0, 0, 0m))
            .ToArray();

        var response = new AdCampaignReportResponse(
            campaign.Id,
            DateTime.UtcNow,
            "none",
            new AdCampaignReportTotals(0, 0, 0m),
            byPlacement,
            []);

        return OperationResult<AdCampaignReportResponse>.Success(response);
    }
}

file static class AdsValidationHelpers
{
    public static ValidationErrors ValidateCampaign(Guid siteId, string name, decimal? budget)
    {
        var errors = new ValidationErrors();
        if (siteId == Guid.Empty) errors.Add("siteId", "Site id is required.");
        if (string.IsNullOrWhiteSpace(name)) errors.Add("name", "Name is required.");
        if (!string.IsNullOrWhiteSpace(name) && name.Trim().Length > 200) errors.Add("name", "Name must be 200 characters or fewer.");
        if (budget is < 0) errors.Add("budget", "Budget cannot be negative.");
        return errors;
    }

    public static IReadOnlyCollection<AdPlacement> NormalizePlacements(IReadOnlyCollection<AdPlacementInput>? placements)
    {
        if (placements is null || placements.Count == 0) return [];

        return placements.Select((placement, index) => new AdPlacement
        {
            SlotKey = placement.SlotKey?.Trim() ?? string.Empty,
            PathPattern = TrimOrNull(placement.PathPattern, 300),
            Device = string.IsNullOrWhiteSpace(placement.Device) ? "all" : placement.Device.Trim(),
            Headline = placement.Headline?.Trim() ?? string.Empty,
            Body = TrimOrNull(placement.Body, 1000),
            ImageUrl = TrimOrNull(placement.ImageUrl, 2048),
            DestinationUrl = placement.DestinationUrl?.Trim() ?? string.Empty,
            CtaText = TrimOrNull(placement.CtaText, 120),
            Order = placement.Order == 0 ? index : placement.Order,
            IsActive = placement.IsActive
        }).OrderBy(item => item.Order).ToArray();
    }

    public static string? TrimOrNull(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
