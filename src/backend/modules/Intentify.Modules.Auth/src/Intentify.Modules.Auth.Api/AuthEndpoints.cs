using Intentify.Modules.Auth.Domain;
using Intentify.Shared.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

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
        if (string.IsNullOrWhiteSpace(request.DisplayName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest();
        }

        var users = database.GetCollection<User>(AuthMongoCollections.Users);
        var existingUser = await users.Find(x => x.Email == request.Email).FirstOrDefaultAsync();
        if (existingUser is not null)
        {
            return Results.BadRequest();
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

    public static async Task<IResult> GetCurrentUser(HttpContext context, IMongoDatabase database)
    {
        var response = await CurrentUserResponseFactory.CreateAsync(context, database);
        return Results.Ok(response);
    }
}
