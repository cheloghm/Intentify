using Intentify.Shared.Validation;

namespace Intentify.Modules.Engage.Application;

public static class AiDecisionValidator
{
    public const string DefaultFallbackReason = "InvalidContract";
    public const string DefaultNoActionMessage = "No safe action available.";

    private static readonly IReadOnlyCollection<AiRecommendationType> DefaultAllowlistedActions = Enum
        .GetValues<AiRecommendationType>()
        .ToArray();

    public static AiDecisionContract ValidateAndNormalize(AiDecisionContract decision)
    {
        var errors = new ValidationErrors();

        if (string.IsNullOrWhiteSpace(decision.SchemaVersion))
        {
            errors.Add("schemaVersion", "Schema version is required.");
        }

        if (string.IsNullOrWhiteSpace(decision.DecisionId))
        {
            errors.Add("decisionId", "Decision id is required.");
        }

        if (decision.ContextRef is null)
        {
            errors.Add("contextRef", "Context reference is required.");
        }
        else
        {
            if (decision.ContextRef.TenantId == Guid.Empty)
            {
                errors.Add("contextRef.tenantId", "Tenant id is required.");
            }

            if (decision.ContextRef.SiteId == Guid.Empty)
            {
                errors.Add("contextRef.siteId", "Site id is required.");
            }
        }

        if (decision.OverallConfidence < 0m || decision.OverallConfidence > 1m)
        {
            errors.Add("overallConfidence", "Overall confidence must be between 0 and 1.");
        }

        var allowlistedActions = (decision.AllowlistedActions is null || decision.AllowlistedActions.Count == 0)
            ? DefaultAllowlistedActions
            : decision.AllowlistedActions;

        foreach (var action in allowlistedActions)
        {
            if (!Enum.IsDefined(action))
            {
                errors.Add("allowlistedActions", $"Unknown allowlisted action '{action}'.");
            }
        }

        if (decision.Recommendations is null)
        {
            errors.Add("recommendations", "Recommendations are required (can be empty).");
        }
        else
        {
            var index = 0;
            foreach (var recommendation in decision.Recommendations)
            {
                ValidateRecommendation(index, recommendation, decision.ContextRef, allowlistedActions, errors);
                index++;
            }

            if (decision.Recommendations.Count == 0 && !decision.ShouldFallback)
            {
                errors.Add("shouldFallback", "Fallback must be enabled when recommendations are empty.");
            }

            if (decision.Recommendations.Count == 0 && string.IsNullOrWhiteSpace(decision.NoActionMessage))
            {
                errors.Add("noActionMessage", "No-action message is required when recommendations are empty.");
            }
        }

        if (!errors.HasErrors)
        {
            return decision with
            {
                ValidationStatus = AiDecisionValidationStatus.Valid,
                ValidationErrors = []
            };
        }

        return CreateInvalidNoActionDecision(decision, errors.Errors.Values.SelectMany(values => values).ToArray(), allowlistedActions);
    }

    private static void ValidateRecommendation(
        int index,
        AiRecommendation recommendation,
        AiDecisionContextRef? context,
        IReadOnlyCollection<AiRecommendationType> allowlistedActions,
        ValidationErrors errors)
    {
        if (!Enum.IsDefined(recommendation.Type))
        {
            errors.Add($"recommendations[{index}].type", $"Unknown recommendation type '{recommendation.Type}'.");
            return;
        }

        if (!allowlistedActions.Contains(recommendation.Type))
        {
            errors.Add($"recommendations[{index}].type", $"Recommendation type '{recommendation.Type}' is not allowlisted.");
        }

        if (recommendation.Confidence < 0m || recommendation.Confidence > 1m)
        {
            errors.Add($"recommendations[{index}].confidence", "Recommendation confidence must be between 0 and 1.");
        }

        if (string.IsNullOrWhiteSpace(recommendation.Rationale))
        {
            errors.Add($"recommendations[{index}].rationale", "Rationale is required.");
        }

        if (IsMutatingAction(recommendation.Type) && !recommendation.RequiresApproval)
        {
            errors.Add($"recommendations[{index}].requiresApproval", "Mutating actions must require approval.");
        }

        switch (recommendation.Type)
        {
            case AiRecommendationType.SuggestPromo:
                if (recommendation.TargetRefs is null
                    || (recommendation.TargetRefs.PromoId is null && string.IsNullOrWhiteSpace(recommendation.TargetRefs.PromoPublicKey)))
                {
                    errors.Add($"recommendations[{index}].targetRefs", "SuggestPromo requires promo target reference.");
                }
                break;

            case AiRecommendationType.SuggestKnowledge:
                if (recommendation.EvidenceRefs is null || recommendation.EvidenceRefs.Count == 0)
                {
                    errors.Add($"recommendations[{index}].evidenceRefs", "SuggestKnowledge requires at least one evidence reference.");
                }
                break;

            case AiRecommendationType.TagVisitor:
                var resolvedVisitorId = recommendation.TargetRefs?.VisitorId ?? context?.VisitorId;
                if (resolvedVisitorId is null || resolvedVisitorId == Guid.Empty)
                {
                    errors.Add($"recommendations[{index}].targetRefs.visitorId", "TagVisitor requires a visitor reference.");
                }
                break;

            case AiRecommendationType.SuggestKnowledgeUpdate:
                if (recommendation.TargetRefs?.KnowledgeSourceId is null || recommendation.TargetRefs.KnowledgeSourceId == Guid.Empty)
                {
                    errors.Add($"recommendations[{index}].targetRefs.knowledgeSourceId", "SuggestKnowledgeUpdate requires knowledge source reference.");
                }
                break;

            case AiRecommendationType.NotifyClientKnowledgeGap:
                if (recommendation.EvidenceRefs is null || recommendation.EvidenceRefs.Count == 0)
                {
                    errors.Add($"recommendations[{index}].evidenceRefs", "NotifyClientKnowledgeGap requires at least one evidence reference.");
                }
                break;
        }
    }

    private static bool IsMutatingAction(AiRecommendationType type)
        => type is AiRecommendationType.EscalateTicket
            or AiRecommendationType.TagVisitor
            or AiRecommendationType.SuggestKnowledgeUpdate
            or AiRecommendationType.NotifyClientKnowledgeGap;

    private static AiDecisionContract CreateInvalidNoActionDecision(
        AiDecisionContract original,
        IReadOnlyCollection<string> validationErrors,
        IReadOnlyCollection<AiRecommendationType> allowlistedActions)
    {
        var noActionRecommendation = new AiRecommendation(
            AiRecommendationType.NoAction,
            0m,
            "Validation failed. Returning safe no-action decision.",
            [],
            null,
            false,
            null);

        return original with
        {
            ValidationStatus = AiDecisionValidationStatus.Invalid,
            ValidationErrors = validationErrors,
            Recommendations = [noActionRecommendation],
            AllowlistedActions = allowlistedActions,
            ShouldFallback = true,
            FallbackReason = string.IsNullOrWhiteSpace(original.FallbackReason) ? DefaultFallbackReason : original.FallbackReason,
            NoActionMessage = string.IsNullOrWhiteSpace(original.NoActionMessage) ? DefaultNoActionMessage : original.NoActionMessage
        };
    }
}
