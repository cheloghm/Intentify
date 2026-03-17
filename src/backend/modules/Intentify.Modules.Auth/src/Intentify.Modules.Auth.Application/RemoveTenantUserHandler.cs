using Intentify.Modules.Auth.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Auth.Application;

public sealed class RemoveTenantUserHandler
{
    private readonly IUserRepository _users;

    public RemoveTenantUserHandler(IUserRepository users)
    {
        _users = users;
    }

    public async Task<OperationResult<RemoveTenantUserResult>> HandleAsync(
        RemoveTenantUserCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.CurrentUserId == command.TargetUserId)
        {
            return OperationResult<RemoveTenantUserResult>.Forbidden();
        }

        var actorRole = AuthRoleHierarchy.ResolveActorRole(command.CurrentUserRoles);
        if (!AuthRoleHierarchy.CanListUsers(actorRole))
        {
            return OperationResult<RemoveTenantUserResult>.Forbidden();
        }

        var actor = await _users.GetByIdAsync(command.CurrentUserId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.TenantId != command.TenantId)
        {
            return OperationResult<RemoveTenantUserResult>.Unauthorized();
        }

        var target = await _users.GetByIdAsync(command.TargetUserId, cancellationToken);
        if (target is null || !target.IsActive || target.TenantId != command.TenantId)
        {
            return OperationResult<RemoveTenantUserResult>.Unauthorized();
        }

        var targetRole = AuthRoleHierarchy.ResolveActorRole(target.Roles) switch
        {
            TenantActorRole.SuperAdmin => AuthRoles.SuperAdmin,
            TenantActorRole.Admin => AuthRoles.Admin,
            TenantActorRole.Manager => AuthRoles.Manager,
            _ => AuthRoles.User
        };

        if (!AuthRoleHierarchy.CanManage(actorRole, targetRole))
        {
            return OperationResult<RemoveTenantUserResult>.Forbidden();
        }

        if (targetRole == AuthRoles.Admin)
        {
            var activeAdmins = await _users.CountActiveByTenantAndRoleAsync(command.TenantId, AuthRoles.Admin, cancellationToken);
            if (activeAdmins <= 1)
            {
                return OperationResult<RemoveTenantUserResult>.Forbidden();
            }
        }

        await _users.DeactivateAsync(target.Id, DateTime.UtcNow, cancellationToken);

        return OperationResult<RemoveTenantUserResult>.Success(new RemoveTenantUserResult());
    }
}

public sealed record RemoveTenantUserResult();
