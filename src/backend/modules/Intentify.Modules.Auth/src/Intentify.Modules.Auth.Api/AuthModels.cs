namespace Intentify.Modules.Auth.Api;

public sealed record LoginRequest(string Email, string Password);

public sealed record RegisterRequest(string DisplayName, string Email, string Password, string OrganizationName, string? Plan = null);

public sealed record LoginResponse(string AccessToken);

public sealed record CurrentUserResponse(
    string UserId,
    string TenantId,
    IReadOnlyCollection<string> Roles,
    string? DisplayName,
    string? Email,
    string? OrganizationName,
    bool IsAdmin);

public sealed record UpdateCurrentUserProfileRequest(string DisplayName, string? OrganizationName);

public sealed record CreateInviteRequest(string Email, string Role);

public sealed record CreateInviteResponse(string Token, string Email, string Role, DateTime ExpiresAtUtc);

public sealed record AcceptInviteRequest(string Token, string DisplayName, string Email, string Password);

public sealed record TenantUserResponse(
    string UserId,
    string Email,
    string DisplayName,
    IReadOnlyCollection<string> Roles,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record ChangeTenantUserRoleRequest(string Role);

public sealed record TenantInviteResponse(
    string InviteId,
    string Email,
    string Role,
    DateTime ExpiresAtUtc,
    DateTime? AcceptedAtUtc,
    DateTime? RevokedAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
