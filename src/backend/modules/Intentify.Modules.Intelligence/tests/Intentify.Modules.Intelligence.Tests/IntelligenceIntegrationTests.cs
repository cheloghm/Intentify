using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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

    [Fact]
    public async Task Dashboard_ReturnsSummarizedData_ForValidInputs()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var refreshResponse = await SendAuthorizedAsync(HttpMethod.Post, "/intelligence/refresh", token, JsonContent.Create(new
        {
            siteId = site.SiteId,
            category = "Marketing",
            location = "US",
            timeWindow = "7d",
            limit = 3
        }));

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var dashboardResponse = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/intelligence/dashboard?siteId={site.SiteId}&category=Marketing&location=US&timeWindow=7d&audienceType=B2B&limit=2",
            token);

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        var payload = await dashboardResponse.Content.ReadFromJsonAsync<IntelligenceDashboardResponse>();
        Assert.NotNull(payload);
        Assert.Equal("Marketing", payload.Category);
        Assert.Equal("US", payload.Location);
        Assert.Equal("7d", payload.TimeWindow);
        Assert.Equal("B2B", payload.AudienceType);
        Assert.Equal("Google", payload.Provider);
        Assert.Equal(2, payload.TopItems.Count);
        Assert.Equal(2, payload.Summary.MatchingItemsCount);
    }

    [Fact]
    public async Task Dashboard_KeywordAndProviderFilters_AreApplied()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var refreshResponse = await SendAuthorizedAsync(HttpMethod.Post, "/intelligence/refresh", token, JsonContent.Create(new
        {
            siteId = site.SiteId,
            category = "Marketing",
            location = "US",
            timeWindow = "7d",
            limit = 5
        }));

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var filteredResponse = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/intelligence/dashboard?siteId={site.SiteId}&category=Marketing&location=US&timeWindow=7d&provider=Google&keyword=lead",
            token);

        Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);
        var filteredPayload = await filteredResponse.Content.ReadFromJsonAsync<IntelligenceDashboardResponse>();
        Assert.NotNull(filteredPayload);
        Assert.Equal(1, filteredPayload.TotalItems);
        Assert.Single(filteredPayload.TopItems);
        Assert.Equal("lead generation", filteredPayload.TopItems[0].QueryOrTopic);

        var providerMissResponse = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/intelligence/dashboard?siteId={site.SiteId}&category=Marketing&location=US&timeWindow=7d&provider=Bing",
            token);

        Assert.Equal(HttpStatusCode.OK, providerMissResponse.StatusCode);
        var providerMissPayload = await providerMissResponse.Content.ReadFromJsonAsync<IntelligenceDashboardResponse>();
        Assert.NotNull(providerMissPayload);
        Assert.Equal(0, providerMissPayload.TotalItems);
        Assert.Empty(providerMissPayload.TopItems);
    }

    [Fact]
    public async Task Dashboard_InvalidLimitOrAudienceType_ReturnsBadRequest()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/intelligence/dashboard?siteId={site.SiteId}&category=Marketing&location=US&timeWindow=7d&audienceType=Other&limit=0",
            token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    [Fact]
    public async Task UpsertProfile_ThenGetProfile_ReturnsStoredProfile()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var upsertResponse = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/intelligence/profiles/{site.SiteId}",
            token,
            JsonContent.Create(new
            {
                profileName = "Intentify Retail",
                industryCategory = "Retail",
                primaryAudienceType = "B2C",
                targetLocations = new[] { "US", "CA" },
                primaryProductsOrServices = new[] { "Loyalty program", "Email campaigns" },
                watchTopics = new[] { "holiday offers" },
                seasonalPriorities = new[] { "Q4" },
                isActive = true,
                refreshIntervalMinutes = 120
            }));

        Assert.Equal(HttpStatusCode.OK, upsertResponse.StatusCode);
        var upsertPayload = await upsertResponse.Content.ReadFromJsonAsync<IntelligenceProfileResponse>();
        Assert.NotNull(upsertPayload);
        Assert.Equal(Guid.Parse(site.SiteId), upsertPayload.SiteId);
        Assert.Equal("B2C", upsertPayload.PrimaryAudienceType);
        Assert.Equal(2, upsertPayload.TargetLocations.Count);

        var getResponse = await SendAuthorizedAsync(HttpMethod.Get, $"/intelligence/profiles/{site.SiteId}", token);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var getPayload = await getResponse.Content.ReadFromJsonAsync<IntelligenceProfileResponse>();
        Assert.NotNull(getPayload);
        Assert.Equal("Intentify Retail", getPayload.ProfileName);
        Assert.Equal("Retail", getPayload.IndustryCategory);
    }

    [Fact]
    public async Task UpsertProfile_MissingRequiredFields_ReturnsBadRequest()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var response = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/intelligence/profiles/{site.SiteId}",
            token,
            JsonContent.Create(new
            {
                profileName = "",
                industryCategory = "",
                primaryAudienceType = "",
                targetLocations = Array.Empty<string>(),
                primaryProductsOrServices = Array.Empty<string>(),
                isActive = true
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_IsTenantIsolated()
    {
        var tokenA = await RegisterUserAsync();
        var tokenB = await RegisterUserAsync();
        var siteA = await CreateSiteAsync(tokenA);

        var upsertResponse = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/intelligence/profiles/{siteA.SiteId}",
            tokenA,
            JsonContent.Create(new
            {
                profileName = "Tenant A",
                industryCategory = "SaaS",
                primaryAudienceType = "B2B",
                targetLocations = new[] { "US" },
                primaryProductsOrServices = new[] { "Platform" },
                isActive = true
            }));

        Assert.Equal(HttpStatusCode.OK, upsertResponse.StatusCode);

        var response = await SendAuthorizedAsync(HttpMethod.Get, $"/intelligence/profiles/{siteA.SiteId}", tokenB);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpsertProfile_SecondWrite_UpdatesSameTenantSiteIdentity()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var first = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/intelligence/profiles/{site.SiteId}",
            token,
            JsonContent.Create(new
            {
                profileName = "Original",
                industryCategory = "Healthcare",
                primaryAudienceType = "B2C",
                targetLocations = new[] { "US" },
                primaryProductsOrServices = new[] { "Consulting" },
                isActive = true,
                refreshIntervalMinutes = 60
            }));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstPayload = await first.Content.ReadFromJsonAsync<IntelligenceProfileResponse>();
        Assert.NotNull(firstPayload);

        var second = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/intelligence/profiles/{site.SiteId}",
            token,
            JsonContent.Create(new
            {
                profileName = "Updated",
                industryCategory = "Healthcare",
                primaryAudienceType = "B2B",
                targetLocations = new[] { "US", "UK" },
                primaryProductsOrServices = new[] { "Consulting", "Audits" },
                watchTopics = new[] { "compliance" },
                seasonalPriorities = new[] { "Budget season" },
                isActive = false,
                refreshIntervalMinutes = 180
            }));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondPayload = await second.Content.ReadFromJsonAsync<IntelligenceProfileResponse>();
        Assert.NotNull(secondPayload);
        Assert.Equal(Guid.Parse(site.SiteId), secondPayload.SiteId);
        Assert.Equal("Updated", secondPayload.ProfileName);
        Assert.Equal("B2B", secondPayload.PrimaryAudienceType);
        Assert.Equal(firstPayload.SiteId, secondPayload.SiteId);
        Assert.True(secondPayload.UpdatedAtUtc >= firstPayload.UpdatedAtUtc);
    }

    [Fact]
    public async Task Dashboard_NoData_ReturnsEmptyResult()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/intelligence/dashboard?siteId={site.SiteId}&category=Unknown&location=US&timeWindow=7d",
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<IntelligenceDashboardResponse>();
        Assert.NotNull(payload);
        Assert.Equal(0, payload.TotalItems);
        Assert.Empty(payload.TopItems);
        Assert.Equal(0, payload.Summary.MatchingItemsCount);
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
