namespace Intentify.Modules.Ads.Domain;

public sealed class AdCampaign
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public Guid SiteId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Objective { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
    public decimal? Budget { get; set; }
    public List<AdPlacement> Placements { get; set; } = [];
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class AdPlacement
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string SlotKey { get; init; } = string.Empty;
    public string? PathPattern { get; init; }
    public string Device { get; init; } = "all";
    public string Headline { get; init; } = string.Empty;
    public string? Body { get; init; }
    public string? ImageUrl { get; init; }
    public string DestinationUrl { get; init; } = string.Empty;
    public string? CtaText { get; init; }
    public int Order { get; init; }
    public bool IsActive { get; init; } = true;
}
