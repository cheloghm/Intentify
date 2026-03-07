using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Intentify.AppHost;
using Intentify.Modules.Auth.Api;
using Intentify.Modules.Collector.Api;
using Intentify.Modules.Sites.Api;
using Intentify.Modules.Visitors.Domain;
using Intentify.Shared.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Xunit;

namespace Intentify.Modules.Visitors.Tests;

public sealed class VisitorsIntegrationTests : IAsyncLifetime
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
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        await _mongo.DisposeAsync();
    }

    [Fact]
    public async Task CollectorEvents_CreateVisitorSession_AndTimeline()
    {
        var accessToken = await RegisterUserAsync();
        var site = await CreateSiteAsync(accessToken);
        await SetAllowedOriginAsync(accessToken, site.SiteId, "http://localhost:8088");

        var now = DateTime.UtcNow;
        await PostCollectorEventAsync(site.SiteKey, "page_view", "http://localhost:8088/home", "http://localhost:8088", now, "sess-a");
        await PostCollectorEventAsync(site.SiteKey, "click", "http://localhost:8088/home", "http://localhost:8088", now.AddSeconds(30), "sess-a");

        var visitorsResponse = await SendAuthorizedAsync(HttpMethod.Get, $"/visitors?siteId={site.SiteId}&page=1&pageSize=10", accessToken);
        Assert.Equal(HttpStatusCode.OK, visitorsResponse.StatusCode);

        using var visitorsDoc = JsonDocument.Parse(await visitorsResponse.Content.ReadAsStringAsync());
        var visitors = visitorsDoc.RootElement;
        Assert.True(visitors.GetArrayLength() >= 1);
        var visitorId = visitors[0].GetProperty("visitorId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(visitorId));

        var timelineResponse = await SendAuthorizedAsync(HttpMethod.Get, $"/visitors/{visitorId}/timeline?siteId={site.SiteId}&limit=10", accessToken);
        Assert.Equal(HttpStatusCode.OK, timelineResponse.StatusCode);

        using var timelineDoc = JsonDocument.Parse(await timelineResponse.Content.ReadAsStringAsync());
        var timeline = timelineDoc.RootElement;
        Assert.True(timeline.GetArrayLength() >= 2);
        var firstOccurred = timeline[0].GetProperty("occurredAtUtc").GetDateTime();
        var secondOccurred = timeline[1].GetProperty("occurredAtUtc").GetDateTime();
        Assert.True(firstOccurred >= secondOccurred);

        var mongoClient = new MongoClient(_mongo.ConnectionString);
        var database = mongoClient.GetDatabase(_mongo.DatabaseName);
        var visitorsCollection = database.GetCollection<Visitor>(VisitorsMongoCollections.Visitors);
        var storedVisitor = await visitorsCollection.Find(item => item.SiteId == Guid.Parse(site.SiteId)).FirstOrDefaultAsync();
        Assert.NotNull(storedVisitor);
        Assert.NotEmpty(storedVisitor!.Sessions);
        Assert.True(storedVisitor.Sessions[0].EngagementScore > 0);
    }

    [Fact]
    public async Task VisitCounts_ReturnsExpectedWindows()
    {
        var accessToken = await RegisterUserAsync();
        var site = await CreateSiteAsync(accessToken);
        await SetAllowedOriginAsync(accessToken, site.SiteId, "http://localhost:8088");

        var now = DateTime.UtcNow;
        await PostCollectorEventAsync(site.SiteKey, "page_view", "http://localhost:8088/now", null, now.AddDays(-3), "s-7");
        await PostCollectorEventAsync(site.SiteKey, "page_view", "http://localhost:8088/now", null, now.AddDays(-20), "s-30");
        await PostCollectorEventAsync(site.SiteKey, "page_view", "http://localhost:8088/now", null, now.AddDays(-70), "s-90");
        await PostCollectorEventAsync(site.SiteKey, "page_view", "http://localhost:8088/old", null, now.AddDays(-120), "s-old");

        var countsResponse = await SendAuthorizedAsync(HttpMethod.Get, $"/visitors/visits/counts?siteId={site.SiteId}", accessToken);
        Assert.Equal(HttpStatusCode.OK, countsResponse.StatusCode);

        using var json = JsonDocument.Parse(await countsResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetProperty("last7").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("last30").GetInt32());
        Assert.Equal(3, json.RootElement.GetProperty("last90").GetInt32());
    }

    [Fact]
    public async Task CollectorEvents_WithoutSessionAndFirstParty_DoNotCollapseIntoSharedVisitor()
    {
        var accessToken = await RegisterUserAsync();
        var site = await CreateSiteAsync(accessToken);
        await SetAllowedOriginAsync(accessToken, site.SiteId, "http://localhost:8088");

        var now = DateTime.UtcNow;
        await PostCollectorEventAsync(site.SiteKey, "page_view", "http://localhost:8088/one", null, now, null);
        await PostCollectorEventAsync(site.SiteKey, "page_view", "http://localhost:8088/two", null, now.AddSeconds(1), null);

        var mongoClient = new MongoClient(_mongo.ConnectionString);
        var database = mongoClient.GetDatabase(_mongo.DatabaseName);
        var visitorsCollection = database.GetCollection<Visitor>(VisitorsMongoCollections.Visitors);
        var visitorCount = await visitorsCollection.CountDocumentsAsync(item => item.SiteId == Guid.Parse(site.SiteId));

        Assert.Equal(2, visitorCount);
    }

    private async Task SetAllowedOriginAsync(string accessToken, string siteId, string origin)
    {
        var updateResponse = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/sites/{siteId}/origins",
            accessToken,
            JsonContent.Create(new UpdateAllowedOriginsRequest(new[] { origin })));

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
    }

    private async Task PostCollectorEventAsync(string siteKey, string eventType, string url, string? referrer, DateTime tsUtc, string? sessionId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/collector/events")
        {
            Content = JsonContent.Create(new CollectorEventRequest(siteKey, eventType, url, referrer, tsUtc, sessionId))
        };
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost:8088");

        var response = await _client!.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<CreateSiteResponse> CreateSiteAsync(string accessToken)
    {
        var domain = $"visitors-{Guid.NewGuid():N}.intentify.local";
        var response = await SendAuthorizedAsync(HttpMethod.Post, "/sites", accessToken, JsonContent.Create(new CreateSiteRequest(domain)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CreateSiteResponse>();
        Assert.NotNull(payload);

        return payload!;
    }

    private async Task<string> RegisterUserAsync()
    {
        var email = $"visitors-{Guid.NewGuid():N}@intentify.local";
        var response = await _client!.PostAsJsonAsync("/auth/register", new RegisterRequest("Visitors Tester", email, "password-123"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);

        return payload!.AccessToken;
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string url, string accessToken, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = content;

        return await _client!.SendAsync(request);
    }
}
