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

        Assert.Equal("Any key constraints like budget, timeline, or scheduling requirements?", question);
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
