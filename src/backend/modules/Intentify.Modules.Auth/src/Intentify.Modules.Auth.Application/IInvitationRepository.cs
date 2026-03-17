using Intentify.Modules.Auth.Domain;

namespace Intentify.Modules.Auth.Application;

public interface IInvitationRepository
{
    Task<Invitation?> GetByIdAsync(Guid invitationId, CancellationToken cancellationToken = default);
    Task<Invitation?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Invitation>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task InsertAsync(Invitation invitation, CancellationToken cancellationToken = default);
    Task<bool> MarkAcceptedAsync(Guid invitationId, DateTime acceptedAtUtc, CancellationToken cancellationToken = default);
    Task<bool> MarkRevokedAsync(Guid invitationId, DateTime revokedAtUtc, CancellationToken cancellationToken = default);
}
