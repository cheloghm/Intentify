using Intentify.Modules.Engage.Application;
using Intentify.Modules.Intelligence.Application;
using Intentify.Shared.Abstractions;
using Intentify.Shared.AI;

namespace Intentify.Modules.Engage.Tests;

public sealed class AiDecisionGenerationServiceTests
{
    [Fact]
    public async Task GenerateAsync_ValidSuggestKnowledgeOutput_ReturnsValidDecision()
    {
        var aiOutput = """
        {
          "schemaVersion":"stage7.v1",
          "decisionId":"dec-001",
          "overallConfidence":0.81,
          "recommendations":[
            {
              "type":"SuggestKnowledge",
              "confidence":0.85,
              "rationale":"Knowledge chunk directly answers question.",
              "evidenceRefs":[{"source":"knowledge","referenceId":"chunk-1"}],
              "requiresApproval":false,
              "proposedCommand":null
            }
          ],
          "shouldFallback":false,
          "fallbackReason":null,
          "noActionMessage":null
        }
        """;

        var service = new AiDecisionGenerationService(new FakeChatCompletionClient(Result<string>.Success(aiOutput)));
        var result = await service.GenerateAsync(CreateContextBundle());

        Assert.Equal(AiDecisionValidationStatus.Valid, result.ValidationStatus);
        var recommendation = Assert.Single(result.Recommendations!);
        Assert.Equal(AiRecommendationType.SuggestKnowledge, recommendation.Type);
        Assert.False(result.ShouldFallback);
    }

    [Fact]
    public async Task GenerateAsync_ValidEscalateTicketOutput_ReturnsValidDecision()
    {
        var aiOutput = """
        {
          "schemaVersion":"stage7.v1",
          "decisionId":"dec-002",
          "overallConfidence":0.88,
          "recommendations":[
            {
              "type":"EscalateTicket",
              "confidence":0.9,
              "rationale":"Needs human review",
              "evidenceRefs":[{"source":"engage","referenceId":"msg-1"}],
              "requiresApproval":true,
              "proposedCommand":{"subject":"Escalate","description":"Handoff required"}
            }
          ],
          "shouldFallback":false,
          "fallbackReason":null,
          "noActionMessage":null
        }
        """;

        var service = new AiDecisionGenerationService(new FakeChatCompletionClient(Result<string>.Success(aiOutput)));
        var result = await service.GenerateAsync(CreateContextBundle());

        Assert.Equal(AiDecisionValidationStatus.Valid, result.ValidationStatus);
        var recommendation = Assert.Single(result.Recommendations!);
        Assert.Equal(AiRecommendationType.EscalateTicket, recommendation.Type);
        Assert.True(recommendation.RequiresApproval);
    }

    [Fact]
    public async Task GenerateAsync_MalformedOutput_ReturnsInvalidNoAction()
    {
        var service = new AiDecisionGenerationService(new FakeChatCompletionClient(Result<string>.Success("not-json-output")));
        var result = await service.GenerateAsync(CreateContextBundle());

        Assert.Equal(AiDecisionValidationStatus.Invalid, result.ValidationStatus);
        Assert.True(result.ShouldFallback);
        Assert.Equal(AiRecommendationType.NoAction, Assert.Single(result.Recommendations!).Type);
    }

    [Fact]
    public async Task GenerateAsync_UnknownAction_ReturnsInvalidNoAction()
    {
        var aiOutput = """
        {
          "schemaVersion":"stage7.v1",
          "decisionId":"dec-003",
          "overallConfidence":0.79,
          "recommendations":[
            {
              "type":"UnknownAction",
              "confidence":0.9,
              "rationale":"Unknown",
              "evidenceRefs":[],
              "requiresApproval":false
            }
          ],
          "shouldFallback":false,
          "fallbackReason":null,
          "noActionMessage":null
        }
        """;

        var service = new AiDecisionGenerationService(new FakeChatCompletionClient(Result<string>.Success(aiOutput)));
        var result = await service.GenerateAsync(CreateContextBundle());

        Assert.Equal(AiDecisionValidationStatus.Invalid, result.ValidationStatus);
        Assert.True(result.ShouldFallback);
        Assert.Equal(AiRecommendationType.NoAction, Assert.Single(result.Recommendations!).Type);
    }

