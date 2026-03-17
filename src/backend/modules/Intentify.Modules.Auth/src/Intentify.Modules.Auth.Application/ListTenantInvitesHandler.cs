using Intentify.Shared.Validation;

namespace Intentify.Modules.Auth.Application;

public sealed record TenantInviteResult(
    Guid InviteId,
    string Email,
    string Role,
    DateTime ExpiresAtUtc,
    DateTime? AcceptedAtUtc,
    DateTime? RevokedAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed class ListTenantInvitesHandler
{
    private readonly IUserRepository _users;
    private readonly IInvitationRepository _invitations;

    public ListTenantInvitesHandler(IUserRepository users, IInvitationRepository invitations)
    {
        _users = users;
        _invitations = invitations;
    }

    public async Task<OperationResult<IReadOnlyCollection<TenantInviteResult>>> HandleAsync(
        ListTenantInvitesQuery query,
        CancellationToken cancellationToken = default)
    {
        var actorRole = AuthRoleHierarchy.ResolveActorRole(query.CurrentUserRoles);
        if (!AuthRoleHierarchy.CanListUsers(actorRole))
        {
            return OperationResult<IReadOnlyCollection<TenantInviteResult>>.Forbidden();
        }

        var actor = await _users.GetByIdAsync(query.CurrentUserId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.TenantId != query.TenantId)
        {
            return OperationResult<IReadOnlyCollection<TenantInviteResult>>.Unauthorized();
        }

        var invites = await _invitations.ListByTenantAsync(query.TenantId, cancellationToken);
        var result = invites
            .Select(invite => new TenantInviteResult(
                invite.Id,
                invite.Email,
                invite.Role,
                invite.ExpiresAtUtc,
                invite.AcceptedAtUtc,
                invite.RevokedAtUtc,
                invite.CreatedAtUtc,
                invite.UpdatedAtUtc))
            .ToArray();

        return OperationResult<IReadOnlyCollection<TenantInviteResult>>.Success(result);
    }
}
