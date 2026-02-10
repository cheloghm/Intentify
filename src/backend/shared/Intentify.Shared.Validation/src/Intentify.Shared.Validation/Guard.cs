namespace Intentify.Shared.Validation;

public static class Guard
{
    public static void AgainstNullOrWhiteSpace(ValidationErrors errors, string? value, string field, string message)
    {
        if (errors is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(field, message);
        }
    }

    public static void Against(bool condition, ValidationErrors errors, string field, string message)
    {
        if (errors is null)
        {
            return;
        }

        if (condition)
        {
            errors.Add(field, message);
        }
    }
}