    [Fact]
    public async Task GenerateAsync_AiUnavailable_ReturnsInvalidNoAction()
    {
        var service = new AiDecisionGenerationService(new FakeChatCompletionClient(Result<string>.Failure(new Error("AI_UNAVAILABLE", "down"))));
        var result = await service.GenerateAsync(CreateContextBundle());

        Assert.Equal(AiDecisionValidationStatus.Invalid, result.ValidationStatus);
        Assert.True(result.ShouldFallback);
        Assert.Equal(AiRecommendationType.NoAction, Assert.Single(result.Recommendations!).Type);
    }

    [Fact]
    public async Task GenerateAsync_LowOverallConfidence_ReturnsSafeNoActionFallback()
    {
        var aiOutput = """
        {
          "schemaVersion":"stage7.v1",
          "decisionId":"dec-004",
          "overallConfidence":0.20,
          "recommendations":[
            {
              "type":"SuggestKnowledge",
              "confidence":0.22,
              "rationale":"Weak confidence",
              "evidenceRefs":[{"source":"knowledge","referenceId":"chunk-1"}],
              "requiresApproval":false
            }
          ],
          "shouldFallback":false,
          "fallbackReason":null,
          "noActionMessage":null
        }
        """;

        var service = new AiDecisionGenerationService(new FakeChatCompletionClient(Result<string>.Success(aiOutput)));
        var result = await service.GenerateAsync(CreateContextBundle());

        Assert.Equal(AiDecisionValidationStatus.Valid, result.ValidationStatus);
        Assert.True(result.ShouldFallback);
        Assert.Equal("LowConfidence", result.FallbackReason);
        Assert.Equal(AiRecommendationType.NoAction, Assert.Single(result.Recommendations!).Type);
    }

    private static VisitorContextBundle CreateContextBundle()
    {
        var contextRef = new AiDecisionContextRef(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        return new VisitorContextBundle(
            contextRef,
            ["collector-1"],
            new KnowledgeRetrievalSnapshot(
                "What is return policy?",
                3,
                [
                    new RetrievedKnowledgeChunkSummary(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        0,
                        4,
                        "Return policy is 30 days.")
                ]),
            new VisitorProfileSummary(
                contextRef.VisitorId!.Value,
                DateTime.UtcNow.AddDays(-10),
                DateTime.UtcNow,
                4,
                22,
                "visitor@example.com",
                "Visitor",
                null,
                "en",
                "web"),
            [
                new TimelineItemSummary(DateTime.UtcNow.AddMinutes(-5), "pageview", "/pricing", "collector-1", null)
            ],
            new EngageSessionSummary(
                contextRef.EngageSessionId!.Value,
                DateTime.UtcNow.AddMinutes(-10),
                DateTime.UtcNow,
                [new EngageMessageSummary("user", "What is your return policy?", DateTime.UtcNow.AddMinutes(-2), null, 0)]),
            [
                new TicketSummary(Guid.NewGuid(), "Return policy clarification", "Open", DateTime.UtcNow.AddHours(-3), DateTime.UtcNow, contextRef.EngageSessionId, contextRef.VisitorId)
            ],
            [
                new PromoInteractionSummary(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddHours(-5), "visitor@example.com", "Visitor", null)
            ],
            new IntelligenceSnapshot(
                "general",
                "US",
                "7d",
                "Google",
                DateTime.UtcNow,
                1,
                new IntelligenceDashboardSummaryResponse(1, 0.7, 0.7),
                [new IntelligenceDashboardTrendItemResponse("return policy", 0.7, 1, "Google")]));
    }

    private sealed class FakeChatCompletionClient(Result<string> nextResult) : IChatCompletionClient
    {
        public Task<Result<string>> CompleteAsync(string prompt, CancellationToken ct) => Task.FromResult(nextResult);
    }
}
