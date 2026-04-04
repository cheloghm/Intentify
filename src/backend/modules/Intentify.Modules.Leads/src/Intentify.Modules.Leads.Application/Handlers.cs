using System.Linq;
using Intentify.Modules.Leads.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Leads.Application;

public sealed class UpsertLeadFromPromoEntryHandler
{
    private readonly ILeadRepository _leadRepository;
    private readonly ILeadVisitorLinker _visitorLinker;
    private readonly IReadOnlyCollection<ILeadEventObserver> _observers;

    public UpsertLeadFromPromoEntryHandler(ILeadRepository leadRepository, ILeadVisitorLinker visitorLinker, IEnumerable<ILeadEventObserver> observers)
    {
        _leadRepository = leadRepository;
        _visitorLinker = visitorLinker;
        _observers = observers.ToArray();
    }

    public async Task<OperationResult<Lead>> HandleAsync(UpsertLeadFromPromoEntryCommand command, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = Normalize(command.Email, 320);
        var normalizedFirstPartyId = Normalize(command.FirstPartyId, 200);
        var normalizedName = Normalize(command.Name, 200);
        var normalizedPhone = Normalize(command.Phone, 64);
        var normalizedPreferredContactMethod = Normalize(command.PreferredContactMethod, 16);
        var normalizedOpportunityLabel = Normalize(command.OpportunityLabel, 64);
        var normalizedConversationSummary = Normalize(command.ConversationSummary, 1200);
        var normalizedSuggestedFollowUp = Normalize(command.SuggestedFollowUp, 800);

        Lead? lead = null;
        if (!string.IsNullOrWhiteSpace(normalizedEmail))
        {
            lead = await _leadRepository.GetByEmailAsync(command.TenantId, command.SiteId, normalizedEmail, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(normalizedFirstPartyId))
        {
            lead = await _leadRepository.GetByFirstPartyIdAsync(command.TenantId, command.SiteId, normalizedFirstPartyId, cancellationToken);
        }

        var linkedVisitorId = await _visitorLinker.ResolveVisitorIdAsync(command.TenantId, command.SiteId, command.VisitorId, normalizedFirstPartyId, command.SessionId, cancellationToken);
        var now = DateTime.UtcNow;

        bool isNew;
        if (lead is null)
        {
            isNew = true;
            lead = new Lead
            {
                TenantId = command.TenantId,
                SiteId = command.SiteId,
                PrimaryEmail = normalizedEmail,
                DisplayName = normalizedName,
                Phone = normalizedPhone,
                PreferredContactMethod = normalizedPreferredContactMethod,
                OpportunityLabel = normalizedOpportunityLabel,
                IntentScore = command.IntentScore,
                ConversationSummary = normalizedConversationSummary,
                SuggestedFollowUp = normalizedSuggestedFollowUp,
                LinkedVisitorId = linkedVisitorId,
                FirstPartyId = normalizedFirstPartyId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            await _leadRepository.InsertAsync(lead, cancellationToken);
        }
        else
        {
            isNew = false;
            if (lead.PrimaryEmail is null && normalizedEmail is not null)
            {
                lead.PrimaryEmail = normalizedEmail;
            }

            if (ShouldReplaceDisplayName(lead.DisplayName, normalizedName))
            {
                lead.DisplayName = normalizedName;
            }

            if (ShouldReplacePhone(lead.Phone, normalizedPhone))
            {
                lead.Phone = normalizedPhone;
            }

            if (lead.PreferredContactMethod is null && normalizedPreferredContactMethod is not null)
            {
                lead.PreferredContactMethod = normalizedPreferredContactMethod;
            }

            if (lead.OpportunityLabel is null && normalizedOpportunityLabel is not null)
            {
                lead.OpportunityLabel = normalizedOpportunityLabel;
            }

            if (lead.IntentScore is null && command.IntentScore is not null)
            {
                lead.IntentScore = command.IntentScore;
            }

            if (lead.ConversationSummary is null && normalizedConversationSummary is not null)
            {
                lead.ConversationSummary = normalizedConversationSummary;
            }

            if (lead.SuggestedFollowUp is null && normalizedSuggestedFollowUp is not null)
            {
                lead.SuggestedFollowUp = normalizedSuggestedFollowUp;
            }

            if (lead.FirstPartyId is null && normalizedFirstPartyId is not null)
            {
                lead.FirstPartyId = normalizedFirstPartyId;
            }

            if (lead.LinkedVisitorId is null && linkedVisitorId is not null)
            {
                lead.LinkedVisitorId = linkedVisitorId;
            }

            lead.UpdatedAtUtc = now;
            await _leadRepository.ReplaceAsync(lead, cancellationToken);
        }

        await _visitorLinker.EnrichVisitorIfPermittedAsync(
            command.TenantId,
            command.SiteId,
            linkedVisitorId,
            command.ConsentGiven,
            normalizedEmail,
            normalizedName,
            normalizedPhone,
            cancellationToken);

        var notification = new LeadCapturedNotification(
            command.TenantId,
            command.SiteId,
            lead.Id,
            lead.PrimaryEmail,
            lead.DisplayName,
            now,
            isNew);

        foreach (var observer in _observers)
        {
            await observer.OnLeadCapturedAsync(notification, cancellationToken);
        }

        return OperationResult<Lead>.Success(lead);
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static bool ShouldReplaceDisplayName(string? current, string? incoming)
    {
        if (incoming is null)
        {
            return false;
        }

        if (current is null)
        {
            return true;
        }

        var currentParts = current.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var incomingParts = incoming.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return incomingParts > currentParts || incoming.Length > current.Length + 2;
    }

    private static bool ShouldReplacePhone(string? current, string? incoming)
    {
        if (incoming is null)
        {
            return false;
        }

        if (current is null)
        {
            return true;
        }

        var currentDigits = new string(current.Where(char.IsDigit).ToArray());
        var incomingDigits = new string(incoming.Where(char.IsDigit).ToArray());
        return incomingDigits.Length > currentDigits.Length;
    }
}

public sealed class ListLeadsHandler
{
    private readonly ILeadRepository _repository;
    public ListLeadsHandler(ILeadRepository repository) => _repository = repository;

    public Task<IReadOnlyCollection<Lead>> HandleAsync(ListLeadsQuery query, CancellationToken cancellationToken = default)
        => _repository.ListAsync(query, cancellationToken);
}

public sealed class GetLeadHandler
{
    private readonly ILeadRepository _repository;
    public GetLeadHandler(ILeadRepository repository) => _repository = repository;

    public async Task<OperationResult<Lead>> HandleAsync(GetLeadQuery query, CancellationToken cancellationToken = default)
    {
        var lead = await _repository.GetByIdAsync(query.TenantId, query.LeadId, cancellationToken);
        return lead is null ? OperationResult<Lead>.NotFound() : OperationResult<Lead>.Success(lead);
    }
}
