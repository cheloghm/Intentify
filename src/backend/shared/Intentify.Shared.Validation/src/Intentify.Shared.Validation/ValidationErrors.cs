namespace Intentify.Shared.Validation;

public sealed class ValidationErrors
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.OrdinalIgnoreCase);

    public bool HasErrors => _errors.Count > 0;

    public IReadOnlyDictionary<string, string[]> Errors => _errors.ToDictionary(
        pair => pair.Key,
        pair => pair.Value.ToArray(),
        StringComparer.OrdinalIgnoreCase);

    public void Add(string field, string message)
    {
        if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (!_errors.TryGetValue(field, out var list))
        {
            list = [];
            _errors[field] = list;
        }

        if (!list.Contains(message, StringComparer.Ordinal))
        {
            list.Add(message);
        }
    }
}
