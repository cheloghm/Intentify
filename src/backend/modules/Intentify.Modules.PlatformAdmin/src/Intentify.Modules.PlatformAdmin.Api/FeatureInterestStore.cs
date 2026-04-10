namespace Intentify.Modules.PlatformAdmin.Api;

public sealed record FeatureInterestEntry(
    string TenantId,
    string Email,
    string Feature,
    DateTime RegisteredAtUtc);

public sealed record RegisterFeatureInterestRequest(string? Feature);

public sealed class FeatureInterestStore
{
    private readonly List<FeatureInterestEntry> _items = [];
    private readonly Lock _lock = new();

    public (bool Added, bool AlreadyRegistered) Register(FeatureInterestEntry entry)
    {
        lock (_lock)
        {
            var exists = _items.Any(e =>
                string.Equals(e.TenantId, entry.TenantId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.Feature, entry.Feature, StringComparison.OrdinalIgnoreCase));
            if (exists) return (false, true);
            _items.Add(entry);
            return (true, false);
        }
    }

    public IReadOnlyList<FeatureInterestEntry> GetAll()
    {
        lock (_lock) { return [.. _items]; }
    }

    public IReadOnlyList<FeatureInterestEntry> GetByFeature(string feature)
    {
        lock (_lock)
            return _items
                .Where(e => string.Equals(e.Feature, feature, StringComparison.OrdinalIgnoreCase))
                .ToArray();
    }
}
