using System.Net.Mail;
using Intentify.Shared.Security;
using Intentify.Shared.Validation;
using Microsoft.Extensions.Options;

namespace Intentify.Modules.Auth.Application;

public sealed class LoginUserHandler
{
    private readonly IUserRepository _users;
    private readonly PasswordHasher _hasher;
    private readonly JwtTokenIssuer _tokenIssuer;
    private readonly IOptions<JwtOptions> _jwtOptions;

    public LoginUserHandler(
        IUserRepository users,
        PasswordHasher hasher,
        JwtTokenIssuer tokenIssuer,
        IOptions<JwtOptions> jwtOptions)
    {
        _users = users;
        _hasher = hasher;
        _tokenIssuer = tokenIssuer;
        _jwtOptions = jwtOptions;
    }

    public async Task<OperationResult<AuthTokenResult>> HandleAsync(LoginUserCommand command, CancellationToken cancellationToken = default)
    {
        var errors = Validate(command);
        if (errors.HasErrors)
        {
            return OperationResult<AuthTokenResult>.ValidationFailed(errors);
        }

        var trimmedEmail = command.Email.Trim();
        var user = await _users.FindByEmailAsync(trimmedEmail, cancellationToken);

        if (user is null || !user.IsActive || !_hasher.VerifyPassword(command.Password, user.PasswordHash))
        {
            return OperationResult<AuthTokenResult>.Unauthorized();
        }

        var tokenResult = _tokenIssuer.IssueAccessToken(
            user.Id.ToString("N"),
            user.TenantId.ToString("N"),
            user.Roles,
            _jwtOptions.Value);

        if (!tokenResult.IsSuccess || string.IsNullOrWhiteSpace(tokenResult.Value))
        {
            return OperationResult<AuthTokenResult>.Error();
        }

        return OperationResult<AuthTokenResult>.Success(new AuthTokenResult(tokenResult.Value));
    }

    private static ValidationErrors Validate(LoginUserCommand command)
    {
        var errors = new ValidationErrors();

        if (!IsValidEmail(command.Email))
        {
            errors.Add("email", "Email is invalid.");
        }

        if (!IsValidPassword(command.Password))
        {
            errors.Add("password", "Password must be at least 10 characters and contain at least one letter and one digit.");
        }

        return errors;
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var trimmedEmail = email.Trim();
        try
        {
            var mailAddress = new MailAddress(trimmedEmail);
            if (!string.Equals(mailAddress.Address, trimmedEmail, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var atIndex = trimmedEmail.IndexOf('@');
            if (atIndex <= 0 || atIndex == trimmedEmail.Length - 1)
            {
                return false;
            }

            var domain = trimmedEmail[(atIndex + 1)..];
            return domain.Contains('.', StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsValidPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        if (password.Length < 10)
        {
            return false;
        }

        var hasLetter = false;
        var hasDigit = false;

        foreach (var character in password)
        {
            if (char.IsLetter(character))
            {
                hasLetter = true;
            }
            else if (char.IsDigit(character))
            {
                hasDigit = true;
            }

            if (hasLetter && hasDigit)
            {
                return true;
            }
        }

        return false;
    }
}
