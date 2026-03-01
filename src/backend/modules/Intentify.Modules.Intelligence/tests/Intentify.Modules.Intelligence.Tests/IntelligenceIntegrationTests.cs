using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Intentify.AppHost;
using Intentify.Modules.Auth.Api;
using Intentify.Modules.Intelligence.Application;
using Intentify.Modules.Sites.Api;
using Intentify.Shared.Testing;
using Intentify.Shared.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Intentify.Modules.Intelligence.Tests;

public sealed class IntelligenceIntegrationTests : IAsyncLifetime
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
            ["Intentify:Mongo:DatabaseName"] = _mongo.DatabaseName
        });

        builder.WebHost.ConfigureTestServices(services =>
        {
            services.AddSingleton<IExternalSearchProvider, FakeExternalSearchProvider>();
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
    public async Task Refresh_Then_GetTrends_AndStatus_ReturnsStoredData()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var refreshResponse = await SendAuthorizedAsync(HttpMethod.Post, "/intelligence/refresh", token, JsonContent.Create(new
        {
            siteId = site.SiteId,
            category = "Marketing",
            location = "US",
            timeWindow = "7d",
            limit = 2
        }));

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var trendsResponse = await SendAuthorizedAsync(HttpMethod.Get, $"/intelligence/trends?siteId={site.SiteId}&category=Marketing&location=US&timeWindow=7d", token);
        Assert.Equal(HttpStatusCode.OK, trendsResponse.StatusCode);
        var trendsPayload = await trendsResponse.Content.ReadFromJsonAsync<IntelligenceTrendsResponse>();
        Assert.NotNull(trendsPayload);
        Assert.Equal("Google", trendsPayload.Provider);
        Assert.Equal(2, trendsPayload.Items.Count);

        var statusResponse = await SendAuthorizedAsync(HttpMethod.Get, $"/intelligence/status?siteId={site.SiteId}&category=Marketing&location=US&timeWindow=7d", token);
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var statusPayload = await statusResponse.Content.ReadFromJsonAsync<IntelligenceStatusResponse>();
        Assert.NotNull(statusPayload);
        Assert.Equal(2, statusPayload.ItemsCount);
    }

    private async Task<string> RegisterUserAsync()
    {
        var email = $"intelligence-{Guid.NewGuid():N}@intentify.local";
        var response = await _client!.PostAsJsonAsync("/auth/register", new RegisterRequest("Intelligence Tester", email, "password-123"));
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return payload!.AccessToken;
    }

    private async Task<CreateSiteResponse> CreateSiteAsync(string accessToken)
    {
        var domain = $"intelligence-{Guid.NewGuid():N}.intentify.local";
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

    private sealed class FakeExternalSearchProvider : IExternalSearchProvider
    {
        public Task<OperationResult<ExternalSearchResult>> SearchAsync(string tenantId, Guid siteId, ExternalSearchQuery query, CancellationToken ct)
        {
            var response = new ExternalSearchResult(
                [
                    new ExternalSearchItem("intent data", 95, 1),
                    new ExternalSearchItem("lead generation", 87, 2)
                ],
                "Google",
                DateTime.UtcNow);

            return Task.FromResult(OperationResult<ExternalSearchResult>.Success(response));
        }
    }
}
