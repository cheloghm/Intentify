using System.Text;
using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Promos.Application;
using Intentify.Modules.Tickets.Application;
using Intentify.Modules.Visitors.Application;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Engage.Application;

public sealed class RecommendationExecutor
{
    private static readonly IReadOnlyCollection<AiRecommendationType> DefaultAllowlistedActions = Enum
        .GetValues<AiRecommendationType>()
        .ToArray();

    private readonly CreateTicketHandler _createTicketHandler;
    private readonly ListTicketsHandler _listTicketsHandler;
    private readonly IEngageChatSessionRepository _chatSessionRepository;
    private readonly GetVisitorDetailHandler _getVisitorDetailHandler;
    private readonly IPromoRepository _promoRepository;
    private readonly IKnowledgeSourceRepository _knowledgeSourceRepository;

    public RecommendationExecutor(
        CreateTicketHandler createTicketHandler,
        ListTicketsHandler listTicketsHandler,
        IEngageChatSessionRepository chatSessionRepository,
        GetVisitorDetailHandler getVisitorDetailHandler,
        IPromoRepository promoRepository,
        IKnowledgeSourceRepository knowledgeSourceRepository)
    {
        _createTicketHandler = createTicketHandler;
        _listTicketsHandler = listTicketsHandler;
        _chatSessionRepository = chatSessionRepository;
        _getVisitorDetailHandler = getVisitorDetailHandler;
        _promoRepository = promoRepository;
        _knowledgeSourceRepository = knowledgeSourceRepository;
    }

    public async Task<OperationResult<RecommendationExecutionResult>> ExecuteAsync(
        ExecuteRecommendationCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = await ValidateCommandAsync(command, cancellationToken);
        if (validationErrors.HasErrors)
        {
            return OperationResult<RecommendationExecutionResult>.ValidationFailed(validationErrors);
        }

        var type = command.Recommendation.Type;

        if (type == AiRecommendationType.NoAction)
        {
            return OperationResult<RecommendationExecutionResult>.Success(
                new RecommendationExecutionResult(RecommendationExecutionStatus.NoOp, "No action executed.", null));
        }

        if (type == AiRecommendationType.TagVisitor)
        {
            return OperationResult<RecommendationExecutionResult>.Success(
                new RecommendationExecutionResult(RecommendationExecutionStatus.Rejected, "TagVisitor is not executable in current platform scope.", "UnsupportedAction"));
        }

        if (type == AiRecommendationType.SuggestPromo)
        {
            var promoPublicKey = command.Recommendation.TargetRefs?.PromoPublicKey;
            return OperationResult<RecommendationExecutionResult>.Success(
                new RecommendationExecutionResult(RecommendationExecutionStatus.DisplayOnly, "Promo suggestion available for display only.", null, null, promoPublicKey));
        }

        if (type == AiRecommendationType.SuggestKnowledge)
        {
            return OperationResult<RecommendationExecutionResult>.Success(
                new RecommendationExecutionResult(RecommendationExecutionStatus.DisplayOnly, "Knowledge suggestion available for display only.", null));
        }

        if (type == AiRecommendationType.EscalateTicket)
        {
            return await ExecuteEscalateTicketAsync(command, cancellationToken);
        }

        if (type == AiRecommendationType.SuggestKnowledgeUpdate)
        {
            return await ExecuteKnowledgeBacklogTicketAsync(command, "Stage7 knowledge update suggestion", cancellationToken);
        }

        if (type == AiRecommendationType.NotifyClientKnowledgeGap)
        {
            return await ExecuteKnowledgeBacklogTicketAsync(command, "Stage7 knowledge gap notification", cancellationToken);
        }

        return OperationResult<RecommendationExecutionResult>.Success(
            new RecommendationExecutionResult(RecommendationExecutionStatus.Rejected, "Recommendation type is not executable.", "UnsupportedAction"));
    }

