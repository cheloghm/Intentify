namespace Intentify.Modules.Knowledge.Domain;

/// <summary>
/// Structured quick facts extracted from a knowledge source by a second AI pass during indexing.
/// Provides the Engage AI with pre-digested, high-signal business facts at the top of every prompt.
/// </summary>
public sealed class KnowledgeQuickFacts
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid TenantId { get; init; }

    public Guid SiteId { get; init; }

    public Guid SourceId { get; init; }

    public string? ServicesOffered { get; set; }

    public string? PricingSignals { get; set; }

    public string? LocationCoverage { get; set; }

    public string? HoursAvailability { get; set; }

    public string? TeamCredentials { get; set; }

    /// <summary>FAQs extracted as plain text — "Q: ... A: ..." format, one per line pair.</summary>
    public string? FaqsText { get; set; }

    public string? UniqueSellingPoints { get; set; }

    public DateTime ExtractedAtUtc { get; set; }
}
