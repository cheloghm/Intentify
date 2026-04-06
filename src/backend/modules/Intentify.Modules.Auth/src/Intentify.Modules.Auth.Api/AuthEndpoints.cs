using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Intentify.Modules.Auth.Application;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

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
            request.OrganizationName,
            request.Plan));

        return result.Status switch
        {
            Intentify.Shared.Validation.OperationStatus.ValidationFailed => Results.BadRequest(
                ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            Intentify.Shared.Validation.OperationStatus.Error => Results.Problem(ProblemDetailsHelpers.CreateInternalErrorProblemDetails()),
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
            Intentify.Shared.Validation.OperationStatus.Error => Results.Problem(ProblemDetailsHelpers.CreateInternalErrorProblemDetails()),
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
            Intentify.Shared.Validation.OperationStatus.Error => Results.Problem(ProblemDetailsHelpers.CreateInternalErrorProblemDetails()),
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
            Intentify.Shared.Validation.OperationStatus.Error => Results.Problem(ProblemDetailsHelpers.CreateInternalErrorProblemDetails()),
            _ => Results.Ok(new LoginResponse(result.Value!.AccessToken))
        };
    }

    public static async Task<IResult> GetCurrentUser(HttpContext context, GetCurrentUserHandler handler)
    {
        var userId = TryGetUserId(context.User);
        var tenantId = TryGetTenantId(context.User);
        if (userId is null || tenantId is null)
        {
            return Results.Unauthorized();
        }

        var roles = context.User.FindAll(ClaimTypes.Role).Select(role => role.Value).ToArray();
        var result = await handler.HandleAsync(new GetCurrentUserQuery(userId.Value, tenantId.Value, roles), context.RequestAborted);
        return Results.Ok(new CurrentUserResponse(result.UserId, result.TenantId, result.Roles, result.DisplayName, result.Email, result.OrganizationName, result.IsAdmin));
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
            Intentify.Shared.Validation.OperationStatus.Error => Results.Problem(ProblemDetailsHelpers.CreateInternalErrorProblemDetails()),
            _ => Results.NoContent()
        };
    }

    public static async Task<IResult> ListTenantUsersAsync(
        HttpContext context,
        ListTenantUsersHandler handler)
    {
        var userId = TryGetUserId(context.User);
        var tenantId = TryGetTenantId(context.User);
        if (userId is null || tenantId is null)
        {
            return Results.Unauthorized();
        }

        var roles = context.User.FindAll(ClaimTypes.Role).Select(role => role.Value).ToArray();
        var result = await handler.HandleAsync(new ListTenantUsersQuery(tenantId.Value, userId.Value, roles));

        return result.Status switch
        {
            Intentify.Shared.Validation.OperationStatus.Unauthorized => Results.Unauthorized(),
            Intentify.Shared.Validation.OperationStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
            Intentify.Shared.Validation.OperationStatus.Error => Results.Problem(ProblemDetailsHelpers.CreateInternalErrorProblemDetails()),
            _ => Results.Ok(result.Value!.Select(user => new TenantUserResponse(
                user.UserId.ToString("N"),
                user.Email,
                user.DisplayName,
                user.Roles,
                user.IsActive,
                user.CreatedAt,
                user.UpdatedAt)))
        };
    }

    public static async Task<IResult> ChangeTenantUserRoleAsync(
        string userId,
        ChangeTenantUserRoleRequest request,
        HttpContext context,
        ChangeTenantUserRoleHandler handler)
    {
        if (!Guid.TryParse(userId, out var targetUserId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["userId"] = ["User id is invalid."]
            }));
        }

        var actorUserId = TryGetUserId(context.User);
        var tenantId = TryGetTenantId(context.User);
        if (actorUserId is null || tenantId is null)
        {
            return Results.Unauthorized();
        }

        var roles = context.User.FindAll(ClaimTypes.Role).Select(role => role.Value).ToArray();
        var result = await handler.HandleAsync(new ChangeTenantUserRoleCommand(
            tenantId.Value,
            actorUserId.Value,
            roles,
            targetUserId,
            request.Role));

        return result.Status switch
        {
            Intentify.Shared.Validation.OperationStatus.ValidationFailed => Results.BadRequest(
                ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            Intentify.Shared.Validation.OperationStatus.Unauthorized => Results.Unauthorized(),
            Intentify.Shared.Validation.OperationStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
            Intentify.Shared.Validation.OperationStatus.Error => Results.Problem(ProblemDetailsHelpers.CreateInternalErrorProblemDetails()),
            _ => Results.NoContent()
        };
    }

    public static async Task<IResult> RemoveTenantUserAsync(
        string userId,
        HttpContext context,
        RemoveTenantUserHandler handler)
    {
        if (!Guid.TryParse(userId, out var targetUserId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["userId"] = ["User id is invalid."]
            }));
        }

        var actorUserId = TryGetUserId(context.User);
        var tenantId = TryGetTenantId(context.User);
        if (actorUserId is null || tenantId is null)
        {
            return Results.Unauthorized();
        }

        var roles = context.User.FindAll(ClaimTypes.Role).Select(role => role.Value).ToArray();
        var result = await handler.HandleAsync(new RemoveTenantUserCommand(
            tenantId.Value,
            actorUserId.Value,
            roles,
            targetUserId));

        return result.Status switch
        {
            Intentify.Shared.Validation.OperationStatus.Unauthorized => Results.Unauthorized(),
            Intentify.Shared.Validation.OperationStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
            Intentify.Shared.Validation.OperationStatus.Error => Results.Problem(ProblemDetailsHelpers.CreateInternalErrorProblemDetails()),
            _ => Results.NoContent()
        };
    }

    public static async Task<IResult> ListTenantInvitesAsync(
        HttpContext context,
        ListTenantInvitesHandler handler)
    {
        var userId = TryGetUserId(context.User);
        var tenantId = TryGetTenantId(context.User);
        if (userId is null || tenantId is null)
        {
            return Results.Unauthorized();
        }

        var roles = context.User.FindAll(ClaimTypes.Role).Select(role => role.Value).ToArray();
        var result = await handler.HandleAsync(new ListTenantInvitesQuery(tenantId.Value, userId.Value, roles));

        return result.Status switch
        {
            Intentify.Shared.Validation.OperationStatus.Unauthorized => Results.Unauthorized(),
            Intentify.Shared.Validation.OperationStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
            Intentify.Shared.Validation.OperationStatus.Error => Results.Problem(ProblemDetailsHelpers.CreateInternalErrorProblemDetails()),
            _ => Results.Ok(result.Value!.Select(invite => new TenantInviteResponse(
                invite.InviteId.ToString("N"),
                invite.Email,
                invite.Role,
                invite.ExpiresAtUtc,
                invite.AcceptedAtUtc,
                invite.RevokedAtUtc,
                invite.CreatedAtUtc,
                invite.UpdatedAtUtc)))
        };
    }

    public static async Task<IResult> RevokeTenantInviteAsync(
        string inviteId,
        HttpContext context,
        RevokeTenantInviteHandler handler)
    {
        if (!Guid.TryParse(inviteId, out var parsedInviteId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["inviteId"] = ["Invite id is invalid."]
            }));
        }

        var userId = TryGetUserId(context.User);
        var tenantId = TryGetTenantId(context.User);
        if (userId is null || tenantId is null)
        {
            return Results.Unauthorized();
        }

        var roles = context.User.FindAll(ClaimTypes.Role).Select(role => role.Value).ToArray();
        var result = await handler.HandleAsync(new RevokeTenantInviteCommand(tenantId.Value, userId.Value, roles, parsedInviteId));

        return result.Status switch
        {
            Intentify.Shared.Validation.OperationStatus.Unauthorized => Results.Unauthorized(),
            Intentify.Shared.Validation.OperationStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
            Intentify.Shared.Validation.OperationStatus.Error => Results.Problem(ProblemDetailsHelpers.CreateInternalErrorProblemDetails()),
            _ => Results.NoContent()
        };
    }

    // ── Google OAuth ──────────────────────────────────────────────────────────

    public static IResult GoogleOAuthInitiate(IOptions<GoogleOAuthOptions> options)
    {
        var clientId = options.Value.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Results.Json(new { error = "Google OAuth not yet configured. Set Intentify__Auth__Google__ClientId in .env" },
                statusCode: StatusCodes.Status501NotImplemented);
        }

        var redirectUri = Uri.EscapeDataString(options.Value.RedirectUri);
        var scope = Uri.EscapeDataString("openid email profile");
        var googleUrl = $"https://accounts.google.com/o/oauth2/v2/auth" +
                        $"?client_id={clientId}" +
                        $"&redirect_uri={redirectUri}" +
                        $"&response_type=code" +
                        $"&scope={scope}" +
                        $"&access_type=offline" +
                        $"&prompt=select_account";

        return Results.Redirect(googleUrl);
    }

    public static IResult GoogleOAuthCallback()
    {
        // Stub: full OAuth exchange requires Google credentials configured.
        // Redirect back to login with an error the frontend can display.
        return Results.Redirect("/public/login.html?error=google_not_configured");
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
