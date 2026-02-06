using Microsoft.Extensions.Hosting;
using Serilog;

namespace Intentify.Shared.Observability;

public static class HostApplicationBuilderExtensions
{
    public static IHostApplicationBuilder AddObservability(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSerilog((_, loggerConfiguration) => loggerConfiguration
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console());

        return builder;
    }
}
