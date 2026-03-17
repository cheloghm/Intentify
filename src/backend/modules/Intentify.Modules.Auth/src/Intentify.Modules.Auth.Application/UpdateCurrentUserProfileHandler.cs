using Intentify.Modules.Auth.Domain;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Auth.Application;

public sealed class UpdateCurrentUserProfileHandler
{
    private readonly IUserRepository _users;
    private readonly ITenantRepository _tenants;

    public UpdateCurrentUserProfileHandler(IUserRepository users, ITenantRepository tenants)
    {
        _users = users;
        _tenants = tenants;
    }

    public async Task<OperationResult<UpdateCurrentUserProfileResult>> HandleAsync(
        UpdateCurrentUserProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrors();

        Guard.AgainstNullOrWhiteSpace(errors, command.DisplayName, "displayName", "Display name is required.");

        var wantsOrganizationChange = !string.IsNullOrWhiteSpace(command.OrganizationName);
        if (wantsOrganizationChange && !CanUpdateOrganization(command.Roles))
        {
            return OperationResult<UpdateCurrentUserProfileResult>.Forbidden();
        }

        if (errors.HasErrors)
        {
            return OperationResult<UpdateCurrentUserProfileResult>.ValidationFailed(errors);
        }

        var user = await _users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null || user.TenantId != command.TenantId)
        {
            return OperationResult<UpdateCurrentUserProfileResult>.Unauthorized();
        }

        await _users.UpdateDisplayNameAsync(
            command.UserId,
            command.DisplayName.Trim(),
            DateTime.UtcNow,
            cancellationToken);

        if (wantsOrganizationChange)
        {
            await _tenants.UpdateNameAsync(
                command.TenantId,
                command.OrganizationName!.Trim(),
                DateTime.UtcNow,
                cancellationToken);
        }

        return OperationResult<UpdateCurrentUserProfileResult>.Success(new UpdateCurrentUserProfileResult());
    }

    private static bool CanUpdateOrganization(IReadOnlyCollection<string> roles)
    {
        return roles.Any(role =>
            string.Equals(role, AuthRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, AuthRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase));
    }
}