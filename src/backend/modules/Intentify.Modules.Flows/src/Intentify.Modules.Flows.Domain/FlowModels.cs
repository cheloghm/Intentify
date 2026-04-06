namespace Intentify.Modules.Flows.Domain;

public static class FlowsMongoCollections
{
    public const string FlowDefinitions = "flows_definitions";
    public const string FlowRuns = "flows_runs";
}

public sealed class FlowDefinition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public Guid SiteId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
    public int Priority { get; init; }
    public int? MaxRunsPerHour { get; init; }
    public FlowTrigger Trigger { get; init; } = new();
    public IReadOnlyCollection<FlowCondition> Conditions { get; init; } = [];
    public IReadOnlyCollection<FlowAction> Actions { get; init; } = [];
}

public sealed class FlowTrigger
{
    public string TriggerType { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Filters { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public enum FlowConditionOperator
{
    Equals = 1,
    Contains = 2,
    GreaterThan = 3
}

public sealed record FlowCondition(string Field, FlowConditionOperator Operator, string Value);

public sealed class FlowAction
{
    public string ActionType { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string>? Params { get; init; }
}

public enum FlowRunStatus
{
    Succeeded = 1,
    Failed = 2
}

public sealed class FlowRun
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid FlowId { get; init; }
    public Guid TenantId { get; init; }
    public Guid SiteId { get; init; }
    public string TriggerType { get; init; } = string.Empty;
    public string TriggerSummary { get; init; } = string.Empty;
    public DateTime ExecutedAtUtc { get; init; }
    public FlowRunStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
}
