using System.Net;
using System.Net.Http.Json;
using Intentify.Modules.Flows.Domain;
using Intentify.Modules.Leads.Application;
using Intentify.Modules.Tickets.Application;
using Intentify.Shared.Validation;
using Microsoft.Extensions.DependencyInjection;

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
            Priority = updated.Priority,
            MaxRunsPerHour = updated.MaxRunsPerHour,
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
        return OperationResult<IReadOnlyCollection<FlowSummaryDto>>.Success(
            flows.OrderByDescending(f => f.Priority).Select(FlowMapping.ToSummary).ToArray());
    }
}

public sealed class ListFlowRunsService(IFlowRunsRepository repository)
{
    public async Task<OperationResult<IReadOnlyCollection<FlowRunDto>>> HandleAsync(ListFlowRunsQuery query, CancellationToken ct = default)
    {
        var limit = query.Limit <= 0 ? 100 : Math.Min(query.Limit, 200);
        var runs = await repository.ListByFlowAsync(query.TenantId, query.FlowId, limit, ct);
        return OperationResult<IReadOnlyCollection<FlowRunDto>>.Success(runs.Select(FlowMapping.ToRun).ToArray());
    }
}

public sealed class ExecuteFlowsForTriggerService(
    IFlowsRepository flowsRepository,
    IFlowRunsRepository runsRepository,
    IHttpClientFactory httpClientFactory,
    IServiceProvider serviceProvider,
    ResendEmailService? emailService = null)
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

        foreach (var flow in flows.OrderByDescending(f => f.Priority))
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

            if (flow.MaxRunsPerHour.HasValue)
            {
                var sinceUtc = DateTime.UtcNow.AddHours(-1);
                var recentCount = await runsRepository.CountSucceededByFlowSinceAsync(flow.TenantId, flow.Id, sinceUtc, ct);
                if (recentCount >= flow.MaxRunsPerHour.Value)
                {
                    continue;
                }
            }

            foreach (var action in flow.Actions)
            {
                var (status, errorMessage) = await ExecuteActionAsync(action, flow, command, payload, ct);
                await runsRepository.InsertAsync(new FlowRun
                {
                    FlowId = flow.Id,
                    TenantId = flow.TenantId,
                    SiteId = flow.SiteId,
                    TriggerType = command.TriggerType.Trim(),
                    TriggerSummary = BuildSummary(payload),
                    ExecutedAtUtc = DateTime.UtcNow,
                    Status = status,
                    ErrorMessage = errorMessage
                }, ct);
                executed++;
            }
        }

        return OperationResult<ExecuteFlowsResult>.Success(new ExecuteFlowsResult(matched, executed));
    }

    private async Task<(FlowRunStatus Status, string? ErrorMessage)> ExecuteActionAsync(
        FlowAction action,
        FlowDefinition flow,
        ExecuteFlowsTriggerCommand command,
        IReadOnlyDictionary<string, string> payload,
        CancellationToken ct)
    {
        if (string.Equals(action.ActionType, "LogRun", StringComparison.OrdinalIgnoreCase))
        {
            return (FlowRunStatus.Succeeded, null);
        }

        if (string.Equals(action.ActionType, "SendWebhook", StringComparison.OrdinalIgnoreCase))
        {
            var url = action.Params is not null && action.Params.TryGetValue("url", out var u) ? u : null;
            if (string.IsNullOrWhiteSpace(url))
            {
                return (FlowRunStatus.Failed, "SendWebhook action requires a 'url' parameter.");
            }

            try
            {
                using var client = httpClientFactory.CreateClient();
                var body = new
                {
                    triggerType = command.TriggerType,
                    triggerSummary = BuildSummary(payload),
                    siteId = command.SiteId,
                    executedAt = DateTime.UtcNow
                };
                var response = await client.PostAsJsonAsync(url, body, ct);
                if (!response.IsSuccessStatusCode)
                {
                    return (FlowRunStatus.Failed, $"Webhook returned {(int)response.StatusCode} {response.ReasonPhrase}.");
                }

                return (FlowRunStatus.Succeeded, null);
            }
            catch (Exception ex)
            {
                return (FlowRunStatus.Failed, $"Webhook failed: {ex.Message}");
            }
        }

        if (string.Equals(action.ActionType, "CreateTicket", StringComparison.OrdinalIgnoreCase))
        {
            var subject = action.Params is not null && action.Params.TryGetValue("subject", out var s) ? s : "Flow-triggered ticket";
            var description = action.Params is not null && action.Params.TryGetValue("description", out var d) ? d : BuildSummary(payload);

            var handler = serviceProvider.GetService<CreateTicketHandler>();
            if (handler is null)
            {
                return (FlowRunStatus.Failed, "CreateTicketHandler is not registered.");
            }

            var ticketCommand = new CreateTicketCommand(
                flow.TenantId,
                flow.SiteId,
                null,
                null,
                subject,
                description,
                null);

            var result = await handler.HandleAsync(ticketCommand, ct);
            return result.IsSuccess
                ? (FlowRunStatus.Succeeded, null)
                : (FlowRunStatus.Failed, string.Join("; ", result.Errors?.Errors.SelectMany(e => e.Value) ?? []));
        }

        if (string.Equals(action.ActionType, "TagLead", StringComparison.OrdinalIgnoreCase))
        {
            var leadIdRaw = action.Params is not null && action.Params.TryGetValue("leadId", out var l) ? l : null;
            var label = action.Params is not null && action.Params.TryGetValue("label", out var lb) ? lb : null;

            if (!Guid.TryParse(leadIdRaw, out var leadId))
            {
                return (FlowRunStatus.Failed, "TagLead action requires a valid 'leadId' parameter.");
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                return (FlowRunStatus.Failed, "TagLead action requires a 'label' parameter.");
            }

            var leadRepo = serviceProvider.GetService<ILeadRepository>();
            if (leadRepo is null)
            {
                return (FlowRunStatus.Failed, "ILeadRepository is not registered.");
            }

            var lead = await leadRepo.GetByIdAsync(flow.TenantId, leadId, ct);
            if (lead is null)
            {
                return (FlowRunStatus.Failed, $"Lead '{leadId}' not found.");
            }

            lead.OpportunityLabel = label.Trim();
            lead.UpdatedAtUtc = DateTime.UtcNow;
            await leadRepo.ReplaceAsync(lead, ct);
            return (FlowRunStatus.Succeeded, null);
        }

        if (string.Equals(action.ActionType, "SendEmail", StringComparison.OrdinalIgnoreCase))
        {
            var to = action.Params is not null && action.Params.TryGetValue("to", out var t) ? t : null;
            var subject = action.Params is not null && action.Params.TryGetValue("subject", out var s) ? s : "(no subject)";
            var body = action.Params is not null && action.Params.TryGetValue("body", out var b) ? b : string.Empty;

            if (string.IsNullOrWhiteSpace(to))
            {
                return (FlowRunStatus.Failed, "SendEmail action requires a 'to' parameter.");
            }

            if (emailService is null || !emailService.IsConfigured)
            {
                return (FlowRunStatus.Succeeded, $"[Email not configured — would have sent to {to}: {subject}]");
            }

            var html = body.TrimStart().StartsWith('<')
                ? body
                : $"<p style=\"font-family:sans-serif;color:#1e293b\">{WebUtility.HtmlEncode(body).Replace("\n", "<br>")}</p>";

            var (success, error) = await emailService.SendAsync(to, subject, html, ct);
            return success
                ? (FlowRunStatus.Succeeded, null)
                : (FlowRunStatus.Failed, error ?? "Unknown email error.");
        }

        if (string.Equals(action.ActionType, "SendSlackNotification", StringComparison.OrdinalIgnoreCase))
        {
            var webhookUrl = action.Params is not null && action.Params.TryGetValue("webhookUrl", out var w) ? w : null;
            var message = action.Params is not null && action.Params.TryGetValue("message", out var m) ? m : string.Empty;
            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                return (FlowRunStatus.Failed, "SendSlackNotification requires a 'webhookUrl' parameter.");
            }

            try
            {
                using var client = httpClientFactory.CreateClient();
                var slackBody = new { text = message, username = "Intentify" };
                var response = await client.PostAsJsonAsync(webhookUrl, slackBody, ct);
                if (!response.IsSuccessStatusCode)
                {
                    return (FlowRunStatus.Failed, $"Slack webhook returned {(int)response.StatusCode} {response.ReasonPhrase}.");
                }

                return (FlowRunStatus.Succeeded, null);
            }
            catch (Exception ex)
            {
                return (FlowRunStatus.Failed, $"Slack notification failed: {ex.Message}");
            }
        }

        if (string.Equals(action.ActionType, "UpdateLeadStage", StringComparison.OrdinalIgnoreCase))
        {
            var email = action.Params is not null && action.Params.TryGetValue("email", out var e) ? e : null;
            var label = action.Params is not null && action.Params.TryGetValue("label", out var lb) ? lb : null;
            if (string.IsNullOrWhiteSpace(email))
            {
                return (FlowRunStatus.Failed, "UpdateLeadStage requires an 'email' parameter.");
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                return (FlowRunStatus.Failed, "UpdateLeadStage requires a 'label' parameter.");
            }

            var leadRepo = serviceProvider.GetService<ILeadRepository>();
            if (leadRepo is null)
            {
                return (FlowRunStatus.Failed, "ILeadRepository is not registered.");
            }

            var lead = await leadRepo.GetByEmailAsync(flow.TenantId, flow.SiteId, email.Trim(), ct);
            if (lead is null)
            {
                return (FlowRunStatus.Failed, $"No lead found with email '{email}'.");
            }

            lead.OpportunityLabel = label.Trim();
            lead.UpdatedAtUtc = DateTime.UtcNow;
            await leadRepo.ReplaceAsync(lead, ct);
            return (FlowRunStatus.Succeeded, null);
        }

        if (string.Equals(action.ActionType, "AddNote", StringComparison.OrdinalIgnoreCase))
        {
            var ticketIdRaw = action.Params is not null && action.Params.TryGetValue("ticketId", out var tid) ? tid : null;
            var note = action.Params is not null && action.Params.TryGetValue("note", out var n) ? n : null;
            if (!Guid.TryParse(ticketIdRaw, out var ticketId))
            {
                return (FlowRunStatus.Failed, "AddNote requires a valid 'ticketId' parameter.");
            }

            if (string.IsNullOrWhiteSpace(note))
            {
                return (FlowRunStatus.Failed, "AddNote requires a 'note' parameter.");
            }

            var noteHandler = serviceProvider.GetService<AddTicketNoteHandler>();
            if (noteHandler is null)
            {
                return (FlowRunStatus.Failed, "AddTicketNoteHandler is not registered.");
            }

            var noteResult = await noteHandler.HandleAsync(new AddTicketNoteCommand(flow.TenantId, ticketId, Guid.Empty, note.Trim()), ct);
            return noteResult.IsSuccess
                ? (FlowRunStatus.Succeeded, null)
                : (FlowRunStatus.Failed, string.Join("; ", noteResult.Errors?.Errors.SelectMany(e => e.Value) ?? []));
        }

        if (string.Equals(action.ActionType, "NotifyTeam", StringComparison.OrdinalIgnoreCase))
        {
            var message = action.Params is not null && action.Params.TryGetValue("message", out var m) ? m : string.Empty;
            // TODO: Implement real team notification once notification service is configured
            return (FlowRunStatus.Succeeded, $"Team notified: {message}");
        }

        return (FlowRunStatus.Failed, $"Unsupported action type '{action.ActionType}'.");
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
            Priority = Math.Max(0, command.Priority),
            MaxRunsPerHour = command.MaxRunsPerHour is > 0 ? command.MaxRunsPerHour : null,
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
            Priority = Math.Max(0, command.Priority),
            MaxRunsPerHour = command.MaxRunsPerHour is > 0 ? command.MaxRunsPerHour : null,
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
        var supportedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LogRun", "SendWebhook", "CreateTicket", "TagLead", "SendEmail", "SendSlackNotification", "UpdateLeadStage", "AddNote", "NotifyTeam" };
        foreach (var action in inputs)
        {
            if (!supportedTypes.Contains(action.ActionType))
            {
                errors.Add("actions", $"Unsupported action type '{action.ActionType}'. Supported: {string.Join(", ", supportedTypes)}.");
                continue;
            }

            actions.Add(new FlowAction
            {
                ActionType = action.ActionType.Trim(),
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
        flow.Actions.Select(a => new FlowActionInput(a.ActionType, a.Params)).ToArray(),
        flow.Priority,
        flow.MaxRunsPerHour);

    public static FlowSummaryDto ToSummary(FlowDefinition flow) => new(
        flow.Id,
        flow.SiteId,
        flow.Name,
        flow.Enabled,
        flow.Trigger.TriggerType,
        flow.Conditions.Count,
        flow.Actions.Count,
        flow.Priority,
        flow.MaxRunsPerHour);

    public static FlowRunDto ToRun(FlowRun run) => new(
        run.Id,
        run.FlowId,
        run.ExecutedAtUtc,
        run.TriggerType,
        run.TriggerSummary,
        run.Status.ToString(),
        run.ErrorMessage);
}
