namespace Intentify.Modules.Ads.Api;

public sealed record AdPlacementRequest(string SlotKey, string? PathPattern, string? Device, string Headline, string? Body, string? ImageUrl, string DestinationUrl, string? CtaText, int Order, bool IsActive = true);

public sealed record CreateAdCampaignRequest(Guid SiteId, string Name, string? Objective, bool IsActive = true, DateTime? StartsAtUtc = null, DateTime? EndsAtUtc = null, decimal? Budget = null, IReadOnlyCollection<AdPlacementRequest>? Placements = null);

public sealed record UpdateAdCampaignRequest(Guid SiteId, string Name, string? Objective, bool IsActive = true, DateTime? StartsAtUtc = null, DateTime? EndsAtUtc = null, decimal? Budget = null);

public sealed record UpsertAdPlacementsRequest(IReadOnlyCollection<AdPlacementRequest> Placements);
