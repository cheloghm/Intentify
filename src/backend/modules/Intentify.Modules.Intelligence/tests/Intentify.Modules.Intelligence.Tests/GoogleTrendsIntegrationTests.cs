using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Intentify.AppHost;
using Intentify.Modules.Auth.Api;
using Intentify.Modules.Sites.Api;
using Intentify.Shared.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Intentify.Modules.Intelligence.Tests;

public sealed class GoogleTrendsIntegrationTests : IAsyncLifetime
{
    private readonly MongoContainerFixture _mongo = new();
    private WebApplication? _app;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        await _mongo.InitializeAsync();

        var builder = AppHostApplication.CreateBuilder([], Environments.Development);
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Intentify:Jwt:Issuer"] = "intentify",
            ["Intentify:Jwt:Audience"] = "intentify-users",
            ["Intentify:Jwt:SigningKey"] = "test-signing-key-1234567890-EXTRA-KEY",
            ["Intentify:Jwt:AccessTokenMinutes"] = "30",
            ["Intentify:Mongo:ConnectionString"] = _mongo.ConnectionString,
            ["Intentify:Mongo:DatabaseName"] = _mongo.DatabaseName,
            ["Intentify:Intelligence:Search:Provider"] = "GoogleTrends",
            ["Intentify:Intelligence:Google:Trends:Enabled"] = "false"
        });

        _app = AppHostApplication.Build(builder);
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        await _mongo.DisposeAsync();
    }

    [Fact]
    public async Task Refresh_WithGoogleTrendsDisabled_DoesNotBreakManualRefresh()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var refreshResponse = await SendAuthorizedAsync(HttpMethod.Post, "/intelligence/refresh", token, JsonContent.Create(new
        {
            siteId = site.SiteId,
            category = "Marketing",
            location = "US",
            timeWindow = "7d",
            limit = 10
        }));

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var payload = await refreshResponse.Content.ReadFromJsonAsync<RefreshPayload>();
        Assert.NotNull(payload);
        Assert.Equal(0, payload.ItemsCount);
        Assert.Equal("GoogleTrends", payload.Provider);
    }

    private async Task<string> RegisterUserAsync()
    {
        var email = $"trends-disabled-{Guid.NewGuid():N}@intentify.local";
        var response = await _client!.PostAsJsonAsync("/auth/register", new RegisterRequest("Trends Tester", email, "password-123"));
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return payload!.AccessToken;
    }

    private async Task<CreateSiteResponse> CreateSiteAsync(string accessToken)
    {
        var domain = $"trends-{Guid.NewGuid():N}.intentify.local";
        var response = await SendAuthorizedAsync(HttpMethod.Post, "/sites", accessToken, JsonContent.Create(new CreateSiteRequest(domain)));
        var payload = await response.Content.ReadFromJsonAsync<CreateSiteResponse>();
        return payload!;
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string url, string accessToken, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = content;
        return await _client!.SendAsync(request);
    }

    private sealed record RefreshPayload(string Provider, DateTime RefreshedAtUtc, int ItemsCount);
}
