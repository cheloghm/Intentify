using Intentify.Modules.Ads.Domain;

namespace Intentify.Modules.Ads.Application;

public sealed record CreateAdCampaignCommand(Guid TenantId, Guid SiteId, string Name, string? Objective, bool IsActive, DateTime? StartsAtUtc, DateTime? EndsAtUtc, decimal? Budget, IReadOnlyCollection<AdPlacementInput>? Placements);
public sealed record UpdateAdCampaignCommand(Guid TenantId, Guid CampaignId, Guid SiteId, string Name, string? Objective, bool IsActive, DateTime? StartsAtUtc, DateTime? EndsAtUtc, decimal? Budget);
public sealed record GetAdCampaignQuery(Guid TenantId, Guid CampaignId);
public sealed record ListAdCampaignsQuery(Guid TenantId, Guid? SiteId);
public sealed record UpsertAdPlacementsCommand(Guid TenantId, Guid CampaignId, IReadOnlyCollection<AdPlacementInput> Placements);
public sealed record SetAdCampaignActiveCommand(Guid TenantId, Guid CampaignId, bool IsActive);
public sealed record GetAdCampaignReportQuery(Guid TenantId, Guid CampaignId, DateTime? FromUtc, DateTime? ToUtc);

public sealed record AdPlacementInput(string SlotKey, string? PathPattern, string? Device, string Headline, string? Body, string? ImageUrl, string DestinationUrl, string? CtaText, int Order, bool IsActive = true);

public sealed record AdCampaignReportResponse(
    Guid CampaignId,
    DateTime GeneratedAtUtc,
    string DataSource,
    AdCampaignReportTotals Totals,
    IReadOnlyCollection<AdCampaignPlacementMetrics> ByPlacement,
    IReadOnlyCollection<AdCampaignSeriesPoint> Series);

public sealed record AdCampaignReportTotals(long Impressions, long Clicks, decimal Ctr);
public sealed record AdCampaignPlacementMetrics(Guid PlacementId, string SlotKey, long Impressions, long Clicks, decimal Ctr);
public sealed record AdCampaignSeriesPoint(DateTime TimestampUtc, long Impressions, long Clicks, decimal Ctr);

public interface IAdCampaignRepository
{
    Task InsertAsync(AdCampaign campaign, CancellationToken cancellationToken = default);
    Task UpdateAsync(AdCampaign campaign, CancellationToken cancellationToken = default);
    Task<AdCampaign?> GetByIdAsync(Guid tenantId, Guid campaignId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<AdCampaign>> ListAsync(ListAdCampaignsQuery query, CancellationToken cancellationToken = default);
    Task<AdCampaign?> ReplacePlacementsAsync(Guid tenantId, Guid campaignId, IReadOnlyCollection<AdPlacement> placements, DateTime updatedAtUtc, CancellationToken cancellationToken = default);
}
