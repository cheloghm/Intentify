using Intentify.Modules.Flows.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Flows.Application;

public sealed class CreateFlowService(IFlowsRepository repository)
{
    public async Task<OperationResult<FlowDetailDto>> HandleAsync(CreateFlowCommand command, CancellationToken ct = default)
    {
        var validation = FlowValidation.ValidateCreate(command, out var flow);
        if (validation.HasErrors)
        {
            return OperationResult<FlowDetailDto>.ValidationFailed(validation);
        }

        await repository.InsertAsync(flow!, ct);
        return OperationResult<FlowDetailDto>.Success(FlowMapping.ToDetail(flow!));
    }
}

public sealed class UpdateFlowService(IFlowsRepository repository)
{
    public async Task<OperationResult<FlowDetailDto>> HandleAsync(UpdateFlowCommand command, CancellationToken ct = default)
    {
        var validation = FlowValidation.ValidateUpdate(command, out var updated);
        if (validation.HasErrors)
        {
            return OperationResult<FlowDetailDto>.ValidationFailed(validation);
        }

        var existing = await repository.GetAsync(command.TenantId, command.FlowId, ct);
        if (existing is null)
        {
            return OperationResult<FlowDetailDto>.NotFound();
        }

        var merged = new FlowDefinition
        {
            Id = existing.Id,
            TenantId = existing.TenantId,
            SiteId = existing.SiteId,
            Name = updated!.Name,
            Enabled = updated.Enabled,
            Trigger = updated.Trigger,
            Conditions = updated.Conditions,
            Actions = updated.Actions
        };

        var replaced = await repository.ReplaceAsync(merged, ct);
        if (replaced is null)
        {
            return OperationResult<FlowDetailDto>.NotFound();
        }

        return OperationResult<FlowDetailDto>.Success(FlowMapping.ToDetail(replaced));
    }
}

public sealed class SetFlowEnabledService(IFlowsRepository repository)
{
    public async Task<OperationResult<FlowDetailDto>> HandleAsync(SetFlowEnabledCommand command, CancellationToken ct = default)
    {
        var flow = await repository.GetAsync(command.TenantId, command.FlowId, ct);
        if (flow is null)
        {
            return OperationResult<FlowDetailDto>.NotFound();
        }

        var updated = new FlowDefinition
        {
            Id = flow.Id,
            TenantId = flow.TenantId,
            SiteId = flow.SiteId,
            Name = flow.Name,
            Enabled = command.Enabled,
            Trigger = flow.Trigger,
            Conditions = flow.Conditions,
            Actions = flow.Actions
        };
        var replaced = await repository.ReplaceAsync(updated, ct);

        return replaced is null
            ? OperationResult<FlowDetailDto>.NotFound()
            : OperationResult<FlowDetailDto>.Success(FlowMapping.ToDetail(replaced));
    }
}

public sealed class GetFlowService(IFlowsRepository repository)
{
    public async Task<OperationResult<FlowDetailDto>> HandleAsync(GetFlowQuery query, CancellationToken ct = default)
    {
        var flow = await repository.GetAsync(query.TenantId, query.FlowId, ct);
        return flow is null
            ? OperationResult<FlowDetailDto>.NotFound()
            : OperationResult<FlowDetailDto>.Success(FlowMapping.ToDetail(flow));
    }
}

public sealed class ListFlowsService(IFlowsRepository repository)
{
    public async Task<OperationResult<IReadOnlyCollection<FlowSummaryDto>>> HandleAsync(ListFlowsQuery query, CancellationToken ct = default)
    {
        if (query.SiteId == Guid.Empty)
        {
            var errors = new ValidationErrors();
            errors.Add("siteId", "Site id is required.");
            return OperationResult<IReadOnlyCollection<FlowSummaryDto>>.ValidationFailed(errors);
        }

        var flows = await repository.ListBySiteAsync(query.TenantId, query.SiteId, ct);
        return OperationResult<IReadOnlyCollection<FlowSummaryDto>>.Success(flows.Select(FlowMapping.ToSummary).ToArray());
    }
}

