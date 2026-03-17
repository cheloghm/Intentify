using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Intentify.AppHost;
using Intentify.Modules.Auth.Api;
using Intentify.Modules.Knowledge.Api;
using Intentify.Modules.Sites.Api;
using Intentify.Shared.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Intentify.Modules.Sites.Tests;

public sealed class SitesIntegrationTests : IAsyncLifetime
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
    public async Task SiteCreated_ReturnsKeys_And_ListOmitsKeys()
    {
        var accessToken = await RegisterUserAsync();
        var domain = $"site-{Guid.NewGuid():N}.intentify.local";
        var name = "Main Site";

        var createResponse = await SendAuthorizedAsync(HttpMethod.Post, "/sites", accessToken,
            JsonContent.Create(new CreateSiteRequest(domain, Name: name)));

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var createPayload = await createResponse.Content.ReadFromJsonAsync<CreateSiteResponse>();
        Assert.NotNull(createPayload);
        Assert.False(string.IsNullOrWhiteSpace(createPayload!.SiteId));
        Assert.Equal(name, createPayload.Name);
        Assert.Equal(domain, createPayload.Domain);
        Assert.False(string.IsNullOrWhiteSpace(createPayload.SiteKey));
        Assert.False(string.IsNullOrWhiteSpace(createPayload.WidgetKey));

        var listResponse = await SendAuthorizedAsync(HttpMethod.Get, "/sites", accessToken);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var listContent = await listResponse.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(listContent);
        var root = document.RootElement;

        Assert.Equal(JsonValueKind.Array, root.ValueKind);

        var found = false;
        foreach (var element in root.EnumerateArray())
        {
            if (element.TryGetProperty("siteId", out var siteId) && siteId.GetString() == createPayload.SiteId)
            {
                found = true;
                Assert.Equal(name, element.GetProperty("name").GetString());
                Assert.Equal(domain, element.GetProperty("domain").GetString());
                Assert.False(element.TryGetProperty("siteKey", out _));
                Assert.False(element.TryGetProperty("widgetKey", out _));
                break;
            }
        }

        Assert.True(found, "Expected created site to appear in list response.");
    }

    [Fact]
    public async Task PublicInstallationStatus_EnforcesOrigins()
    {
        var accessToken = await RegisterUserAsync();
        var domain = $"origin-{Guid.NewGuid():N}.intentify.local";

        var createResponse = await SendAuthorizedAsync(HttpMethod.Post, "/sites", accessToken,
            JsonContent.Create(new CreateSiteRequest(domain)));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var createPayload = await createResponse.Content.ReadFromJsonAsync<CreateSiteResponse>();
        Assert.NotNull(createPayload);

        var updateResponse = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/sites/{createPayload!.SiteId}/origins",
            accessToken,
            JsonContent.Create(new UpdateAllowedOriginsRequest(new[] { "http://localhost:8088" })));

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var okResponse = await SendOriginStatusRequestAsync(createPayload.WidgetKey, "http://localhost:8088");
        Assert.Equal(HttpStatusCode.OK, okResponse.StatusCode);

        var forbiddenResponse = await SendOriginStatusRequestAsync(createPayload.WidgetKey, "http://evil.com");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        var missingOriginResponse = await _client!.GetAsync($"/sites/installation/status?widgetKey={createPayload.WidgetKey}");
        Assert.Equal(HttpStatusCode.BadRequest, missingOriginResponse.StatusCode);
    }


    [Fact]
    public async Task PublicInstallationStatus_AllowsLocalhostOriginInDevelopment_WithoutConfiguredAllowedOrigins()
    {
        var accessToken = await RegisterUserAsync();
        var domain = $"dev-local-{Guid.NewGuid():N}.intentify.local";

        var createResponse = await SendAuthorizedAsync(HttpMethod.Post, "/sites", accessToken,
            JsonContent.Create(new CreateSiteRequest(domain)));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var createPayload = await createResponse.Content.ReadFromJsonAsync<CreateSiteResponse>();
        Assert.NotNull(createPayload);

        var localOriginResponse = await SendOriginStatusRequestAsync(createPayload!.WidgetKey, "http://localhost:8088");

        Assert.Equal(HttpStatusCode.OK, localOriginResponse.StatusCode);
    }

    [Fact]
    public async Task GetSiteKeys_ReturnsCurrentKeysWithoutRegeneration()
    {
        var accessToken = await RegisterUserAsync();
        var domain = $"keys-{Guid.NewGuid():N}.intentify.local";

        var createResponse = await SendAuthorizedAsync(HttpMethod.Post, "/sites", accessToken,
            JsonContent.Create(new CreateSiteRequest(domain)));
        var createPayload = await createResponse.Content.ReadFromJsonAsync<CreateSiteResponse>();

        var keysResponse = await SendAuthorizedAsync(HttpMethod.Get, $"/sites/{createPayload!.SiteId}/keys", accessToken);
        Assert.Equal(HttpStatusCode.OK, keysResponse.StatusCode);

        var keysPayload = await keysResponse.Content.ReadFromJsonAsync<SiteKeysResponse>();
        Assert.NotNull(keysPayload);
        Assert.Equal(createPayload.SiteKey, keysPayload!.SiteKey);
        Assert.Equal(createPayload.WidgetKey, keysPayload.WidgetKey);
    }

    [Fact]
    public async Task SecondSiteCreation_IsBlocked_ForSameTenant()
    {
        var accessToken = await RegisterUserAsync();

        var firstResponse = await SendAuthorizedAsync(HttpMethod.Post, "/sites", accessToken,
            JsonContent.Create(new CreateSiteRequest($"one-{Guid.NewGuid():N}.intentify.local", "First", "Retail", new[] { "brand" })));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var secondResponse = await SendAuthorizedAsync(HttpMethod.Post, "/sites", accessToken,
            JsonContent.Create(new CreateSiteRequest($"two-{Guid.NewGuid():N}.intentify.local")));

        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);

        var listResponse = await SendAuthorizedAsync(HttpMethod.Get, "/sites", accessToken);
        var sites = await listResponse.Content.ReadFromJsonAsync<SiteSummaryResponse[]>();
        Assert.NotNull(sites);
        Assert.Single(sites!);
        Assert.Equal("First", sites[0].Description);
        Assert.Equal("Retail", sites[0].Category);
        Assert.Contains("brand", sites[0].Tags);
    }

    [Fact]
    public async Task UpdateSiteProfile_UpdatesNameDomainAndMetadata()
    {
        var accessToken = await RegisterUserAsync();
        var createResponse = await SendAuthorizedAsync(HttpMethod.Post, "/sites", accessToken,
            JsonContent.Create(new CreateSiteRequest($"update-{Guid.NewGuid():N}.intentify.local", Name: "Original")));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateSiteResponse>();

        var updateResponse = await SendAuthorizedAsync(HttpMethod.Put, $"/sites/{created!.SiteId}/profile", accessToken,
            JsonContent.Create(new UpdateSiteProfileRequest("Updated Name", "LOCALHOST", "New description", "SaaS", new[] { " growth ", "Growth" })));

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<SiteSummaryResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated!.Name);
        Assert.Equal("localhost", updated.Domain);
        Assert.Equal("New description", updated.Description);
        Assert.Equal("SaaS", updated.Category);
        Assert.Single(updated.Tags);
        Assert.Equal("growth", updated.Tags[0], ignoreCase: true);
    }

    [Fact]
    public async Task UpdateSiteProfile_InvalidDomain_ReturnsBadRequest()
    {
        var accessToken = await RegisterUserAsync();
        var createResponse = await SendAuthorizedAsync(HttpMethod.Post, "/sites", accessToken,
            JsonContent.Create(new CreateSiteRequest($"invalid-domain-{Guid.NewGuid():N}.intentify.local", Name: "Site")));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateSiteResponse>();

        var updateResponse = await SendAuthorizedAsync(HttpMethod.Put, $"/sites/{created!.SiteId}/profile", accessToken,
            JsonContent.Create(new UpdateSiteProfileRequest("Site", "not a valid host", null, null, null)));

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteSite_RemovesSite_AndCascadesKnowledgeSources()
    {
        var accessToken = await RegisterUserAsync();
        var createResponse = await SendAuthorizedAsync(HttpMethod.Post, "/sites", accessToken,
            JsonContent.Create(new CreateSiteRequest($"delete-{Guid.NewGuid():N}.intentify.local", Name: "Delete Me")));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateSiteResponse>();

        var sourceResponse = await SendAuthorizedAsync(HttpMethod.Post, "/knowledge/sources", accessToken,
            JsonContent.Create(new CreateKnowledgeSourceRequest(created!.SiteId, "text", "Doc", null, "hello")));
        Assert.Equal(HttpStatusCode.OK, sourceResponse.StatusCode);

        var deleteResponse = await SendAuthorizedAsync(HttpMethod.Delete, $"/sites/{created.SiteId}", accessToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var sitesResponse = await SendAuthorizedAsync(HttpMethod.Get, "/sites", accessToken);
        var sites = await sitesResponse.Content.ReadFromJsonAsync<SiteSummaryResponse[]>();
        Assert.NotNull(sites);
        Assert.Empty(sites!);

        var listSourcesResponse = await SendAuthorizedAsync(HttpMethod.Get, $"/knowledge/sources?siteId={created.SiteId}", accessToken);
        var sources = await listSourcesResponse.Content.ReadFromJsonAsync<KnowledgeSourceSummaryResponse[]>();
        Assert.NotNull(sources);
        Assert.Empty(sources!);
    }

    [Fact]
    public async Task DeleteSite_TenantOwnership_IsEnforced()
    {
        var ownerToken = await RegisterUserAsync();
        var otherToken = await RegisterUserAsync();

        var createResponse = await SendAuthorizedAsync(HttpMethod.Post, "/sites", ownerToken,
            JsonContent.Create(new CreateSiteRequest($"ownership-{Guid.NewGuid():N}.intentify.local", Name: "Owned")));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateSiteResponse>();

        var deleteResponse = await SendAuthorizedAsync(HttpMethod.Delete, $"/sites/{created!.SiteId}", otherToken);
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    private async Task<string> RegisterUserAsync()
    {
        var email = $"tester-{Guid.NewGuid():N}@intentify.local";
        var request = new RegisterRequest("Sites Tester", email, "password-123", "Default Org");

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

    private async Task<HttpResponseMessage> SendOriginStatusRequestAsync(string widgetKey, string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/sites/installation/status?widgetKey={widgetKey}");
        request.Headers.TryAddWithoutValidation("Origin", origin);

        return await _client!.SendAsync(request);
    }
}
