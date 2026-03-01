using System.Net;
using System.Text;
using Intentify.Modules.Intelligence.Infrastructure;
using Intentify.Shared.Validation;

namespace Intentify.Modules.Intelligence.Tests;

public sealed class GoogleSearchProviderTests
{
    [Fact]
    public async Task SearchAsync_ParsesSuccessfulResponse()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "provider":"Google",
                  "retrievedAtUtc":"2026-01-01T00:00:00Z",
                  "items":[
                    {"queryOrTopic":"intent data","score":95.2,"rank":1},
                    {"queryOrTopic":"lead scoring","score":84.0}
                  ]
                }
                """, Encoding.UTF8, "application/json")
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var provider = new GoogleSearchProvider(httpClient, new GoogleSearchOptions
        {
            BaseUrl = "https://example.test",
            ApiKey = "test-key"
        });

        var result = await provider.SearchAsync(Guid.NewGuid().ToString("D"), Guid.NewGuid(), new("marketing", "us", "7d", 10), CancellationToken.None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.NotNull(result.Value);
        Assert.Equal("Google", result.Value.Provider);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Equal("intent data", result.Value.Items[0].QueryOrTopic);
    }

    [Fact]
    public async Task SearchAsync_ReturnsValidationFailed_OnNon200()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var provider = new GoogleSearchProvider(httpClient, new GoogleSearchOptions { BaseUrl = "https://example.test", ApiKey = "test-key" });

        var result = await provider.SearchAsync(Guid.NewGuid().ToString("D"), Guid.NewGuid(), new("marketing", "us", "7d", 10), CancellationToken.None);

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
        Assert.NotNull(result.Errors);
    }

    [Fact]
    public async Task SearchAsync_ReturnsValidationFailed_OnInvalidJson()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json", Encoding.UTF8, "application/json")
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var provider = new GoogleSearchProvider(httpClient, new GoogleSearchOptions { BaseUrl = "https://example.test", ApiKey = "test-key" });

        var result = await provider.SearchAsync(Guid.NewGuid().ToString("D"), Guid.NewGuid(), new("marketing", "us", "7d", 10), CancellationToken.None);

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }
}
