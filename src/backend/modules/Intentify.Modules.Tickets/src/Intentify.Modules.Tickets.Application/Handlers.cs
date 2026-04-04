using Intentify.Modules.Tickets.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Tickets.Application;

public sealed class CreateTicketHandler
{
    private readonly ITicketRepository _repository;
    private readonly IReadOnlyCollection<ITicketEventObserver> _observers;

    public CreateTicketHandler(ITicketRepository repository, IEnumerable<ITicketEventObserver> observers)
    {
        _repository = repository;
        _observers = observers.ToArray();
    }

    public async Task<OperationResult<Ticket>> HandleAsync(CreateTicketCommand command, CancellationToken cancellationToken = default)
    {
        var errors = TicketValidation.ValidateSubjectAndDescription(command.Subject, command.Description);
        if (errors.HasErrors)
        {
            return OperationResult<Ticket>.ValidationFailed(errors);
        }

        var now = DateTime.UtcNow;
        var ticket = new Ticket
        {
            TenantId = command.TenantId,
            SiteId = command.SiteId,
            VisitorId = command.VisitorId,
            EngageSessionId = command.EngageSessionId,
            Subject = command.Subject.Trim(),
            Description = command.Description.Trim(),
            ContactName = Normalize(command.ContactName, 200),
            PreferredContactMethod = Normalize(command.PreferredContactMethod, 32),
            PreferredContactDetail = Normalize(command.PreferredContactDetail, 320),
            OpportunityLabel = Normalize(command.OpportunityLabel, 80),
            IntentScore = command.IntentScore,
            ConversationSummary = Normalize(command.ConversationSummary, 1200),
            SuggestedFollowUp = Normalize(command.SuggestedFollowUp, 800),
            Status = TicketStatuses.Open,
            AssignedToUserId = command.AssignedToUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _repository.InsertAsync(ticket, cancellationToken);

        var notification = new TicketCreatedNotification(
            command.TenantId,
            command.SiteId,
            ticket.Id,
            ticket.Subject,
            ticket.CreatedAtUtc);

        foreach (var observer in _observers)
        {
            await observer.OnTicketCreatedAsync(notification, cancellationToken);
        }

        return OperationResult<Ticket>.Success(ticket);
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

public sealed class GetTicketHandler
{
    private readonly ITicketRepository _repository;
    public GetTicketHandler(ITicketRepository repository) => _repository = repository;

    public async Task<OperationResult<Ticket>> HandleAsync(GetTicketQuery query, CancellationToken cancellationToken = default)
    {
        var ticket = await _repository.GetByIdAsync(query.TenantId, query.TicketId, cancellationToken);
        return ticket is null ? OperationResult<Ticket>.NotFound() : OperationResult<Ticket>.Success(ticket);
    }
}

public sealed class ListTicketsHandler
{
    private readonly ITicketRepository _repository;
    public ListTicketsHandler(ITicketRepository repository) => _repository = repository;

    public Task<IReadOnlyCollection<TicketListItem>> HandleAsync(ListTicketsQuery query, CancellationToken cancellationToken = default)
        => _repository.ListAsync(query, cancellationToken);
}

public sealed class UpdateTicketHandler
{
    private readonly ITicketRepository _repository;
    public UpdateTicketHandler(ITicketRepository repository) => _repository = repository;

    public async Task<OperationResult<Ticket>> HandleAsync(UpdateTicketCommand command, CancellationToken cancellationToken = default)
    {
        var errors = TicketValidation.ValidateSubjectAndDescription(command.Subject, command.Description);
        if (errors.HasErrors)
        {
            return OperationResult<Ticket>.ValidationFailed(errors);
        }

        var ticket = await _repository.GetByIdAsync(command.TenantId, command.TicketId, cancellationToken);
        if (ticket is null)
        {
            return OperationResult<Ticket>.NotFound();
        }

        ticket.Subject = command.Subject.Trim();
        ticket.Description = command.Description.Trim();
        ticket.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.ReplaceAsync(ticket, cancellationToken);
        return OperationResult<Ticket>.Success(ticket);
    }
}

public sealed class SetTicketAssignmentHandler
{
    private readonly ITicketRepository _repository;
    public SetTicketAssignmentHandler(ITicketRepository repository) => _repository = repository;

    public async Task<OperationResult<Ticket>> HandleAsync(SetTicketAssignmentCommand command, CancellationToken cancellationToken = default)
    {
        var ticket = await _repository.GetByIdAsync(command.TenantId, command.TicketId, cancellationToken);
        if (ticket is null)
        {
            return OperationResult<Ticket>.NotFound();
        }

        ticket.AssignedToUserId = command.AssignedToUserId;
        ticket.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.ReplaceAsync(ticket, cancellationToken);
        return OperationResult<Ticket>.Success(ticket);
    }
}

public sealed class AddTicketNoteHandler
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketNoteRepository _noteRepository;

    public AddTicketNoteHandler(ITicketRepository ticketRepository, ITicketNoteRepository noteRepository)
    {
        _ticketRepository = ticketRepository;
        _noteRepository = noteRepository;
    }

    public async Task<OperationResult<TicketNote>> HandleAsync(AddTicketNoteCommand command, CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrors();
        if (string.IsNullOrWhiteSpace(command.Content))
        {
            errors.Add("content", "Content is required.");
        }

        if (errors.HasErrors)
        {
            return OperationResult<TicketNote>.ValidationFailed(errors);
        }

        var ticket = await _ticketRepository.GetByIdAsync(command.TenantId, command.TicketId, cancellationToken);
        if (ticket is null)
        {
            return OperationResult<TicketNote>.NotFound();
        }

        var note = new TicketNote
        {
            TenantId = command.TenantId,
            TicketId = command.TicketId,
            AuthorUserId = command.AuthorUserId,
            Content = command.Content.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        await _noteRepository.InsertAsync(note, cancellationToken);
        return OperationResult<TicketNote>.Success(note);
    }
}

public sealed class ListTicketNotesHandler
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketNoteRepository _noteRepository;

    public ListTicketNotesHandler(ITicketRepository ticketRepository, ITicketNoteRepository noteRepository)
    {
        _ticketRepository = ticketRepository;
        _noteRepository = noteRepository;
    }

    public async Task<OperationResult<IReadOnlyCollection<TicketNote>>> HandleAsync(ListTicketNotesQuery query, CancellationToken cancellationToken = default)
    {
        var ticket = await _ticketRepository.GetByIdAsync(query.TenantId, query.TicketId, cancellationToken);
        if (ticket is null)
        {
            return OperationResult<IReadOnlyCollection<TicketNote>>.NotFound();
        }

        var notes = await _noteRepository.ListAsync(query, cancellationToken);
        return OperationResult<IReadOnlyCollection<TicketNote>>.Success(notes);
    }
}

public sealed class TransitionTicketStatusHandler
{
    private static readonly IReadOnlyDictionary<string, HashSet<string>> AllowedTransitions = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
    {
        [TicketStatuses.Open] = new HashSet<string>(StringComparer.Ordinal) { TicketStatuses.InProgress },
        [TicketStatuses.InProgress] = new HashSet<string>(StringComparer.Ordinal) { TicketStatuses.Resolved },
        [TicketStatuses.Resolved] = new HashSet<string>(StringComparer.Ordinal) { TicketStatuses.Closed, TicketStatuses.Open },
        [TicketStatuses.Closed] = new HashSet<string>(StringComparer.Ordinal) { TicketStatuses.Open }
    };

    private readonly ITicketRepository _repository;
    public TransitionTicketStatusHandler(ITicketRepository repository) => _repository = repository;

    public async Task<OperationResult<Ticket>> HandleAsync(TransitionTicketStatusCommand command, CancellationToken cancellationToken = default)
    {
        if (!TicketStatuses.Allowed.Contains(command.ToStatus))
        {
            var errors = new ValidationErrors();
            errors.Add("status", "Status is invalid.");
            return OperationResult<Ticket>.ValidationFailed(errors);
        }

        var ticket = await _repository.GetByIdAsync(command.TenantId, command.TicketId, cancellationToken);
        if (ticket is null)
        {
            return OperationResult<Ticket>.NotFound();
        }

        if (!AllowedTransitions.TryGetValue(ticket.Status, out var allowedNext)
            || !allowedNext.Contains(command.ToStatus))
        {
            var errors = new ValidationErrors();
            errors.Add("status", $"Invalid status transition from '{ticket.Status}' to '{command.ToStatus}'.");
            return OperationResult<Ticket>.ValidationFailed(errors);
        }

        ticket.Status = command.ToStatus;
        if (string.Equals(command.ToStatus, TicketStatuses.InProgress, StringComparison.Ordinal)
            && ticket.AssignedToUserId is null
            && command.CurrentUserId is { } currentUserId)
        {
            ticket.AssignedToUserId = currentUserId;
        }

        ticket.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.ReplaceAsync(ticket, cancellationToken);
        return OperationResult<Ticket>.Success(ticket);
    }
}

internal static class TicketValidation
{
    public static ValidationErrors ValidateSubjectAndDescription(string subject, string description)
    {
        var errors = new ValidationErrors();

        if (string.IsNullOrWhiteSpace(subject))
        {
            errors.Add("subject", "Subject is required.");
        }
        else if (subject.Trim().Length > 200)
        {
            errors.Add("subject", "Subject must be 200 characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            errors.Add("description", "Description is required.");
        }

        return errors;
    }
}