public sealed class ListFlowRunsService(IFlowRunsRepository repository)
{
    public async Task<OperationResult<IReadOnlyCollection<FlowRunDto>>> HandleAsync(ListFlowRunsQuery query, CancellationToken ct = default)
    {
        var limit = query.Limit <= 0 ? 50 : Math.Min(query.Limit, 200);
        var runs = await repository.ListByFlowAsync(query.TenantId, query.FlowId, limit, ct);
        return OperationResult<IReadOnlyCollection<FlowRunDto>>.Success(runs.Select(FlowMapping.ToRun).ToArray());
    }
}

public sealed class ExecuteFlowsForTriggerService(IFlowsRepository flowsRepository, IFlowRunsRepository runsRepository)
{
    public async Task<OperationResult<ExecuteFlowsResult>> HandleAsync(ExecuteFlowsTriggerCommand command, CancellationToken ct = default)
    {
        var errors = new ValidationErrors();
        if (command.SiteId == Guid.Empty)
        {
            errors.Add("siteId", "Site id is required.");
        }

        if (string.IsNullOrWhiteSpace(command.TriggerType))
        {
            errors.Add("triggerType", "Trigger type is required.");
        }

        if (errors.HasErrors)
        {
            return OperationResult<ExecuteFlowsResult>.ValidationFailed(errors);
        }

        var flows = await flowsRepository.ListBySiteAsync(command.TenantId, command.SiteId, ct);
        var payload = command.Payload ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var triggerFilters = command.TriggerFilters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var matched = 0;
        var executed = 0;

        foreach (var flow in flows)
        {
            if (!flow.Enabled)
            {
                continue;
            }

            if (!string.Equals(flow.Trigger.TriggerType, command.TriggerType.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TriggerMatches(flow.Trigger.Filters, triggerFilters))
            {
                continue;
            }

            if (!FlowConditionEvaluator.MatchesAll(flow.Conditions, payload))
            {
                continue;
            }

            matched++;

            foreach (var action in flow.Actions)
            {
                if (!string.Equals(action.ActionType, "LogRun", StringComparison.OrdinalIgnoreCase))
                {
                    await runsRepository.InsertAsync(new FlowRun
                    {
                        FlowId = flow.Id,
                        TenantId = flow.TenantId,
                        SiteId = flow.SiteId,
                        TriggerType = command.TriggerType.Trim(),
                        TriggerSummary = BuildSummary(payload),
                        ExecutedAtUtc = DateTime.UtcNow,
                        Status = FlowRunStatus.Failed,
                        ErrorMessage = $"Unsupported action type '{action.ActionType}'."
                    }, ct);
                    executed++;
                    continue;
                }

                await runsRepository.InsertAsync(new FlowRun
                {
                    FlowId = flow.Id,
                    TenantId = flow.TenantId,
                    SiteId = flow.SiteId,
                    TriggerType = command.TriggerType.Trim(),
                    TriggerSummary = BuildSummary(payload),
                    ExecutedAtUtc = DateTime.UtcNow,
                    Status = FlowRunStatus.Succeeded,
                    ErrorMessage = null
                }, ct);
                executed++;
            }
        }

        return OperationResult<ExecuteFlowsResult>.Success(new ExecuteFlowsResult(matched, executed));
    }

    private static bool TriggerMatches(IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual)
    {
        foreach (var pair in expected)
        {
            if (!actual.TryGetValue(pair.Key, out var value) || !string.Equals(value, pair.Value, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildSummary(IReadOnlyDictionary<string, string> payload)
    {
        return string.Join(", ", payload.Take(10).Select(pair => $"{pair.Key}={pair.Value}"));
    }
}

internal static class FlowValidation
{
    public static ValidationErrors ValidateCreate(CreateFlowCommand command, out FlowDefinition? flow)
    {
        flow = null;
        var errors = new ValidationErrors();
        if (command.TenantId == Guid.Empty)
        {
            errors.Add("tenantId", "Tenant id is required.");
        }

        if (command.SiteId == Guid.Empty)
        {
            errors.Add("siteId", "Site id is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            errors.Add("name", "Name is required.");
        }

        var trigger = ValidateTrigger(command.Trigger, errors);
        var conditions = ValidateConditions(command.Conditions, errors);
        var actions = ValidateActions(command.Actions, errors);

        if (errors.HasErrors)
        {
            return errors;
        }

        flow = new FlowDefinition
        {
            TenantId = command.TenantId,
            SiteId = command.SiteId,
            Name = command.Name.Trim(),
            Enabled = true,
            Trigger = trigger!,
            Conditions = conditions!,
            Actions = actions!
        };

        return errors;
    }

    public static ValidationErrors ValidateUpdate(UpdateFlowCommand command, out FlowDefinition? flow)
    {
        flow = null;
        var errors = new ValidationErrors();
        if (command.TenantId == Guid.Empty)
        {
            errors.Add("tenantId", "Tenant id is required.");
        }

        if (command.FlowId == Guid.Empty)
        {
            errors.Add("id", "Flow id is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            errors.Add("name", "Name is required.");
        }

        var trigger = ValidateTrigger(command.Trigger, errors);
        var conditions = ValidateConditions(command.Conditions, errors);
        var actions = ValidateActions(command.Actions, errors);

        if (errors.HasErrors)
        {
            return errors;
        }

        flow = new FlowDefinition
        {
            Id = command.FlowId,
            TenantId = command.TenantId,
            Name = command.Name.Trim(),
            Enabled = command.Enabled,
            Trigger = trigger!,
            Conditions = conditions!,
            Actions = actions!,
            SiteId = Guid.Empty
        };

        return errors;
    }

    private static FlowTrigger? ValidateTrigger(FlowTriggerInput trigger, ValidationErrors errors)
    {
        if (string.IsNullOrWhiteSpace(trigger.TriggerType))
        {
            errors.Add("trigger.triggerType", "Trigger type is required.");
            return null;
        }

        var filters = (trigger.Filters ?? new Dictionary<string, string>())
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        return new FlowTrigger
        {
            TriggerType = trigger.TriggerType.Trim(),
            Filters = filters
        };
    }

    private static IReadOnlyCollection<FlowCondition>? ValidateConditions(IReadOnlyCollection<FlowConditionInput>? inputs, ValidationErrors errors)
    {
        var conditions = new List<FlowCondition>();
        if (inputs is null)
        {
            return conditions;
        }

        foreach (var condition in inputs)
        {
            if (string.IsNullOrWhiteSpace(condition.Field) || string.IsNullOrWhiteSpace(condition.Value))
            {
                errors.Add("conditions", "Each condition requires field and value.");
                continue;
            }

            if (!Enum.IsDefined(condition.Operator))
            {
                errors.Add("conditions", "Condition operator is invalid.");
                continue;
            }

            conditions.Add(new FlowCondition(condition.Field.Trim(), condition.Operator, condition.Value.Trim()));
        }

        return conditions;
    }

    private static IReadOnlyCollection<FlowAction>? ValidateActions(IReadOnlyCollection<FlowActionInput>? inputs, ValidationErrors errors)
    {
        if (inputs is null || inputs.Count == 0)
        {
            errors.Add("actions", "At least one action is required.");
            return null;
        }

        var actions = new List<FlowAction>();
        foreach (var action in inputs)
        {
            if (!string.Equals(action.ActionType, "LogRun", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("actions", "Only LogRun action is supported in MVP.");
                continue;
            }

            actions.Add(new FlowAction
            {
                ActionType = "LogRun",
                Params = action.Params?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            });
        }

        return actions;
    }
}

internal static class FlowMapping
{
    public static FlowDetailDto ToDetail(FlowDefinition flow) => new(
        flow.Id,
        flow.SiteId,
        flow.Name,
        flow.Enabled,
        new FlowTriggerInput(flow.Trigger.TriggerType, flow.Trigger.Filters),
        flow.Conditions.Select(c => new FlowConditionInput(c.Field, c.Operator, c.Value)).ToArray(),
        flow.Actions.Select(a => new FlowActionInput(a.ActionType, a.Params)).ToArray());

    public static FlowSummaryDto ToSummary(FlowDefinition flow) => new(
        flow.Id,
        flow.SiteId,
        flow.Name,
        flow.Enabled,
        flow.Trigger.TriggerType,
        flow.Conditions.Count,
        flow.Actions.Count);

    public static FlowRunDto ToRun(FlowRun run) => new(
        run.Id,
        run.FlowId,
        run.ExecutedAtUtc,
        run.TriggerType,
        run.TriggerSummary,
        run.Status.ToString(),
        run.ErrorMessage);
}
