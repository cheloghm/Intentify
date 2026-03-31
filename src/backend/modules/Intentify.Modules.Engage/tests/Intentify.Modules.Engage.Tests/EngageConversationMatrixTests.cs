using Intentify.Modules.Engage.Application;
using Intentify.Modules.Engage.Domain;

namespace Intentify.Modules.Engage.Tests;

public sealed class EngageConversationMatrixTests
{
    private static readonly EngageConversationPolicy Policy = new();
    private static readonly EngageNextActionSelector Selector = new(Policy);

    [Fact]
    public void Matrix_GreetingTypo_InitialTurn_GoesGreeting()
    {
        var ctx = CreateContext("helo", new EngageAnalysisSummary("Greeting", false, false, 0.8m, true));
        var action = Selector.Select(ctx);
        Assert.Equal(EngageNextAction.Greeting, action.Action);
    }

    [Fact]
    public void Matrix_ServicesQuestion_WithKnowledge_GoesFactual()
    {
        var ctx = CreateContext(
            "What services do you offer?",
            new EngageAnalysisSummary("Discover", false, false, 0.8m, false),
            knowledge: "We offer HVAC repair.");

        var action = Selector.Select(ctx);

        Assert.Equal(EngageNextAction.AnswerFactual, action.Action);
    }

    [Fact]
    public void Matrix_TypoServicesQuestion_WithKnowledge_GoesFactual()
    {
        var session = new EngageChatSession { ConversationState = "Discover", PendingCaptureMode = "Support", CaptureContext = "login issue" };
        var ctx = CreateContext(
            "yur services?",
            new EngageAnalysisSummary("Discover", false, false, 0.8m, false),
            session,
            knowledge: "We offer HVAC repair.");

        var action = Selector.Select(ctx);

        Assert.Equal(EngageNextAction.AnswerFactual, action.Action);
    }

    [Fact]
    public void Matrix_DiscoveryContinuation_ShortReply_UpdatesLocation()
    {
        var session = new EngageChatSession { CaptureGoal = "new site", CaptureType = "clinic" };
        var changed = Policy.TryApplyStageContinuation(session, "Dallas", "What location should we plan for?");

        Assert.True(changed);
        Assert.Equal("Dallas", session.CaptureLocation);
    }

    [Fact]
    public void Matrix_CaptureContinuation_PrefersCaptureAction()
    {
        var session = new EngageChatSession { ConversationState = "CaptureLead", PendingCaptureMode = "Commercial" };
        var ctx = CreateContext("5k", new EngageAnalysisSummary("CaptureLead", true, false, 0.8m, false), session);

        var action = Selector.Select(ctx);

        Assert.Equal(EngageNextAction.AskCaptureQuestion, action.Action);
    }

    [Fact]
    public void Matrix_ObjectionHandling_IsDeterministic()
    {
        var ctx = CreateContext("too expensive", new EngageAnalysisSummary("Discover", false, false, 0.8m, false));
        var action = Selector.Select(ctx);
        Assert.Equal(EngageNextAction.HandleNarrowObjection, action.Action);
    }

    [Fact]
    public void Matrix_SupportEscalation_HasPriority()
    {
        var ctx = CreateContext("I need human support", new EngageAnalysisSummary("Discover", false, false, 0.8m, false));
        var action = Selector.Select(ctx);
        Assert.Equal(EngageNextAction.EscalateSupport, action.Action);
    }

    [Fact]
    public void Matrix_CloseSignal_PrefersNaturalClose()
    {
        var ctx = CreateContext("thanks that's all", new EngageAnalysisSummary("Discover", false, false, 0.8m, false));
        var action = Selector.Select(ctx);
        Assert.Equal(EngageNextAction.CloseConversation, action.Action);
    }

    [Fact]
    public void Matrix_AlreadyToldYou_RecoverySignalDetected()
    {
        Assert.True(Policy.IsContextRecoverySignal("I already told you that"));
    }

    [Fact]
    public void Matrix_ShortReply_ExtractsContactEmailAndMethod()
    {
        var session = new EngageChatSession();
        var changed = Policy.TryMergeShortReplySlots(session, "alex@example.com", "How should we reach you?");

        Assert.True(changed);
        Assert.Equal("alex@example.com", session.CapturedEmail);
        Assert.Equal("Email", session.CapturedPreferredContactMethod);
    }

    [Fact]
    public void Matrix_BuildNextDiscoveryQuestion_UsesCurrentSequence()
    {
        var session = new EngageChatSession();

        var first = Policy.BuildNextDiscoveryQuestion(session);
        Assert.Equal("What are you trying to achieve first?", first);

        session.CaptureGoal = "launch a new site";
        var second = Policy.BuildNextDiscoveryQuestion(session);
        Assert.Equal("What kind of business is this for?", second);

        session.CaptureType = "hvac";
        var third = Policy.BuildNextDiscoveryQuestion(session);
        Assert.Equal("What location should we plan for?", third);

        session.CaptureLocation = "Dallas";
        var fourth = Policy.BuildNextDiscoveryQuestion(session);
        Assert.Equal("Any key constraints like budget or timeline?", fourth);
    }

    private static EngageConversationContext CreateContext(
        string userMessage,
        EngageAnalysisSummary analysis,
        EngageChatSession? session = null,
        string knowledge = "")
    {
        return new EngageConversationContext(
            session ?? new EngageChatSession { ConversationState = "Discover" },
            [],
            userMessage,
            new AiDecisionContract("1.0", "d1", null, 0.8m, [], AiDecisionValidationStatus.Valid, [], null, false, null, null),
            null,
            knowledge,
            analysis);
    }
}
