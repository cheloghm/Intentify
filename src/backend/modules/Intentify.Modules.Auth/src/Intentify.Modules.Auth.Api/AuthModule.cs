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
        services.AddSingleton<JwtTokenIssuer>();
        services.AddSingleton<JwtTokenValidator>();
        services.AddSingleton<PasswordHasher>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<ITenantRepository, TenantRepository>();
        services.AddSingleton<RegisterUserHandler>();
        services.AddSingleton<LoginUserHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/auth");

        group.MapPost("/register", AuthEndpoints.RegisterAsync);
        group.MapPost("/login", AuthEndpoints.LoginAsync);
        group.MapGet("/me", AuthEndpoints.GetCurrentUser)
            .AddEndpointFilter<RequireAuthFilter>();
    }
}
