using Intentify.Modules.Auth.Api;
using Intentify.Modules.Leads.Application;
using Intentify.Modules.Leads.Infrastructure;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intentify.Modules.Leads.Api;

public sealed class LeadsModule : IAppModule
{
    public string Name => "Leads";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ILeadRepository, LeadRepository>();
        services.AddSingleton<ILeadVisitorLinker, LeadVisitorLinker>();
        services.AddSingleton<UpsertLeadFromPromoEntryHandler>();
        services.AddSingleton<ListLeadsHandler>();
        services.AddSingleton<GetLeadHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/leads").AddEndpointFilter<RequireAuthFilter>();
        group.MapGet(string.Empty, LeadsEndpoints.ListAsync);
        group.MapGet("/{leadId}", LeadsEndpoints.GetAsync);
    }
}
