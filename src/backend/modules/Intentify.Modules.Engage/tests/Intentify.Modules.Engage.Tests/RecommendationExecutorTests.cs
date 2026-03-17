using Intentify.Modules.Engage.Application;
using Intentify.Modules.Engage.Domain;
using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Knowledge.Domain;
using Intentify.Modules.Promos.Application;
using Intentify.Modules.Promos.Domain;
using Intentify.Modules.Tickets.Application;
using Intentify.Modules.Tickets.Domain;
using Intentify.Modules.Visitors.Application;
using Intentify.Modules.Visitors.Domain;

namespace Intentify.Modules.Engage.Tests;

public sealed class RecommendationExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_ApprovedEscalateTicket_ExecutesCreateTicket()
    {
        var fixture = new ExecutorFixture();

        var recommendation = new AiRecommendation(
            AiRecommendationType.EscalateTicket,
            0.9m,
            "Needs human support",
            [new AiEvidenceRef("engage", "m1")],
            new AiTargetRefs(VisitorId: fixture.VisitorId),
            true,
            new Dictionary<string, string> { ["subject"] = "Escalation", ["description"] = "Please follow up" });

        var command = fixture.CreateCommand(recommendation, approved: true);
        var result = await fixture.Executor.ExecuteAsync(command);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(RecommendationExecutionStatus.Executed, result.Value!.Status);
        Assert.NotNull(result.Value.TicketId);
        Assert.Single(fixture.TicketRepository.Items);
    }

    [Fact]
    public async Task ExecuteAsync_UnapprovedMutatingAction_IsRejected()
    {
        var fixture = new ExecutorFixture();

        var recommendation = new AiRecommendation(
            AiRecommendationType.EscalateTicket,
            0.8m,
            "Needs escalation",
            [],
            null,
            true,
            null);

        var result = await fixture.Executor.ExecuteAsync(fixture.CreateCommand(recommendation, approved: false));

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidTenantSiteEntityRef_IsRejected()
    {
        var fixture = new ExecutorFixture();

        var recommendation = new AiRecommendation(
            AiRecommendationType.SuggestPromo,
            0.7m,
            "Promo suggestion",
            [],
            new AiTargetRefs(PromoId: fixture.PromoId),
            false,
            null);

        var result = await fixture.Executor.ExecuteAsync(fixture.CreateCommand(recommendation, approved: false) with { SiteId = Guid.NewGuid() });

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_TagVisitor_IsRejectedSafely()
    {
        var fixture = new ExecutorFixture();

        var recommendation = new AiRecommendation(
            AiRecommendationType.TagVisitor,
            0.8m,
            "Tag user",
            [],
            new AiTargetRefs(VisitorId: fixture.VisitorId),
            true,
            null);

        var result = await fixture.Executor.ExecuteAsync(fixture.CreateCommand(recommendation, approved: true));

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(RecommendationExecutionStatus.Rejected, result.Value!.Status);
        Assert.Equal("UnsupportedAction", result.Value.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_SuggestPromo_RemainsNonMutating()
    {
        var fixture = new ExecutorFixture();

        var recommendation = new AiRecommendation(
            AiRecommendationType.SuggestPromo,
            0.75m,
            "Suggest promo",
            [],
            new AiTargetRefs(PromoId: fixture.PromoId, PromoPublicKey: "promo-key"),
            false,
            null);

        var result = await fixture.Executor.ExecuteAsync(fixture.CreateCommand(recommendation, approved: false));

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(RecommendationExecutionStatus.DisplayOnly, result.Value!.Status);
        Assert.Empty(fixture.TicketRepository.Items);
    }

    [Fact]
    public async Task ExecuteAsync_NoAction_IsNoOp()
    {
        var fixture = new ExecutorFixture();

        var recommendation = new AiRecommendation(
            AiRecommendationType.NoAction,
            1m,
            "No action",
            [],
            null,
            false,
            null);

        var result = await fixture.Executor.ExecuteAsync(fixture.CreateCommand(recommendation, approved: false));

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(RecommendationExecutionStatus.NoOp, result.Value!.Status);
        Assert.Empty(fixture.TicketRepository.Items);
    }

    private sealed class ExecutorFixture
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid SiteId { get; } = Guid.NewGuid();
        public Guid VisitorId { get; } = Guid.NewGuid();
        public Guid EngageSessionId { get; } = Guid.NewGuid();
        public Guid PromoId { get; } = Guid.NewGuid();

        public InMemoryTicketRepository TicketRepository { get; }
        public RecommendationExecutor Executor { get; }

        public ExecutorFixture()
        {
            TicketRepository = new InMemoryTicketRepository();

            var createTicketHandler = new CreateTicketHandler(TicketRepository);
            var listTicketsHandler = new ListTicketsHandler(TicketRepository);
            var chatSessions = new FakeEngageSessionRepository(TenantId, SiteId, EngageSessionId);
            var visitors = new FakeVisitorRepository(new Visitor
            {
                Id = VisitorId,
                TenantId = TenantId,
                SiteId = SiteId,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
                LastSeenAtUtc = DateTime.UtcNow
            });
            var visitorDetail = new GetVisitorDetailHandler(visitors);
            var promos = new FakePromoRepository(new Promo
            {
                Id = PromoId,
                TenantId = TenantId,
                SiteId = SiteId,
                Name = "Promo",
                PublicKey = "promo-key",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            var knowledge = new FakeKnowledgeSourceRepository(new KnowledgeSource
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                SiteId = SiteId,
                BotId = Guid.Empty,
                Type = "text",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });

            Executor = new RecommendationExecutor(
                createTicketHandler,
                listTicketsHandler,
                chatSessions,
                visitorDetail,
                promos,
                knowledge);
        }

        public ExecuteRecommendationCommand CreateCommand(AiRecommendation recommendation, bool approved)
            => new(
                TenantId,
                SiteId,
                new AiDecisionContextRef(TenantId, SiteId, VisitorId, EngageSessionId),
                recommendation,
                approved,
                Enum.GetValues<AiRecommendationType>());
    }

    private sealed class InMemoryTicketRepository : ITicketRepository
    {
        private readonly List<Ticket> _items = [];
        public IReadOnlyCollection<Ticket> Items => _items;

        public Task InsertAsync(Ticket ticket, CancellationToken cancellationToken = default)
        {
            _items.Add(ticket);
            return Task.CompletedTask;
        }

        public Task<Ticket?> GetByIdAsync(Guid tenantId, Guid ticketId, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(item => item.TenantId == tenantId && item.Id == ticketId));

        public Task<IReadOnlyCollection<TicketListItem>> ListAsync(ListTicketsQuery query, CancellationToken cancellationToken = default)
        {
            var filtered = _items
                .Where(item => item.TenantId == query.TenantId)
                .Where(item => query.SiteId is null || item.SiteId == query.SiteId)
                .Where(item => query.VisitorId is null || item.VisitorId == query.VisitorId)
                .Where(item => query.EngageSessionId is null || item.EngageSessionId == query.EngageSessionId)
                .Take(query.PageSize)
                .Select(item => new TicketListItem(
                    item.Id,
                    item.SiteId,
                    item.VisitorId,
                    item.EngageSessionId,
                    item.Subject,
                    item.Status,
                    item.AssignedToUserId,
                    item.CreatedAtUtc,
                    item.UpdatedAtUtc))
                .ToArray();

            return Task.FromResult((IReadOnlyCollection<TicketListItem>)filtered);
        }

        public Task ReplaceAsync(Ticket ticket, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeEngageSessionRepository(Guid tenantId, Guid siteId, Guid engageSessionId) : IEngageChatSessionRepository
    {
        public Task<EngageChatSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            if (sessionId != engageSessionId)
            {
                return Task.FromResult<EngageChatSession?>(null);
            }

            return Task.FromResult<EngageChatSession?>(new EngageChatSession
            {
                Id = engageSessionId,
                TenantId = tenantId,
                SiteId = siteId,
                BotId = Guid.NewGuid(),
                WidgetKey = "widget",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        public Task<EngageChatSession?> GetByIdAsync(Guid tenantIdArg, Guid siteIdArg, Guid sessionId, CancellationToken cancellationToken = default)
        {
            if (tenantIdArg != tenantId || siteIdArg != siteId)
            {
                return Task.FromResult<EngageChatSession?>(null);
            }

            return GetByIdAsync(sessionId, cancellationToken);
        }


        public Task InsertAsync(EngageChatSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task TouchAsync(Guid sessionId, DateTime timestampUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetCollectorSessionIdIfEmptyAsync(Guid sessionId, string collectorSessionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<EngageChatSession>> ListBySiteAsync(Guid tenantIdArg, Guid siteIdArg, string? collectorSessionId, CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyCollection<EngageChatSession>)Array.Empty<EngageChatSession>());
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

    private sealed class FakePromoRepository(Promo promo) : IPromoRepository
    {
        public Task InsertAsync(Promo promoArg, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyCollection<Promo>> ListAsync(ListPromosQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyCollection<Promo>)new[] { promo });

        public Task<Promo?> GetActiveByPublicKeyAsync(string publicKey, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Equals(publicKey, promo.PublicKey, StringComparison.Ordinal) ? promo : null);

        public Task<Promo?> GetByIdAsync(Guid tenantId, Guid promoId, CancellationToken cancellationToken = default)
            => Task.FromResult(promo.TenantId == tenantId && promo.Id == promoId ? promo : null);
    }

    private sealed class FakeKnowledgeSourceRepository(KnowledgeSource source) : IKnowledgeSourceRepository
    {
        public Task InsertSourceAsync(KnowledgeSource sourceArg, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<KnowledgeSource?> GetSourceByIdAsync(Guid tenantId, Guid sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(source.TenantId == tenantId && source.Id == sourceId ? source : null);

        public Task<IReadOnlyCollection<KnowledgeSource>> ListSourcesAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyCollection<KnowledgeSource>)new[] { source });

        public Task UpdateStatusAsync(Guid tenantId, Guid sourceId, IndexStatus status, string? failureReason, DateTime? indexedAtUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ReplaceSourceContentAsync(Guid tenantId, Guid sourceId, byte[] pdfBytes, IndexStatus status, DateTime updatedAtUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
