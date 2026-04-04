using Intentify.Modules.Leads.Application;
using Intentify.Modules.Tickets.Application;

namespace Intentify.Modules.Engage.Application;

public sealed class GenerateDigestHandler(
    ILeadRepository leadRepository,
    ITicketRepository ticketRepository,
    IEngageChatSessionRepository sessionRepository)
{
    public async Task<DigestResult> HandleAsync(GenerateDigestQuery query, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);

        var allLeads = await leadRepository.ListAsync(
            new ListLeadsQuery(query.TenantId, query.SiteId, 1, 200),
            cancellationToken);

        var newLeads = allLeads
            .Where(l => l.CreatedAtUtc >= cutoff)
            .ToList();

        var allTickets = await ticketRepository.ListAsync(
            new ListTicketsQuery(query.TenantId, query.SiteId, null, null, 1, 200),
            cancellationToken);

        var newTickets = allTickets
            .Where(t => t.CreatedAtUtc >= cutoff)
            .ToList();

        var allSessions = await sessionRepository.ListBySiteAsync(
            query.TenantId, query.SiteId, null, cancellationToken);

        var recentSessions = allSessions
            .Where(s => s.CreatedAtUtc >= cutoff)
            .ToList();

        var topOpportunity = allLeads
            .Where(l => l.IntentScore.HasValue)
            .OrderByDescending(l => l.IntentScore)
            .FirstOrDefault();

        return new DigestResult(
            query.SiteId,
            DateTime.UtcNow,
            newLeads.Count,
            newLeads.Select(l => new DigestLeadEntry(l.DisplayName, l.PrimaryEmail, l.OpportunityLabel, l.IntentScore)).ToArray(),
            newTickets.Count,
            newTickets.Select(t => new DigestTicketEntry(t.Subject, t.Status)).ToArray(),
            recentSessions.Count,
            topOpportunity is null ? null : new DigestLeadEntry(topOpportunity.DisplayName, topOpportunity.PrimaryEmail, topOpportunity.OpportunityLabel, topOpportunity.IntentScore));
    }
}
