using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Intentify.Modules.Knowledge.Infrastructure;

public static class OpenSearchServiceCollectionExtensions
{
    public const string ClientName = "knowledge-opensearch";

    public static IServiceCollection AddKnowledgeOpenSearchClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IOpenSearchKnowledgeClient>(serviceProvider =>
        {
            var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient(ClientName);
            var options = serviceProvider.GetRequiredService<OpenSearchOptions>();
            var logger = serviceProvider.GetRequiredService<ILogger<OpenSearchRestClient>>();

            return new OpenSearchRestClient(httpClient, options, logger);
        });

        return services;
    }
}
