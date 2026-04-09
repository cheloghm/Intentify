using Intentify.Modules.Visitors.Application;
using Intentify.Modules.Visitors.Domain;
using Intentify.Shared.Data.Mongo;
using MongoDB.Driver;

namespace Intentify.Modules.Visitors.Infrastructure;

public sealed class VisitorRepository : IVisitorRepository
{
    private const string PageViewEventType = "page_view";
    private readonly IMongoCollection<Visitor> _visitors;
    private readonly Task _ensureIndexes;

    public VisitorRepository(IMongoDatabase database)
    {
        _visitors = database.GetCollection<Visitor>(VisitorsMongoCollections.Visitors);
        _ensureIndexes = EnsureIndexesAsync();
    }

    public async Task<UpsertVisitorResult> UpsertFromCollectorEventAsync(UpsertVisitorFromCollectorEvent command, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;

        var resolvedSessionId = ResolveSessionId(command);
        var normalizedFirstPartyId = NormalizeOptional(command.FirstPartyId);

        var filter = Builders<Visitor>.Filter.Eq(visitor => visitor.SiteId, command.SiteId)
            & Builders<Visitor>.Filter.Eq(visitor => visitor.TenantId, command.TenantId)
            & BuildIdentityFilter(resolvedSessionId, normalizedFirstPartyId);

        var visitor = await _visitors.Find(filter).FirstOrDefaultAsync(cancellationToken);

        if (visitor is null)
        {
            visitor = new Visitor
            {
                SiteId = command.SiteId,
                TenantId = command.TenantId,
                CreatedAtUtc = command.OccurredAtUtc,
                LastSeenAtUtc = command.OccurredAtUtc,
                FirstPartyId = normalizedFirstPartyId,
                UserAgentHint = Truncate(command.UserAgent, 256),
                Language = Truncate(command.Language, 32),
                Platform = Truncate(command.Platform, 32),
                Country = Truncate(command.Country, 64),
                City = Truncate(command.City, 64),
                Region = Truncate(command.Region, 64),
                Sessions = CreateInitialSessions(command, resolvedSessionId)
            };
            UpdateProductMeta(visitor, command);

            await _visitors.InsertOneAsync(visitor, cancellationToken: cancellationToken);
            return new UpsertVisitorResult(visitor.Id, visitor.Sessions.FirstOrDefault() ?? CreateDetachedSession(command, resolvedSessionId), visitor.Sessions.Count);
        }

        visitor.LastSeenAtUtc = Max(visitor.LastSeenAtUtc, command.OccurredAtUtc);
        visitor.FirstPartyId ??= normalizedFirstPartyId;
        visitor.UserAgentHint ??= Truncate(command.UserAgent, 256);
        visitor.Language ??= Truncate(command.Language, 32);
        visitor.Platform ??= Truncate(command.Platform, 32);
        visitor.Country ??= Truncate(command.Country, 64);
        visitor.City ??= Truncate(command.City, 64);
        visitor.Region ??= Truncate(command.Region, 64);
        UpdateProductMeta(visitor, command);

        VisitorSession session;
        if (resolvedSessionId is null)
        {
            session = CreateDetachedSession(command, resolvedSessionId);
        }
        else
        {
            session = visitor.Sessions.FirstOrDefault(item => item.SessionId == resolvedSessionId);
            if (session is null)
            {
                session = CreateSession(command, resolvedSessionId);
                visitor.Sessions.Add(session);
            }
            else
            {
                UpdateSession(session, command);
            }
        }

        await _visitors.ReplaceOneAsync(existing => existing.Id == visitor.Id, visitor, cancellationToken: cancellationToken);
        return new UpsertVisitorResult(visitor.Id, session, visitor.Sessions.Count);
    }

