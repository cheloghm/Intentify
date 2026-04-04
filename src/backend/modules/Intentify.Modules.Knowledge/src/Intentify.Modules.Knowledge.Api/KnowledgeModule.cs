using Intentify.Modules.Auth.Api;
using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Knowledge.Infrastructure;
using Intentify.Shared.AI;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Intentify.Modules.Knowledge.Api;

public sealed class KnowledgeModule : IAppModule
{
    public string Name => "Knowledge";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddHttpClient("knowledge");

        var openSearchOptions = new OpenSearchOptions();
        configuration.GetSection(OpenSearchOptions.ConfigurationSection).Bind(openSearchOptions);
        services.AddSingleton(openSearchOptions);
        services.AddSingleton<IOpenSearchOptions>(serviceProvider =>
            serviceProvider.GetRequiredService<OpenSearchOptions>());

        services.AddHttpClient(OpenSearchServiceCollectionExtensions.ClientName, (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<OpenSearchOptions>();
            client.BaseAddress = new Uri(options.Url);

            if (options.RequestTimeoutSeconds > 0)
            {
                client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
            }
        });

        services.AddKnowledgeOpenSearchClient();
        services.AddSingleton<Intentify.Modules.Knowledge.Application.IOpenSearchKnowledgeClient>(serviceProvider =>
            serviceProvider.GetRequiredService<Intentify.Modules.Knowledge.Infrastructure.IOpenSearchKnowledgeClient>());

        services.AddSingleton<IKnowledgeSourceRepository, KnowledgeSourceRepository>();
        services.AddSingleton<IKnowledgeChunkRepository, KnowledgeChunkRepository>();
        services.AddSingleton<IKnowledgeQuickFactsRepository, KnowledgeQuickFactsRepository>();
        services.AddSingleton<ISiteQuickFactRepository, SiteQuickFactRepository>();
        services.AddSingleton<IEngageBotResolver, EngageBotResolver>();

        // IChatCompletionClient is registered by EngageModule via TryAddSingleton.
        // Register a null fallback here so IndexKnowledgeSourceHandler resolves cleanly
        // even if the Engage module is not loaded (e.g. integration test environments).
        services.TryAddSingleton<IChatCompletionClient>(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Intentify.Modules.Knowledge");
            logger.LogWarning(
                "AI client is not configured (Knowledge module fallback active). " +
                "Quick facts extraction during knowledge indexing will be skipped. " +
                "Configure Intentify:AI:ApiBaseUrl and Intentify:AI:ApiKey via the Engage module to enable it.");
            return new NullChatCompletionClient(new AiOptions());
        });
        services.AddSingleton<IKnowledgeTextExtractor, KnowledgeTextExtractor>();
        services.AddSingleton<IKnowledgeChunker, KnowledgeChunker>();
        services.AddSingleton<CreateKnowledgeSourceHandler>();
        services.AddSingleton<UploadPdfHandler>();
        services.AddSingleton<IndexKnowledgeSourceHandler>();
        services.AddSingleton<DeleteKnowledgeSourceHandler>();
        services.AddSingleton<RetrieveTopChunksHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/knowledge")
            .RequireAuthorization();

        group.MapPost("/sources", KnowledgeEndpoints.CreateSourceAsync);
        group.MapPost("/sources/{sourceId}/pdf", KnowledgeEndpoints.UploadPdfAsync);
        group.MapPost("/sources/{sourceId}/index", KnowledgeEndpoints.IndexSourceAsync);
        group.MapDelete("/sources/{sourceId}", KnowledgeEndpoints.DeleteSourceAsync);
        group.MapGet("/sources", KnowledgeEndpoints.ListSourcesAsync);
        group.MapGet("/retrieve", KnowledgeEndpoints.RetrieveAsync);

        group.MapGet("/quick-facts", KnowledgeEndpoints.ListQuickFactsAsync);
        group.MapPost("/quick-facts", KnowledgeEndpoints.AddQuickFactAsync);
        group.MapDelete("/quick-facts/{factId}", KnowledgeEndpoints.DeleteQuickFactAsync);
    }
}