    private async Task<ValidationErrors> ValidateCommandAsync(ExecuteRecommendationCommand command, CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();

        if (command.TenantId == Guid.Empty)
        {
            errors.Add("tenantId", "Tenant id is required.");
        }

        if (command.SiteId == Guid.Empty)
        {
            errors.Add("siteId", "Site id is required.");
        }

        if (command.ContextRef.TenantId != command.TenantId)
        {
            errors.Add("contextRef.tenantId", "Context tenant id must match command tenant id.");
        }

        if (command.ContextRef.SiteId != command.SiteId)
        {
            errors.Add("contextRef.siteId", "Context site id must match command site id.");
        }

        if (!Enum.IsDefined(command.Recommendation.Type))
        {
            errors.Add("recommendation.type", "Recommendation type is invalid.");
            return errors;
        }

        var allowlistedActions = command.AllowlistedActions is null || command.AllowlistedActions.Count == 0
            ? DefaultAllowlistedActions
            : command.AllowlistedActions;

        if (!allowlistedActions.Contains(command.Recommendation.Type))
        {
            errors.Add("recommendation.type", "Recommendation type is not allowlisted.");
        }

        if (IsMutating(command.Recommendation.Type))
        {
            if (!command.Approved)
            {
                errors.Add("approved", "Mutating recommendation execution requires explicit approval.");
            }

            if (!command.Recommendation.RequiresApproval)
            {
                errors.Add("recommendation.requiresApproval", "Mutating recommendation must require approval.");
            }
        }

        if (command.ContextRef.EngageSessionId is { } engageSessionId)
        {
            var session = await _chatSessionRepository.GetByIdAsync(engageSessionId, cancellationToken);
            if (session is null || session.TenantId != command.TenantId || session.SiteId != command.SiteId)
            {
                errors.Add("contextRef.engageSessionId", "Engage session does not belong to tenant/site scope.");
            }
        }

        var targetVisitorId = command.Recommendation.TargetRefs?.VisitorId ?? command.ContextRef.VisitorId;
        if (targetVisitorId is { } visitorId)
        {
            var visitor = await _getVisitorDetailHandler.HandleAsync(
                new GetVisitorDetailQuery(command.TenantId, command.SiteId, visitorId),
                cancellationToken);
            if (visitor is null)
            {
                errors.Add("targetRefs.visitorId", "Visitor does not belong to tenant/site scope.");
            }
        }

        if (command.Recommendation.Type == AiRecommendationType.SuggestPromo && command.Recommendation.TargetRefs?.PromoId is { } promoId)
        {
            var promo = await _promoRepository.GetByIdAsync(command.TenantId, promoId, cancellationToken);
            if (promo is null || promo.SiteId != command.SiteId)
            {
                errors.Add("targetRefs.promoId", "Promo does not belong to tenant/site scope.");
            }
        }

        if (command.Recommendation.Type == AiRecommendationType.SuggestKnowledgeUpdate)
        {
            var knowledgeSourceId = command.Recommendation.TargetRefs?.KnowledgeSourceId;
            if (knowledgeSourceId is null || knowledgeSourceId == Guid.Empty)
            {
                errors.Add("targetRefs.knowledgeSourceId", "Knowledge source id is required for SuggestKnowledgeUpdate.");
            }
            else
            {
                var source = await _knowledgeSourceRepository.GetSourceByIdAsync(command.TenantId, knowledgeSourceId.Value, cancellationToken);
                if (source is null || source.SiteId != command.SiteId)
                {
                    errors.Add("targetRefs.knowledgeSourceId", "Knowledge source does not belong to tenant/site scope.");
                }
            }
        }

        return errors;
    }

    private async Task<OperationResult<RecommendationExecutionResult>> ExecuteEscalateTicketAsync(
        ExecuteRecommendationCommand command,
        CancellationToken cancellationToken)
    {
        var subject = ReadCommandValue(command.Recommendation.ProposedCommand, "subject")
            ?? "Stage7 escalation recommendation";

        var description = ReadCommandValue(command.Recommendation.ProposedCommand, "description")
            ?? command.Recommendation.Rationale;

        if (string.IsNullOrWhiteSpace(description))
        {
            description = "Escalation recommended by Stage7.";
        }

        var duplicate = await FindDuplicateTicketAsync(command, subject, cancellationToken);
        if (duplicate is { } existingTicketId)
        {
            return OperationResult<RecommendationExecutionResult>.Success(
                new RecommendationExecutionResult(
                    RecommendationExecutionStatus.Rejected,
                    "Duplicate escalation suppressed.",
                    "DuplicateSuppressed",
                    existingTicketId));
        }

        var createResult = await _createTicketHandler.HandleAsync(
            new CreateTicketCommand(
                command.TenantId,
                command.SiteId,
                command.Recommendation.TargetRefs?.VisitorId ?? command.ContextRef.VisitorId,
                command.ContextRef.EngageSessionId,
                subject,
                description,
                null),
            cancellationToken);

        if (createResult.Status != OperationStatus.Success || createResult.Value is null)
        {
            return createResult.Status == OperationStatus.ValidationFailed && createResult.Errors is not null
                ? OperationResult<RecommendationExecutionResult>.ValidationFailed(createResult.Errors)
                : OperationResult<RecommendationExecutionResult>.Error();
        }

        return OperationResult<RecommendationExecutionResult>.Success(
            new RecommendationExecutionResult(
                RecommendationExecutionStatus.Executed,
                "Escalation ticket created.",
                null,
                createResult.Value.Id));
    }

