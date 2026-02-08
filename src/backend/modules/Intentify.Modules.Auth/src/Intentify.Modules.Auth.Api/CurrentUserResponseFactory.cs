using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Intentify.Modules.Auth.Domain;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;

namespace Intentify.Modules.Auth.Api;

public static class CurrentUserResponseFactory
{
    public static async Task<CurrentUserResponse> CreateAsync(HttpContext context, IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(database);

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
            string.Empty;
        var tenantId = context.User.FindFirstValue("tenantId") ?? string.Empty;
        var roles = context.User.FindAll(ClaimTypes.Role).Select(role => role.Value).ToArray();

        string? displayName = null;
        if (Guid.TryParse(userId, out var userGuid))
        {
            var users = database.GetCollection<User>(AuthMongoCollections.Users);
            var user = await users.Find(candidate => candidate.Id == userGuid).FirstOrDefaultAsync();
            displayName = user?.DisplayName;
        }

        return new CurrentUserResponse(userId, tenantId, roles, displayName);
    }
}
