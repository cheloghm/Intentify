using Intentify.Modules.Auth.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Auth.Application;

public sealed class ChangeTenantUserRoleHandler
{
    private readonly IUserRepository _users;

    public ChangeTenantUserRoleHandler(IUserRepository users)
    {
        _users = users;
    }

    public async Task<OperationResult<ChangeTenantUserRoleResult>> HandleAsync(
        ChangeTenantUserRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrors();
        var requestedRole = AuthRoleHierarchy.NormalizeRole(command.Role);
        if (requestedRole is null || requestedRole == AuthRoles.SuperAdmin)
        {
            errors.Add("role", "Role must be 'user', 'manager', or 'admin'.");
        }

        if (errors.HasErrors)
        {
            return OperationResult<ChangeTenantUserRoleResult>.ValidationFailed(errors);
        }

        if (command.CurrentUserId == command.TargetUserId)
        {
            return OperationResult<ChangeTenantUserRoleResult>.Forbidden();
        }

        var actorRole = AuthRoleHierarchy.ResolveActorRole(command.CurrentUserRoles);
        if (!AuthRoleHierarchy.CanListUsers(actorRole))
        {
            return OperationResult<ChangeTenantUserRoleResult>.Forbidden();
        }

        var actor = await _users.GetByIdAsync(command.CurrentUserId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.TenantId != command.TenantId)
        {
            return OperationResult<ChangeTenantUserRoleResult>.Unauthorized();
        }

        var target = await _users.GetByIdAsync(command.TargetUserId, cancellationToken);
        if (target is null || !target.IsActive || target.TenantId != command.TenantId)
        {
            return OperationResult<ChangeTenantUserRoleResult>.Unauthorized();
        }

        var targetPrimaryRole = AuthRoleHierarchy.ResolveActorRole(target.Roles) switch
        {
            TenantActorRole.SuperAdmin => AuthRoles.SuperAdmin,
            TenantActorRole.Admin => AuthRoles.Admin,
            TenantActorRole.Manager => AuthRoles.Manager,
            _ => AuthRoles.User
        };

        if (!AuthRoleHierarchy.CanManage(actorRole, targetPrimaryRole)
            || !AuthRoleHierarchy.CanInvite(actorRole, requestedRole!))
        {
            return OperationResult<ChangeTenantUserRoleResult>.Forbidden();
        }

        if (targetPrimaryRole == AuthRoles.Admin && requestedRole != AuthRoles.Admin)
        {
            var activeAdmins = await _users.CountActiveByTenantAndRoleAsync(command.TenantId, AuthRoles.Admin, cancellationToken);
            if (activeAdmins <= 1)
            {
                return OperationResult<ChangeTenantUserRoleResult>.Forbidden();
            }
        }

        await _users.UpdateRolesAsync(target.Id, [requestedRole!], DateTime.UtcNow, cancellationToken);

        return OperationResult<ChangeTenantUserRoleResult>.Success(new ChangeTenantUserRoleResult());
    }
}

public sealed record ChangeTenantUserRoleResult();
