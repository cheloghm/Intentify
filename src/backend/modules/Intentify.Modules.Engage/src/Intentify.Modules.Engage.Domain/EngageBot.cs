namespace Intentify.Modules.Engage.Domain;

public sealed class EngageBot
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid BotId { get; init; } = Guid.NewGuid();

    public Guid TenantId { get; init; }

    public Guid SiteId { get; init; }

    public string DisplayName { get; set; } = "Assistant";

    public string? Name { get; set; }

    public string? PrimaryColor { get; set; }

    public bool? LauncherVisible { get; set; }

    public string? Tone { get; set; }

    public string? Verbosity { get; set; }

    public string? FallbackStyle { get; set; }

    /// <summary>What the business does — one or two sentences used in AI Layer 1 briefing.</summary>
    public string? BusinessDescription { get; set; }

    /// <summary>Industry vertical, e.g. "web design", "plumbing", "food truck catering".</summary>
    public string? Industry { get; set; }

    /// <summary>Comma-separated or prose list of core services offered.</summary>
    public string? ServicesDescription { get; set; }

    /// <summary>Geographic focus, e.g. "Dublin, Ireland" or "nationwide".</summary>
    public string? GeoFocus { get; set; }

    /// <summary>Personality descriptor, e.g. "friendly local experts", "premium and precise".</summary>
    public string? PersonalityDescriptor { get; set; }
}
