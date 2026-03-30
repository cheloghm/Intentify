using Intentify.Modules.Engage.Application;
using Intentify.Modules.Engage.Domain;
using Intentify.Modules.Leads.Application;
using Intentify.Modules.Leads.Domain;
using Intentify.Modules.Tickets.Application;
using Intentify.Modules.Tickets.Domain;

namespace Intentify.Modules.Engage.Tests;

public sealed class EngageBusinessOutcomeExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_Escalation_CreatesThenUpdatesSingleSessionTicket()
    {
        var fixture = new Fixture();
        var context = fixture.CreateContext("I need a human agent", EngageNextAction.EscalateSupport);
        var cmd = new ChatSendCommand("wk", fixture.Session.Id, "I need a human agent", "fp-1");
        var response = new ChatSendResult(fixture.Session.Id, "Escalating now?", 0.9m, false, []);

        var first = await fixture.Executor.ExecuteAsync(context, cmd, response, CancellationToken.None);
        var second = await fixture.Executor.ExecuteAsync(context, cmd, response, CancellationToken.None);

        Assert.True(first.TicketTouched);
        Assert.True(second.TicketTouched);
        Assert.Single(fixture.TicketRepo.Items.Where(item => item.EngageSessionId == fixture.Session.Id));
        Assert.Single(fixture.HandoffRepo.Items.Where(item => item.SessionId == fixture.Session.Id));
    }

    [Fact]
    public async Task ExecuteAsync_CaptureReady_UpsertsLeadWithTenantSiteScope()
    {
        var fixture = new Fixture();
        fixture.Session.CaptureGoal = "book more consultations";
        fixture.Session.CaptureType = "law firm";
        fixture.Session.CaptureLocation = "Austin";
        fixture.Session.CapturedEmail = "owner@example.com";
        fixture.Session.CapturedName = "Alex";
        fixture.Session.CapturedPreferredContactMethod = "Email";

        var context = fixture.CreateContext("owner@example.com", EngageNextAction.AskCaptureQuestion);
        var cmd = new ChatSendCommand("wk", fixture.Session.Id, "owner@example.com", "fp-1");
        var response = new ChatSendResult(fixture.Session.Id, "Thanks, what's best contact?", 0.8m, false, []);

        var outcome = await fixture.Executor.ExecuteAsync(context, cmd, response, CancellationToken.None);

        Assert.True(outcome.LeadTouched);
        Assert.Single(fixture.LeadRepo.Items);
        Assert.Equal(fixture.Session.TenantId, fixture.LeadRepo.Items[0].TenantId);
        Assert.Equal(fixture.Session.SiteId, fixture.LeadRepo.Items[0].SiteId);
    }

    [Fact]
    public async Task ExecuteAsync_Escalation_DoesNotCrossTenantSiteTickets()
    {
        var fixture = new Fixture();
        fixture.TicketRepo.Items.Add(new Ticket
        {
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            EngageSessionId = fixture.Session.Id,
            Subject = "Engage support escalation",
            Description = "other scope",
            Status = TicketStatuses.Open,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        var context = fixture.CreateContext("help", EngageNextAction.EscalateSupport);
        var cmd = new ChatSendCommand("wk", fixture.Session.Id, "help", "fp-1");
        var response = new ChatSendResult(fixture.Session.Id, "Escalating?", 0.9m, false, []);

        await fixture.Executor.ExecuteAsync(context, cmd, response, CancellationToken.None);

        Assert.Equal(2, fixture.TicketRepo.Items.Count);
        Assert.Single(fixture.TicketRepo.Items.Where(item => item.TenantId == fixture.Session.TenantId && item.SiteId == fixture.Session.SiteId));
    }

    private sealed class Fixture
    {
        public EngageChatSession Session { get; } = new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            BotId = Guid.NewGuid(),
            ConversationState = "Discover",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        public InMemoryTicketRepository TicketRepo { get; } = new();
        public InMemoryLeadRepository LeadRepo { get; } = new();
        public InMemoryHandoffRepository HandoffRepo { get; } = new();
        public EngageBusinessOutcomeExecutor Executor { get; }

        public Fixture()
        {
            var leadHandler = new UpsertLeadFromPromoEntryHandler(LeadRepo, new PassThroughLeadVisitorLinker());
            Executor = new EngageBusinessOutcomeExecutor(
                new ListTicketsHandler(TicketRepo),
                new CreateTicketHandler(TicketRepo),
                new UpdateTicketHandler(TicketRepo),
                leadHandler,
                HandoffRepo,
                new EngageConversationPolicy());
        }

        public EngageConversationContext CreateContext(string userMessage, EngageNextAction action)
        {
            var ctx = new EngageConversationContext(
                Session,
                [],
                userMessage,
                new AiDecisionContract("1.0", "d1", null, 0.8m, [], AiDecisionValidationStatus.Valid, [], null, false, null, null),
                "How should we contact you?",
                "",
                new EngageAnalysisSummary("Discover", true, false, 0.8m, false));
            ctx.SetPrimaryAction(new EngageNextActionDecision(action, "Discover", "test"));
            return ctx;
        }
    }

    private sealed class InMemoryHandoffRepository : IEngageHandoffTicketRepository
    {
        public List<EngageHandoffTicket> Items { get; } = [];
        public Task InsertAsync(EngageHandoffTicket ticket, CancellationToken cancellationToken = default) { Items.Add(ticket); return Task.CompletedTask; }
        public Task<IReadOnlyCollection<EngageHandoffTicket>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyCollection<EngageHandoffTicket>)Items.Where(item => item.SessionId == sessionId).ToArray());
        public Task<IReadOnlyCollection<EngageHandoffTicket>> ListBySiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyCollection<EngageHandoffTicket>)Items.Where(item => item.TenantId == tenantId && item.SiteId == siteId).ToArray());
    }

    private sealed class InMemoryTicketRepository : ITicketRepository
    {
        public List<Ticket> Items { get; } = [];
        public Task InsertAsync(Ticket ticket, CancellationToken cancellationToken = default) { Items.Add(ticket); return Task.CompletedTask; }
        public Task<Ticket?> GetByIdAsync(Guid tenantId, Guid ticketId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(item => item.TenantId == tenantId && item.Id == ticketId));
        public Task<IReadOnlyCollection<TicketListItem>> ListAsync(ListTicketsQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyCollection<TicketListItem>)Items
                .Where(item => item.TenantId == query.TenantId)
                .Where(item => query.SiteId is null || item.SiteId == query.SiteId)
                .Where(item => query.EngageSessionId is null || item.EngageSessionId == query.EngageSessionId)
                .Select(item => new TicketListItem(item.Id, item.SiteId, item.VisitorId, item.EngageSessionId, item.Subject, item.Status, item.AssignedToUserId, item.CreatedAtUtc, item.UpdatedAtUtc))
                .ToArray());
        public Task ReplaceAsync(Ticket ticket, CancellationToken cancellationToken = default)
        {
            var index = Items.FindIndex(item => item.Id == ticket.Id);
            if (index >= 0) Items[index] = ticket;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryLeadRepository : ILeadRepository
    {
        public List<Lead> Items { get; } = [];
        public Task<Lead?> GetByEmailAsync(Guid tenantId, Guid siteId, string email, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(item => item.TenantId == tenantId && item.SiteId == siteId && item.PrimaryEmail == email));
        public Task<Lead?> GetByFirstPartyIdAsync(Guid tenantId, Guid siteId, string firstPartyId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(item => item.TenantId == tenantId && item.SiteId == siteId && item.FirstPartyId == firstPartyId));
        public Task<Lead?> GetByIdAsync(Guid tenantId, Guid leadId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(item => item.TenantId == tenantId && item.Id == leadId));
        public Task InsertAsync(Lead lead, CancellationToken cancellationToken = default) { Items.Add(lead); return Task.CompletedTask; }
        public Task ReplaceAsync(Lead lead, CancellationToken cancellationToken = default)
        {
            var index = Items.FindIndex(item => item.Id == lead.Id);
            if (index >= 0) Items[index] = lead;
            return Task.CompletedTask;
        }
        public Task<IReadOnlyCollection<Lead>> ListAsync(ListLeadsQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyCollection<Lead>)Items.Where(item => item.TenantId == query.TenantId && (query.SiteId is null || item.SiteId == query.SiteId)).ToArray());
    }

    private sealed class PassThroughLeadVisitorLinker : ILeadVisitorLinker
    {
        public Task<Guid?> ResolveVisitorIdAsync(Guid tenantId, Guid siteId, Guid? visitorId, string? firstPartyId, string? sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(visitorId);
        public Task EnrichVisitorIfPermittedAsync(Guid tenantId, Guid siteId, Guid? visitorId, bool consentGiven, string? email, string? displayName, string? phone, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
