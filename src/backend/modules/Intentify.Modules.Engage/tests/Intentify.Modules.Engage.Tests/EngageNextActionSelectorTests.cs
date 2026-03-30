using Intentify.Modules.Engage.Application;
using Intentify.Modules.Engage.Domain;

namespace Intentify.Modules.Engage.Tests;

public sealed class EngageNextActionSelectorTests
{
    private static readonly EngageNextActionSelector Selector = new(new EngageConversationPolicy());

    [Fact]
    public void Select_InitialTurn_ReturnsGreetingAction()
    {
        var context = CreateContext(
            session: new EngageChatSession { ConversationState = "Greeting" },
            recentMessages:
            [
                new EngageChatMessage { Role = "user", Content = "hello", CreatedAtUtc = DateTime.UtcNow }
            ],
            analysis: new EngageAnalysisSummary("Greeting", false, false, 0.9m, true));

        var decision = Selector.Select(context);

        Assert.Equal(EngageNextAction.Greeting, decision.Action);
        Assert.Equal("Greeting", decision.TargetState);
    }

    [Fact]
    public void Select_CaptureSignal_ReturnsCaptureAction()
    {
        var session = new EngageChatSession
        {
            ConversationState = "Discover",
            CaptureGoal = "new website",
            CaptureType = "retail",
            CaptureLocation = "Austin"
        };

        var context = CreateContext(
            session,
            recentMessages:
            [
                new EngageChatMessage { Role = "assistant", Content = "What kind of business is this for?", CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1) },
                new EngageChatMessage { Role = "user", Content = "retail store", CreatedAtUtc = DateTime.UtcNow }
            ],
            analysis: new EngageAnalysisSummary("CaptureLead", true, false, 0.8m, false),
            userMessage: "retail store");

        var decision = Selector.Select(context);

        Assert.Equal(EngageNextAction.AskCaptureQuestion, decision.Action);
        Assert.Equal("CaptureLead", decision.TargetState);
    }

    [Fact]
    public void Select_Default_ReturnsDiscoveryAction()
    {
        var context = CreateContext(
            session: new EngageChatSession { ConversationState = "Discover" },
            recentMessages:
            [
                new EngageChatMessage { Role = "assistant", Content = "Hi, what are you trying to achieve?", CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1) },
                new EngageChatMessage { Role = "user", Content = "I need help", CreatedAtUtc = DateTime.UtcNow }
            ],
            analysis: new EngageAnalysisSummary("Discover", true, false, 0.9m, false),
            userMessage: "I need help");

        var decision = Selector.Select(context);

        Assert.Equal(EngageNextAction.AskDiscoveryQuestion, decision.Action);
        Assert.Equal("Discover", decision.TargetState);
    }

    private static EngageConversationContext CreateContext(
        EngageChatSession session,
        IReadOnlyCollection<EngageChatMessage> recentMessages,
        EngageAnalysisSummary analysis,
        string userMessage = "hello")
    {
        return new EngageConversationContext(
            session,
            recentMessages,
            userMessage,
            new AiDecisionContract("stage7.v1", "d1", null, 0.9m, [], AiDecisionValidationStatus.Valid, [], null, false, null, null),
            null,
            string.Empty,
            analysis);
    }
}
