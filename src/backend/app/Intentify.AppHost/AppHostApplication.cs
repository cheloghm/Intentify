using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Intentify.AppHost;

internal static class AppHostApplication
{
    private const string CorsPolicyName = "IntentifyCors";
    private static readonly string[] LocalCorsFallbackOrigins =
    [
        "http://localhost:3000",
        "http://127.0.0.1:3000",
        "http://localhost:8088",
        "http://127.0.0.1:8088"
    ];

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
        app.UseCors(CorsPolicyName);

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapAppModules();
        app.MapDebugEndpoints();

        return app;
    }

    private static void ConfigureCors(WebApplicationBuilder builder)
    {
        // Supports env var:
        // Intentify__Cors__AllowedOrigins=http://127.0.0.1:3000,http://localhost:3000
        var configured = builder.Configuration["Intentify:Cors:AllowedOrigins"];

        var origins = (configured ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Dev fallback to avoid local friction.
        if (origins.Length == 0)
        {
            if (builder.Environment.IsDevelopment())
            {
                origins = LocalCorsFallbackOrigins;
            }
            else if (IsLocalNonContainerRun(builder.Configuration))
            {
                origins = LocalCorsFallbackOrigins;

                using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
                var logger = loggerFactory.CreateLogger("Intentify.AppHost.Cors");
                logger.LogWarning(
                    "Intentify:Cors:AllowedOrigins is not configured in non-development local run. " +
                    "Using local-only fallback origins: {Origins}",
                    string.Join(",", origins));
            }
            else
            {
                throw new InvalidOperationException(
                    "CORS is not configured. Set Intentify__Cors__AllowedOrigins (e.g. http://localhost:3000).");
            }
        }

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                policy
                    .WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();

                // IMPORTANT:
                // Do NOT call AllowCredentials() unless you're using cookies.
                // Your frontend uses Bearer tokens, so credentials are not required.
            });
        });
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

            // Operation-level security overrides global security.
            // So for /debug we must explicitly require BOTH Bearer and DebugSecret.
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
