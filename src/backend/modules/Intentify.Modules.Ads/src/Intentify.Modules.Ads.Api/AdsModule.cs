using Intentify.Modules.Ads.Application;
using Intentify.Modules.Ads.Infrastructure;
using Intentify.Modules.Auth.Api;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intentify.Modules.Ads.Api;

public sealed class AdsModule : IAppModule
{
    public string Name => "Ads";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IAdCampaignRepository, AdCampaignRepository>();
        services.AddSingleton<CreateAdCampaignHandler>();
        services.AddSingleton<UpdateAdCampaignHandler>();
        services.AddSingleton<GetAdCampaignHandler>();
        services.AddSingleton<ListAdCampaignsHandler>();
        services.AddSingleton<UpsertAdPlacementsHandler>();
        services.AddSingleton<SetAdCampaignActiveHandler>();
        services.AddSingleton<GetAdCampaignReportHandler>();
        services.AddSingleton<GetAdsReportHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/ads").AddEndpointFilter<RequireAuthFilter>();

        group.MapGet("/report", AdsEndpoints.GetReportSummaryAsync);
        group.MapPost("/campaigns", AdsEndpoints.CreateCampaignAsync);
        group.MapGet("/campaigns", AdsEndpoints.ListCampaignsAsync);
        group.MapGet("/campaigns/{campaignId}", AdsEndpoints.GetCampaignAsync);
        group.MapPut("/campaigns/{campaignId}", AdsEndpoints.UpdateCampaignAsync);
        group.MapPut("/campaigns/{campaignId}/placements", AdsEndpoints.UpsertPlacementsAsync);
        group.MapPost("/campaigns/{campaignId}/activate", AdsEndpoints.ActivateAsync);
        group.MapPost("/campaigns/{campaignId}/deactivate", AdsEndpoints.DeactivateAsync);
        group.MapGet("/campaigns/{campaignId}/report", AdsEndpoints.GetReportAsync);
    }
}
