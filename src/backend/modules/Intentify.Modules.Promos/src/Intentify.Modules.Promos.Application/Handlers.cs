using System.Security.Cryptography;
using Intentify.Modules.Promos.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Promos.Application;

public sealed class CreatePromoHandler
{
    private readonly IPromoRepository _promoRepository;
    public CreatePromoHandler(IPromoRepository promoRepository) => _promoRepository = promoRepository;

    public async Task<OperationResult<Promo>> HandleAsync(CreatePromoCommand command, CancellationToken cancellationToken = default)
    {
        var errors = ValidateCreate(command.Name, command.Questions);
        if (errors.HasErrors) return OperationResult<Promo>.ValidationFailed(errors);

        var now = DateTime.UtcNow;
        var promo = new Promo
        {
            TenantId = command.TenantId,
            SiteId = command.SiteId,
            Name = command.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            IsActive = command.IsActive,
            PublicKey = GeneratePublicKey(),
            FlyerFileName = TrimOrNull(command.FlyerFileName, 260),
            FlyerContentType = TrimOrNull(command.FlyerContentType, 100),
            FlyerBytes = command.FlyerBytes,
            FlyerSizeBytes = command.FlyerBytes?.LongLength,
            Questions = NormalizeQuestions(command.Questions),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _promoRepository.InsertAsync(promo, cancellationToken);
        return OperationResult<Promo>.Success(promo);
    }

    private static ValidationErrors ValidateCreate(string name, IReadOnlyCollection<PromoQuestion>? questions)
    {
        var errors = new ValidationErrors();
        if (string.IsNullOrWhiteSpace(name)) errors.Add("name", "Name is required.");
        if (!string.IsNullOrWhiteSpace(name) && name.Trim().Length > 200) errors.Add("name", "Name must be 200 characters or fewer.");

        if (questions is null)
        {
            return errors;
        }

        var uniqueKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var question in questions)
        {
            var key = question.Key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add("questions", "Question key is required.");
                continue;
            }

            if (!uniqueKeys.Add(key))
            {
                errors.Add("questions", $"Question key '{key}' is duplicated.");
            }
        }

