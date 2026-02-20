using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Intentify.AppHost;
using Intentify.Modules.Auth.Api;
using Intentify.Modules.Sites.Api;
using Intentify.Shared.AI;
using Intentify.Shared.Abstractions;
using Intentify.Shared.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Xunit;

namespace Intentify.Modules.Engage.Tests;

public sealed class EngageIntegrationTests : IAsyncLifetime
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
    public async Task AdminEndpoints_RequireAuth()
    {
        var response = await _client!.GetAsync($"/engage/conversations?siteId={Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Conversations_RequireSiteId_WhenAuthenticated()
    {
        var token = await RegisterUserAsync();

        var response = await SendAuthorizedAsync(HttpMethod.Get, "/engage/conversations", token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConversationMessages_ReturnNotFound_WhenSiteDoesNotMatchSession()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);
        var otherSite = await CreateSiteAsync(token);

        var sendResponse = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "hello"
        });

        using var sendJson = JsonDocument.Parse(await sendResponse.Content.ReadAsStringAsync());
        var sessionId = sendJson.RootElement.GetProperty("sessionId").GetString();

        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/engage/conversations/{sessionId}/messages?siteId={otherSite.SiteId}",
            token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WidgetScript_ReturnsJavascript()
    {
        var response = await _client!.GetAsync("/engage/widget.js");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/javascript", response.Content.Headers.ContentType?.MediaType);

        var script = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-widget-key", script);
        Assert.Contains("/engage/chat/send?widgetKey=", script);
    }

    [Fact]
    public async Task ChatSend_WithKnowledge_WhenAiNotConfigured_ReturnsFallback_AndCreatesTicket()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        await AddKnowledgeAsync(token, site.SiteId, "Return policy is 30 days with original receipt.");

        var response = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "what is your return policy?"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var sessionId = json.RootElement.GetProperty("sessionId").GetString();
        Assert.Equal("Thanks — we’ll get back to you shortly.", json.RootElement.GetProperty("response").GetString());
        Assert.True(json.RootElement.GetProperty("ticketCreated").GetBoolean());

        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var handoffTickets = database.GetCollection<BsonEngageTicket>("EngageHandoffTickets");
        var handoffCount = await handoffTickets.CountDocumentsAsync(item => item.SessionId == Guid.Parse(sessionId!));
        Assert.Equal(1, handoffCount);

        var tickets = database.GetCollection<BsonTicket>("tickets");
        var createdTicket = await tickets.Find(item => item.EngageSessionId == Guid.Parse(sessionId!)).FirstOrDefaultAsync();
        Assert.NotNull(createdTicket);
        Assert.Equal(site.SiteId, createdTicket!.SiteId.ToString());
        Assert.Equal("Engage handoff: AiUnavailable", createdTicket.Subject);
        Assert.Equal("what is your return policy?", createdTicket.Description);
    }

    [Fact]
    public async Task ChatSend_WithKnowledge_AndAiAvailable_ReturnsGroundedAnswer()
    {
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
            services.RemoveAll<IChatCompletionClient>();
            services.AddSingleton<IChatCompletionClient>(new FakeChatCompletionClient("Return policy is 30 days with original receipt."));
        });

        await using var app = AppHostApplication.Build(builder);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var token = await RegisterUserAsync(client);
        var site = await CreateSiteAsync(client, token);
        await AddKnowledgeAsync(client, token, site.SiteId, "Return policy is 30 days with original receipt.");

        var response = await client.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "what is your return policy?"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var sessionId = Guid.Parse(json.RootElement.GetProperty("sessionId").GetString()!);
        Assert.Equal("Return policy is 30 days with original receipt.", json.RootElement.GetProperty("response").GetString());
        Assert.False(json.RootElement.GetProperty("ticketCreated").GetBoolean());
        Assert.True(json.RootElement.GetProperty("confidence").GetDecimal() >= 0.5m);
        Assert.True(json.RootElement.GetProperty("sources").GetArrayLength() > 0);

        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var handoffTickets = database.GetCollection<BsonEngageTicket>("EngageHandoffTickets");
        var handoffCount = await handoffTickets.CountDocumentsAsync(item => item.SessionId == sessionId);
        Assert.Equal(0, handoffCount);

        var tickets = database.GetCollection<BsonTicket>("tickets");
        var createdTicket = await tickets.Find(item => item.EngageSessionId == sessionId).FirstOrDefaultAsync();
        Assert.Null(createdTicket);
    }

    [Fact]
    public async Task ChatSend_WithoutKnowledge_CreatesLowConfidenceTicket()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var response = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "can you help me"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var sessionId = json.RootElement.GetProperty("sessionId").GetString();
        Assert.Equal("Thanks — we’ll get back to you shortly.", json.RootElement.GetProperty("response").GetString());
        Assert.True(json.RootElement.GetProperty("ticketCreated").GetBoolean());

        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var handoffTickets = database.GetCollection<BsonEngageTicket>("EngageHandoffTickets");
        var handoffCount = await handoffTickets.CountDocumentsAsync(item => item.SessionId == Guid.Parse(sessionId!));
        Assert.Equal(1, handoffCount);

        var tickets = database.GetCollection<BsonTicket>("tickets");
        var createdTicket = await tickets.Find(item => item.EngageSessionId == Guid.Parse(sessionId!)).FirstOrDefaultAsync();
        Assert.NotNull(createdTicket);
        Assert.Equal(site.SiteId, createdTicket!.SiteId.ToString());
        Assert.Equal("Open", createdTicket.Status);
        Assert.Equal("Engage handoff: LowConfidence", createdTicket.Subject);
        Assert.Equal("can you help me", createdTicket.Description);
    }

    [Fact]
    public async Task ChatSend_AcceptsWidgetKeyFromQuery_AndRequiresWidgetKeyWhenMissingFromBoth()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var withQueryResponse = await _client!.PostAsJsonAsync($"/engage/chat/send?widgetKey={Uri.EscapeDataString(site.WidgetKey)}", new
        {
            message = "hello from query"
        });

        Assert.Equal(HttpStatusCode.OK, withQueryResponse.StatusCode);

        var withoutWidgetKeyResponse = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            message = "hello without key"
        });

        Assert.Equal(HttpStatusCode.BadRequest, withoutWidgetKeyResponse.StatusCode);
    }

    private async Task AddKnowledgeAsync(string token, string siteId, string text)
    {
        var createResponse = await SendAuthorizedAsync(HttpMethod.Post, "/knowledge/sources", token, JsonContent.Create(new
        {
            siteId,
            type = "Text",
            name = "faq",
            text
        }));

        using var createdJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var sourceId = createdJson.RootElement.GetProperty("sourceId").GetString();
        await SendAuthorizedAsync(HttpMethod.Post, $"/knowledge/sources/{sourceId}/index", token);
    }

    private static async Task AddKnowledgeAsync(HttpClient client, string token, string siteId, string text)
    {
        var createResponse = await SendAuthorizedAsync(client, HttpMethod.Post, "/knowledge/sources", token, JsonContent.Create(new
        {
            siteId,
            type = "Text",
            name = "faq",
            text
        }));

        using var createdJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var sourceId = createdJson.RootElement.GetProperty("sourceId").GetString();
        await SendAuthorizedAsync(client, HttpMethod.Post, $"/knowledge/sources/{sourceId}/index", token);
    }

    private async Task<CreateSiteResponse> CreateSiteAsync(string accessToken)
    {
        var domain = $"engage-{Guid.NewGuid():N}.intentify.local";
        var response = await SendAuthorizedAsync(HttpMethod.Post, "/sites", accessToken, JsonContent.Create(new CreateSiteRequest(domain)));
        var payload = await response.Content.ReadFromJsonAsync<CreateSiteResponse>();
        return payload!;
    }

    private async Task<string> RegisterUserAsync()
    {
        var email = $"engage-{Guid.NewGuid():N}@intentify.local";
        var response = await _client!.PostAsJsonAsync("/auth/register", new RegisterRequest("Engage Tester", email, "password-123"));
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
        var domain = $"engage-{Guid.NewGuid():N}.intentify.local";
        var response = await SendAuthorizedAsync(client, HttpMethod.Post, "/sites", accessToken, JsonContent.Create(new CreateSiteRequest(domain)));
        var payload = await response.Content.ReadFromJsonAsync<CreateSiteResponse>();
        return payload!;
    }

    private static async Task<string> RegisterUserAsync(HttpClient client)
    {
        var email = $"engage-{Guid.NewGuid():N}@intentify.local";
        var response = await client.PostAsJsonAsync("/auth/register", new RegisterRequest("Engage Tester", email, "password-123"));
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

    private sealed class BsonEngageTicket
    {
        public Guid SessionId { get; init; }
    }

    private sealed class BsonTicket
    {
        public Guid SiteId { get; init; }
        public Guid EngageSessionId { get; init; }
        public string Subject { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
    }

    private sealed class FakeChatCompletionClient : IChatCompletionClient
    {
        private readonly string _response;

        public FakeChatCompletionClient(string response)
        {
            _response = response;
        }

        public Task<Result<string>> CompleteAsync(string prompt, CancellationToken ct)
        {
            return Task.FromResult(Result<string>.Success(_response));
        }
    }
}
