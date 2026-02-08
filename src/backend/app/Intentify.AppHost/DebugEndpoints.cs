using Intentify.Modules.Auth.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using MongoDB.Driver;
using System.Diagnostics;

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
            IMongoDatabase database) =>
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

            var user = await CurrentUserResponseFactory.CreateAsync(context, database);

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
