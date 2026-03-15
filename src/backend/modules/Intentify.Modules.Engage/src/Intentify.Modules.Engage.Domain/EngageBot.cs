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
}
