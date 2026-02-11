namespace Intentify.Modules.Visitors.Application;

public sealed class UpsertVisitorFromCollectorEventHandler
{
    private readonly IVisitorRepository _repository;

    public UpsertVisitorFromCollectorEventHandler(IVisitorRepository repository)
    {
        _repository = repository;
    }

    public Task<UpsertVisitorResult> HandleAsync(UpsertVisitorFromCollectorEvent command, CancellationToken cancellationToken = default)
    {
        return _repository.UpsertFromCollectorEventAsync(command, cancellationToken);
    }
}

public sealed class ListVisitorsHandler
{
    private readonly IVisitorRepository _repository;

    public ListVisitorsHandler(IVisitorRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyCollection<VisitorListItem>> HandleAsync(ListVisitorsQuery query, CancellationToken cancellationToken = default)
    {
        return _repository.ListAsync(query, cancellationToken);
    }
}

public sealed class GetVisitorTimelineHandler
{
    private readonly IVisitorRepository _repository;
    private readonly IVisitorTimelineReader _timelineReader;
    private readonly VisitorsRetentionOptions _retentionOptions;

    public GetVisitorTimelineHandler(IVisitorRepository repository, IVisitorTimelineReader timelineReader, VisitorsRetentionOptions retentionOptions)
    {
        _repository = repository;
        _timelineReader = timelineReader;
        _retentionOptions = retentionOptions;
    }

    public async Task<IReadOnlyCollection<VisitorTimelineItem>> HandleAsync(VisitorTimelineQuery query, CancellationToken cancellationToken = default)
    {
        var visitor = await _repository.GetByIdAsync(query.TenantId, query.SiteId, query.VisitorId, cancellationToken);
        if (visitor is null)
        {
            return Array.Empty<VisitorTimelineItem>();
        }

        var retentionFloorUtc = _retentionOptions.RetentionDays is > 0
            ? DateTime.UtcNow.AddDays(-_retentionOptions.RetentionDays.Value)
            : null;

        var sessionIds = visitor.Sessions.Select(session => session.SessionId).Distinct(StringComparer.Ordinal).ToArray();
        return await _timelineReader.GetTimelineAsync(query, sessionIds, retentionFloorUtc, cancellationToken);
    }
}

public sealed class GetVisitCountWindowsHandler
{
    private readonly IVisitorRepository _repository;
    private readonly VisitorsRetentionOptions _retentionOptions;

    public GetVisitCountWindowsHandler(IVisitorRepository repository, VisitorsRetentionOptions retentionOptions)
    {
        _repository = repository;
        _retentionOptions = retentionOptions;
    }

    public async Task<VisitCountWindows> HandleAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        DateTime? retentionFloorUtc = _retentionOptions.RetentionDays is > 0
            ? now.AddDays(-_retentionOptions.RetentionDays.Value)
            : null;

        var last7SinceUtc = now.AddDays(-7);
        var last30SinceUtc = now.AddDays(-30);
        var last90SinceUtc = now.AddDays(-90);

        if (retentionFloorUtc is { } floor)
        {
            last7SinceUtc = Max(last7SinceUtc, floor);
            last30SinceUtc = Max(last30SinceUtc, floor);
            last90SinceUtc = Max(last90SinceUtc, floor);
        }

        var last7 = await _repository.CountSessionsSinceAsync(tenantId, siteId, last7SinceUtc, retentionFloorUtc, cancellationToken);
        var last30 = await _repository.CountSessionsSinceAsync(tenantId, siteId, last30SinceUtc, retentionFloorUtc, cancellationToken);
        var last90 = await _repository.CountSessionsSinceAsync(tenantId, siteId, last90SinceUtc, retentionFloorUtc, cancellationToken);

        return new VisitCountWindows(last7, last30, last90);
    }

    private static DateTime Max(DateTime left, DateTime right) => left > right ? left : right;
}
