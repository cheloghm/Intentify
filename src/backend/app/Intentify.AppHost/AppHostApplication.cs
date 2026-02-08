using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Intentify.AppHost;

internal static class AppHostApplication
{
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

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapAppModules();
        app.MapDebugEndpoints();

        return app;
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