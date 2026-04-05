using Intentify.Modules.Flows.Application;
using Intentify.Modules.Flows.Domain;
using Xunit;

namespace Intentify.Modules.Flows.Tests;

public sealed class FlowConditionEvaluatorTests
{
    // ── Equals ────────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_Equals_Matches_CaseInsensitive()
    {
        var conditions = Conditions("eventType", FlowConditionOperator.Equals, "PageView");
        var payload = Payload("eventType", "pageview");
        Assert.True(FlowConditionEvaluator.MatchesAll(conditions, payload));
    }

    [Fact]
    public void Evaluate_Equals_NoMatch()
    {
        var conditions = Conditions("eventType", FlowConditionOperator.Equals, "PageView");
        var payload = Payload("eventType", "click");
        Assert.False(FlowConditionEvaluator.MatchesAll(conditions, payload));
    }

    // ── Contains ──────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_PageView_UrlContains_Matches()
    {
        var conditions = Conditions("url", FlowConditionOperator.Contains, "/pricing");
        var payload = Payload("url", "/pricing?plan=pro");
        Assert.True(FlowConditionEvaluator.MatchesAll(conditions, payload));
    }

    [Fact]
    public void Evaluate_PageView_UrlContains_NoMatch()
    {
        var conditions = Conditions("url", FlowConditionOperator.Contains, "/pricing");
        var payload = Payload("url", "/about");
        Assert.False(FlowConditionEvaluator.MatchesAll(conditions, payload));
    }

    // ── GreaterThan ───────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_EngageScore_GreaterThan_Matches()
    {
        var conditions = Conditions("engagementScore", FlowConditionOperator.GreaterThan, "80");
        var payload = Payload("engagementScore", "85");
        Assert.True(FlowConditionEvaluator.MatchesAll(conditions, payload));
    }

    [Fact]
    public void Evaluate_EngageScore_GreaterThan_NoMatch()
    {
        var conditions = Conditions("engagementScore", FlowConditionOperator.GreaterThan, "80");
        var payload = Payload("engagementScore", "50");
        Assert.False(FlowConditionEvaluator.MatchesAll(conditions, payload));
    }

    [Fact]
    public void Evaluate_EngageScore_GreaterThan_EqualValue_NoMatch()
    {
        var conditions = Conditions("engagementScore", FlowConditionOperator.GreaterThan, "80");
        var payload = Payload("engagementScore", "80");
        Assert.False(FlowConditionEvaluator.MatchesAll(conditions, payload));
    }

    // ── And (multiple conditions) ─────────────────────────────────────────────

    [Fact]
    public void Evaluate_And_AllTrue_Matches()
    {
        var conditions = new[]
        {
            new FlowCondition("url", FlowConditionOperator.Contains, "/pricing"),
            new FlowCondition("engagementScore", FlowConditionOperator.GreaterThan, "50"),
        };
        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["url"] = "/pricing",
            ["engagementScore"] = "75",
        };
        Assert.True(FlowConditionEvaluator.MatchesAll(conditions, payload));
    }

    [Fact]
    public void Evaluate_And_OneFalse_NoMatch()
    {
        var conditions = new[]
        {
            new FlowCondition("url", FlowConditionOperator.Contains, "/pricing"),
            new FlowCondition("engagementScore", FlowConditionOperator.GreaterThan, "50"),
        };
        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["url"] = "/pricing",
            ["engagementScore"] = "20",  // fails GreaterThan(50)
        };
        Assert.False(FlowConditionEvaluator.MatchesAll(conditions, payload));
    }

    [Fact]
    public void Evaluate_MissingField_NoMatch()
    {
        var conditions = Conditions("url", FlowConditionOperator.Contains, "/pricing");
        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Assert.False(FlowConditionEvaluator.MatchesAll(conditions, payload));
    }

    [Fact]
    public void Evaluate_EmptyConditions_AlwaysMatches()
    {
        var payload = Payload("url", "/anything");
        Assert.True(FlowConditionEvaluator.MatchesAll([], payload));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IReadOnlyCollection<FlowCondition> Conditions(string field, FlowConditionOperator op, string value)
        => [new FlowCondition(field, op, value)];

    private static IReadOnlyDictionary<string, string> Payload(string key, string value)
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [key] = value };
}
