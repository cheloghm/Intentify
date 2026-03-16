namespace Intentify.Modules.Auth.Api;

public sealed record LoginRequest(string Email, string Password);

public sealed record RegisterRequest(string DisplayName, string Email, string Password, string OrganizationName);

public sealed record LoginResponse(string AccessToken);

public sealed record CurrentUserResponse(string UserId, string TenantId, IReadOnlyCollection<string> Roles, string? DisplayName);

public sealed record CreateInviteRequest(string Email, string Role);

public sealed record CreateInviteResponse(string Token, string Email, string Role, DateTime ExpiresAtUtc);

public sealed record AcceptInviteRequest(string Token, string DisplayName, string Email, string Password);
