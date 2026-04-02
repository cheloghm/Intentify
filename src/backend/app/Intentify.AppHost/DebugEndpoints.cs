using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Intentify.Modules.Auth.Api;
using Intentify.Modules.Auth.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;

namespace Intentify.AppHost;

internal static class DebugEndpoints
{
    internal const string DebugSecretHeaderName = "X-Debug-Secret";
    internal const string DebugSecretEnvironmentVariable = "INTENTIFY_DEBUG_SECRET";

    public static IEndpointRouteBuilder MapDebugEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var environment = endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (!environment.IsDevelopment())
        {
            return endpoints;
        }

        endpoints.MapGet("/debug", async (
            HttpContext context,
            IHostEnvironment hostEnvironment,
            GetCurrentUserHandler handler) =>
        {
            var expectedSecret = Environment.GetEnvironmentVariable(DebugSecretEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(expectedSecret))
            {
                return Results.Unauthorized();
            }

            context.Request.Headers.TryGetValue(DebugSecretHeaderName, out var providedSecret);

            if (!StringValuesMatch(providedSecret, expectedSecret))
            {
                return Results.Unauthorized();
            }

            var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var tenantIdValue = context.User.FindFirstValue("tenantId");

            if (!Guid.TryParse(userIdValue, out var userId) || !Guid.TryParse(tenantIdValue, out var tenantId))
            {
                return Results.Unauthorized();
            }

            var roles = context.User.FindAll(ClaimTypes.Role).Select(r => r.Value).ToArray();
            var user = await handler.HandleAsync(new GetCurrentUserQuery(userId, tenantId, roles), context.RequestAborted);

            return Results.Ok(new
            {
                environment = hostEnvironment.EnvironmentName,
                user,
                correlationId = Activity.Current?.Id ?? context.TraceIdentifier,
                utcNow = DateTime.UtcNow.ToString("O")
            });
        })
        .AddEndpointFilter<RequireAuthFilter>(); // keep ONLY if /auth/me uses it successfully

        return endpoints;
    }

    private static bool StringValuesMatch(StringValues provided, string expected)
    {
        if (StringValues.IsNullOrEmpty(provided))
        {
            return false;
        }

        return string.Equals(provided.ToString(), expected, StringComparison.Ordinal);
    }
}
