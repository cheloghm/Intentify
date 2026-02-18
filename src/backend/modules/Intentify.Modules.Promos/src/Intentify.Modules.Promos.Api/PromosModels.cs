namespace Intentify.Modules.Promos.Api;

public sealed record CreatePromoRequest(Guid SiteId, string Name, string? Description, bool IsActive = true);
public sealed record CreatePublicPromoEntryRequest(string? VisitorId, string? FirstPartyId, string? SessionId, string? Email, string? Name, bool ConsentGiven, string ConsentStatement);
