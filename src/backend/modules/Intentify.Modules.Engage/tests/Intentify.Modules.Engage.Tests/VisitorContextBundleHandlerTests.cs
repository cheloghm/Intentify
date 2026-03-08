using Intentify.Modules.Engage.Application;
using Intentify.Modules.Engage.Domain;
using Intentify.Modules.Intelligence.Application;
using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Knowledge.Domain;
using Intentify.Modules.Leads.Application;
using Intentify.Modules.Promos.Application;
using Intentify.Modules.Promos.Domain;
using Intentify.Modules.Tickets.Application;
using Intentify.Modules.Visitors.Application;
using Intentify.Modules.Visitors.Domain;
using Intentify.Shared.Validation;
using Microsoft.Extensions.Logging.Abstractions;

namespace Intentify.Modules.Engage.Tests;

public sealed class VisitorContextBundleHandlerTests
{
    [Fact]
    public async Task HandleAsync_BundleWithEngageSessionOnly_Succeeds()
    {
        var fixture = new Fixture();
        var query = fixture.CreateQuery(visitorId: null, engageSessionId: fixture.EngageSession.Id);

        var result = await fixture.Handler.HandleAsync(query);

        Assert.Equal(OperationStatus.Success, result.Status);
        var value = Assert.IsType<VisitorContextBundle>(result.Value);
        Assert.Equal(fixture.EngageSession.Id, value.ContextRef.EngageSessionId);
        Assert.Equal(fixture.Visitor.Id, value.ContextRef.VisitorId);
        Assert.NotNull(value.RecentEngageSummary);
        Assert.NotEmpty(value.KnowledgeRetrievalSnapshot.TopChunks);
    }

    [Fact]
    public async Task HandleAsync_BundleWithVisitorOnly_Succeeds()
    {
        var fixture = new Fixture();
        var query = fixture.CreateQuery(visitorId: fixture.Visitor.Id, engageSessionId: null);

        var result = await fixture.Handler.HandleAsync(query);

        Assert.Equal(OperationStatus.Success, result.Status);
        var value = Assert.IsType<VisitorContextBundle>(result.Value);
        Assert.Equal(fixture.Visitor.Id, value.ContextRef.VisitorId);
        Assert.Null(value.ContextRef.EngageSessionId);
        Assert.NotNull(value.VisitorProfile);
        Assert.NotNull(value.RecentTimelineSummary);
    }

    [Fact]
    public async Task HandleAsync_BundleWithBoth_Succeeds()
    {
        var fixture = new Fixture();
        var query = fixture.CreateQuery(visitorId: fixture.Visitor.Id, engageSessionId: fixture.EngageSession.Id);

        var result = await fixture.Handler.HandleAsync(query);

        Assert.Equal(OperationStatus.Success, result.Status);
        var value = Assert.IsType<VisitorContextBundle>(result.Value);
        Assert.Equal(fixture.Visitor.Id, value.ContextRef.VisitorId);
        Assert.Equal(fixture.EngageSession.Id, value.ContextRef.EngageSessionId);
    }

    [Fact]
    public async Task HandleAsync_MissingOptionalSources_StillSucceeds()
    {
        var fixture = new Fixture(includeOptionalData: false);
        var query = fixture.CreateQuery(visitorId: fixture.Visitor.Id, engageSessionId: fixture.EngageSession.Id);

        var result = await fixture.Handler.HandleAsync(query);

        Assert.Equal(OperationStatus.Success, result.Status);
        var value = Assert.IsType<VisitorContextBundle>(result.Value);
        Assert.NotNull(value.KnowledgeRetrievalSnapshot);
        Assert.True(value.LinkedTicketsSummary is null || value.LinkedTicketsSummary.Count == 0);
        Assert.True(value.PromoInteractionSummary is null || value.PromoInteractionSummary.Count == 0);
    }

