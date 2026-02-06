namespace Intentify.Modules.Auth.Api;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string AccessToken);

public sealed record CurrentUserResponse(string UserId, string TenantId, IReadOnlyCollection<string> Roles);
