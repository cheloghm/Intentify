using Intentify.Modules.Engage.Application;
using Intentify.Modules.Engage.Domain;

namespace Intentify.Modules.Engage.Tests;

public sealed class EngageConversationPolicyTests
{
    private static readonly EngageConversationPolicy Policy = new();

    [Fact]
    public void BuildNextDiscoveryQuestion_DigitalContext_PrioritizesUniversalLocationBeforeConstraintRefinement()
    {
        var session = CreateSession(
            captureGoal: "launch a new website",
            captureType: "ecommerce");

        var question = Policy.BuildNextDiscoveryQuestion(session);

        Assert.Equal("What location should we plan for?", question);
    }

    [Fact]
    public void BuildNextDiscoveryQuestion_BookingContext_UsesBusinessAwareConstraintQuestion()
    {
        var session = CreateSession(
            captureGoal: "improve appointment booking flow",
            captureType: "hospitality",
            captureLocation: "Austin");

        var question = Policy.BuildNextDiscoveryQuestion(session);

        Assert.Equal("Any key constraints like budget or timeline?", question);
    }

    [Fact]
    public void IsStrongCommercialIntent_DetectsRestaurantOrderingIntent()
    {
        var result = Policy.IsStrongCommercialIntent("We need to improve online ordering for our restaurant.");

        Assert.True(result);
    }

    [Fact]
    public void TryBuildCommercialIntentContactPrompt_DetectsSoftwareIntegrationIntent()
    {
        var result = Policy.TryBuildCommercialIntentContactPrompt(
            "We are looking to upgrade integrations for our software platform.",
            "Thanks for reaching out about",
            out var prompt);

        Assert.True(result);
        Assert.Contains("what’s your first name?", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void TryBuildSmalltalkResponse_Acknowledgement_UsesConfiguredAckResponse()
    {
        var result = Policy.TryBuildSmalltalkResponse(
            "ok",
            priorAssistantAskedQuestion: false,
            greetingResponse: "Hi! How can I help you today?",
            ackResponse: "Thanks for confirming — what would you like help with next?",
            out var response);

        Assert.True(result);
        Assert.Equal("Thanks for confirming — what would you like help with next?", response);
    }

    [Fact]
    public void TryBuildSmalltalkResponse_VeryShortReplyAfterAssistantQuestion_DoesNotSwallowCaptureAnswer()
    {
        var result = Policy.TryBuildSmalltalkResponse(
            "sam",
            priorAssistantAskedQuestion: true,
            greetingResponse: "Hi! How can I help you today?",
            ackResponse: "Thanks for confirming — what would you like help with next?",
            out var response);

        Assert.False(result);
        Assert.Equal(string.Empty, response);
    }

    [Theory]
    [InlineData("What location should we plan for?", "dublin", "dublin")]
    [InlineData("Any key constraints like budget or timeline?", "5k", "5k")]
    [InlineData("Any key constraints like budget or timeline?", "next month", "next month")]
    public void TryApplyStageContinuation_ShortReply_UpdatesExpectedSlot(string lastQuestion, string reply, string expected)
    {
        var session = CreateSession(captureGoal: "new site", captureType: "retail");

        var changed = Policy.TryApplyStageContinuation(session, reply, lastQuestion);

        Assert.True(changed);
        if (lastQuestion.Contains("location", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Equal(expected, session.CaptureLocation);
        }
        else
        {
            Assert.Equal(expected, session.CaptureConstraints);
        }
    }

    [Fact]
    public void IsContextRecoverySignal_RecognizesAlreadyToldYou()
    {
        var signal = Policy.IsContextRecoverySignal("I already told you that.");
        Assert.True(signal);
    }

    [Fact]
    public void BuildNextDiscoveryQuestion_WhenContactAlreadyCaptured_DoesNotReaskNameOrContact()
    {
        var session = CreateSession(
            captureGoal: "increase leads",
            captureType: "home services",
            captureLocation: "Austin",
            captureConstraints: "3 month timeline");
        session.CapturedName = "Taylor";
        session.CapturedEmail = "taylor@example.com";
        session.CapturedPreferredContactMethod = "Email";

        var question = Policy.BuildNextDiscoveryQuestion(session);

        Assert.DoesNotContain("first name", question, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contact method", question, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("no thanks")]
    [InlineData("that's all")]
    [InlineData("nothing else")]
    [InlineData("i'm good")]
    public void IsClosureSignal_RecognizesCommonClosePhrases(string message)
    {
        Assert.True(Policy.IsClosureSignal(message));
    }

    private static EngageChatSession CreateSession(
        string? captureGoal = null,
        string? captureType = null,
        string? captureLocation = null,
        string? captureConstraints = null)
    {
        return new EngageChatSession
        {
            CaptureGoal = captureGoal,
            CaptureType = captureType,
            CaptureLocation = captureLocation,
            CaptureConstraints = captureConstraints
        };
    }
}
