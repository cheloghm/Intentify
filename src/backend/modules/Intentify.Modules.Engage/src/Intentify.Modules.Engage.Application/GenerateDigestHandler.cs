using Intentify.Modules.Leads.Application;
using Intentify.Modules.Tickets.Application;
using Intentify.Shared.AI;

namespace Intentify.Modules.Engage.Application;

public sealed class GenerateDigestHandler(
    ILeadRepository leadRepository,
    ITicketRepository ticketRepository,
    IEngageChatSessionRepository sessionRepository,
    IChatCompletionClient ai)
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

        string? aiNarrative = null;
        try
        {
            const string systemPrompt =
                "You are a concise business intelligence analyst writing a weekly summary email for a small business owner. " +
                "Be specific, insightful, and actionable. Maximum 4 sentences.";

            var topLeadLine = topOpportunity is not null
                ? $"\n- Most promising lead: {topOpportunity.DisplayName ?? topOpportunity.PrimaryEmail ?? "unknown"}" +
                  (string.IsNullOrWhiteSpace(topOpportunity.OpportunityLabel) ? "" : $" — interested in {topOpportunity.OpportunityLabel}")
                : "";

            var userPrompt =
                $"Write a friendly, specific 3-4 sentence business intelligence summary for this week's report.\n" +
                $"Week ending: {DateTime.UtcNow:dddd d MMMM}\n\n" +
                $"Data:\n" +
                $"- New leads captured: {newLeads.Count}\n" +
                $"- Support tickets opened: {newTickets.Count}\n" +
                $"- AI conversations: {recentSessions.Count}" +
                topLeadLine + "\n\n" +
                "Focus on: What's worth the owner's attention this week. Mention specific numbers. End with one concrete suggestion for the coming week.";

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            var result = await ai.CompleteAsync(systemPrompt, userPrompt, cts.Token);
            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value))
            {
                aiNarrative = result.Value.Trim();
            }
        }
        catch
        {
            // AI narrative is best-effort — don't block the digest
        }

        return new DigestResult(
            query.SiteId,
            DateTime.UtcNow,
            newLeads.Count,
            newLeads.Select(l => new DigestLeadEntry(l.DisplayName, l.PrimaryEmail, l.OpportunityLabel, l.IntentScore)).ToArray(),
            newTickets.Count,
            newTickets.Select(t => new DigestTicketEntry(t.Subject, t.Status)).ToArray(),
            recentSessions.Count,
            topOpportunity is null ? null : new DigestLeadEntry(topOpportunity.DisplayName, topOpportunity.PrimaryEmail, topOpportunity.OpportunityLabel, topOpportunity.IntentScore),
            AiNarrative: aiNarrative);
    }
}
