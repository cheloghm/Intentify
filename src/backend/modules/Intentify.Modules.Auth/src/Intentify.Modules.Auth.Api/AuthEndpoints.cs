using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Intentify.Modules.Auth.Application;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;

namespace Intentify.Modules.Auth.Api;

internal static class AuthEndpoints
{
    public static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        RegisterUserHandler handler)
    {
        var result = await handler.HandleAsync(new RegisterUserCommand(
            request.DisplayName,
            request.Email,
            request.Password,
            request.OrganizationName));

        return result.Status switch
        {
            Intentify.Shared.Validation.OperationStatus.ValidationFailed => Results.BadRequest(
                ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            Intentify.Shared.Validation.OperationStatus.Error => Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError),
            _ => Results.Ok(new LoginResponse(result.Value!.AccessToken))
        };
    }

    public static async Task<IResult> LoginAsync(
        LoginRequest request,
        LoginUserHandler handler)
    {
        var result = await handler.HandleAsync(new LoginUserCommand(request.Email, request.Password));

        return result.Status switch
        {
            Intentify.Shared.Validation.OperationStatus.ValidationFailed => Results.BadRequest(
                ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            Intentify.Shared.Validation.OperationStatus.Unauthorized => Results.Unauthorized(),
            Intentify.Shared.Validation.OperationStatus.Error => Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError),
            _ => Results.Ok(new LoginResponse(result.Value!.AccessToken))
        };
    }

    public static async Task<IResult> CreateInviteAsync(
        CreateInviteRequest request,
        HttpContext context,
        CreateInviteHandler handler)
    {
        var userId = TryGetUserId(context.User);
        var tenantId = TryGetTenantId(context.User);
        if (userId is null || tenantId is null)
        {
            return Results.Unauthorized();
        }

        var roles = context.User.FindAll(ClaimTypes.Role).Select(role => role.Value).ToArray();

        var result = await handler.HandleAsync(new CreateInviteCommand(
            tenantId.Value,
            userId.Value,
            roles,
            request.Email,
            request.Role));

        return result.Status switch
        {
            Intentify.Shared.Validation.OperationStatus.ValidationFailed => Results.BadRequest(
                ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            Intentify.Shared.Validation.OperationStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
            Intentify.Shared.Validation.OperationStatus.Error => Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError),
            _ => Results.Ok(new CreateInviteResponse(
                result.Value!.Token,
                result.Value.Email,
                result.Value.Role,
                result.Value.ExpiresAtUtc))
        };
    }

    public static async Task<IResult> AcceptInviteAsync(
        AcceptInviteRequest request,
        AcceptInviteHandler handler)
    {
        var result = await handler.HandleAsync(new AcceptInviteCommand(
            request.Token,
            request.DisplayName,
            request.Email,
            request.Password));

        return result.Status switch
        {
            Intentify.Shared.Validation.OperationStatus.ValidationFailed => Results.BadRequest(
                ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            Intentify.Shared.Validation.OperationStatus.Unauthorized => Results.Unauthorized(),
            Intentify.Shared.Validation.OperationStatus.Error => Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError),
            _ => Results.Ok(new LoginResponse(result.Value!.AccessToken))
        };
    }

    public static async Task<IResult> GetCurrentUser(HttpContext context, IMongoDatabase database)
    {
        var response = await CurrentUserResponseFactory.CreateAsync(context, database);
        return Results.Ok(response);
    }

    public static async Task<IResult> UpdateCurrentUserProfileAsync(
        UpdateCurrentUserProfileRequest request,
        HttpContext context,
        UpdateCurrentUserProfileHandler handler)
    {
        var userId = TryGetUserId(context.User);
        var tenantId = TryGetTenantId(context.User);
        if (userId is null || tenantId is null)
        {
            return Results.Unauthorized();
        }

        var roles = context.User.FindAll(ClaimTypes.Role).Select(role => role.Value).ToArray();

        var result = await handler.HandleAsync(new UpdateCurrentUserProfileCommand(
            userId.Value,
            tenantId.Value,
            roles,
            request.DisplayName,
            request.OrganizationName));

        return result.Status switch
        {
            Intentify.Shared.Validation.OperationStatus.ValidationFailed => Results.BadRequest(
                ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            Intentify.Shared.Validation.OperationStatus.Unauthorized => Results.Unauthorized(),
            Intentify.Shared.Validation.OperationStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
            Intentify.Shared.Validation.OperationStatus.Error => Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError),
            _ => Results.NoContent()
        };
    }

    private static Guid? TryGetUserId(ClaimsPrincipal user)
    {
        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrWhiteSpace(userIdValue))
        {
            return null;
        }

        if (Guid.TryParse(userIdValue, out var userGuid))
        {
            return userGuid;
        }

        return null;
    }

    private static Guid? TryGetTenantId(ClaimsPrincipal user)
    {
        var tenantIdValue = user.FindFirstValue("tenantId");
        if (string.IsNullOrWhiteSpace(tenantIdValue))
        {
            return null;
        }

        if (Guid.TryParse(tenantIdValue, out var tenantGuid))
        {
            return tenantGuid;
        }

        return null;
    }
}
