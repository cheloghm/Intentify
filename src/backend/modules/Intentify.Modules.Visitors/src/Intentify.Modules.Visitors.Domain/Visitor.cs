namespace Intentify.Modules.Visitors.Domain;

public sealed class Visitor
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid SiteId { get; init; }

    public Guid TenantId { get; init; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime LastSeenAtUtc { get; set; }

    public string? FirstPartyId { get; set; }

    public string? UserAgentHint { get; set; }

    public string? Language { get; set; }

    public string? Platform { get; set; }

    // Phase 2: country inferred from IP or collector event geo data
    public string? Country { get; set; }

    public string? City { get; set; }

    public string? PrimaryEmail { get; set; }

    public string? DisplayName { get; set; }

    public string? Phone { get; set; }

    public DateTime? LastIdentifiedAtUtc { get; set; }

    public List<VisitorSession> Sessions { get; set; } = [];
}

public sealed class VisitorSession
{
    public string SessionId { get; set; } = string.Empty;

    public DateTime FirstSeenAtUtc { get; set; }

    public DateTime LastSeenAtUtc { get; set; }

    public int PagesVisited { get; set; }

    public int TimeOnSiteSeconds { get; set; }

    public int EngagementScore { get; set; }

    public string? LastPath { get; set; }

    public string? LastReferrer { get; set; }

    public Dictionary<string, int> Referrers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, int> TopActions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
