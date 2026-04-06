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

    /// <summary>Whether the weekly digest email is enabled for this bot.</summary>
    public bool DigestEmailEnabled { get; set; }

    /// <summary>Comma-separated list of recipient email addresses for the digest.</summary>
    public string? DigestEmailRecipients { get; set; }

    /// <summary>Digest frequency: "weekly" or "daily". Defaults to "weekly".</summary>
    public string? DigestEmailFrequency { get; set; }

    // ── Phase 5: Widget customisation ─────────────────────────────────────────

    /// <summary>Widget launcher position: "bottom-right" (default) | "bottom-left".</summary>
    public string? WidgetPosition { get; set; }

    /// <summary>Custom greeting shown in the chat bubble before the user opens it.</summary>
    public string? GreetingMessage { get; set; }

    /// <summary>Avatar emoji or short text shown on the launcher button, e.g. "💬" or "Hi!".</summary>
    public string? LauncherIcon { get; set; }

    // ── Phase 5: Auto-trigger rules ────────────────────────────────────────────

    /// <summary>
    /// JSON-serialised list of auto-trigger rules. Each rule has:
    ///   { type: "time_on_page" | "scroll_percent" | "exit_intent" | "url_match",
    ///     value: number | string,
    ///     message: string }
    /// Stored as a JSON string so no schema migration is needed.
    /// </summary>
    public string? AutoTriggerRulesJson { get; set; }
}

/// <summary>
/// A single auto-trigger rule deserialized from EngageBot.AutoTriggerRulesJson.
/// </summary>
public sealed class EngageAutoTriggerRule
{
    /// <summary>"time_on_page" | "scroll_percent" | "exit_intent" | "url_match"</summary>
    public string Type { get; set; } = "time_on_page";

    /// <summary>Numeric threshold (seconds for time_on_page, 0–100 for scroll_percent) or URL pattern.</summary>
    public string Value { get; set; } = "30";

    /// <summary>Message to open the widget with when the rule fires.</summary>
    public string Message { get; set; } = "Need help?";

    /// <summary>Whether this rule is active.</summary>
    public bool Enabled { get; set; } = true;
}
