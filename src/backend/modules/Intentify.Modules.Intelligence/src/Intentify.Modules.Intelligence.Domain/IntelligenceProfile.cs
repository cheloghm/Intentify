namespace Intentify.Modules.Intelligence.Domain;

public sealed class IntelligenceProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid TenantId { get; init; }

    public Guid SiteId { get; init; }

    public string ProfileName { get; init; } = string.Empty;

    public string IndustryCategory { get; init; } = string.Empty;

    public string PrimaryAudienceType { get; init; } = string.Empty;

    public IReadOnlyCollection<string> TargetLocations { get; init; } = [];

    public IReadOnlyCollection<string> PrimaryProductsOrServices { get; init; } = [];

    public IReadOnlyCollection<string> WatchTopics { get; init; } = [];

    public IReadOnlyCollection<string> SeasonalPriorities { get; init; } = [];

    public bool IsActive { get; init; } = true;

    public int? RefreshIntervalMinutes { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}
