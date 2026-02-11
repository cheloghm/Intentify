using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Intentify.AppHost;
using Intentify.Shared.Security;
using Intentify.Shared.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Intentify.AppHost.Tests;

public sealed class DebugEndpointTests : IAsyncLifetime
{
    private readonly MongoContainerFixture _mongo = new();
    private readonly JwtOptions _jwtOptions = new()
    {
        Issuer = "intentify",
        Audience = "intentify-users",
        SigningKey = "test-signing-key-1234567890-EXTRA-KEY",
        AccessTokenMinutes = 30
    };

    public async Task InitializeAsync()
    {
        await _mongo.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _mongo.DisposeAsync();
    }

    [Fact]
    public async Task DebugEndpoint_IsMappedInDevelopment()
    {
        Environment.SetEnvironmentVariable(DebugEndpoints.DebugSecretEnvironmentVariable, "test-secret");

        try
        {
            await using var app = await BuildApp(Environments.Development);

            using var request = new HttpRequestMessage(HttpMethod.Get, "/debug");
            request.Headers.Add(DebugEndpoints.DebugSecretHeaderName, "test-secret");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateAccessToken());

            var response = await app.GetTestClient().SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable(DebugEndpoints.DebugSecretEnvironmentVariable, null);
        }
    }

    [Fact]
    public async Task DebugEndpoint_RequiresSecretHeader()
    {
        Environment.SetEnvironmentVariable(DebugEndpoints.DebugSecretEnvironmentVariable, "test-secret");

        try
        {
            await using var app = await BuildApp(Environments.Development);

            using var request = new HttpRequestMessage(HttpMethod.Get, "/debug");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateAccessToken());

            var response = await app.GetTestClient().SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable(DebugEndpoints.DebugSecretEnvironmentVariable, null);
        }
    }

    [Fact]
    public async Task DebugEndpoint_IsNotMappedInProduction()
    {
        await using var app = await BuildApp(Environments.Production);

        var response = await app.GetTestClient().GetAsync("/debug");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cors_AllowsConfiguredFrontendOrigin_ForAuthRegisterPreflight()
    {
        await using var app = await BuildApp(Environments.Development);

        using var request = new HttpRequestMessage(HttpMethod.Options, "/auth/register");
        request.Headers.Add("Origin", "http://127.0.0.1:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await app.GetTestClient().SendAsync(request);

        Assert.True(
            response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values) &&
            values.Contains("http://127.0.0.1:3000"),
            "Expected Access-Control-Allow-Origin header for configured frontend origin.");
    }


    [Fact]
    public async Task Cors_UsesLocalFallbackOrigins_ForLocalProductionRunWithoutConfiguredOrigins()
    {
        await using var app = await BuildApp(
            Environments.Production,
            includeCorsConfiguration: false,
            extraConfig: new Dictionary<string, string?>
            {
                ["ASPNETCORE_URLS"] = "http://localhost:5000"
            });

        using var request = new HttpRequestMessage(HttpMethod.Options, "/collector/events");
        request.Headers.Add("Origin", "http://127.0.0.1:8088");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await app.GetTestClient().SendAsync(request);

        Assert.True(
            response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values) &&
            values.Contains("http://127.0.0.1:8088"),
            "Expected Access-Control-Allow-Origin header for local fallback origin.");
    }

    private async Task<WebApplication> BuildApp(
        string environment,
        bool includeCorsConfiguration = true,
        IDictionary<string, string?>? extraConfig = null)
    {
        var builder = AppHostApplication.CreateBuilder([], environment);
        builder.WebHost.UseTestServer();

        var config = new Dictionary<string, string?>
        {
            ["Intentify:Jwt:Issuer"] = _jwtOptions.Issuer,
            ["Intentify:Jwt:Audience"] = _jwtOptions.Audience,
            ["Intentify:Jwt:SigningKey"] = _jwtOptions.SigningKey,
            ["Intentify:Jwt:AccessTokenMinutes"] = _jwtOptions.AccessTokenMinutes.ToString(),
            ["Intentify:Mongo:ConnectionString"] = _mongo.ConnectionString,
            ["Intentify:Mongo:DatabaseName"] = _mongo.DatabaseName
        };

        if (includeCorsConfiguration)
        {
            config["Intentify:Cors:AllowedOrigins"] = "http://127.0.0.1:3000,http://localhost:3000";
        }

        if (extraConfig is not null)
        {
            foreach (var pair in extraConfig)
            {
                config[pair.Key] = pair.Value;
            }
        }

        builder.Configuration.AddInMemoryCollection(config);

        var app = AppHostApplication.Build(builder);
        await app.StartAsync();
        return app;
    }

    private string CreateAccessToken()
    {
        var issuer = new JwtTokenIssuer();
        var tokenResult = issuer.IssueAccessToken(
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"),
            new[] { "user" },
            _jwtOptions);

        if (!tokenResult.IsSuccess || string.IsNullOrWhiteSpace(tokenResult.Value))
        {
            throw new InvalidOperationException("Failed to generate access token for debug endpoint tests.");
        }

        return tokenResult.Value;
    }
}
