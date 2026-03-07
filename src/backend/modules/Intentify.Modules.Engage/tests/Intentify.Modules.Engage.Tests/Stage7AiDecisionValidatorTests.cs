using Intentify.Modules.Engage.Application;

namespace Intentify.Modules.Engage.Tests;

public sealed class Stage7AiDecisionValidatorTests
{
    [Fact]
    public void ValidateAndNormalize_ValidNoActionDecision_ReturnsValid()
    {
        var decision = CreateBaseDecision(
            recommendations: [],
            shouldFallback: true,
            noActionMessage: "No safe action.");

        var validated = Stage7AiDecisionValidator.ValidateAndNormalize(decision);

        Assert.Equal(AiDecisionValidationStatus.Valid, validated.ValidationStatus);
        Assert.False(validated.ValidationErrors?.Any() == true);
        Assert.Empty(validated.Recommendations!);
    }

    [Fact]
    public void ValidateAndNormalize_ValidSuggestPromoDecision_ReturnsValid()
    {
        var recommendation = new AiRecommendation(
            AiRecommendationType.SuggestPromo,
            0.9m,
            "Promo matches visitor intent.",
            [new AiEvidenceRef("knowledge", Guid.NewGuid().ToString("N"))],
            new AiTargetRefs(PromoPublicKey: "promo-public-key-1"),
            false,
            new Dictionary<string, string> { ["promoPublicKey"] = "promo-public-key-1" });

        var decision = CreateBaseDecision(recommendations: [recommendation]);

        var validated = Stage7AiDecisionValidator.ValidateAndNormalize(decision);

        Assert.Equal(AiDecisionValidationStatus.Valid, validated.ValidationStatus);
        Assert.False(validated.ValidationErrors?.Any() == true);
    }

    [Fact]
    public void ValidateAndNormalize_InvalidUnknownAction_ReturnsInvalidNoAction()
    {
        var recommendation = new AiRecommendation(
            (AiRecommendationType)999,
            0.7m,
            "Unknown",
            [],
            null,
            false,
            null);

        var decision = CreateBaseDecision(recommendations: [recommendation]);

        var validated = Stage7AiDecisionValidator.ValidateAndNormalize(decision);

        Assert.Equal(AiDecisionValidationStatus.Invalid, validated.ValidationStatus);
        Assert.True(validated.ShouldFallback);
        Assert.Equal(AiRecommendationType.NoAction, Assert.Single(validated.Recommendations!).Type);
    }

    [Fact]
    public void ValidateAndNormalize_InvalidConfidenceOutOfRange_ReturnsInvalidNoAction()
    {
        var recommendation = new AiRecommendation(
            AiRecommendationType.SuggestKnowledge,
            1.5m,
            "Out of range confidence.",
            [new AiEvidenceRef("knowledge", Guid.NewGuid().ToString("N"))],
            null,
            false,
            null);

        var decision = CreateBaseDecision(recommendations: [recommendation], overallConfidence: 1.2m);

        var validated = Stage7AiDecisionValidator.ValidateAndNormalize(decision);

        Assert.Equal(AiDecisionValidationStatus.Invalid, validated.ValidationStatus);
        Assert.True(validated.ShouldFallback);
        Assert.Contains(validated.ValidationErrors!, message => message.Contains("confidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAndNormalize_InvalidMissingContext_ReturnsInvalidNoAction()
    {
        var decision = new AiDecisionContract(
            SchemaVersion: "stage7.v1",
            DecisionId: Guid.NewGuid().ToString("N"),
            ContextRef: null,
            OverallConfidence: 0.8m,
            Recommendations: [],
            ValidationStatus: AiDecisionValidationStatus.Valid,
            ValidationErrors: [],
            AllowlistedActions: Enum.GetValues<AiRecommendationType>(),
            ShouldFallback: true,
            FallbackReason: "NoContext",
            NoActionMessage: "No context");

        var validated = Stage7AiDecisionValidator.ValidateAndNormalize(decision);

        Assert.Equal(AiDecisionValidationStatus.Invalid, validated.ValidationStatus);
        Assert.True(validated.ShouldFallback);
        Assert.Contains(validated.ValidationErrors!, message => message.Contains("Context reference", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAndNormalize_InvalidMutatingActionWithoutApproval_ReturnsInvalidNoAction()
    {
        var recommendation = new AiRecommendation(
            AiRecommendationType.EscalateTicket,
            0.85m,
            "Needs escalation.",
            [new AiEvidenceRef("engage", Guid.NewGuid().ToString("N"))],
            null,
            false,
            new Dictionary<string, string>
            {
                ["subject"] = "Escalate",
                ["description"] = "Needs human handoff"
            });

        var decision = CreateBaseDecision(recommendations: [recommendation]);

        var validated = Stage7AiDecisionValidator.ValidateAndNormalize(decision);

        Assert.Equal(AiDecisionValidationStatus.Invalid, validated.ValidationStatus);
        Assert.True(validated.ShouldFallback);
        Assert.Contains(validated.ValidationErrors!, message => message.Contains("require approval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAndNormalize_EmptyRecommendationsWithoutFallback_ReturnsInvalidNoAction()
    {
        var decision = CreateBaseDecision(
            recommendations: [],
            shouldFallback: false,
            noActionMessage: null);

        var validated = Stage7AiDecisionValidator.ValidateAndNormalize(decision);

        Assert.Equal(AiDecisionValidationStatus.Invalid, validated.ValidationStatus);
        Assert.True(validated.ShouldFallback);
        Assert.Equal(AiRecommendationType.NoAction, Assert.Single(validated.Recommendations!).Type);
    }

    private static AiDecisionContract CreateBaseDecision(
        IReadOnlyCollection<AiRecommendation>? recommendations,
        decimal overallConfidence = 0.8m,
        bool shouldFallback = false,
        string? noActionMessage = "No safe action.")
    {
        return new AiDecisionContract(
            SchemaVersion: "stage7.v1",
            DecisionId: Guid.NewGuid().ToString("N"),
            ContextRef: new AiDecisionContextRef(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            OverallConfidence: overallConfidence,
            Recommendations: recommendations,
            ValidationStatus: AiDecisionValidationStatus.Valid,
            ValidationErrors: [],
            AllowlistedActions: Enum.GetValues<AiRecommendationType>(),
            ShouldFallback: shouldFallback,
            FallbackReason: shouldFallback ? "NoAction" : null,
            NoActionMessage: noActionMessage);
    }
}