    private async Task<OperationResult<RecommendationExecutionResult>> ExecuteKnowledgeBacklogTicketAsync(
        ExecuteRecommendationCommand command,
        string subjectPrefix,
        CancellationToken cancellationToken)
    {
        var detailsBuilder = new StringBuilder();
        detailsBuilder.AppendLine(command.Recommendation.Rationale);

        if (command.Recommendation.EvidenceRefs is { Count: > 0 })
        {
            foreach (var evidence in command.Recommendation.EvidenceRefs.Take(5))
            {
                detailsBuilder.AppendLine($"Evidence: {evidence.Source}::{evidence.ReferenceId}");
            }
        }

        var description = detailsBuilder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            description = "Stage7 suggested a knowledge follow-up.";
        }

        var subject = subjectPrefix;

        var duplicate = await FindDuplicateTicketAsync(command, subject, cancellationToken);
        if (duplicate is { } existingTicketId)
        {
            return OperationResult<RecommendationExecutionResult>.Success(
                new RecommendationExecutionResult(
                    RecommendationExecutionStatus.Rejected,
                    "Duplicate backlog ticket suppressed.",
                    "DuplicateSuppressed",
                    existingTicketId));
        }

        var createResult = await _createTicketHandler.HandleAsync(
            new CreateTicketCommand(
                command.TenantId,
                command.SiteId,
                command.Recommendation.TargetRefs?.VisitorId ?? command.ContextRef.VisitorId,
                command.ContextRef.EngageSessionId,
                subject,
                description,
                null),
            cancellationToken);

        if (createResult.Status != OperationStatus.Success || createResult.Value is null)
        {
            return createResult.Status == OperationStatus.ValidationFailed && createResult.Errors is not null
                ? OperationResult<RecommendationExecutionResult>.ValidationFailed(createResult.Errors)
                : OperationResult<RecommendationExecutionResult>.Error();
        }

        return OperationResult<RecommendationExecutionResult>.Success(
            new RecommendationExecutionResult(
                RecommendationExecutionStatus.Executed,
                "Backlog ticket created.",
                null,
                createResult.Value.Id));
    }

    private async Task<Guid?> FindDuplicateTicketAsync(
        ExecuteRecommendationCommand command,
        string subject,
        CancellationToken cancellationToken)
    {
        var engageSessionId = command.ContextRef.EngageSessionId;
        var visitorId = command.Recommendation.TargetRefs?.VisitorId ?? command.ContextRef.VisitorId;

        var tickets = await _listTicketsHandler.HandleAsync(
            new ListTicketsQuery(command.TenantId, command.SiteId, visitorId, engageSessionId, 1, 50),
            cancellationToken);

        var duplicate = tickets.FirstOrDefault(item =>
            string.Equals(item.Subject.Trim(), subject.Trim(), StringComparison.OrdinalIgnoreCase));

        return duplicate?.Id;
    }

    private static bool IsMutating(AiRecommendationType type)
        => type is AiRecommendationType.EscalateTicket
            or AiRecommendationType.TagVisitor
            or AiRecommendationType.SuggestKnowledgeUpdate
            or AiRecommendationType.NotifyClientKnowledgeGap;

    private static string? ReadCommandValue(IReadOnlyDictionary<string, string>? values, string key)
    {
        if (values is null || !values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
