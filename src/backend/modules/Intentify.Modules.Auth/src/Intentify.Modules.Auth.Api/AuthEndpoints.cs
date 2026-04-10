using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Intentify.Modules.Auth.Application;
using Intentify.Modules.Auth.Domain;
using Intentify.Shared.Security;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Hosting;
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
        return Results.Ok(new CurrentUserResponse(result.UserId, result.TenantId, result.Roles, result.DisplayName, result.Email, result.OrganizationName, result.IsAdmin, result.Plan));
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

    public static IResult GoogleOAuthInitiate(IOptions<GoogleOAuthOptions> options, string? orgName = null)
    {
        var clientId = options.Value.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Results.Json(new { error = "Google OAuth not yet configured. Set Intentify__Auth__Google__ClientId in .env" },
                statusCode: StatusCodes.Status501NotImplemented);
        }

        var redirectUri = Uri.EscapeDataString(options.Value.RedirectUri);
        var scope = Uri.EscapeDataString("openid email profile");

        // Encode orgName into the state parameter so it survives the OAuth round-trip
        var stateValue = string.IsNullOrWhiteSpace(orgName)
            ? string.Empty
            : Convert.ToBase64String(Encoding.UTF8.GetBytes(orgName.Trim()[..Math.Min(orgName.Trim().Length, 200)]));

        var googleUrl = $"https://accounts.google.com/o/oauth2/v2/auth" +
                        $"?client_id={clientId}" +
                        $"&redirect_uri={redirectUri}" +
                        $"&response_type=code" +
                        $"&scope={scope}" +
                        $"&access_type=offline" +
                        $"&prompt=select_account";

        if (!string.IsNullOrWhiteSpace(stateValue))
        {
            googleUrl += $"&state={Uri.EscapeDataString(stateValue)}";
        }

        return Results.Redirect(googleUrl);
    }

    public static async Task<IResult> GoogleOAuthCallback(
        HttpContext context,
        IOptions<GoogleOAuthOptions> options,
        IHttpClientFactory httpClientFactory,
        IUserRepository users,
        ITenantRepository tenants,
        JwtTokenIssuer tokenIssuer,
        IOptions<JwtOptions> jwtOptions)
    {
        try
        {
            var code = context.Request.Query["code"].ToString();
            if (string.IsNullOrWhiteSpace(code))
            {
                return Results.Redirect("/public/login.html?error=google_auth_failed");
            }

            // Decode orgName from state parameter (base64-encoded, set during initiation)
            string? orgNameFromState = null;
            var stateParam = context.Request.Query["state"].ToString();
            if (!string.IsNullOrWhiteSpace(stateParam))
            {
                try { orgNameFromState = Encoding.UTF8.GetString(Convert.FromBase64String(stateParam)); } catch { }
            }

            var opts = options.Value;
            var httpClient = httpClientFactory.CreateClient();

            // Exchange authorisation code for tokens
            var tokenResponse = await httpClient.PostAsync(
                "https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["client_id"] = opts.ClientId,
                    ["client_secret"] = opts.ClientSecret,
                    ["redirect_uri"] = opts.RedirectUri
                }));

            if (!tokenResponse.IsSuccessStatusCode)
            {
                return Results.Redirect("/public/login.html?error=google_auth_failed");
            }

            var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
            var googleAccessToken = tokenJson.GetProperty("access_token").GetString();
            if (string.IsNullOrWhiteSpace(googleAccessToken))
            {
                return Results.Redirect("/public/login.html?error=google_auth_failed");
            }

            // Fetch user info from Google
            using var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
            userInfoRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", googleAccessToken);
            var userInfoResponse = await httpClient.SendAsync(userInfoRequest);

            if (!userInfoResponse.IsSuccessStatusCode)
            {
                return Results.Redirect("/public/login.html?error=google_auth_failed");
            }

            var userInfoJson = await userInfoResponse.Content.ReadFromJsonAsync<JsonElement>();
            var email = userInfoJson.GetProperty("email").GetString();
            var displayName = userInfoJson.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;

            if (string.IsNullOrWhiteSpace(email))
            {
                return Results.Redirect("/public/login.html?error=google_auth_failed");
            }

            // Find or create user
            var existingUser = await users.FindByEmailAsync(email, context.RequestAborted);
            User user;
            if (existingUser is not null)
            {
                user = existingUser;
            }
            else
            {
                var now = DateTime.UtcNow;
                var tenant = new Tenant
                {
                    Name = !string.IsNullOrWhiteSpace(orgNameFromState) ? orgNameFromState : (displayName ?? email),
                    Domain = $"{Guid.NewGuid():N}.tenant.local",
                    Plan = "starter",
                    Industry = "software",
                    Category = "default",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await tenants.InsertAsync(tenant, context.RequestAborted);

                user = new User
                {
                    TenantId = tenant.Id,
                    Email = email,
                    PasswordHash = string.Empty,
                    DisplayName = displayName ?? email,
                    Roles = new[] { AuthRoles.Admin },
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await users.InsertAsync(user, context.RequestAborted);
            }

            // Issue JWT
            var tokenResult = tokenIssuer.IssueAccessToken(
                user.Id.ToString("N"),
                user.TenantId.ToString("N"),
                user.Roles,
                jwtOptions.Value);

            if (!tokenResult.IsSuccess || string.IsNullOrWhiteSpace(tokenResult.Value))
            {
                return Results.Redirect("/public/login.html?error=google_auth_failed");
            }

            return Results.Redirect($"/app?token={Uri.EscapeDataString(tokenResult.Value)}");
        }
        catch
        {
            return Results.Redirect("/public/login.html?error=google_auth_failed");
        }
    }

    public static async Task<IResult> PromoteSelfAsync(
        HttpContext context,
        IUserRepository userRepository,
        IWebHostEnvironment? env = null)
    {
        var isDev = env is null || env.EnvironmentName == "Development";
        if (!isDev) return Results.Forbid();

        var userId = TryGetUserId(context.User);
        if (userId is null) return Results.Unauthorized();

        await userRepository.UpdateRolesAsync(userId.Value, [AuthRoles.SuperAdmin], DateTime.UtcNow, context.RequestAborted);
        return Results.Ok(new { promoted = true, role = AuthRoles.SuperAdmin });
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
