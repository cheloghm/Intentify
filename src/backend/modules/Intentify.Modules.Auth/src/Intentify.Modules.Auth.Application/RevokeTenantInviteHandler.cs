using Intentify.Modules.Auth.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Auth.Application;

public sealed class RevokeTenantInviteHandler
{
    private readonly IUserRepository _users;
    private readonly IInvitationRepository _invitations;

    public RevokeTenantInviteHandler(IUserRepository users, IInvitationRepository invitations)
    {
        _users = users;
        _invitations = invitations;
    }

    public async Task<OperationResult<RevokeTenantInviteResult>> HandleAsync(
        RevokeTenantInviteCommand command,
        CancellationToken cancellationToken = default)
    {
        var actorRole = AuthRoleHierarchy.ResolveActorRole(command.CurrentUserRoles);
        if (!AuthRoleHierarchy.CanListUsers(actorRole))
        {
            return OperationResult<RevokeTenantInviteResult>.Forbidden();
        }

        var actor = await _users.GetByIdAsync(command.CurrentUserId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.TenantId != command.TenantId)
        {
            return OperationResult<RevokeTenantInviteResult>.Unauthorized();
        }

        var invite = await _invitations.GetByIdAsync(command.InviteId, cancellationToken);
        if (invite is null || invite.TenantId != command.TenantId)
        {
            return OperationResult<RevokeTenantInviteResult>.Unauthorized();
        }

        if (invite.AcceptedAtUtc is not null || invite.RevokedAtUtc is not null || invite.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return OperationResult<RevokeTenantInviteResult>.Forbidden();
        }

        if (!AuthRoleHierarchy.CanInvite(actorRole, invite.Role))
        {
            return OperationResult<RevokeTenantInviteResult>.Forbidden();
        }

        await _invitations.MarkRevokedAsync(invite.Id, DateTime.UtcNow, cancellationToken);
        return OperationResult<RevokeTenantInviteResult>.Success(new RevokeTenantInviteResult());
    }
}

public sealed record RevokeTenantInviteResult();
