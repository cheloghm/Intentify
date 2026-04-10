namespace Intentify.Modules.PlatformAdmin.Api;

public sealed record FeedbackSubmission(
    string Id,
    string Type,
    string Title,
    string? Description,
    string? Priority,
    string SubmittedAt,
    string Status,
    string? SubmittedByUserId);

public sealed class FeedbackStore
{
    private readonly List<FeedbackSubmission> _items = [];
    private readonly Lock _lock = new();

    public FeedbackSubmission Add(FeedbackSubmission item)
    {
        lock (_lock)
        {
            _items.Insert(0, item);
            return item;
        }
    }

    public IReadOnlyList<FeedbackSubmission> GetAll()
    {
        lock (_lock) { return [.. _items]; }
    }

    public FeedbackSubmission? UpdateStatus(string id, string status)
    {
        lock (_lock)
        {
            var idx = _items.FindIndex(i => i.Id == id);
            if (idx < 0) return null;
            var updated = _items[idx] with { Status = status };
            _items[idx] = updated;
            return updated;
        }
    }
}
