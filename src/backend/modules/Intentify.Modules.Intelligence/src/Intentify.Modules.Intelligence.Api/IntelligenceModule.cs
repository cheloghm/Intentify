using Intentify.Modules.Intelligence.Application;
using Intentify.Modules.Intelligence.Infrastructure;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intentify.Modules.Intelligence.Api;

public sealed class IntelligenceModule : IAppModule
{
    public string Name => "Intelligence";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var googleOptions = new GoogleSearchOptions();
        configuration.GetSection(GoogleSearchOptions.ConfigurationSection).Bind(googleOptions);
        services.AddSingleton(googleOptions);

        var searchOptions = new IntelligenceSearchOptions();
        configuration.GetSection(IntelligenceSearchOptions.ConfigurationSection).Bind(searchOptions);
        services.AddSingleton(searchOptions);

        services.AddHttpClient(GoogleSearchProvider.ClientName, (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<GoogleSearchOptions>();
            if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
            {
                client.BaseAddress = baseUri;
            }

            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 10);
        });

        services.AddSingleton<IExternalSearchProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IntelligenceSearchOptions>();
            var providerName = options.Provider?.Trim() ?? "Google";
            if (!providerName.Equals("Google", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Unsupported intelligence search provider '{providerName}'.");
            }

            var clientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = clientFactory.CreateClient(GoogleSearchProvider.ClientName);
            var googleSearchOptions = serviceProvider.GetRequiredService<GoogleSearchOptions>();

            return new GoogleSearchProvider(httpClient, googleSearchOptions);
        });

        services.AddSingleton<IIntelligenceTrendsRepository, IntelligenceTrendsRepository>();
        services.AddSingleton<RefreshIntelligenceTrendsService>();
        services.AddSingleton<QueryIntelligenceTrendsService>();
        services.AddSingleton<GetIntelligenceStatusService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/intelligence")
            .RequireAuthorization();

        group.MapPost("/refresh", IntelligenceEndpoints.RefreshAsync);
        group.MapGet("/trends", IntelligenceEndpoints.GetTrendsAsync);
        group.MapGet("/status", IntelligenceEndpoints.GetStatusAsync);
        group.MapGet("/dashboard", IntelligenceEndpoints.GetDashboardAsync);
    }
}
