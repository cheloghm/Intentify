using Intentify.Modules.Engage.Domain;
using Intentify.Modules.Leads.Application;
using Intentify.Modules.Tickets.Application;

namespace Intentify.Modules.Engage.Application;

public sealed class EngageBusinessOutcomeExecutor
{
    private readonly ListTicketsHandler _listTicketsHandler;
    private readonly CreateTicketHandler _createTicketHandler;
    private readonly UpdateTicketHandler _updateTicketHandler;
    private readonly UpsertLeadFromPromoEntryHandler _upsertLeadHandler;
    private readonly IEngageHandoffTicketRepository _handoffTicketRepository;
    private readonly EngageConversationPolicy _policy;

    public EngageBusinessOutcomeExecutor(
        ListTicketsHandler listTicketsHandler,
        CreateTicketHandler createTicketHandler,
        UpdateTicketHandler updateTicketHandler,
        UpsertLeadFromPromoEntryHandler upsertLeadHandler,
        IEngageHandoffTicketRepository handoffTicketRepository,
        EngageConversationPolicy policy)
    {
        _listTicketsHandler = listTicketsHandler;
        _createTicketHandler = createTicketHandler;
        _updateTicketHandler = updateTicketHandler;
        _upsertLeadHandler = upsertLeadHandler;
        _handoffTicketRepository = handoffTicketRepository;
        _policy = policy;
    }

    public async Task<BusinessOutcomeResult> ExecuteAsync(
        EngageConversationContext context,
        ChatSendCommand command,
        ChatSendResult response,
        CancellationToken cancellationToken)
    {
        var action = context.PrimaryActionDecision?.Action;
        if (action == EngageNextAction.EscalateSupport)
        {
            var ticketUpdated = await EnsureEscalationTicketAsync(context, command, cancellationToken);
            return new BusinessOutcomeResult(ticketUpdated, false);
        }

        if (action == EngageNextAction.AskCaptureQuestion
            && _policy.IsCommercialCaptureReady(context.Session, explicitContactRequest: false)
            && HasLeadIdentity(context.Session, command))
        {
            var leadUpdated = await UpsertLeadAsync(context, command, cancellationToken);
            return new BusinessOutcomeResult(false, leadUpdated);
        }

        return BusinessOutcomeResult.None;
    }

    private async Task<bool> EnsureEscalationTicketAsync(
        EngageConversationContext context,
        ChatSendCommand command,
        CancellationToken cancellationToken)
    {
        var ticketSubject = "Engage support escalation";
        var ticketDescription = $"User: {context.UserMessage}\n\nAssistant: {context.LastAssistantQuestion ?? "(none)"}";

        var existing = await FindSessionEscalationTicketAsync(context.Session, cancellationToken);
        if (existing is { } item)
        {
            var update = await _updateTicketHandler.HandleAsync(
                new UpdateTicketCommand(context.Session.TenantId, item.Id, item.Subject, ticketDescription),
                cancellationToken);
            return update.Status == Shared.Validation.OperationStatus.Success;
        }

        var create = await _createTicketHandler.HandleAsync(
            new CreateTicketCommand(
                context.Session.TenantId,
                context.Session.SiteId,
                null,
                context.Session.Id,
                ticketSubject,
                ticketDescription,
                null,
                context.Session.CapturedName,
                context.Session.CapturedPreferredContactMethod,
                context.Session.CapturedEmail ?? context.Session.CapturedPhone,
                context.Session.OpportunityLabel,
                context.Session.IntentScore,
                context.Session.ConversationSummary,
                context.Session.SuggestedFollowUp),
            cancellationToken);

        if (create.Status != Shared.Validation.OperationStatus.Success || create.Value is null)
        {
            return false;
        }

        await _handoffTicketRepository.InsertAsync(
            new EngageHandoffTicket
            {
                TenantId = context.Session.TenantId,
                SiteId = context.Session.SiteId,
                SessionId = context.Session.Id,
                UserMessage = context.UserMessage,
                Reason = context.PrimaryActionDecision?.Reason ?? "SupportEscalation",
                LastAssistantMessage = context.LastAssistantQuestion,
                CitationCount = 0,
                CreatedAtUtc = DateTime.UtcNow
            },
            cancellationToken);

        return true;
    }

    private async Task<TicketListItem?> FindSessionEscalationTicketAsync(EngageChatSession session, CancellationToken cancellationToken)
    {
        var items = await _listTicketsHandler.HandleAsync(
            new ListTicketsQuery(session.TenantId, session.SiteId, null, session.Id, 1, 20),
            cancellationToken);

        return items
            .Where(item => string.Equals(item.Subject, "Engage support escalation", StringComparison.Ordinal))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefault();
    }

    private async Task<bool> UpsertLeadAsync(
        EngageConversationContext context,
        ChatSendCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _upsertLeadHandler.HandleAsync(
            new UpsertLeadFromPromoEntryCommand(
                context.Session.TenantId,
                context.Session.SiteId,
                context.Session.Id,
                null,
                command.CollectorSessionId,
                context.Session.Id.ToString("N"),
                context.Session.CapturedEmail,
                context.Session.CapturedName,
                false,
                context.Session.CapturedPhone,
                context.Session.CapturedPreferredContactMethod,
                context.Session.OpportunityLabel,
                context.Session.IntentScore,
                context.Session.ConversationSummary,
                context.Session.SuggestedFollowUp),
            cancellationToken);

        return result.Status == Shared.Validation.OperationStatus.Success;
    }

    private static bool HasLeadIdentity(EngageChatSession session, ChatSendCommand command)
        => !string.IsNullOrWhiteSpace(session.CapturedEmail)
           || !string.IsNullOrWhiteSpace(command.CollectorSessionId);
}

public sealed record BusinessOutcomeResult(bool TicketTouched, bool LeadTouched)
{
    public static readonly BusinessOutcomeResult None = new(false, false);
}
