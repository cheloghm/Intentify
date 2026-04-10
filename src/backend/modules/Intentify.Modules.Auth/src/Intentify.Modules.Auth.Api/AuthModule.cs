using Intentify.Modules.Auth.Application;
using Intentify.Modules.Auth.Infrastructure;
using Intentify.Shared.Data.Mongo;
using Intentify.Shared.Security;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Intentify.Modules.Auth.Api;

public sealed class AuthModule : IAppModule
{
    public string Name => "Auth";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var mongoOptions = configuration.GetSection("Intentify:Mongo").Get<MongoOptions>() ?? new MongoOptions();
        var mongoClientResult = MongoClientFactory.CreateMongoClient(mongoOptions);
        if (!mongoClientResult.IsSuccess || mongoClientResult.Value is null)
        {
            throw new InvalidOperationException(mongoClientResult.Error?.Message ?? "Mongo options are invalid.");
        }

        MongoConventions.Register();

        services.AddSingleton(mongoClientResult.Value);
        services.AddSingleton(sp => mongoClientResult.Value.GetDatabase(mongoOptions.DatabaseName));
        services.Configure<JwtOptions>(configuration.GetSection("Intentify:Jwt"));
        services.Configure<GoogleOAuthOptions>(configuration.GetSection("Intentify:Auth:Google"));
        services.AddHttpClient();
        services.AddSingleton<JwtTokenIssuer>();
        services.AddSingleton<JwtTokenValidator>();
        services.AddSingleton<PasswordHasher>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<ITenantRepository, TenantRepository>();
        services.AddSingleton<IInvitationRepository, InvitationRepository>();
        services.AddSingleton<GetCurrentUserHandler>();
        services.AddSingleton<RegisterUserHandler>();
        services.AddSingleton<LoginUserHandler>();
        services.AddSingleton<CreateInviteHandler>();
        services.AddSingleton<AcceptInviteHandler>();
        services.AddSingleton<UpdateCurrentUserProfileHandler>();
        services.AddSingleton<ListTenantUsersHandler>();
        services.AddSingleton<ChangeTenantUserRoleHandler>();
        services.AddSingleton<RemoveTenantUserHandler>();
        services.AddSingleton<ListTenantInvitesHandler>();
        services.AddSingleton<RevokeTenantInviteHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/auth");

        group.MapPost("/register", AuthEndpoints.RegisterAsync);
        group.MapPost("/login", AuthEndpoints.LoginAsync);
        group.MapGet("/google", AuthEndpoints.GoogleOAuthInitiate);
        group.MapGet("/google/callback", AuthEndpoints.GoogleOAuthCallback);
        group.MapPost("/invites/accept", AuthEndpoints.AcceptInviteAsync);
        group.MapGet("/me", AuthEndpoints.GetCurrentUser)
            .AddEndpointFilter<RequireAuthFilter>();
        group.MapPut("/me", AuthEndpoints.UpdateCurrentUserProfileAsync)
            .AddEndpointFilter<RequireAuthFilter>();
        group.MapPost("/invites", AuthEndpoints.CreateInviteAsync)
            .AddEndpointFilter<RequireAuthFilter>();
        group.MapGet("/users", AuthEndpoints.ListTenantUsersAsync)
            .AddEndpointFilter<RequireAuthFilter>();
        group.MapPut("/users/{userId}/role", AuthEndpoints.ChangeTenantUserRoleAsync)
            .AddEndpointFilter<RequireAuthFilter>();
        group.MapDelete("/users/{userId}", AuthEndpoints.RemoveTenantUserAsync)
            .AddEndpointFilter<RequireAuthFilter>();
        group.MapGet("/invites", AuthEndpoints.ListTenantInvitesAsync)
            .AddEndpointFilter<RequireAuthFilter>();
        group.MapDelete("/invites/{inviteId}", AuthEndpoints.RevokeTenantInviteAsync)
            .AddEndpointFilter<RequireAuthFilter>();
        group.MapPost("/admin/promote-self", AuthEndpoints.PromoteSelfAsync)
            .AddEndpointFilter<RequireAuthFilter>();
    }
}
