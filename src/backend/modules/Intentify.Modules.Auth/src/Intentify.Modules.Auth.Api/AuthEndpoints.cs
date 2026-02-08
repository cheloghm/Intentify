using Intentify.Modules.Auth.Domain;
using Intentify.Shared.Security;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Net.Mail;

namespace Intentify.Modules.Auth.Api;

internal static class AuthEndpoints
{
    public static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IMongoDatabase database,
        PasswordHasher hasher,
        JwtTokenIssuer tokenIssuer,
        IOptions<JwtOptions> jwtOptions)
    {
        var registrationErrors = ValidateRegistrationRequest(request);
        if (registrationErrors.Count > 0)
        {
            var problemDetails = ProblemDetailsHelpers.CreateValidationProblemDetails(registrationErrors);
            return Results.BadRequest(problemDetails);
        }

        var users = database.GetCollection<User>(AuthMongoCollections.Users);
        var existingUser = await users.Find(x => x.Email == request.Email).FirstOrDefaultAsync();
        if (existingUser is not null)
        {
            var problemDetails = ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["email"] = ["Email is already registered."]
            });
            return Results.BadRequest(problemDetails);
        }

        var tenants = database.GetCollection<Tenant>(AuthMongoCollections.Tenants);
        var tenant = await tenants.Find(_ => true).FirstOrDefaultAsync();
        if (tenant is null)
        {
            tenant = new Tenant
            {
                Name = "Intentify",
                Domain = "intentify.local",
                Plan = "dev",
                Industry = "software",
                Category = "default"
            };

            await tenants.InsertOneAsync(tenant);
        }

        var user = new User
        {
            TenantId = tenant.Id,
            Email = request.Email,
            PasswordHash = hasher.HashPassword(request.Password),
            DisplayName = request.DisplayName,
            Roles = new[] { AuthRoles.User }
        };

        await users.InsertOneAsync(user);

        var tokenResult = tokenIssuer.IssueAccessToken(
            user.Id.ToString("N"),
            user.TenantId.ToString("N"),
            user.Roles,
            jwtOptions.Value);

        if (!tokenResult.IsSuccess || string.IsNullOrWhiteSpace(tokenResult.Value))
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Ok(new LoginResponse(tokenResult.Value));
    }

    public static async Task<IResult> LoginAsync(
        LoginRequest request,
        IMongoDatabase database,
        PasswordHasher hasher,
        JwtTokenIssuer tokenIssuer,
        IOptions<JwtOptions> jwtOptions)
    {
        var loginErrors = ValidateLoginRequest(request);
        if (loginErrors.Count > 0)
        {
            var problemDetails = ProblemDetailsHelpers.CreateValidationProblemDetails(loginErrors);
            return Results.BadRequest(problemDetails);
        }

        var users = database.GetCollection<User>(AuthMongoCollections.Users);
        var user = await users.Find(x => x.Email == request.Email).FirstOrDefaultAsync();

        if (user is null || !user.IsActive || !hasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Results.Unauthorized();
        }

        var tokenResult = tokenIssuer.IssueAccessToken(
            user.Id.ToString("N"),
            user.TenantId.ToString("N"),
            user.Roles,
            jwtOptions.Value);

        if (!tokenResult.IsSuccess || string.IsNullOrWhiteSpace(tokenResult.Value))
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Ok(new LoginResponse(tokenResult.Value));
    }

    public static async Task<IResult> GetCurrentUser(HttpContext context, IMongoDatabase database)
    {
        var response = await CurrentUserResponseFactory.CreateAsync(context, database);
        return Results.Ok(response);
    }

    private static Dictionary<string, string[]> ValidateRegistrationRequest(RegisterRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            errors["displayName"] = ["Display name is required."];
        }

        if (!IsValidEmail(request.Email))
        {
            errors["email"] = ["Email is invalid."];
        }

        if (!IsValidPassword(request.Password))
        {
            errors["password"] = ["Password must be at least 10 characters and contain at least one letter and one digit."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateLoginRequest(LoginRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (!IsValidEmail(request.Email))
        {
            errors["email"] = ["Email is invalid."];
        }

        if (!IsValidPassword(request.Password))
        {
            errors["password"] = ["Password must be at least 10 characters and contain at least one letter and one digit."];
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
