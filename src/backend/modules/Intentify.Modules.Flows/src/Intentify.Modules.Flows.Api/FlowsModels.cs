using Intentify.Modules.Flows.Application;
using Intentify.Modules.Flows.Domain;

namespace Intentify.Modules.Flows.Api;

public sealed record CreateFlowRequest(
    string SiteId,
    string Name,
    FlowTriggerRequest Trigger,
    IReadOnlyCollection<FlowConditionRequest>? Conditions,
    IReadOnlyCollection<FlowActionRequest>? Actions,
    int Priority = 0,
    int? MaxRunsPerHour = null);

public sealed record UpdateFlowRequest(
    string Name,
    bool Enabled,
    FlowTriggerRequest Trigger,
    IReadOnlyCollection<FlowConditionRequest>? Conditions,
    IReadOnlyCollection<FlowActionRequest>? Actions,
    int Priority = 0,
    int? MaxRunsPerHour = null);

public sealed record FlowTriggerRequest(string TriggerType, IReadOnlyDictionary<string, string>? Filters);
public sealed record FlowConditionRequest(string Field, FlowConditionOperator Operator, string Value);
public sealed record FlowActionRequest(string ActionType, IReadOnlyDictionary<string, string>? Params);

internal static class FlowApiMapping
{
    public static FlowTriggerInput ToInput(this FlowTriggerRequest request) => new(request.TriggerType, request.Filters);
    public static FlowConditionInput ToInput(this FlowConditionRequest request) => new(request.Field, request.Operator, request.Value);
    public static FlowActionInput ToInput(this FlowActionRequest request) => new(request.ActionType, request.Params);
}
