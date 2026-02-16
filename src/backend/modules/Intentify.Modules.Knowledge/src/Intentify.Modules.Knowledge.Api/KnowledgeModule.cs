using Intentify.Modules.Auth.Api;
using Intentify.Modules.Knowledge.Application;
using Intentify.Modules.Knowledge.Infrastructure;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intentify.Modules.Knowledge.Api;

public sealed class KnowledgeModule : IAppModule
{
    public string Name => "Knowledge";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddHttpClient("knowledge");
        services.AddSingleton<IKnowledgeSourceRepository, KnowledgeSourceRepository>();
        services.AddSingleton<IKnowledgeChunkRepository, KnowledgeChunkRepository>();
        services.AddSingleton<IEngageBotResolver, EngageBotResolver>();
        services.AddSingleton<IKnowledgeTextExtractor, KnowledgeTextExtractor>();
        services.AddSingleton<IKnowledgeChunker, KnowledgeChunker>();
        services.AddSingleton<CreateKnowledgeSourceHandler>();
        services.AddSingleton<UploadPdfHandler>();
        services.AddSingleton<IndexKnowledgeSourceHandler>();
        services.AddSingleton<RetrieveTopChunksHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/knowledge")
            .RequireAuthorization();

        group.MapPost("/sources", KnowledgeEndpoints.CreateSourceAsync);
        group.MapPost("/sources/{sourceId}/pdf", KnowledgeEndpoints.UploadPdfAsync);
        group.MapPost("/sources/{sourceId}/index", KnowledgeEndpoints.IndexSourceAsync);
        group.MapGet("/sources", KnowledgeEndpoints.ListSourcesAsync);
        group.MapGet("/retrieve", KnowledgeEndpoints.RetrieveAsync);
    }
}