    public async Task<IReadOnlyCollection<VisitorListItem>> ListAsync(ListVisitorsQuery query, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;

        var visitors = await _visitors.Find(visitor => visitor.TenantId == query.TenantId && visitor.SiteId == query.SiteId)
            .SortByDescending(visitor => visitor.LastSeenAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Limit(query.PageSize)
            .ToListAsync(cancellationToken);

        return visitors.Select(visitor =>
        {
            var latestSession = visitor.Sessions.OrderByDescending(item => item.LastSeenAtUtc).FirstOrDefault();
            return new VisitorListItem(
                visitor.Id,
                visitor.LastSeenAtUtc,
                visitor.Sessions.Count,
                visitor.Sessions.Sum(item => item.PagesVisited),
                latestSession?.EngagementScore ?? 0,
                latestSession?.LastPath,
                latestSession?.LastReferrer);
        }).ToArray();
    }

    public async Task<Visitor?> GetByIdAsync(Guid tenantId, Guid siteId, Guid visitorId, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;
        return await _visitors.Find(visitor => visitor.Id == visitorId && visitor.SiteId == siteId && visitor.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> CountSessionsSinceAsync(Guid tenantId, Guid siteId, DateTime sinceUtc, DateTime? retentionFloorUtc, CancellationToken cancellationToken = default)
    {
        await _ensureIndexes;

        var cutoff = retentionFloorUtc is { } floor && floor > sinceUtc ? floor : sinceUtc;
        var visitors = await _visitors.Find(visitor => visitor.SiteId == siteId && visitor.TenantId == tenantId)
            .Project(visitor => visitor.Sessions)
            .ToListAsync(cancellationToken);

        return visitors.Sum(sessions => sessions.Count(session => session.FirstSeenAtUtc >= cutoff));
    }

    private static FilterDefinition<Visitor> BuildIdentityFilter(string? sessionId, string? firstPartyId)
    {
        var filters = new List<FilterDefinition<Visitor>>();

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            filters.Add(Builders<Visitor>.Filter.Eq("Sessions.SessionId", sessionId));
        }

        if (!string.IsNullOrWhiteSpace(firstPartyId))
        {
            filters.Add(Builders<Visitor>.Filter.Eq(visitor => visitor.FirstPartyId, firstPartyId));
        }

        if (filters.Count == 0)
        {
            return Builders<Visitor>.Filter.Where(_ => false);
        }

        return Builders<Visitor>.Filter.Or(filters);
    }

    private static List<VisitorSession> CreateInitialSessions(UpsertVisitorFromCollectorEvent command, string? sessionId)
    {
        if (sessionId is null)
        {
            return [];
        }

        return [CreateSession(command, sessionId)];
    }

    private static VisitorSession CreateDetachedSession(UpsertVisitorFromCollectorEvent command, string? sessionId)
    {
        var session = new VisitorSession
        {
            SessionId = sessionId ?? string.Empty,
            FirstSeenAtUtc = command.OccurredAtUtc,
            LastSeenAtUtc = command.OccurredAtUtc,
            LastPath = command.Url,
            LastReferrer = command.Referrer
        };

        UpdateSession(session, command);
        return session;
    }

    private static VisitorSession CreateSession(UpsertVisitorFromCollectorEvent command, string sessionId)
    {
        var session = new VisitorSession
        {
            SessionId = sessionId,
            FirstSeenAtUtc = command.OccurredAtUtc,
            LastSeenAtUtc = command.OccurredAtUtc,
            LastPath = command.Url,
            LastReferrer = command.Referrer
        };

        UpdateSession(session, command);
        return session;
    }

    private static void UpdateSession(VisitorSession session, UpsertVisitorFromCollectorEvent command)
    {
        session.FirstSeenAtUtc = Min(session.FirstSeenAtUtc, command.OccurredAtUtc);
        session.LastSeenAtUtc = Max(session.LastSeenAtUtc, command.OccurredAtUtc);
        session.TimeOnSiteSeconds = (int)Math.Max(0, (session.LastSeenAtUtc - session.FirstSeenAtUtc).TotalSeconds);
        session.LastPath = command.Url ?? session.LastPath;
        session.LastReferrer = command.Referrer ?? session.LastReferrer;

        if (IsPageView(command.EventType))
        {
            session.PagesVisited += 1;
        }

        if (!string.IsNullOrWhiteSpace(command.Referrer))
        {
            IncrementCounter(session.Referrers, command.Referrer!);
        }

        IncrementCounter(session.TopActions, command.EventType);
        session.EngagementScore += GetEngagementWeight(command.EventType);
    }

    private static bool IsPageView(string eventType)
    {
        return string.Equals(eventType, PageViewEventType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, "pageview", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetEngagementWeight(string eventType)
    {
        if (string.Equals(eventType, "click", StringComparison.OrdinalIgnoreCase)) return 2;
        if (IsPageView(eventType)) return 3;
        if (string.Equals(eventType, "time_on_page", StringComparison.OrdinalIgnoreCase)) return 4;
        if (string.Equals(eventType, "form_submit", StringComparison.OrdinalIgnoreCase)) return 5;
        return 1;
    }

    private static void IncrementCounter(Dictionary<string, int> counter, string key)
    {
        var normalizedKey = key.Trim();
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return;
        }

        counter[normalizedKey] = counter.TryGetValue(normalizedKey, out var existing)
            ? existing + 1
            : 1;
    }

    private static string? ResolveSessionId(UpsertVisitorFromCollectorEvent command)
    {
        if (!string.IsNullOrWhiteSpace(command.SessionId))
        {
            return command.SessionId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(command.FirstPartyId))
        {
            return $"fp:{command.FirstPartyId.Trim()}";
        }

        return null;
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private Task EnsureIndexesAsync()
    {
        var indexes = new[]
        {
            new CreateIndexModel<Visitor>(
                Builders<Visitor>.IndexKeys.Ascending(visitor => visitor.SiteId).Descending(visitor => visitor.LastSeenAtUtc)),
            new CreateIndexModel<Visitor>(
                Builders<Visitor>.IndexKeys.Ascending(visitor => visitor.SiteId).Ascending("Sessions.SessionId")),
            new CreateIndexModel<Visitor>(
                Builders<Visitor>.IndexKeys.Ascending(visitor => visitor.SiteId).Ascending("Sessions.FirstSeenAtUtc"))
        };

        return MongoIndexHelper.EnsureIndexesAsync(_visitors, indexes);
    }

    private static void UpdateProductMeta(Visitor visitor, UpsertVisitorFromCollectorEvent command)
    {
        if (string.IsNullOrWhiteSpace(command.ProductName))
            return;

        var name = command.ProductName.Trim();
        visitor.LastProductViewed = name;
        visitor.LastProductPrice = command.ProductPrice;

        if (!visitor.RecentProductViews.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            visitor.RecentProductViews.Add(name);
            if (visitor.RecentProductViews.Count > 20)
                visitor.RecentProductViews.RemoveAt(0);
        }
    }

    private static DateTime Max(DateTime left, DateTime right) => left > right ? left : right;
    private static DateTime Min(DateTime left, DateTime right) => left < right ? left : right;
}