    [Fact]
    public async Task HandleAsync_InvalidTenantSiteContext_FailsValidation()
    {
        var fixture = new Fixture();
        var query = fixture.CreateQuery(visitorId: fixture.Visitor.Id, engageSessionId: null) with
        {
            TenantId = Guid.Empty,
            SiteId = Guid.Empty
        };

        var result = await fixture.Handler.HandleAsync(query);

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task HandleAsync_ReturnsBoundedSummariesOnly()
    {
        var fixture = new Fixture();
        var query = fixture.CreateQuery(visitorId: fixture.Visitor.Id, engageSessionId: fixture.EngageSession.Id) with
        {
            EngageMessageLimit = 2,
            TimelineLimit = 2,
            TicketsLimit = 1,
            PromoEntriesLimit = 1,
            KnowledgeTop = 1
        };

        var result = await fixture.Handler.HandleAsync(query);

        Assert.Equal(OperationStatus.Success, result.Status);
        var value = Assert.IsType<VisitorContextBundle>(result.Value);
        Assert.True(value.RecentEngageSummary?.Messages.Count <= 2);
        Assert.True((value.RecentTimelineSummary?.Count ?? 0) <= 2);
        Assert.True((value.LinkedTicketsSummary?.Count ?? 0) <= 1);
        Assert.True((value.PromoInteractionSummary?.Count ?? 0) <= 1);
        Assert.True(value.KnowledgeRetrievalSnapshot.TopChunks.Count <= 1);
        Assert.All(value.RecentEngageSummary?.Messages ?? [], item => Assert.True(item.ContentExcerpt.Length <= 220));
        Assert.All(value.KnowledgeRetrievalSnapshot.TopChunks, item => Assert.True(item.ContentExcerpt.Length <= 220));
    }

    [Fact]
    public async Task HandleAsync_TenantSiteMismatch_FailsSafely()
    {
        var fixture = new Fixture();
        var query = fixture.CreateQuery(visitorId: null, engageSessionId: fixture.EngageSession.Id) with
        {
            SiteId = Guid.NewGuid()
        };

        var result = await fixture.Handler.HandleAsync(query);

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    private sealed class Fixture
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid SiteId { get; } = Guid.NewGuid();
        public Visitor Visitor { get; }
        public EngageChatSession EngageSession { get; }
        public VisitorContextBundleHandler Handler { get; }

        public Fixture(bool includeOptionalData = true)
        {
            Visitor = new Visitor
            {
                TenantId = TenantId,
                SiteId = SiteId,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-5),
                LastSeenAtUtc = DateTime.UtcNow,
                PrimaryEmail = "visitor@example.com",
                DisplayName = "Visitor",
                Language = "en",
                Platform = "web",
                Sessions =
                [
                    new VisitorSession
                    {
                        SessionId = "collector-1",
                        FirstSeenAtUtc = DateTime.UtcNow.AddDays(-3),
                        LastSeenAtUtc = DateTime.UtcNow,
                        PagesVisited = 7,
                        TimeOnSiteSeconds = 220,
                        EngagementScore = 80,
                        TopActions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["click"] = 4
                        }
                    }
                ]
            };

            EngageSession = new EngageChatSession
            {
                TenantId = TenantId,
                SiteId = SiteId,
                BotId = Guid.NewGuid(),
                WidgetKey = "widget-key",
                CollectorSessionId = "collector-1",
                CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
                UpdatedAtUtc = DateTime.UtcNow
            };

            var chatSessions = new FakeEngageChatSessionRepository([EngageSession]);
            var chatMessages = new FakeEngageChatMessageRepository([
                new EngageChatMessage { SessionId = EngageSession.Id, Role = "user", Content = new string('a', 280), CreatedAtUtc = DateTime.UtcNow.AddMinutes(-3) },
                new EngageChatMessage { SessionId = EngageSession.Id, Role = "assistant", Content = "Short answer", CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2), Confidence = 0.8m },
                new EngageChatMessage { SessionId = EngageSession.Id, Role = "user", Content = "Another question", CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1) }
            ]);

            var leadLinker = new FakeLeadVisitorLinker(Visitor.Id);

            var knowledgeSources = new FakeKnowledgeSourceRepository([
                new KnowledgeSource { Id = Guid.NewGuid(), TenantId = TenantId, SiteId = SiteId, BotId = Guid.Empty, Type = "text", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow }
            ]);
            var sourceId = knowledgeSources.Items[0].Id;
            var knowledgeChunks = new FakeKnowledgeChunkRepository([
                new KnowledgeChunk { Id = Guid.NewGuid(), TenantId = TenantId, SiteId = SiteId, SourceId = sourceId, ChunkIndex = 0, Content = new string('k', 400), CreatedAtUtc = DateTime.UtcNow }
            ]);
            var retrieveTopChunks = new RetrieveTopChunksHandler(knowledgeChunks, knowledgeSources, NullLogger<RetrieveTopChunksHandler>.Instance);

            var visitorRepository = new FakeVisitorRepository(Visitor);
            var visitorTimelineReader = new FakeTimelineReader();
            var visitorDetailHandler = new GetVisitorDetailHandler(visitorRepository);
            var visitorTimelineHandler = new GetVisitorTimelineHandler(visitorRepository, visitorTimelineReader, new VisitorsRetentionOptions { RetentionDays = 0 });

            var ticketList = includeOptionalData
                ? new[]
                {
                    new TicketListItem(Guid.NewGuid(), SiteId, Visitor.Id, EngageSession.Id, "Long ticket subject here", "Open", null, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow)
                }
                : Array.Empty<TicketListItem>();
            var listTicketsHandler = new ListTicketsHandler(new FakeTicketRepository(ticketList));

            var promoEntries = includeOptionalData
                ? new[]
                {
                    new PromoEntry
                    {
                        TenantId = TenantId,
                        SiteId = SiteId,
                        PromoId = Guid.NewGuid(),
                        VisitorId = Visitor.Id,
                        Email = "visitor@example.com",
                        Name = "Visitor",
                        Answers = new Dictionary<string, string> { ["q1"] = "answer" },
                        CreatedAtUtc = DateTime.UtcNow.AddHours(-6)
                    }
                }
                : Array.Empty<PromoEntry>();

            var promoEntryRepository = new FakePromoEntryRepository(promoEntries);

            var intelligenceRepo = new FakeIntelligenceRepository(new IntelligenceDashboardResponse(
                SiteId,
                "general",
                "US",
                "7d",
                null,
                "Google",
                DateTime.UtcNow,
                1,
                new IntelligenceDashboardSummaryResponse(1, 0.8, 0.8),
                [new IntelligenceDashboardTrendItemResponse("topic", 0.8, 1, "Google")]));
            var intelligenceService = new QueryIntelligenceTrendsService(intelligenceRepo);

            Handler = new VisitorContextBundleHandler(
                chatSessions,
                chatMessages,
                leadLinker,
                retrieveTopChunks,
                visitorDetailHandler,
                visitorTimelineHandler,
                listTicketsHandler,
                promoEntryRepository,
                intelligenceService);
        }

        public BuildVisitorContextBundleQuery CreateQuery(Guid? visitorId, Guid? engageSessionId)
            => new(TenantId, SiteId, visitorId, engageSessionId, "return policy", IncludeIntelligenceSnapshot: true);
    }

    private sealed class FakeEngageChatSessionRepository(IReadOnlyCollection<EngageChatSession> sessions) : IEngageChatSessionRepository
    {
        public Task<EngageChatSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(sessions.FirstOrDefault(item => item.Id == sessionId));

        public Task InsertAsync(EngageChatSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task TouchAsync(Guid sessionId, DateTime timestampUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetCollectorSessionIdIfEmptyAsync(Guid sessionId, string collectorSessionId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyCollection<EngageChatSession>> ListBySiteAsync(Guid tenantId, Guid siteId, string? collectorSessionId, CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyCollection<EngageChatSession>)sessions.Where(item => item.TenantId == tenantId && item.SiteId == siteId).ToArray());
    }

    private sealed class FakeEngageChatMessageRepository(IReadOnlyCollection<EngageChatMessage> messages) : IEngageChatMessageRepository
    {
        public Task InsertAsync(EngageChatMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyCollection<EngageChatMessage>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyCollection<EngageChatMessage>)messages.Where(item => item.SessionId == sessionId).ToArray());
    }

    private sealed class FakeLeadVisitorLinker(Guid visitorId) : ILeadVisitorLinker
    {
        public Task<Guid?> ResolveVisitorIdAsync(Guid tenantId, Guid siteId, Guid? visitorIdArg, string? firstPartyId, string? sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult<Guid?>(visitorId);

        public Task EnrichVisitorIfPermittedAsync(Guid tenantId, Guid siteId, Guid? visitorIdArg, bool consentGiven, string? email, string? displayName, string? phone, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeKnowledgeChunkRepository(IReadOnlyCollection<KnowledgeChunk> chunks) : IKnowledgeChunkRepository
    {
        public Task UpsertChunksAsync(Guid tenantId, Guid sourceId, IReadOnlyCollection<KnowledgeChunk> chunksArg, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyCollection<KnowledgeChunk>> ListBySiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyCollection<KnowledgeChunk>)chunks.Where(item => item.TenantId == tenantId && item.SiteId == siteId).ToArray());
    }

    private sealed class FakeKnowledgeSourceRepository(IReadOnlyCollection<KnowledgeSource> items) : IKnowledgeSourceRepository
    {
        public IReadOnlyCollection<KnowledgeSource> Items { get; } = items;

        public Task InsertSourceAsync(KnowledgeSource source, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<KnowledgeSource?> GetSourceByIdAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(items.FirstOrDefault(item => item.TenantId == tenantId && item.Id == sourceId));

        public Task<IReadOnlyCollection<KnowledgeSource>> ListSourcesAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyCollection<KnowledgeSource>)items.Where(item => item.TenantId == tenantId && item.SiteId == siteId).ToArray());

        public Task UpdateStatusAsync(Guid tenantId, Guid sourceId, IndexStatus status, string? failureReason, DateTime? indexedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ReplaceSourceContentAsync(Guid tenantId, Guid sourceId, byte[] pdfBytes, IndexStatus status, DateTime updatedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeVisitorRepository(Visitor visitor) : IVisitorRepository
    {
        public Task<UpsertVisitorResult> UpsertFromCollectorEventAsync(UpsertVisitorFromCollectorEvent command, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<VisitorListItem>> ListAsync(ListVisitorsQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyCollection<VisitorListItem>)Array.Empty<VisitorListItem>());

        public Task<Visitor?> GetByIdAsync(Guid tenantId, Guid siteId, Guid visitorId, CancellationToken cancellationToken = default)
            => Task.FromResult(visitor.TenantId == tenantId && visitor.SiteId == siteId && visitor.Id == visitorId ? visitor : null);

        public Task<int> CountSessionsSinceAsync(Guid tenantId, Guid siteId, DateTime sinceUtc, DateTime? retentionFloorUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class FakeTimelineReader : IVisitorTimelineReader
    {
        public Task<IReadOnlyCollection<VisitorTimelineItem>> GetTimelineAsync(VisitorTimelineQuery query, IReadOnlyCollection<string> sessionIds, DateTime? retentionFloorUtc, CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyCollection<VisitorTimelineItem>)
            [
                new VisitorTimelineItem(DateTime.UtcNow.AddMinutes(-5), "pageview", "collector-1", "https://example.test/pricing", null, new Dictionary<string, string> { ["cta"] = "clicked" }),
                new VisitorTimelineItem(DateTime.UtcNow.AddMinutes(-2), "click", "collector-1", "https://example.test/contact", null, new Dictionary<string, string> { ["target"] = "form" })
            ]);
    }

    private sealed class FakeTicketRepository(IReadOnlyCollection<TicketListItem> tickets) : ITicketRepository
    {
        public Task InsertAsync(Intentify.Modules.Tickets.Domain.Ticket ticket, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Intentify.Modules.Tickets.Domain.Ticket?> GetByIdAsync(Guid tenantId, Guid ticketId, CancellationToken cancellationToken = default)
            => Task.FromResult<Intentify.Modules.Tickets.Domain.Ticket?>(null);

        public Task<IReadOnlyCollection<TicketListItem>> ListAsync(ListTicketsQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyCollection<TicketListItem>)tickets
                .Where(item => item.SiteId == query.SiteId)
                .Where(item => query.VisitorId is null || item.VisitorId == query.VisitorId)
                .Where(item => query.EngageSessionId is null || item.EngageSessionId == query.EngageSessionId)
                .Take(query.PageSize)
                .ToArray());

        public Task ReplaceAsync(Intentify.Modules.Tickets.Domain.Ticket ticket, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakePromoEntryRepository(IReadOnlyCollection<PromoEntry> entries) : IPromoEntryRepository
    {
        public Task InsertAsync(PromoEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyCollection<PromoEntry>> ListByPromoAsync(ListPromoEntriesQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyCollection<PromoEntry>)Array.Empty<PromoEntry>());

        public Task<IReadOnlyCollection<PromoEntry>> ListByVisitorAsync(ListVisitorPromoEntriesQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyCollection<PromoEntry>)entries
                .Where(item => item.TenantId == query.TenantId && item.SiteId == query.SiteId && item.VisitorId == query.VisitorId)
                .Take(query.PageSize)
                .ToArray());
    }

    private sealed class FakeIntelligenceRepository(IntelligenceDashboardResponse dashboard) : IIntelligenceTrendsRepository
    {
        public Task UpsertAsync(Intentify.Modules.Intelligence.Domain.IntelligenceTrendRecord record, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<Intentify.Modules.Intelligence.Domain.IntelligenceTrendRecord?> GetAsync(string tenantId, Guid siteId, string category, string location, string timeWindow, CancellationToken ct = default)
        {
            var record = new Intentify.Modules.Intelligence.Domain.IntelligenceTrendRecord
            {
                TenantId = Guid.Parse(tenantId),
                SiteId = siteId,
                Category = dashboard.Category,
                Location = dashboard.Location,
                TimeWindow = dashboard.TimeWindow,
                Provider = dashboard.Provider ?? "Google",
                RefreshedAtUtc = dashboard.RefreshedAtUtc ?? DateTime.UtcNow,
                Items = dashboard.TopItems.Select(item => new Intentify.Modules.Intelligence.Domain.IntelligenceTrendItem(item.QueryOrTopic, item.Score, item.Rank)).ToArray()
            };

            return Task.FromResult<Intentify.Modules.Intelligence.Domain.IntelligenceTrendRecord?>(record);
        }

        public Task<IntelligenceStatusResponse?> GetStatusAsync(string tenantId, Guid siteId, string category, string location, string timeWindow, CancellationToken ct = default)
            => Task.FromResult<IntelligenceStatusResponse?>(new IntelligenceStatusResponse("Google", category, location, timeWindow, DateTime.UtcNow, 1));
    }
}
