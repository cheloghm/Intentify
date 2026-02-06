using Intentify.Modules.Auth.Api;
using Intentify.Shared.Data.Mongo;
using Intentify.Shared.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
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

        endpoints.MapGet("/debug", (HttpContext context, AppModuleRegistry registry, IHostEnvironment hostEnvironment, IConfiguration configuration) =>
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

            var jwtOptions = configuration.GetSection("Intentify:Jwt").Get<JwtOptions>() ?? new JwtOptions();
            var mongoOptions = configuration.GetSection("Intentify:Mongo").Get<MongoOptions>() ?? new MongoOptions();
            var response = new
            {
                environment = hostEnvironment.EnvironmentName,
                isDevelopment = hostEnvironment.IsDevelopment(),
                modules = registry.Modules.Select(module => new
                {
                    name = module.Name
                }),
                auth = new
                {
                    jwt = new
                    {
                        issuer = jwtOptions.Issuer,
                        audience = jwtOptions.Audience,
                        accessTokenMinutes = jwtOptions.AccessTokenMinutes,
                        signingKeyConfigured = !string.IsNullOrWhiteSpace(jwtOptions.SigningKey)
                    },
                    collections = new
                    {
                        users = AuthMongoCollections.Users,
                        tenants = AuthMongoCollections.Tenants
                    },
                    mongo = new
                    {
                        connectionStringConfigured = !string.IsNullOrWhiteSpace(mongoOptions.ConnectionString),
                        databaseName = mongoOptions.DatabaseName
                    }
                }
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
