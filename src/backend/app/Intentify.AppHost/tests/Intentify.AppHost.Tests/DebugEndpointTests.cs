using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Intentify.AppHost;
using Intentify.Modules.Sites.Application;
using Intentify.Modules.Sites.Domain;
using Intentify.Shared.Security;
using Intentify.Shared.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Intentify.AppHost.Tests;

// IMPORTANT:
// xUnit creates a new instance of the test class per [Fact].
// ICollectionFixture gives us a single Mongo container instance shared across all tests in this file.
// This prevents repeated container start/stop and avoids Docker named-pipe timeouts on Windows.
[CollectionDefinition(MongoCollectionName)]
public sealed class AppHostMongoCollection : ICollectionFixture<MongoContainerFixture>
{
    public const string MongoCollectionName = "Intentify.AppHost.Tests.Mongo";
}

[Collection(AppHostMongoCollection.MongoCollectionName)]
public sealed class DebugEndpointTests
{
    private readonly MongoContainerFixture _mongo;
    private readonly JwtOptions _jwtOptions = new()
    {
        Issuer = "intentify",
        Audience = "intentify-users",
        SigningKey = "test-signing-key-1234567890-EXTRA-KEY",
        AccessTokenMinutes = 30
    };

    public DebugEndpointTests(MongoContainerFixture mongo)
    {
        _mongo = mongo;
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
            var body = await response.Content.ReadAsStringAsync();

            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"Expected 200 OK but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
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

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(
            response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values) &&
            values.Contains("http://127.0.0.1:3000"),
            "Expected Access-Control-Allow-Origin header for configured frontend origin.");

        using var collectorRequest = new HttpRequestMessage(HttpMethod.Options, "/collector/events");
        collectorRequest.Headers.Add("Origin", "http://127.0.0.1:3000");
        collectorRequest.Headers.Add("Access-Control-Request-Method", "POST");
        collectorRequest.Headers.Add("Access-Control-Request-Headers", "content-type");

        var collectorResponse = await app.GetTestClient().SendAsync(collectorRequest);

        Assert.Equal(HttpStatusCode.NoContent, collectorResponse.StatusCode);
        Assert.True(
            collectorResponse.Headers.TryGetValues("Access-Control-Allow-Origin", out var collectorValues) &&
            collectorValues.Contains("http://127.0.0.1:3000"),
            "Expected Access-Control-Allow-Origin header for collector preflight from configured frontend origin.");
    }


    [Fact]
    public async Task Cors_AllowsCollectorOrigin_FromSiteKeyPreflight()
    {
        await using var app = await BuildApp(Environments.Development);

        var siteRepository = app.Services.GetRequiredService<ISiteRepository>();
        var site = new Site
        {
            TenantId = Guid.NewGuid(),
            Domain = "example.com",
            AllowedOrigins = ["http://localhost:8088"],
            SiteKey = Guid.NewGuid().ToString("N"),
            WidgetKey = Guid.NewGuid().ToString("N")
        };

        await siteRepository.InsertAsync(site);

        using var request = new HttpRequestMessage(HttpMethod.Options, $"/collector/events?siteKey={site.SiteKey}");
        request.Headers.Add("Origin", "http://localhost:8088");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await app.GetTestClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(
            response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values) &&
            values.Contains("http://localhost:8088"),
            "Expected Access-Control-Allow-Origin header for collector preflight from allowed site origin.");
    }

    [Fact]
    public async Task Cors_RequiresConfiguredOrigins_WhenDashboardOriginsMissing()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildApp(
            Environments.Production,
            includeCorsConfiguration: false));
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

        config["Intentify:Cors:AllowedOrigins"] = includeCorsConfiguration
            ? "http://127.0.0.1:3000,http://localhost:3000"
            : string.Empty;

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
