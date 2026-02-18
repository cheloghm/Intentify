using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Intentify.Modules.Sites.Application;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Intentify.AppHost;

internal static class AppHostApplication
{
    private const string CorsPolicyName = "IntentifyCors";

    private const string MongoConnectionStringKey = "Intentify:Mongo:ConnectionString";
    private const string MongoDatabaseNameKey = "Intentify:Mongo:DatabaseName";

    public static WebApplicationBuilder CreateBuilder(string[] args, string? environmentName = null)
    {
        DotEnvLoader.Load();

        var options = new WebApplicationOptions
        {
            Args = args,
            EnvironmentName = environmentName
        };

        return WebApplication.CreateBuilder(options);
    }

    public static WebApplication Build(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddSwaggerGen(options =>
        {
            // JWT Bearer
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Name = "Authorization"
            });

            // X-Debug-Secret (set via Swagger "Authorize"; never auto-filled from env)
            options.AddSecurityDefinition("DebugSecret", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = "X-Debug-Secret",
                Description = "Required for /debug endpoint."
            });

            // Apply BOTH Bearer + DebugSecret to /debug (AND requirement)
            options.OperationFilter<DebugSecretForDebugEndpointOperationFilter>();

            // Keep Bearer globally for other endpoints (matches your existing behavior)
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        // ✅ Ensure Mongo is configured for local/dev runs even if .env is missing,
        // to prevent module registration from throwing during app build/tests.
        EnsureMongoConfiguration(builder);

        // ✅ Register JWT authentication so RequireAuthorization() works.
        ConfigureAuthentication(builder);

        builder.Services.AddAppModules(builder.Configuration);

        builder.Services.AddAuthorization();

        // ✅ CORS (required for frontend calling backend)
        ConfigureCors(builder);

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // ✅ Apply global CORS middleware
        app.UseCors();

        // ✅ Auth middleware (must be before endpoints)
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapAppModules();
        app.MapDebugEndpoints();

        return app;
    }

    private static void ConfigureAuthentication(WebApplicationBuilder builder)
    {
        var issuer = builder.Configuration["Intentify:Jwt:Issuer"];
        var audience = builder.Configuration["Intentify:Jwt:Audience"];
        var signingKey = builder.Configuration["Intentify:Jwt:SigningKey"];

        if (string.IsNullOrWhiteSpace(issuer) ||
            string.IsNullOrWhiteSpace(audience) ||
            string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException(
                "JWT is not configured. Set Intentify__Jwt__Issuer, Intentify__Jwt__Audience, Intentify__Jwt__SigningKey.");
        }

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });
    }

    private static void ConfigureCors(WebApplicationBuilder builder)
    {
        // Supports env var:
        // Intentify__Cors__AllowedOrigins=http://127.0.0.1:3000,http://localhost:3000
        var configured = builder.Configuration["Intentify:Cors:AllowedOrigins"];

        var configuredOrigins = (configured ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var origins = configuredOrigins;

        if (origins.Length == 0)
        {
            throw new InvalidOperationException(
                "CORS is not configured. Set Intentify__Cors__AllowedOrigins (e.g. http://localhost:3000).");
        }

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                policy
                    .WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        builder.Services.AddSingleton<ICorsPolicyProvider>(serviceProvider =>
            new DynamicCorsPolicyProvider(
                serviceProvider.GetRequiredService<ISiteRepository>(),
                origins));
    }

    private static void EnsureMongoConfiguration(WebApplicationBuilder builder)
    {
        var hasConnectionString = !string.IsNullOrWhiteSpace(builder.Configuration[MongoConnectionStringKey]);
        var hasDatabaseName = !string.IsNullOrWhiteSpace(builder.Configuration[MongoDatabaseNameKey]);

        if (hasConnectionString && hasDatabaseName)
        {
            return;
        }

        // Only apply fallback for local/dev runs to avoid masking real config issues in real deployments.
        var allowLocalFallback = builder.Environment.IsDevelopment() || IsLocalNonContainerRun(builder.Configuration);
        if (!allowLocalFallback)
        {
            throw new InvalidOperationException(
                "Mongo is not configured. Set Intentify__Mongo__ConnectionString and Intentify__Mongo__DatabaseName.");
        }

        var defaults = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (!hasConnectionString)
        {
            defaults[MongoConnectionStringKey] = "mongodb://localhost:27017";
        }

        if (!hasDatabaseName)
        {
            defaults[MongoDatabaseNameKey] = "Intentify";
        }

        builder.Configuration.AddInMemoryCollection(defaults);

        using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
        var logger = loggerFactory.CreateLogger("Intentify.AppHost.Mongo");
        logger.LogWarning(
            "Mongo configuration was missing for local/dev run. Applied fallback values for: {Keys}",
            string.Join(", ", defaults.Keys));
    }

    private static bool IsLocalNonContainerRun(IConfiguration configuration)
    {
        var inContainer = configuration["DOTNET_RUNNING_IN_CONTAINER"];
        if (bool.TryParse(inContainer, out var runningInContainer) && runningInContainer)
        {
            return false;
        }

        var urlsValue = configuration["ASPNETCORE_URLS"]
            ?? configuration["URLS"]
            ?? configuration["urls"];

        if (string.IsNullOrWhiteSpace(urlsValue))
        {
            return true;
        }

        var urls = urlsValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (urls.Length == 0)
        {
            return true;
        }

        foreach (var url in urls)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            var host = uri.Host;
            if (!host.Equals("localhost", StringComparison.OrdinalIgnoreCase) && host != "127.0.0.1")
            {
                return false;
            }
        }

        return true;
    }

    private sealed class DebugSecretForDebugEndpointOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (!string.Equals(context.ApiDescription.RelativePath?.Trim('/'), "debug", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            operation.Security = new List<OpenApiSecurityRequirement>
            {
                new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    },
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "DebugSecret"
                            }
                        },
                        Array.Empty<string>()
                    }
                }
            };
        }
    }
}
