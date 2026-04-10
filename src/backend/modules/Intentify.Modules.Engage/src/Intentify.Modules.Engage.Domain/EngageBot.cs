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

    /// <summary>JSON array of auto-trigger rules, e.g. [{"type":"page_view","urlPattern":"/pricing","message":"..."}]</summary>
    public string? AutoTriggerRulesJson { get; set; }

    /// <summary>When true, the 'Powered by Hven' footer is hidden from the widget.</summary>
    public bool HideBranding { get; set; }

    /// <summary>Replaces the default 'Powered by Hven' text when set (e.g. "Powered by Acme AI"). Only shown when HideBranding is false.</summary>
    public string? CustomBrandingText { get; set; }

    /// <summary>When true, the widget randomly shows OpeningMessageA or OpeningMessageB to new visitors.</summary>
    public bool AbTestEnabled { get; set; }

    /// <summary>Opening message variant A shown to the widget visitor on first load.</summary>
    public string? OpeningMessageA { get; set; }

    /// <summary>Opening message variant B shown to the widget visitor on first load.</summary>
    public string? OpeningMessageB { get; set; }

    /// <summary>Number of times variant A has been shown (impression).</summary>
    public int AbTestImpressionCountA { get; set; }

    /// <summary>Number of times variant B has been shown (impression).</summary>
    public int AbTestImpressionCountB { get; set; }

    /// <summary>Number of leads created from sessions where variant A was shown.</summary>
    public int AbTestConversionCountA { get; set; }

    /// <summary>Number of leads created from sessions where variant B was shown.</summary>
    public int AbTestConversionCountB { get; set; }

    /// <summary>When true, a micro-survey is shown to new visitors before the first message.</summary>
    public bool SurveyEnabled { get; set; }

    /// <summary>The question displayed in the survey, e.g. "What brought you here today?"</summary>
    public string? SurveyQuestion { get; set; }

    /// <summary>JSON array of survey option strings, e.g. ["Just browsing","Checking prices","Ready to buy","Comparing options"]</summary>
    public string? SurveyOptions { get; set; }

    /// <summary>When true, the widget fires an exit intent overlay when the visitor moves their cursor to leave the page.</summary>
    public bool ExitIntentEnabled { get; set; }

    /// <summary>Message shown in the exit intent overlay, e.g. "Before you go — can I help you find what you need?"</summary>
    public string? ExitIntentMessage { get; set; }
}
