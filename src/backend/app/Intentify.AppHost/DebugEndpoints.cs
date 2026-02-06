using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;

namespace Intentify.AppHost;

internal static class DebugEndpoints
{
    internal const string DebugSecretHeaderName = "X-Intentify-Debug-Secret";
    internal const string DebugSecretEnvironmentVariable = "INTENTIFY_DEBUG_SECRET";

    public static IEndpointRouteBuilder MapDebugEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var environment = endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (!environment.IsDevelopment())
        {
            return endpoints;
        }

        endpoints.MapGet("/debug", (HttpContext context, AppModuleRegistry registry, IHostEnvironment hostEnvironment) =>
        {
            var expectedSecret = Environment.GetEnvironmentVariable(DebugSecretEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(expectedSecret))
            {
                return Results.Unauthorized();
            }

            if (!context.Request.Headers.TryGetValue(DebugSecretHeaderName, out var providedSecret) ||
                !StringValuesMatch(providedSecret, expectedSecret))
            {
                return Results.Unauthorized();
            }

            var response = new
            {
                environment = hostEnvironment.EnvironmentName,
                isDevelopment = hostEnvironment.IsDevelopment(),
                modules = registry.Modules.Select(module => new
                {
                    name = module.Name
                })
            };

            return Results.Ok(response);
        });

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
