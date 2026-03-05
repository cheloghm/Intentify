using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Intentify.AppHost;
using Intentify.Modules.Auth.Api;
using Intentify.Modules.Sites.Api;
using Intentify.Shared.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Intentify.Modules.Ads.Tests;

public sealed class AdsIntegrationTests : IAsyncLifetime
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

        _app = AppHostApplication.Build(builder);
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null) await _app.DisposeAsync();
        await _mongo.DisposeAsync();
    }

    [Fact]
    public async Task Unauthorized_List_ReturnsUnauthorized()
    {
        var response = await _client!.GetAsync("/ads/campaigns");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TenantIsolation_BlocksCrossTenantGet()
    {
        var tokenA = await RegisterUserAsync("ads-tenant-a");
        var tokenB = await RegisterUserAsync("ads-tenant-b");

        var siteA = await CreateSiteAsync(tokenA);
        var created = await CreateCampaignAsync(tokenA, siteA.SiteId, "Tenant A Campaign");

        var crossTenant = await SendAuthorizedAsync(HttpMethod.Get, $"/ads/campaigns/{created.CampaignId}", tokenB);
        Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);
    }

    [Fact]
    public async Task Create_WithSiteNotOwnedByTenant_ReturnsNotFound()
    {
        var tokenA = await RegisterUserAsync("ads-owner-a");
        var tokenB = await RegisterUserAsync("ads-owner-b");
        var siteB = await CreateSiteAsync(tokenB);

        var response = await SendAuthorizedAsync(HttpMethod.Post, "/ads/campaigns", tokenA,
            JsonContent.Create(new { siteId = siteB.SiteId, name = "Wrong Site", isActive = true }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Report_ReturnsZeroMetrics_AndPlacementRows()
    {
        var token = await RegisterUserAsync("ads-report");
        var site = await CreateSiteAsync(token);

        var createResponse = await SendAuthorizedAsync(HttpMethod.Post, "/ads/campaigns", token,
            JsonContent.Create(new
            {
                siteId = site.SiteId,
                name = "Zero Campaign",
                isActive = true,
                placements = new[]
                {
                    new { slotKey = "hero", headline = "H1", destinationUrl = "https://example.com/a", order = 1, isActive = true },
                    new { slotKey = "sidebar", headline = "H2", destinationUrl = "https://example.com/b", order = 2, isActive = true }
                }
            }));

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        using var createdJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var campaignId = createdJson.RootElement.GetProperty("id").GetGuid();

        var reportResponse = await SendAuthorizedAsync(HttpMethod.Get, $"/ads/campaigns/{campaignId}/report", token);
        Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);

        using var reportJson = JsonDocument.Parse(await reportResponse.Content.ReadAsStringAsync());
        Assert.Equal("none", reportJson.RootElement.GetProperty("dataSource").GetString());
        Assert.Equal(0, reportJson.RootElement.GetProperty("totals").GetProperty("impressions").GetInt64());
        Assert.Equal(0, reportJson.RootElement.GetProperty("totals").GetProperty("clicks").GetInt64());
        Assert.Equal(2, reportJson.RootElement.GetProperty("byPlacement").GetArrayLength());
        foreach (var placement in reportJson.RootElement.GetProperty("byPlacement").EnumerateArray())
        {
            Assert.Equal(0, placement.GetProperty("impressions").GetInt64());
            Assert.Equal(0, placement.GetProperty("clicks").GetInt64());
        }
    }

    private async Task<(Guid CampaignId, Guid SiteId)> CreateCampaignAsync(string token, string siteId, string name)
    {
        var response = await SendAuthorizedAsync(HttpMethod.Post, "/ads/campaigns", token,
            JsonContent.Create(new { siteId, name, isActive = true }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (json.RootElement.GetProperty("id").GetGuid(), json.RootElement.GetProperty("siteId").GetGuid());
    }

    private async Task<CreateSiteResponse> CreateSiteAsync(string token)
    {
        var response = await SendAuthorizedAsync(HttpMethod.Post, "/sites", token,
            JsonContent.Create(new CreateSiteRequest($"ads-{Guid.NewGuid():N}.intentify.local")));
        var payload = await response.Content.ReadFromJsonAsync<CreateSiteResponse>();
        return payload!;
    }

    private async Task<string> RegisterUserAsync(string prefix)
    {
        var response = await _client!.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Ads Tester", $"{prefix}-{Guid.NewGuid():N}@intentify.local", "password-123"));
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return payload!.AccessToken;
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string url, string token, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = content;
        return await _client!.SendAsync(request);
    }
}
