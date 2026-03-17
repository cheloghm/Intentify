using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Intentify.AppHost;
using Intentify.Modules.Auth.Api;
using Intentify.Modules.Collector.Api;
using Intentify.Modules.Collector.Domain;
using Intentify.Modules.Flows.Api;
using Intentify.Modules.Sites.Api;
using Intentify.Shared.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Xunit;

namespace Intentify.Modules.Collector.Tests;

public sealed class CollectorIntegrationTests : IAsyncLifetime
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
    public async Task TrackerScript_IsServed()
    {
        var response = await _client!.GetAsync("/collector/tracker.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/javascript", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("/collector/events", content);
    }

    [Fact]
    public async Task SdkScript_IsServed()
    {
        var response = await _client!.GetAsync("/collector/sdk.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/javascript", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("/collector/sdk/bootstrap", content);
        Assert.Contains("/engage/widget.js", content);
    }

    [Fact]
    public async Task PostEvent_StoresEvent_AndMarksInstalled()
    {
        var accessToken = await RegisterUserAsync();
        var site = await CreateSiteAsync(accessToken);

        var updateResponse = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/sites/{site.SiteId}/origins",
            accessToken,
            JsonContent.Create(new UpdateAllowedOriginsRequest(new[] { "http://localhost:8088" })));

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var collectRequest = new HttpRequestMessage(HttpMethod.Post, "/collector/events")
        {
            Content = JsonContent.Create(new CollectorEventRequest(
                site.SiteKey,
                "pageview",
                "http://localhost:8088/home",
                "http://localhost:8088/",
                DateTime.UtcNow))
        };
        collectRequest.Headers.TryAddWithoutValidation("Origin", "http://localhost:8088");

        var collectResponse = await _client!.SendAsync(collectRequest);
        Assert.Equal(HttpStatusCode.OK, collectResponse.StatusCode);

        var mongoClient = new MongoClient(_mongo.ConnectionString);
        var database = mongoClient.GetDatabase(_mongo.DatabaseName);
        var events = database.GetCollection<CollectorEvent>(CollectorMongoCollections.Events);
        var eventCount = await events.CountDocumentsAsync(evt => evt.SiteId == Guid.Parse(site.SiteId));
        Assert.Equal(1, eventCount);

        var statusResponse = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/sites/{site.SiteId}/installation-status",
            accessToken);

        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var statusPayload = await statusResponse.Content.ReadAsStringAsync();
        using var statusDocument = JsonDocument.Parse(statusPayload);
        var statusRoot = statusDocument.RootElement;
        Assert.True(statusRoot.GetProperty("isInstalled").GetBoolean());
        Assert.Equal(JsonValueKind.String, statusRoot.GetProperty("firstEventReceivedAtUtc").ValueKind);
    }

    [Fact]
    public async Task PostEvent_InvalidOrigin_IsRejected()
    {
        var accessToken = await RegisterUserAsync();
        var site = await CreateSiteAsync(accessToken);

        var updateResponse = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/sites/{site.SiteId}/origins",
            accessToken,
            JsonContent.Create(new UpdateAllowedOriginsRequest(new[] { "http://localhost:8088" })));

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var collectRequest = new HttpRequestMessage(HttpMethod.Post, "/collector/events")
        {
            Content = JsonContent.Create(new CollectorEventRequest(
                site.SiteKey,
                "pageview",
                "http://evil.com/",
                null,
                DateTime.UtcNow))
        };
        collectRequest.Headers.TryAddWithoutValidation("Origin", "http://evil.com");

        var collectResponse = await _client!.SendAsync(collectRequest);
        Assert.Equal(HttpStatusCode.Forbidden, collectResponse.StatusCode);

        var mongoClient = new MongoClient(_mongo.ConnectionString);
        var database = mongoClient.GetDatabase(_mongo.DatabaseName);
        var events = database.GetCollection<CollectorEvent>(CollectorMongoCollections.Events);
        var eventCount = await events.CountDocumentsAsync(evt => evt.SiteId == Guid.Parse(site.SiteId));
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public async Task PostEvent_SiteKeyMismatchBetweenQueryAndBody_IsRejected()
    {
        var accessToken = await RegisterUserAsync();
        var site = await CreateSiteAsync(accessToken);

        var updateResponse = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/sites/{site.SiteId}/origins",
            accessToken,
            JsonContent.Create(new UpdateAllowedOriginsRequest(new[] { "http://localhost:8088" })));

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var collectRequest = new HttpRequestMessage(HttpMethod.Post, $"/collector/events?siteKey={Guid.NewGuid():N}")
        {
            Content = JsonContent.Create(new CollectorEventRequest(
                site.SiteKey,
                "pageview",
                "http://localhost:8088/home",
                null,
                DateTime.UtcNow))
        };
        collectRequest.Headers.TryAddWithoutValidation("Origin", "http://localhost:8088");

        var collectResponse = await _client!.SendAsync(collectRequest);
        Assert.Equal(HttpStatusCode.BadRequest, collectResponse.StatusCode);

        using var json = JsonDocument.Parse(await collectResponse.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("siteKey", out _));
    }

    [Fact]
    public async Task PostPageview_TriggersCollectorPageViewFlowRun()
    {
        var accessToken = await RegisterUserAsync();
        var site = await CreateSiteAsync(accessToken);

        var flowCreateResponse = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/flows",
            accessToken,
            JsonContent.Create(new CreateFlowRequest(
                site.SiteId,
                "Collector pageview trigger",
                new FlowTriggerRequest("CollectorPageView", null),
                null,
                new[] { new FlowActionRequest("LogRun", null) })));

        Assert.Equal(HttpStatusCode.OK, flowCreateResponse.StatusCode);

        using var flowCreateJson = JsonDocument.Parse(await flowCreateResponse.Content.ReadAsStringAsync());
        var flowId = flowCreateJson.RootElement.GetProperty("id").GetGuid();

        var updateOriginsResponse = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/sites/{site.SiteId}/origins",
            accessToken,
            JsonContent.Create(new UpdateAllowedOriginsRequest(new[] { "http://localhost:8088" })));

        Assert.Equal(HttpStatusCode.OK, updateOriginsResponse.StatusCode);

        var collectRequest = new HttpRequestMessage(HttpMethod.Post, "/collector/events")
        {
            Content = JsonContent.Create(new CollectorEventRequest(
                site.SiteKey,
                "pageview",
                "http://localhost:8088/pricing",
                "http://localhost:8088/",
                DateTime.UtcNow,
                SessionId: "session-a"))
        };
        collectRequest.Headers.TryAddWithoutValidation("Origin", "http://localhost:8088");

        var collectResponse = await _client!.SendAsync(collectRequest);
        Assert.Equal(HttpStatusCode.OK, collectResponse.StatusCode);

        var runsResponse = await SendAuthorizedAsync(HttpMethod.Get, $"/flows/{flowId:N}/runs", accessToken);
        Assert.Equal(HttpStatusCode.OK, runsResponse.StatusCode);

        using var runsJson = JsonDocument.Parse(await runsResponse.Content.ReadAsStringAsync());
        var runs = runsJson.RootElement;
        Assert.Equal(JsonValueKind.Array, runs.ValueKind);
        Assert.True(runs.GetArrayLength() > 0);
        Assert.Equal("CollectorPageView", runs[0].GetProperty("triggerType").GetString());
    }

    private async Task<CreateSiteResponse> CreateSiteAsync(string accessToken)
    {
        var domain = $"collector-{Guid.NewGuid():N}.intentify.local";
        var createResponse = await SendAuthorizedAsync(HttpMethod.Post, "/sites", accessToken,
            JsonContent.Create(new CreateSiteRequest(domain)));

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var createPayload = await createResponse.Content.ReadFromJsonAsync<CreateSiteResponse>();
        Assert.NotNull(createPayload);
        Assert.False(string.IsNullOrWhiteSpace(createPayload!.SiteId));

        return createPayload;
    }

    private async Task<string> RegisterUserAsync()
    {
        var email = $"collector-{Guid.NewGuid():N}@intentify.local";
        var request = new RegisterRequest("Collector Tester", email, "password-123", "Default Org");

        var response = await _client!.PostAsJsonAsync("/auth/register", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));

        return payload.AccessToken;
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpMethod method,
        string url,
        string accessToken,
        HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (content is not null)
        {
            request.Content = content;
        }

        return await _client!.SendAsync(request);
    }
}
