using Intentify.Modules.Auth.Api;
using Intentify.Modules.Promos.Application;
using Intentify.Modules.Promos.Infrastructure;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intentify.Modules.Promos.Api;

public sealed class PromosModule : IAppModule
{
    public string Name => "Promos";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IPromoRepository, PromoRepository>();
        services.AddSingleton<IPromoEntryRepository, PromoEntryRepository>();
        services.AddSingleton<IPromoConsentLogRepository, PromoConsentLogRepository>();
        services.AddSingleton<IPromoVisitorLookup, PromoVisitorLookup>();
        services.AddSingleton<CreatePromoHandler>();
        services.AddSingleton<ListPromosHandler>();
        services.AddSingleton<ListPromoEntriesHandler>();
        services.AddSingleton<GetPromoDetailHandler>();
        services.AddSingleton<CreatePublicPromoEntryHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/promos").AddEndpointFilter<RequireAuthFilter>();
        admin.MapPost(string.Empty, PromosEndpoints.CreatePromoAsync);
        admin.MapGet(string.Empty, PromosEndpoints.ListPromosAsync);
        admin.MapGet("/{promoId}", PromosEndpoints.GetPromoDetailAsync);
        admin.MapGet("/{promoId}/entries", PromosEndpoints.ListEntriesAsync);
        admin.MapGet("/entries/by-visitor", PromosEndpoints.ListEntriesByVisitorAsync);
        admin.MapGet("/{promoId}/flyer", PromosEndpoints.DownloadFlyerAsync);
        admin.MapGet("/{promoId}/export.csv", PromosEndpoints.ExportCsvAsync);

        endpoints.MapGet("/promos/public/{promoKey}", PromosEndpoints.GetPublicPromoAsync);
        endpoints.MapPost("/promos/public/{promoKey}/entries", PromosEndpoints.CreatePublicEntryAsync);
    }
}
