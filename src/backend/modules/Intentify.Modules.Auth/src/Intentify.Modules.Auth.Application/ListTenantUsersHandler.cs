using Intentify.Shared.Validation;

namespace Intentify.Modules.Auth.Application;

public sealed record TenantUserResult(
    Guid UserId,
    string Email,
    string DisplayName,
    IReadOnlyCollection<string> Roles,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed class ListTenantUsersHandler
{
    private readonly IUserRepository _users;

    public ListTenantUsersHandler(IUserRepository users)
    {
        _users = users;
    }

    public async Task<OperationResult<IReadOnlyCollection<TenantUserResult>>> HandleAsync(
        ListTenantUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        var actorRole = AuthRoleHierarchy.ResolveActorRole(query.CurrentUserRoles);
        if (!AuthRoleHierarchy.CanListUsers(actorRole))
        {
            return OperationResult<IReadOnlyCollection<TenantUserResult>>.Forbidden();
        }

        var actor = await _users.GetByIdAsync(query.CurrentUserId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.TenantId != query.TenantId)
        {
            return OperationResult<IReadOnlyCollection<TenantUserResult>>.Unauthorized();
        }

        var users = await _users.ListByTenantAsync(query.TenantId, cancellationToken);

        var result = users
            .Select(user => new TenantUserResult(
                user.Id,
                user.Email,
                user.DisplayName,
                user.Roles,
                user.IsActive,
                user.CreatedAt,
                user.UpdatedAt))
            .ToArray();

        return OperationResult<IReadOnlyCollection<TenantUserResult>>.Success(result);
    }
}
