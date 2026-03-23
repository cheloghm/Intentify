using Intentify.Modules.Engage.Domain;

namespace Intentify.Modules.Engage.Application;

public interface IEngageHandoffTicketRepository
{
    Task InsertAsync(EngageHandoffTicket ticket, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<EngageHandoffTicket>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<EngageHandoffTicket>> ListBySiteAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken = default);
}
