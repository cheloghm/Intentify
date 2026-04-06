using Intentify.Modules.Flows.Domain;

namespace Intentify.Modules.Flows.Application;

public static class FlowConditionEvaluator
{
    public static bool MatchesAll(IReadOnlyCollection<FlowCondition> conditions, IReadOnlyDictionary<string, string> payload)
    {
        foreach (var condition in conditions)
        {
            if (!payload.TryGetValue(condition.Field, out var currentValue))
            {
                return false;
            }

            switch (condition.Operator)
            {
                case FlowConditionOperator.Equals:
                    if (!string.Equals(currentValue, condition.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                    break;
                case FlowConditionOperator.Contains:
                    if (currentValue.IndexOf(condition.Value, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return false;
                    }
                    break;
                case FlowConditionOperator.GreaterThan:
                    if (!double.TryParse(currentValue, out var currentNum) ||
                        !double.TryParse(condition.Value, out var conditionNum) ||
                        currentNum <= conditionNum)
                    {
                        return false;
                    }
                    break;
                default:
                    return false;
            }
        }

        return true;
    }
}
