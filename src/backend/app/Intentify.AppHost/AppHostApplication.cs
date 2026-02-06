using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

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

        builder.Services.AddAppModules(builder.Configuration);

        var app = builder.Build();

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapAppModules();
        app.MapDebugEndpoints();

        return app;
    }
}
