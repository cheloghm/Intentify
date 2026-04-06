namespace Intentify.Modules.Auth.Application;

public sealed record RegisterUserCommand(string DisplayName, string Email, string Password, string OrganizationName, string? Plan = null);

public sealed record LoginUserCommand(string Email, string Password);

public sealed record CreateInviteCommand(Guid TenantId, Guid InvitedByUserId, IReadOnlyCollection<string> InviterRoles, string Email, string Role);

public sealed record AcceptInviteCommand(string Token, string DisplayName, string Email, string Password);

public sealed record UpdateCurrentUserProfileCommand(
    Guid UserId,
    Guid TenantId,
    IReadOnlyCollection<string> Roles,
    string DisplayName,
    string? OrganizationName);

public sealed record ListTenantUsersQuery(Guid TenantId, Guid CurrentUserId, IReadOnlyCollection<string> CurrentUserRoles);

public sealed record ChangeTenantUserRoleCommand(
    Guid TenantId,
    Guid CurrentUserId,
    IReadOnlyCollection<string> CurrentUserRoles,
    Guid TargetUserId,
    string Role);

public sealed record RemoveTenantUserCommand(
    Guid TenantId,
    Guid CurrentUserId,
    IReadOnlyCollection<string> CurrentUserRoles,
    Guid TargetUserId);

public sealed record ListTenantInvitesQuery(
    Guid TenantId,
    Guid CurrentUserId,
    IReadOnlyCollection<string> CurrentUserRoles);

public sealed record RevokeTenantInviteCommand(
    Guid TenantId,
    Guid CurrentUserId,
    IReadOnlyCollection<string> CurrentUserRoles,
    Guid InviteId);
