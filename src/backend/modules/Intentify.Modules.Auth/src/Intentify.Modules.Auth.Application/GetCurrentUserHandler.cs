using Intentify.Modules.Auth.Domain;

namespace Intentify.Modules.Auth.Application;

public sealed record GetCurrentUserQuery(
    Guid UserId,
    Guid TenantId,
    IReadOnlyCollection<string> Roles);

public sealed record GetCurrentUserResult(
    string UserId,
    string TenantId,
    IReadOnlyCollection<string> Roles,
    string? DisplayName,
    string? Email,
    string? OrganizationName,
    bool IsAdmin);

public sealed class GetCurrentUserHandler
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;

    public GetCurrentUserHandler(IUserRepository userRepository, ITenantRepository tenantRepository)
    {
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
    }

    public async Task<GetCurrentUserResult> HandleAsync(GetCurrentUserQuery query, CancellationToken cancellationToken = default)
    {
        string? displayName = null;
        string? email = null;
        var user = await _userRepository.GetByIdAsync(query.UserId, cancellationToken);
        if (user is not null)
        {
            displayName = user.DisplayName;
            email = user.Email;
        }

        string? organizationName = null;
        var tenant = await _tenantRepository.GetByIdAsync(query.TenantId, cancellationToken);
        if (tenant is not null)
        {
            organizationName = tenant.Name;
        }

        var isAdmin = query.Roles.Any(role =>
            role.Equals(AuthRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
            role.Equals(AuthRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase));

        return new GetCurrentUserResult(
            query.UserId.ToString("D"),
            query.TenantId.ToString("D"),
            query.Roles,
            displayName,
            email,
            organizationName,
            isAdmin);
    }
}
