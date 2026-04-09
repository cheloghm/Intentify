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
    private readonly ILeadNotificationService? _leadNotifier;
    private readonly IEngageBotRepository? _botRepository;

    public EngageBusinessOutcomeExecutor(
        ListTicketsHandler listTicketsHandler,
        CreateTicketHandler createTicketHandler,
        UpdateTicketHandler updateTicketHandler,
        UpsertLeadFromPromoEntryHandler upsertLeadHandler,
        IEngageHandoffTicketRepository handoffTicketRepository,
        EngageConversationPolicy policy,
        ILeadNotificationService? leadNotifier = null,
        IEngageBotRepository? botRepository = null)
    {
        _listTicketsHandler = listTicketsHandler;
        _createTicketHandler = createTicketHandler;
        _updateTicketHandler = updateTicketHandler;
        _upsertLeadHandler = upsertLeadHandler;
        _handoffTicketRepository = handoffTicketRepository;
        _policy = policy;
        _leadNotifier = leadNotifier;
        _botRepository = botRepository;
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

            var isCommercial = string.IsNullOrWhiteSpace(context.TurnDecision.TicketType)
                || string.Equals(context.TurnDecision.TicketType, "commercial", StringComparison.OrdinalIgnoreCase);

            var leadUpdated = isCommercial && HasLeadIdentity(context.Session, command)
                ? await UpsertLeadAsync(context, command, cancellationToken)
                : false;

            if (leadUpdated)
            {
                FireHotLeadNotification(context);
                FireAbTestConversion(context.Session, command);
            }
            return new BusinessOutcomeResult(ticketUpdated, leadUpdated);
        }

        if (action == EngageNextAction.AskCaptureQuestion
            && _policy.IsCommercialCaptureReady(context.Session, explicitContactRequest: false)
            && HasLeadIdentity(context.Session, command))
        {
            var leadUpdated = await UpsertLeadAsync(context, command, cancellationToken);
            if (leadUpdated)
            {
                FireHotLeadNotification(context);
                FireAbTestConversion(context.Session, command);
            }
            return new BusinessOutcomeResult(false, leadUpdated);
        }

        return BusinessOutcomeResult.None;
    }

    private async Task<bool> EnsureEscalationTicketAsync(
        EngageConversationContext context,
        ChatSendCommand command,
        CancellationToken cancellationToken)
    {
        var ticketSubject = !string.IsNullOrWhiteSpace(context.TurnDecision.TicketSubject)
            ? context.TurnDecision.TicketSubject
            : "Engage support escalation";
        var ticketDescription = !string.IsNullOrWhiteSpace(context.TurnDecision.TicketSummary)
            ? context.TurnDecision.TicketSummary
            : $"User: {context.UserMessage}\n\nAssistant: {context.LastAssistantQuestion ?? "(none)"}";

        var existing = await FindSessionEscalationTicketAsync(context.Session, cancellationToken);
        if (existing is { } item)
        {
            var update = await _updateTicketHandler.HandleAsync(
                new UpdateTicketCommand(context.Session.TenantId, item.Id, item.Subject, ticketDescription),
                cancellationToken);
            return update.Status == Shared.Validation.OperationStatus.Success;
        }

        // ── Resolve visitor ID ────────────────────────────────────────────────
        // Prefer explicit visitorId from the command (set by the widget when visitor is known).
        // This ensures GET /visitors/:id/tickets works on the visitor profile page.
        Guid? visitorId = null;
        if (!string.IsNullOrWhiteSpace(command.VisitorId) && Guid.TryParse(command.VisitorId, out var parsedVid))
            visitorId = parsedVid;

        var create = await _createTicketHandler.HandleAsync(
            new CreateTicketCommand(
                TenantId:                context.Session.TenantId,
                SiteId:                  context.Session.SiteId,
                VisitorId:               visitorId,            // ← was null; now populated
                EngageSessionId:         context.Session.Id,
                Subject:                 ticketSubject,
                Description:             ticketDescription,
                AssignedToUserId:        null,
                ContactName:             context.Session.CapturedName,
                PreferredContactMethod:  context.Session.CapturedPreferredContactMethod,
                PreferredContactDetail:  context.Session.CapturedEmail ?? context.Session.CapturedPhone,
                OpportunityLabel:        context.Session.OpportunityLabel,
                IntentScore:             context.Session.IntentScore,
                ConversationSummary:     context.Session.ConversationSummary,
                SuggestedFollowUp:       context.Session.SuggestedFollowUp),
            cancellationToken);

        if (create.Status != Shared.Validation.OperationStatus.Success || create.Value is null)
            return false;

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

    private async Task<TicketListItem?> FindSessionEscalationTicketAsync(
        EngageChatSession session, CancellationToken cancellationToken)
    {
        var items = await _listTicketsHandler.HandleAsync(
            new ListTicketsQuery(session.TenantId, session.SiteId, null, session.Id, 1, 20),
            cancellationToken);
        return items.OrderByDescending(item => item.UpdatedAtUtc).FirstOrDefault();
    }

    private async Task<bool> UpsertLeadAsync(
        EngageConversationContext context,
        ChatSendCommand command,
        CancellationToken cancellationToken)
    {
        Guid? visitorId = null;
        if (Guid.TryParse(command.VisitorId, out var parsedVisitorId))
            visitorId = parsedVisitorId;

        const bool consentGiven = true;

        var result = await _upsertLeadHandler.HandleAsync(
            new UpsertLeadFromPromoEntryCommand(
                TenantId:                context.Session.TenantId,
                SiteId:                  context.Session.SiteId,
                VisitorId:               visitorId,
                FirstPartyId:            null,
                SessionId:               command.CollectorSessionId,
                Email:                   context.Session.CapturedEmail,
                Name:                    context.Session.CapturedName,
                ConsentGiven:            consentGiven,
                Phone:                   context.Session.CapturedPhone,
                PreferredContactMethod:  context.Session.CapturedPreferredContactMethod,
                PreferredContactDetail:  null,
                OpportunityLabel:        context.Session.OpportunityLabel,
                IntentScore:             context.Session.IntentScore,
                ConversationSummary:     context.Session.ConversationSummary,
                SuggestedFollowUp:       context.Session.SuggestedFollowUp),
            cancellationToken);

        return result.Status == Shared.Validation.OperationStatus.Success;
    }

    private static bool HasLeadIdentity(EngageChatSession session, ChatSendCommand command)
        => !string.IsNullOrWhiteSpace(session.CapturedEmail)
           || !string.IsNullOrWhiteSpace(command.VisitorId)
           || !string.IsNullOrWhiteSpace(command.CollectorSessionId);

    // ── A/B test conversion tracking (fire-and-forget) ───────────────────────
    private void FireAbTestConversion(EngageChatSession session, ChatSendCommand command)
    {
        if (_botRepository is null || string.IsNullOrWhiteSpace(command.AbTestVariant)) return;
        _ = _botRepository.IncrementAbTestConversionAsync(session.TenantId, session.SiteId, command.AbTestVariant, CancellationToken.None);
    }

    // ── Hot lead notification (fire-and-forget via ILeadNotificationService) ────
    private void FireHotLeadNotification(EngageConversationContext context)
    {
        _leadNotifier?.NotifyHotLead(
            tenantId:         context.Session.TenantId,
            siteId:           context.Session.SiteId,
            capturedName:     context.Session.CapturedName,
            capturedEmail:    context.Session.CapturedEmail,
            userMessage:      context.UserMessage,
            intentScore:      context.Session.IntentScore,
            opportunityLabel: context.Session.OpportunityLabel);
    }
}

public sealed record BusinessOutcomeResult(bool TicketTouched, bool LeadTouched)
{
    public static readonly BusinessOutcomeResult None = new(false, false);
}
