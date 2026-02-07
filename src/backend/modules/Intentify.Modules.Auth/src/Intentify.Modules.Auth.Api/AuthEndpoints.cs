using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Intentify.Modules.Auth.Domain;
using Intentify.Shared.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Intentify.Modules.Auth.Api;

internal static class AuthEndpoints
{
    public static async Task<IResult> LoginAsync(
        LoginRequest request,
        IMongoDatabase database,
        PasswordHasher hasher,
        JwtTokenIssuer tokenIssuer,
        IOptions<JwtOptions> jwtOptions)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest();
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

    public static IResult GetCurrentUser(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
            string.Empty;
        var tenantId = context.User.FindFirstValue("tenantId") ?? string.Empty;
        var roles = context.User.FindAll(ClaimTypes.Role).Select(role => role.Value).ToArray();

        return Results.Ok(new CurrentUserResponse(userId, tenantId, roles));
    }
}
