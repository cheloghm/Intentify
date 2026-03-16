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
        string? email = null;
        if (Guid.TryParse(userId, out var userGuid))
        {
            var users = database.GetCollection<User>(AuthMongoCollections.Users);
            var user = await users.Find(candidate => candidate.Id == userGuid).FirstOrDefaultAsync();
            displayName = user?.DisplayName;
            email = user?.Email;
        }

        string? organizationName = null;
        if (Guid.TryParse(tenantId, out var tenantGuid))
        {
            var tenants = database.GetCollection<Tenant>(AuthMongoCollections.Tenants);
            var tenant = await tenants.Find(candidate => candidate.Id == tenantGuid).FirstOrDefaultAsync();
            organizationName = tenant?.Name;
        }

        var isAdmin = roles.Any(role => role.Equals(AuthRoles.Admin, StringComparison.OrdinalIgnoreCase)
            || role.Equals(AuthRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase));

        return new CurrentUserResponse(userId, tenantId, roles, displayName, email, organizationName, isAdmin);
    }
}
