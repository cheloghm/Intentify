using System.Net;
using System.Text;
using Intentify.Modules.Intelligence.Infrastructure;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Intelligence.Tests;

public sealed class GoogleTrendsProviderTests
{
    [Fact]
    public async Task SearchAsync_WhenDisabled_ReturnsSuccessWithEmptyItems()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var provider = new GoogleTrendsProvider(httpClient, new GoogleTrendsOptions
        {
            Enabled = false,
        });

        var result = await provider.SearchAsync(Guid.NewGuid().ToString("D"), Guid.NewGuid(), new("marketing", "US", "7d", 10), CancellationToken.None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value.Items);
        Assert.Equal("GoogleTrends", result.Value.Provider);
    }

    [Fact]
    public async Task SearchAsync_WhenEnabledAndConfigured_MapsItems()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "provider":"GoogleTrends",
                  "retrievedAtUtc":"2026-03-01T00:00:00Z",
                  "items":[
                    {"queryOrTopic":"ai crm","interest":82},
                    {"queryOrTopic":"sales automation","score":74}
                  ]
                }
                """, Encoding.UTF8, "application/json")
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var provider = new GoogleTrendsProvider(httpClient, new GoogleTrendsOptions
        {
            Enabled = true,
            BaseUrl = "https://example.test",
            ApiKey = "test-key",
        });

        var result = await provider.SearchAsync(Guid.NewGuid().ToString("D"), Guid.NewGuid(), new("marketing", "US", "7d", 10), CancellationToken.None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Equal("ai crm", result.Value.Items[0].QueryOrTopic);
        Assert.Equal(82, result.Value.Items[0].Score);
    }

    [Fact]
    public async Task SearchAsync_WhenEnabledMissingConfig_ReturnsValidationFailed()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var provider = new GoogleTrendsProvider(httpClient, new GoogleTrendsOptions
        {
            Enabled = true,
            BaseUrl = "",
            ApiKey = null,
        });

        var result = await provider.SearchAsync(Guid.NewGuid().ToString("D"), Guid.NewGuid(), new("marketing", "US", "7d", 10), CancellationToken.None);

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
        Assert.NotNull(result.Errors);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }
}
