using Intentify.Modules.Flows.Domain;

namespace Intentify.Modules.Flows.Application;

public sealed record FlowTriggerInput(string TriggerType, IReadOnlyDictionary<string, string>? Filters);
public sealed record FlowConditionInput(string Field, FlowConditionOperator Operator, string Value);
public sealed record FlowActionInput(string ActionType, IReadOnlyDictionary<string, string>? Params);

public sealed record CreateFlowCommand(Guid TenantId, Guid SiteId, string Name, FlowTriggerInput Trigger, IReadOnlyCollection<FlowConditionInput>? Conditions, IReadOnlyCollection<FlowActionInput>? Actions);
public sealed record UpdateFlowCommand(Guid TenantId, Guid FlowId, string Name, bool Enabled, FlowTriggerInput Trigger, IReadOnlyCollection<FlowConditionInput>? Conditions, IReadOnlyCollection<FlowActionInput>? Actions);
public sealed record SetFlowEnabledCommand(Guid TenantId, Guid FlowId, bool Enabled);
public sealed record GetFlowQuery(Guid TenantId, Guid FlowId);
public sealed record ListFlowsQuery(Guid TenantId, Guid SiteId);
public sealed record ListFlowRunsQuery(Guid TenantId, Guid FlowId, int Limit);

public sealed record ExecuteFlowsTriggerCommand(Guid TenantId, Guid SiteId, string TriggerType, IReadOnlyDictionary<string, string>? TriggerFilters, IReadOnlyDictionary<string, string>? Payload);

public sealed record FlowSummaryDto(Guid Id, Guid SiteId, string Name, bool Enabled, string TriggerType, int ConditionsCount, int ActionsCount);
public sealed record FlowDetailDto(Guid Id, Guid SiteId, string Name, bool Enabled, FlowTriggerInput Trigger, IReadOnlyCollection<FlowConditionInput> Conditions, IReadOnlyCollection<FlowActionInput> Actions);
public sealed record FlowRunDto(Guid Id, Guid FlowId, DateTime ExecutedAtUtc, string TriggerType, string TriggerSummary, string Status, string? ErrorMessage);
public sealed record ExecuteFlowsResult(int MatchedFlows, int ExecutedRuns);

public interface IFlowsRepository
{
    Task InsertAsync(FlowDefinition flow, CancellationToken ct = default);
    Task<FlowDefinition?> GetAsync(Guid tenantId, Guid flowId, CancellationToken ct = default);
    Task<IReadOnlyCollection<FlowDefinition>> ListBySiteAsync(Guid tenantId, Guid siteId, CancellationToken ct = default);
    Task<FlowDefinition?> ReplaceAsync(FlowDefinition flow, CancellationToken ct = default);
}

public interface IFlowRunsRepository
{
    Task InsertAsync(FlowRun run, CancellationToken ct = default);
    Task<IReadOnlyCollection<FlowRun>> ListByFlowAsync(Guid tenantId, Guid flowId, int limit, CancellationToken ct = default);
}
