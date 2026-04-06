using Intentify.Modules.Engage.Application;
using Intentify.Modules.Engage.Domain;
using Intentify.Modules.Leads.Application;
using Intentify.Modules.Leads.Domain;
using Intentify.Modules.Tickets.Application;
using Intentify.Modules.Tickets.Domain;
using Xunit;

namespace Intentify.Modules.Engage.Tests;

public sealed class EngageBusinessOutcomeExecutorTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _siteId   = Guid.NewGuid();
    private readonly Guid _sessionId = Guid.NewGuid();

    // ── EscalateSupport ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Escalation_SetsVisitorIdOnTicket()
    {
        var visitorId = Guid.NewGuid();
        var (ticketRepo, handoffRepo, executor) = BuildExecutor();

        var context = BuildContext(EngageNextAction.EscalateSupport, ticketSubject: "Need help");
        var command = new ChatSendCommand("wk", _sessionId, "Hello", null, visitorId.ToString());
        var response = DummyResponse(_sessionId);

        var result = await executor.ExecuteAsync(context, command, response, default);

        Assert.True(result.TicketTouched);
        Assert.Single(ticketRepo.Items);
        Assert.Equal(visitorId, ticketRepo.Items[0].VisitorId);
        Assert.Single(handoffRepo.Items);
    }

    [Fact]
    public async Task ExecuteAsync_DoubleEscalation_DoesNotDuplicateHandoff()
    {
        var (ticketRepo, handoffRepo, executor) = BuildExecutor();

        var context = BuildContext(EngageNextAction.EscalateSupport, ticketSubject: "Support needed");
        var command = new ChatSendCommand("wk", _sessionId, "Help", null, null);
        var response = DummyResponse(_sessionId);

        // First escalation — creates ticket + handoff
        await executor.ExecuteAsync(context, command, response, default);
        Assert.Single(ticketRepo.Items);
        Assert.Single(handoffRepo.Items);

        // Second escalation — finds existing ticket, updates it, no new handoff
        await executor.ExecuteAsync(context, command, response, default);
        Assert.Single(ticketRepo.Items);
        Assert.Single(handoffRepo.Items); // still only one
    }

    // ── Fixture ───────────────────────────────────────────────────────────────

    private (FakeTicketRepository, FakeHandoffRepository, EngageBusinessOutcomeExecutor) BuildExecutor()
    {
        var ticketRepo  = new FakeTicketRepository();
        var handoffRepo = new FakeHandoffRepository();
        var leadRepo    = new FakeLeadRepository();
        var linker      = new FakeLeadVisitorLinker();

        var listHandler   = new ListTicketsHandler(ticketRepo);
        var createHandler = new CreateTicketHandler(ticketRepo, Enumerable.Empty<ITicketEventObserver>());
        var updateHandler = new UpdateTicketHandler(ticketRepo);
        var upsertLead    = new UpsertLeadFromPromoEntryHandler(leadRepo, linker, Enumerable.Empty<ILeadEventObserver>());
        var policy        = new EngageConversationPolicy();

        var executor = new EngageBusinessOutcomeExecutor(
            listHandler, createHandler, updateHandler, upsertLead, handoffRepo, policy);

        return (ticketRepo, handoffRepo, executor);
    }

    private EngageConversationContext BuildContext(EngageNextAction action, string ticketSubject = "Escalation")
    {
        var session = new EngageChatSession
        {
            Id       = _sessionId,
            TenantId = _tenantId,
            SiteId   = _siteId,
            BotId    = Guid.NewGuid(),
            WidgetKey = "wk"
        };

        var turnDecision = new EngageTurnDecision(
            Reply: "Let me connect you.",
            Intent: "support",
            CapturedSlots: new EngageTurnSlots(),
            CreateLead: false,
            CreateTicket: true,
            TicketSubject: ticketSubject,
            TicketType: "support",
            TicketSummary: "User needs assistance.",
            SuggestedFollowUp: null,
            ConversationComplete: false,
            Confidence: 0.9m,
            IsValid: true,
            FallbackReason: null);

        var analysis = new EngageAnalysisSummary(IsInitialTurn: false, AiConfidence: 0.9m);

        var ctx = new EngageConversationContext(
            session, [], "Help me", turnDecision, null, analysis);

        ctx.SetPrimaryAction(new EngageNextActionDecision(action, "escalate", "User requested support"));
        return ctx;
    }

    private static ChatSendResult DummyResponse(Guid sessionId)
        => new(sessionId, "Let me connect you.", 0.9m, false, []);

    // ── Fakes ─────────────────────────────────────────────────────────────────

    private sealed class FakeTicketRepository : ITicketRepository
    {
        public List<Ticket> Items { get; } = [];

        public Task InsertAsync(Ticket ticket, CancellationToken cancellationToken = default)
        {
            Items.Add(ticket);
            return Task.CompletedTask;
        }

        public Task<Ticket?> GetByIdAsync(Guid tenantId, Guid ticketId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(t => t.TenantId == tenantId && t.Id == ticketId));

        public Task<IReadOnlyCollection<TicketListItem>> ListAsync(ListTicketsQuery query, CancellationToken cancellationToken = default)
        {
            var filtered = Items
                .Where(t => t.TenantId == query.TenantId
                         && (query.SiteId is null || t.SiteId == query.SiteId)
                         && (query.EngageSessionId is null || t.EngageSessionId == query.EngageSessionId))
                .Select(t => new TicketListItem(
                    t.Id, t.SiteId, t.VisitorId, t.EngageSessionId,
                    t.Subject, t.Status, t.AssignedToUserId,
                    t.CreatedAtUtc, t.UpdatedAtUtc))
                .ToList();

            return Task.FromResult<IReadOnlyCollection<TicketListItem>>(filtered);
        }

        public Task ReplaceAsync(Ticket ticket, CancellationToken cancellationToken = default)
        {
            var idx = Items.FindIndex(t => t.Id == ticket.Id);
            if (idx >= 0) Items[idx] = ticket;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHandoffRepository : IEngageHandoffTicketRepository
    {
        public List<EngageHandoffTicket> Items { get; } = [];

        public Task InsertAsync(EngageHandoffTicket ticket, CancellationToken cancellationToken = default)
        {
            Items.Add(ticket);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<EngageHandoffTicket>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<EngageHandoffTicket>>(Items.Where(h => h.SessionId == sessionId).ToList());

        public Task<IReadOnlyCollection<EngageHandoffTicket>> ListBySiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<EngageHandoffTicket>>(Items.Where(h => h.TenantId == tenantId && h.SiteId == siteId).ToList());
    }

    private sealed class FakeLeadRepository : ILeadRepository
    {
        public Task<Lead?> GetByEmailAsync(Guid tenantId, Guid siteId, string email, CancellationToken cancellationToken = default) => Task.FromResult<Lead?>(null);
        public Task<Lead?> GetByFirstPartyIdAsync(Guid tenantId, Guid siteId, string firstPartyId, CancellationToken cancellationToken = default) => Task.FromResult<Lead?>(null);
        public Task<Lead?> GetByIdAsync(Guid tenantId, Guid leadId, CancellationToken cancellationToken = default) => Task.FromResult<Lead?>(null);
        public Task<Lead?> GetByLinkedVisitorIdAsync(Guid tenantId, Guid siteId, Guid visitorId, CancellationToken cancellationToken = default) => Task.FromResult<Lead?>(null);
        public Task InsertAsync(Lead lead, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReplaceAsync(Lead lead, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<Lead>> ListAsync(ListLeadsQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<Lead>>([]);
    }

    private sealed class FakeLeadVisitorLinker : ILeadVisitorLinker
    {
        public Task<Guid?> ResolveVisitorIdAsync(Guid tenantId, Guid siteId, Guid? visitorId, string? firstPartyId, string? sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(visitorId);

        public Task EnrichVisitorIfPermittedAsync(Guid tenantId, Guid siteId, Guid? visitorId, bool consentGiven, string? email, string? displayName, string? phone, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
