namespace Intentify.Modules.Auth.Application;

public static class PlanLimits
{
    public static readonly IReadOnlyDictionary<string, PlanDefinition> Plans = new Dictionary<string, PlanDefinition>(StringComparer.OrdinalIgnoreCase)
    {
        ["individual"] = new PlanDefinition("individual", MaxSites: 0,  MaxVisitorsPerMonth: 1000, MaxKnowledgeSources: 5,              AllowWhiteLabel: false, AllowAbTest: false, AllowFlows: false, AllowWebhooks: false, AllowMultiSiteAnalytics: false, AllowLinkHub: true,  MaxTeamMembers: 1),
        ["starter"]    = new PlanDefinition("starter",    MaxSites: 1,  MaxVisitorsPerMonth: 500,  MaxKnowledgeSources: 10,             AllowWhiteLabel: false, AllowAbTest: false, AllowFlows: false, AllowWebhooks: false, AllowMultiSiteAnalytics: false, AllowLinkHub: false, MaxTeamMembers: 3),
        ["growth"]     = new PlanDefinition("growth",     MaxSites: 5,  MaxVisitorsPerMonth: 5000, MaxKnowledgeSources: int.MaxValue,   AllowWhiteLabel: false, AllowAbTest: true,  AllowFlows: true,  AllowWebhooks: true,  AllowMultiSiteAnalytics: true,  AllowLinkHub: false, MaxTeamMembers: 10),
        ["agency"]     = new PlanDefinition("agency",     MaxSites: 0,  MaxVisitorsPerMonth: 0,    MaxKnowledgeSources: int.MaxValue,   AllowWhiteLabel: true,  AllowAbTest: true,  AllowFlows: true,  AllowWebhooks: true,  AllowMultiSiteAnalytics: true,  AllowLinkHub: false, MaxTeamMembers: int.MaxValue),
    };

    // 0 = unlimited
    public static PlanDefinition Get(string? plan) =>
        Plans.TryGetValue(plan ?? "starter", out var def) ? def : Plans["starter"];
}

public sealed record PlanDefinition(
    string Name,
    int MaxSites,
    int MaxVisitorsPerMonth,
    int MaxKnowledgeSources,
    bool AllowWhiteLabel,
    bool AllowAbTest,
    bool AllowFlows           = false,
    bool AllowWebhooks        = false,
    bool AllowMultiSiteAnalytics = false,
    bool AllowLinkHub         = false,
    int  MaxTeamMembers       = int.MaxValue);
