using Intentify.Modules.Auth.Api;
using Intentify.Modules.LinkHub.Application;
using Intentify.Modules.LinkHub.Infrastructure;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intentify.Modules.LinkHub.Api;

public sealed class LinkHubModule : IAppModule
{
    public string Name => "LinkHub";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ILinkHubRepository, LinkHubRepository>();
        services.AddSingleton<GetOrCreateProfileHandler>();
        services.AddSingleton<SaveProfileHandler>();
        services.AddSingleton<GetPublicProfileHandler>();
        services.AddSingleton<RecordClickHandler>();
        services.AddSingleton<GetAnalyticsHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Admin routes (require auth)
        var admin = endpoints.MapGroup("/linkhub").AddEndpointFilter<RequireAuthFilter>();
        admin.MapGet("/profile",              LinkHubEndpoints.GetProfileAsync);
        admin.MapPut("/profile",              LinkHubEndpoints.SaveProfileAsync);
        admin.MapPost("/profile/avatar",     LinkHubEndpoints.UploadAvatarAsync);
        admin.MapGet("/analytics",           LinkHubEndpoints.GetAnalyticsAsync);

        // Public routes (no auth)
        var pub = endpoints.MapGroup("/hub");
        pub.MapGet("/{slug}",              LinkHubEndpoints.GetPublicPageAsync).AllowAnonymous();
        pub.MapPost("/{slug}/view",        LinkHubEndpoints.RecordViewAsync).AllowAnonymous();
        pub.MapPost("/{slug}/click",       LinkHubEndpoints.RecordClickAsync).AllowAnonymous();
    }
}