        return errors;
    }

    private static IReadOnlyCollection<PromoQuestion> NormalizeQuestions(IReadOnlyCollection<PromoQuestion>? questions)
    {
        if (questions is null || questions.Count == 0)
        {
            return [];
        }

        return questions
            .Select((question, index) => new PromoQuestion(
                question.Key.Trim(),
                string.IsNullOrWhiteSpace(question.Label) ? question.Key.Trim() : question.Label.Trim(),
                string.IsNullOrWhiteSpace(question.Type) ? "text" : question.Type.Trim(),
                question.Required,
                question.Order == 0 ? index : question.Order))
            .OrderBy(question => question.Order)
            .ToArray();
    }

    private static string GeneratePublicKey()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string? TrimOrNull(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

public sealed class ListPromosHandler
{
    private readonly IPromoRepository _promoRepository;
    public ListPromosHandler(IPromoRepository promoRepository) => _promoRepository = promoRepository;
    public Task<IReadOnlyCollection<Promo>> HandleAsync(ListPromosQuery query, CancellationToken cancellationToken = default) => _promoRepository.ListAsync(query, cancellationToken);
}

public sealed class ListPromoEntriesHandler
{
    private readonly IPromoRepository _promoRepository;
    private readonly IPromoEntryRepository _entryRepository;
    public ListPromoEntriesHandler(IPromoRepository promoRepository, IPromoEntryRepository entryRepository)
    {
        _promoRepository = promoRepository;
        _entryRepository = entryRepository;
    }

    public async Task<OperationResult<IReadOnlyCollection<PromoEntry>>> HandleAsync(ListPromoEntriesQuery query, CancellationToken cancellationToken = default)
    {
        var promo = await _promoRepository.GetByIdAsync(query.TenantId, query.PromoId, cancellationToken);
        if (promo is null) return OperationResult<IReadOnlyCollection<PromoEntry>>.NotFound();
        var entries = await _entryRepository.ListByPromoAsync(query, cancellationToken);
        return OperationResult<IReadOnlyCollection<PromoEntry>>.Success(entries);
    }
}

public sealed class GetPromoDetailHandler
{
    private readonly IPromoRepository _promoRepository;
    private readonly IPromoEntryRepository _entryRepository;

    public GetPromoDetailHandler(IPromoRepository promoRepository, IPromoEntryRepository entryRepository)
    {
        _promoRepository = promoRepository;
        _entryRepository = entryRepository;
    }

    public async Task<OperationResult<PromoDetailResult>> HandleAsync(GetPromoDetailQuery query, CancellationToken cancellationToken = default)
    {
        var promo = await _promoRepository.GetByIdAsync(query.TenantId, query.PromoId, cancellationToken);
        if (promo is null)
        {
            return OperationResult<PromoDetailResult>.NotFound();
        }

        var entries = await _entryRepository.ListByPromoAsync(new ListPromoEntriesQuery(query.TenantId, query.PromoId, query.EntryPage, query.EntryPageSize), cancellationToken);
        return OperationResult<PromoDetailResult>.Success(new PromoDetailResult(promo, entries));
    }
}

public sealed class CreatePublicPromoEntryHandler
{
    private readonly IPromoRepository _promoRepository;
    private readonly IPromoEntryRepository _entryRepository;
    private readonly IPromoConsentLogRepository _consentRepository;
    private readonly IPromoVisitorLookup _visitorLookup;

    public CreatePublicPromoEntryHandler(IPromoRepository promoRepository, IPromoEntryRepository entryRepository, IPromoConsentLogRepository consentRepository, IPromoVisitorLookup visitorLookup)
    {
        _promoRepository = promoRepository;
        _entryRepository = entryRepository;
        _consentRepository = consentRepository;
        _visitorLookup = visitorLookup;
    }

    public async Task<OperationResult<PromoEntry>> HandleAsync(CreatePublicPromoEntryCommand command, CancellationToken cancellationToken = default)
    {
        var errors = Validate(command);
        if (errors.HasErrors) return OperationResult<PromoEntry>.ValidationFailed(errors);

        var promo = await _promoRepository.GetActiveByPublicKeyAsync(command.PromoKey, cancellationToken);
        if (promo is null) return OperationResult<PromoEntry>.NotFound();

        var normalizedAnswers = NormalizeAnswers(command.Answers);
        ValidateRequiredAnswers(errors, promo.Questions, normalizedAnswers);
        if (errors.HasErrors)
        {
            return OperationResult<PromoEntry>.ValidationFailed(errors);
        }

        var parsedVisitorId = Guid.TryParse(command.VisitorId, out var visitorIdValue) ? visitorIdValue : (Guid?)null;
        var linkedVisitorId = await _visitorLookup.ResolveVisitorIdAsync(promo.TenantId, promo.SiteId, parsedVisitorId, command.FirstPartyId, command.SessionId, cancellationToken);

        var now = DateTime.UtcNow;
        var entry = new PromoEntry
        {
            TenantId = promo.TenantId,
            PromoId = promo.Id,
            VisitorId = linkedVisitorId,
            FirstPartyId = TrimOrNull(command.FirstPartyId, 200),
            SessionId = TrimOrNull(command.SessionId, 200),
            Email = TrimOrNull(command.Email, 320),
            Name = TrimOrNull(command.Name, 200),
            Answers = normalizedAnswers,
            CreatedAtUtc = now
        };

        await _entryRepository.InsertAsync(entry, cancellationToken);
        await _consentRepository.InsertAsync(new PromoConsentLog
        {
            TenantId = promo.TenantId,
            PromoEntryId = entry.Id,
            ConsentGiven = command.ConsentGiven,
            ConsentStatement = command.ConsentStatement.Trim(),
            CreatedAtUtc = now
        }, cancellationToken);

        return OperationResult<PromoEntry>.Success(entry);
    }

    private static ValidationErrors Validate(CreatePublicPromoEntryCommand command)
    {
        var errors = new ValidationErrors();
        if (string.IsNullOrWhiteSpace(command.PromoKey)) errors.Add("promoKey", "Promo key is required.");
        if (string.IsNullOrWhiteSpace(command.ConsentStatement)) errors.Add("consentStatement", "Consent statement is required.");
        if (!string.IsNullOrWhiteSpace(command.ConsentStatement) && command.ConsentStatement.Length > 2000) errors.Add("consentStatement", "Consent statement is too long.");
        if (!string.IsNullOrWhiteSpace(command.Email) && command.Email.Length > 320) errors.Add("email", "Email is too long.");
        if (!string.IsNullOrWhiteSpace(command.Name) && command.Name.Length > 200) errors.Add("name", "Name is too long.");
        if (!string.IsNullOrWhiteSpace(command.FirstPartyId) && command.FirstPartyId.Length > 200) errors.Add("firstPartyId", "First-party id is too long.");
        if (!string.IsNullOrWhiteSpace(command.SessionId) && command.SessionId.Length > 200) errors.Add("sessionId", "Session id is too long.");
        return errors;
    }

    private static IReadOnlyDictionary<string, string>? NormalizeAnswers(IReadOnlyDictionary<string, string>? answers)
    {
        if (answers is null || answers.Count == 0)
        {
            return null;
        }

        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in answers)
        {
            var trimmedKey = key?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedKey))
            {
                continue;
            }

            normalized[trimmedKey] = value?.Trim() ?? string.Empty;
        }

        return normalized.Count == 0 ? null : normalized;
    }

    private static void ValidateRequiredAnswers(ValidationErrors errors, IReadOnlyCollection<PromoQuestion> questions, IReadOnlyDictionary<string, string>? answers)
    {
        if (questions.Count == 0)
        {
            return;
        }

        foreach (var requiredQuestion in questions.Where(question => question.Required))
        {
            if (answers is null || !answers.TryGetValue(requiredQuestion.Key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                errors.Add("answers", $"Answer for required question '{requiredQuestion.Key}' is required.");
            }
        }
    }

    private static string? TrimOrNull(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
