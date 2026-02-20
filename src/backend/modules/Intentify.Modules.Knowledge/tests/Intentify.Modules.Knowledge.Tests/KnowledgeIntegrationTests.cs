using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Intentify.AppHost;
using Intentify.Modules.Auth.Api;
using Intentify.Modules.Sites.Api;
using Intentify.Shared.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Intentify.Modules.Knowledge.Tests;

public sealed class KnowledgeIntegrationTests : IAsyncLifetime
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
    public async Task Source_CanBeIndexed_AndRetrieved()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var createResponse = await SendAuthorizedAsync(HttpMethod.Post, "/knowledge/sources", token, JsonContent.Create(new
        {
            siteId = site.SiteId,
            type = "Text",
            name = "test",
            text = "alpha beta alpha gamma"
        }));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        using var createdJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var sourceId = createdJson.RootElement.GetProperty("sourceId").GetString();

        var indexResponse = await SendAuthorizedAsync(HttpMethod.Post, $"/knowledge/sources/{sourceId}/index", token);
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);

        var retrieveResponse = await SendAuthorizedAsync(HttpMethod.Get, $"/knowledge/retrieve?siteId={site.SiteId}&query=alpha&top=5", token);
        Assert.Equal(HttpStatusCode.OK, retrieveResponse.StatusCode);
        using var retrieveJson = JsonDocument.Parse(await retrieveResponse.Content.ReadAsStringAsync());
        Assert.True(retrieveJson.RootElement.GetArrayLength() >= 1);

        var listResponse = await SendAuthorizedAsync(HttpMethod.Get, $"/knowledge/sources?siteId={site.SiteId}", token);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        Assert.Equal("Indexed", listJson.RootElement[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Retrieve_UsesOpenSearch_WhenEnabled()
    {
        await using var openSearchServer = await FakeOpenSearchServer.StartAsync();

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
            ["Intentify:OpenSearch:Enabled"] = "true",
            ["Intentify:OpenSearch:Url"] = openSearchServer.BaseAddress,
            ["Intentify:OpenSearch:IndexName"] = "intentify-test-chunks"
        });

        await using var app = AppHostApplication.Build(builder);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var token = await RegisterUserAsync(client);
        var site = await CreateSiteAsync(client, token);

        var retrieveResponse = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            $"/knowledge/retrieve?siteId={site.SiteId}&query=shipping%20policy&top=2",
            token);

        Assert.Equal(HttpStatusCode.OK, retrieveResponse.StatusCode);

        using var retrieveJson = JsonDocument.Parse(await retrieveResponse.Content.ReadAsStringAsync());
        Assert.Single(retrieveJson.RootElement.EnumerateArray());
        Assert.Equal("OpenSearch shipping policy answer", retrieveJson.RootElement[0].GetProperty("content").GetString());

        var searchRequest = Assert.Single(openSearchServer.SearchRequests);
        Assert.Equal("/intentify-test-chunks/_search", searchRequest.Path);
        using var searchBody = JsonDocument.Parse(searchRequest.BodyJson);
        Assert.Equal("shipping policy", searchBody.RootElement.GetProperty("query").GetProperty("bool").GetProperty("should")[0].GetProperty("match").GetProperty("content").GetString());
        Assert.Equal(2, searchBody.RootElement.GetProperty("size").GetInt32());
    }

    private async Task<CreateSiteResponse> CreateSiteAsync(string accessToken)
    {
        var domain = $"knowledge-{Guid.NewGuid():N}.intentify.local";
        var response = await SendAuthorizedAsync(HttpMethod.Post, "/sites", accessToken, JsonContent.Create(new CreateSiteRequest(domain)));
        var payload = await response.Content.ReadFromJsonAsync<CreateSiteResponse>();
        return payload!;
    }

    private async Task<string> RegisterUserAsync()
    {
        var email = $"knowledge-{Guid.NewGuid():N}@intentify.local";
        var response = await _client!.PostAsJsonAsync("/auth/register", new RegisterRequest("Knowledge Tester", email, "password-123"));
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return payload!.AccessToken;
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string url, string accessToken, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = content;

        return await _client!.SendAsync(request);
    }

    private static async Task<CreateSiteResponse> CreateSiteAsync(HttpClient client, string accessToken)
    {
        var domain = $"knowledge-{Guid.NewGuid():N}.intentify.local";
        var response = await SendAuthorizedAsync(client, HttpMethod.Post, "/sites", accessToken, JsonContent.Create(new CreateSiteRequest(domain)));
        var payload = await response.Content.ReadFromJsonAsync<CreateSiteResponse>();
        return payload!;
    }

    private static async Task<string> RegisterUserAsync(HttpClient client)
    {
        var email = $"knowledge-{Guid.NewGuid():N}@intentify.local";
        var response = await client.PostAsJsonAsync("/auth/register", new RegisterRequest("Knowledge Tester", email, "password-123"));
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return payload!.AccessToken;
    }

    private static async Task<HttpResponseMessage> SendAuthorizedAsync(HttpClient client, HttpMethod method, string url, string accessToken, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = content;

        return await client.SendAsync(request);
    }

    private sealed class FakeOpenSearchServer : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _backgroundTask;
        private readonly Guid _sourceId = Guid.NewGuid();
        private readonly Guid _chunkId = Guid.NewGuid();

        private FakeOpenSearchServer(HttpListener listener, string baseAddress)
        {
            _listener = listener;
            BaseAddress = baseAddress;
            _backgroundTask = Task.Run(() => ProcessLoopAsync(_cts.Token));
        }

        public string BaseAddress { get; }

        public List<SearchRequest> SearchRequests { get; } = [];

        public static Task<FakeOpenSearchServer> StartAsync()
        {
            var listener = new HttpListener();
            var prefix = $"http://127.0.0.1:{GetFreePort()}/";
            listener.Prefixes.Add(prefix);
            listener.Start();

            return Task.FromResult(new FakeOpenSearchServer(listener, prefix.TrimEnd('/')));
        }

        private async Task ProcessLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                await HandleRequestAsync(context);
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            if (request.HttpMethod == HttpMethod.Head.Method)
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                response.Close();
                return;
            }

            if (request.HttpMethod == HttpMethod.Post.Method && request.Url?.AbsolutePath.EndsWith("/_search", StringComparison.Ordinal) == true)
            {
                using var reader = new StreamReader(request.InputStream);
                var bodyJson = await reader.ReadToEndAsync();
                SearchRequests.Add(new SearchRequest(request.Url.AbsolutePath, bodyJson));

                var payload = JsonContent.Create(new
                {
                    hits = new
                    {
                        hits = new object[]
                        {
                            new
                            {
                                _source = new
                                {
                                    sourceId = _sourceId,
                                    chunkId = _chunkId,
                                    chunkIndex = 1,
                                    content = "OpenSearch shipping policy answer"
                                }
                            }
                        }
                    }
                });

                response.StatusCode = (int)HttpStatusCode.OK;
                await payload.CopyToAsync(response.OutputStream);
                response.Close();
                return;
            }

            response.StatusCode = (int)HttpStatusCode.OK;
            response.Close();
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _listener.Close();
            await _backgroundTask;
            _cts.Dispose();
        }

        private static int GetFreePort()
        {
            using var socket = new TcpListener(IPAddress.Loopback, 0);
            socket.Start();
            return ((IPEndPoint)socket.LocalEndpoint).Port;
        }
    }

    private sealed record SearchRequest(string Path, string BodyJson);
}
