namespace Intentify.Modules.Auth.Application;

public sealed record RegisterUserCommand(string DisplayName, string Email, string Password);

public sealed record LoginUserCommand(string Email, string Password);
