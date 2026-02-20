using AppOpenSearchClient = Intentify.Modules.Knowledge.Application.IOpenSearchKnowledgeClient;
using AppOpenSearchOptions = Intentify.Modules.Knowledge.Application.IOpenSearchOptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Intentify.Modules.Knowledge.Infrastructure;

public static class OpenSearchServiceCollectionExtensions
{
    public const string ClientName = "knowledge-opensearch";

    public static IServiceCollection AddKnowledgeOpenSearchClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<OpenSearchRestClient>(serviceProvider =>
        {
            var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient(ClientName);
            var options = serviceProvider.GetRequiredService<OpenSearchOptions>();
            var logger = serviceProvider.GetRequiredService<ILogger<OpenSearchRestClient>>();

            return new OpenSearchRestClient(httpClient, options, logger);
        });

        services.AddSingleton<IOpenSearchKnowledgeClient>(serviceProvider => serviceProvider.GetRequiredService<OpenSearchRestClient>());
        services.AddSingleton<AppOpenSearchClient>(serviceProvider => serviceProvider.GetRequiredService<OpenSearchRestClient>());
        services.AddSingleton<AppOpenSearchOptions>(serviceProvider => serviceProvider.GetRequiredService<OpenSearchOptions>());

        return services;
    }
}
