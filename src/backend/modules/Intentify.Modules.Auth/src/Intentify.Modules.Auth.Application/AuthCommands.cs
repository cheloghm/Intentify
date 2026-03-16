namespace Intentify.Modules.Auth.Application;

public sealed record RegisterUserCommand(string DisplayName, string Email, string Password, string OrganizationName);

public sealed record LoginUserCommand(string Email, string Password);

public sealed record CreateInviteCommand(Guid TenantId, Guid InvitedByUserId, IReadOnlyCollection<string> InviterRoles, string Email, string Role);

public sealed record AcceptInviteCommand(string Token, string DisplayName, string Email, string Password);


public sealed record UpdateCurrentUserProfileCommand(Guid UserId, Guid TenantId, IReadOnlyCollection<string> Roles, string DisplayName, string? OrganizationName);
