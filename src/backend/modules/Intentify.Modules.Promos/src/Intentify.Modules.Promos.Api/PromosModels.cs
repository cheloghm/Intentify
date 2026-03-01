namespace Intentify.Modules.Promos.Api;

public sealed record PromoQuestionRequest(string Key, string Label, string Type, bool Required, int Order);

public sealed record CreatePromoRequest(Guid SiteId, string Name, string? Description, bool IsActive = true, IReadOnlyCollection<PromoQuestionRequest>? Questions = null);

public sealed record CreatePublicPromoEntryRequest(
    string? VisitorId,
    string? FirstPartyId,
    string? SessionId,
    string? Email,
    string? Name,
    bool ConsentGiven,
    string ConsentStatement,
    IReadOnlyDictionary<string, string>? Answers = null);
