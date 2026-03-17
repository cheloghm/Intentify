using Intentify.Modules.Flows.Application;
using Intentify.Modules.Flows.Domain;

namespace Intentify.Modules.Flows.Tests;

public sealed class FlowConditionEvaluatorTests
{
    [Fact]
    public void MatchesAll_ReturnsTrue_WhenAllConditionsSatisfied()
    {
        var conditions = new[]
        {
            new FlowCondition("category", FlowConditionOperator.Equals, "marketing"),
            new FlowCondition("topic", FlowConditionOperator.Contains, "intent")
        };

        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["category"] = "Marketing",
            ["topic"] = "buyer intent signals"
        };

        Assert.True(FlowConditionEvaluator.MatchesAll(conditions, payload));
    }

    [Fact]
    public void MatchesAll_ReturnsFalse_WhenMissingField()
    {
        var conditions = new[] { new FlowCondition("category", FlowConditionOperator.Equals, "marketing") };
        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Assert.False(FlowConditionEvaluator.MatchesAll(conditions, payload));
    }
}
