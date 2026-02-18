using Intentify.Modules.Auth.Api;
using Intentify.Modules.Tickets.Application;
using Intentify.Modules.Tickets.Infrastructure;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intentify.Modules.Tickets.Api;

public sealed class TicketsModule : IAppModule
{
    public string Name => "Tickets";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<ITicketRepository, TicketRepository>();
        services.AddSingleton<ITicketNoteRepository, TicketNoteRepository>();
        services.AddSingleton<CreateTicketHandler>();
        services.AddSingleton<GetTicketHandler>();
        services.AddSingleton<ListTicketsHandler>();
        services.AddSingleton<UpdateTicketHandler>();
        services.AddSingleton<SetTicketAssignmentHandler>();
        services.AddSingleton<AddTicketNoteHandler>();
        services.AddSingleton<ListTicketNotesHandler>();
        services.AddSingleton<TransitionTicketStatusHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/tickets")
            .AddEndpointFilter<RequireAuthFilter>();

        group.MapPost(string.Empty, TicketsEndpoints.CreateAsync);
        group.MapGet(string.Empty, TicketsEndpoints.ListAsync);
        group.MapGet("/{ticketId}", TicketsEndpoints.GetAsync);
        group.MapPut("/{ticketId}", TicketsEndpoints.UpdateAsync);
        group.MapPut("/{ticketId}/assignment", TicketsEndpoints.SetAssignmentAsync);
        group.MapPost("/{ticketId}/notes", TicketsEndpoints.AddNoteAsync);
        group.MapGet("/{ticketId}/notes", TicketsEndpoints.ListNotesAsync);
        group.MapPut("/{ticketId}/status", TicketsEndpoints.TransitionStatusAsync);
    }
}
