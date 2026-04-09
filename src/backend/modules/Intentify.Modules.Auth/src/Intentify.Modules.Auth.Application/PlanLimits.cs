namespace Intentify.Modules.Auth.Application;

public static class PlanLimits
{
    public static readonly IReadOnlyDictionary<string, PlanDefinition> Plans = new Dictionary<string, PlanDefinition>(StringComparer.OrdinalIgnoreCase)
    {
        ["starter"] = new PlanDefinition("starter", MaxSites: 1, MaxVisitorsPerMonth: 500,  MaxKnowledgeSources: 10, AllowWhiteLabel: false, AllowAbTest: false),
        ["growth"]  = new PlanDefinition("growth",  MaxSites: 5, MaxVisitorsPerMonth: 5000, MaxKnowledgeSources: 0,  AllowWhiteLabel: false, AllowAbTest: true),
        ["agency"]  = new PlanDefinition("agency",  MaxSites: 0, MaxVisitorsPerMonth: 0,    MaxKnowledgeSources: 0,  AllowWhiteLabel: true,  AllowAbTest: true),
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
    bool AllowAbTest);
