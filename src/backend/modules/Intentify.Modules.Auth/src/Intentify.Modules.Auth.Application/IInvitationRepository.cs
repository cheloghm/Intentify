using Intentify.Modules.Auth.Domain;

namespace Intentify.Modules.Auth.Application;

public interface IInvitationRepository
{
    Task<Invitation?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task InsertAsync(Invitation invitation, CancellationToken cancellationToken = default);
    Task<bool> MarkAcceptedAsync(Guid invitationId, DateTime acceptedAtUtc, CancellationToken cancellationToken = default);
}
