using System.Net.Mail;
using Intentify.Modules.Auth.Domain;
using Intentify.Shared.Security;
using Intentify.Shared.Validation;
using Microsoft.Extensions.Options;

namespace Intentify.Modules.Auth.Application;

public sealed class RegisterUserHandler
{
    private readonly IUserRepository _users;
    private readonly ITenantRepository _tenants;
    private readonly PasswordHasher _hasher;
    private readonly JwtTokenIssuer _tokenIssuer;
    private readonly IOptions<JwtOptions> _jwtOptions;

    public RegisterUserHandler(
        IUserRepository users,
        ITenantRepository tenants,
        PasswordHasher hasher,
        JwtTokenIssuer tokenIssuer,
        IOptions<JwtOptions> jwtOptions)
    {
        _users = users;
        _tenants = tenants;
        _hasher = hasher;
        _tokenIssuer = tokenIssuer;
        _jwtOptions = jwtOptions;
    }

    public async Task<OperationResult<AuthTokenResult>> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken = default)
    {
        var errors = Validate(command);
        if (errors.HasErrors)
        {
            return OperationResult<AuthTokenResult>.ValidationFailed(errors);
        }

        var trimmedEmail = command.Email.Trim();
        var existingUser = await _users.FindByEmailAsync(trimmedEmail, cancellationToken);
        if (existingUser is not null)
        {
            errors.Add("email", "Email is already registered.");
            return OperationResult<AuthTokenResult>.ValidationFailed(errors);
        }

        var tenant = new Tenant
        {
            Name = command.DisplayName.Trim(),
            Domain = trimmedEmail.Split('@')[1],
            Plan = "dev",
            Industry = "software",
            Category = "default"
        };

        await _tenants.InsertAsync(tenant, cancellationToken);

        var user = new User
        {
            TenantId = tenant.Id,
            Email = trimmedEmail,
            PasswordHash = _hasher.HashPassword(command.Password),
            DisplayName = command.DisplayName.Trim(),
            Roles = new[] { AuthRoles.User }
        };

        await _users.InsertAsync(user, cancellationToken);

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

    private static ValidationErrors Validate(RegisterUserCommand command)
    {
        var errors = new ValidationErrors();

        Guard.AgainstNullOrWhiteSpace(errors, command.DisplayName, "displayName", "Display name is required.");

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
