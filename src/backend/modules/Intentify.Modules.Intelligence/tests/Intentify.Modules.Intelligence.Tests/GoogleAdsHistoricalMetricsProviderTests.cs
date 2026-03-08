using System.Net;
using System.Text;
using System.Text.Json;
using Intentify.Modules.Intelligence.Application;
using Intentify.Modules.Intelligence.Domain;
using Intentify.Modules.Intelligence.Infrastructure;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Intelligence.Tests;

public sealed class GoogleAdsHistoricalMetricsProviderTests
{
    [Fact]
    public async Task SearchAsync_ReturnsValidationFailed_WhenConfigMissing()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var provider = new GoogleAdsHistoricalMetricsProvider(
            httpClient,
            new GoogleAdsOptions { BaseUrl = "https://example.test" },
            new FakeProfileRepository());

        var result = await provider.SearchAsync(Guid.NewGuid().ToString("D"), Guid.NewGuid(), new("marketing", "US", "7d", 10), CancellationToken.None);

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
        Assert.NotNull(result.Errors);
    }

    [Fact]
    public async Task SearchAsync_UsesProfileKeywords_AndMapsHistoricalMetrics()
    {
        HttpRequestMessage? captured = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "provider":"GoogleAds",
                      "retrievedAtUtc":"2026-02-01T00:00:00Z",
                      "items":[
                        {"keyword":"audits","avgMonthlySearches":3200},
                        {"keyword":"consulting","avgMonthlySearches":1200}
                      ]
                    }
                    """, Encoding.UTF8, "application/json")
            };
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var siteId = Guid.NewGuid();
        var provider = new GoogleAdsHistoricalMetricsProvider(
            httpClient,
            new GoogleAdsOptions
            {
                BaseUrl = "https://example.test",
                DeveloperToken = "dev-token",
                ClientId = "client-id",
                ClientSecret = "client-secret"
            },
            new FakeProfileRepository(new IntelligenceProfile
            {
                TenantId = Guid.NewGuid(),
                SiteId = siteId,
                ProfileName = "Acme",
                IndustryCategory = "B2B Services",
                PrimaryAudienceType = "B2B",
                TargetLocations = ["US"],
                PrimaryProductsOrServices = ["consulting", "audits"],
                WatchTopics = ["audits"],
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            }));

        var result = await provider.SearchAsync(Guid.NewGuid().ToString("D"), siteId, new("services", "US", "90d", 10), CancellationToken.None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.NotNull(result.Value);
        Assert.Equal("GoogleAds", result.Value.Provider);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Equal("audits", result.Value.Items[0].QueryOrTopic);
        Assert.Equal(3200, result.Value.Items[0].Score);

        Assert.NotNull(captured);
        var payload = await captured!.Content!.ReadAsStringAsync();
        Assert.Contains("consulting", payload);
        Assert.Contains("audits", payload);
        Assert.Contains("\"location\":\"US\"", payload);
    }

    [Fact]
    public async Task SearchAsync_ReturnsValidationFailed_OnProviderFailure()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var provider = new GoogleAdsHistoricalMetricsProvider(
            httpClient,
            new GoogleAdsOptions
            {
                BaseUrl = "https://example.test",
                DeveloperToken = "dev-token",
                ClientId = "client-id",
                ClientSecret = "client-secret"
            },
            new FakeProfileRepository());

        var result = await provider.SearchAsync(Guid.NewGuid().ToString("D"), Guid.NewGuid(), new("marketing", "US", "7d", 10), CancellationToken.None);

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }

    private sealed class FakeProfileRepository(IntelligenceProfile? profile = null) : IIntelligenceProfileRepository
    {
        public Task UpsertAsync(IntelligenceProfile profile, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IntelligenceProfile?> GetAsync(string tenantId, Guid siteId, CancellationToken ct = default)
            => Task.FromResult(profile is not null && profile.SiteId == siteId ? profile : null);
    }
}
