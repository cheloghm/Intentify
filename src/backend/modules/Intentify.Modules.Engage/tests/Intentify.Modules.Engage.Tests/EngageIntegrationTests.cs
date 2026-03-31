using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
using MongoDB.Bson;
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
    public async Task Conversations_CanFilterByCollectorSessionId()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "first",
            collectorSessionId = "collector-one"
        });

        await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "second",
            collectorSessionId = "collector-two"
        });

        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/engage/conversations?siteId={site.SiteId}&collectorSessionId=collector-one",
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        Assert.Single(json.RootElement.EnumerateArray());
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
    public async Task WidgetConversationMessages_ReturnsTranscript_ForMatchingWidgetAndActiveSession()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var sendResponse = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "hello"
        });

        using var sendJson = JsonDocument.Parse(await sendResponse.Content.ReadAsStringAsync());
        var sessionId = sendJson.RootElement.GetProperty("sessionId").GetString();

        var response = await _client.GetAsync($"/engage/widget/conversations/{sessionId}/messages?widgetKey={Uri.EscapeDataString(site.WidgetKey)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        Assert.True(json.RootElement.GetArrayLength() >= 2);
    }

    [Fact]
    public async Task WidgetConversationMessages_ReturnsNotFound_ForMismatchedWidgetOrExpiredSession()
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
        Assert.False(string.IsNullOrWhiteSpace(sessionId));

        var wrongWidgetResponse = await _client.GetAsync($"/engage/widget/conversations/{sessionId}/messages?widgetKey={Uri.EscapeDataString(otherSite.WidgetKey)}");
        Assert.Equal(HttpStatusCode.NotFound, wrongWidgetResponse.StatusCode);

        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var sessions = database.GetCollection<BsonEngageSession>("EngageChatSessions");
        var expireUpdate = Builders<BsonEngageSession>.Update.Set(item => item.UpdatedAtUtc, DateTime.UtcNow.AddMinutes(-31));
        await sessions.UpdateOneAsync(item => item.Id == Guid.Parse(sessionId!), expireUpdate);

        var expiredResponse = await _client.GetAsync($"/engage/widget/conversations/{sessionId}/messages?widgetKey={Uri.EscapeDataString(site.WidgetKey)}");
        Assert.Equal(HttpStatusCode.NotFound, expiredResponse.StatusCode);
    }

    [Fact]
    public async Task BotEndpoints_CanReadAndUpdateName()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var getResponse = await SendAuthorizedAsync(HttpMethod.Get, $"/engage/bot?siteId={site.SiteId}", token);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var putResponse = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/engage/bot?siteId={site.SiteId}",
            token,
            JsonContent.Create(new { name = "Forti" }));

        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        using var putJson = JsonDocument.Parse(await putResponse.Content.ReadAsStringAsync());
        Assert.Equal("Forti", putJson.RootElement.GetProperty("name").GetString());

        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var bots = database.GetCollection<BsonEngageBot>("EngageBots");
        var persisted = await bots.Find(item => item.SiteId == Guid.Parse(site.SiteId)).FirstOrDefaultAsync();
        Assert.NotNull(persisted);
        Assert.Equal("Forti", persisted!.Name);
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
        Assert.Contains("/engage/widget/conversations/", script);
        Assert.Contains("responseKind === 'promo'", script);
        Assert.Contains("/promos/public/", script);
        Assert.Contains("/entries", script);
        Assert.Contains("ensureCollectorSessionId", script);
        Assert.Contains("hydrateRequestId", script);
        Assert.Contains("secondaryResponse", script);
        Assert.Contains("is typing…", script);
    }

    [Fact]
    public async Task ChatSend_FirstTurnDefaultsToGreeting_ForGeneralMessage()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var response = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "hello"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Hi! How can I help you today?", json.RootElement.GetProperty("response").GetString());
        Assert.False(json.RootElement.GetProperty("ticketCreated").GetBoolean());
    }

    [Fact]
    public async Task ChatSend_FirstTurnCommercialMessage_CurrentlyUsesGreeting()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var response = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "We are looking to remodel our office"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Hi! How can I help you today?", json.RootElement.GetProperty("response").GetString());
        Assert.False(json.RootElement.GetProperty("ticketCreated").GetBoolean());
    }

    [Fact]
    public async Task ChatSend_ExplicitSupportEscalation_UsesCurrentEscalationPrompt_WithoutTicketSideEffect()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var response = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "I need to speak to a human agent about a payment issue"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "Thanks — what’s the best way for our team to contact you (email or phone)?",
            json.RootElement.GetProperty("response").GetString());
        Assert.False(json.RootElement.GetProperty("ticketCreated").GetBoolean());
    }

    [Fact]
    public async Task ChatSend_SupportProblemSignal_UsesCurrentEscalationPrompt_WithoutTicketSideEffect()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var response = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "checkout not working"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "Thanks — what’s the best way for our team to contact you (email or phone)?",
            json.RootElement.GetProperty("response").GetString());
        Assert.False(json.RootElement.GetProperty("ticketCreated").GetBoolean());
    }

    [Fact]
    public async Task ChatSend_ActiveSupportCapture_ViaEmail_AdvancesPrompt()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var first = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "checkout not working"
        });

        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var sessionId = firstJson.RootElement.GetProperty("sessionId").GetString();

        var second = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            sessionId,
            message = "via email"
        });

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        using var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal(
            "Thanks — what’s the best email address to reach you?",
            secondJson.RootElement.GetProperty("response").GetString());
    }

    [Fact]
    public async Task ChatSend_ClearCloseSignal_ReturnsNaturalClose()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var response = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "thanks that's all"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "You’re all set — happy to help. If anything comes up, just message me.",
            json.RootElement.GetProperty("response").GetString());
    }

    [Fact]
    public async Task ChatSend_AfterGreeting_CommercialFollowUp_UsesCurrentDiscoveryQuestion()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var first = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "hello"
        });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var sessionId = firstJson.RootElement.GetProperty("sessionId").GetString();

        var second = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            sessionId,
            message = "We are looking to remodel our office"
        });

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        using var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal("What kind of business is this for?", secondJson.RootElement.GetProperty("response").GetString());
        Assert.False(secondJson.RootElement.GetProperty("ticketCreated").GetBoolean());
    }

    [Fact]
    public async Task ChatSend_DiscoveryProgress_UsesCurrentPromptSequence()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var first = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "hello"
        });

        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var sessionId = firstJson.RootElement.GetProperty("sessionId").GetString();

        var second = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            sessionId,
            message = "We are looking to remodel our office"
        });

        using var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal("What kind of business is this for?", secondJson.RootElement.GetProperty("response").GetString());

        var third = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            sessionId,
            message = "renovation"
        });

        using var thirdJson = JsonDocument.Parse(await third.Content.ReadAsStringAsync());
        Assert.Equal("What location should we plan for?", thirdJson.RootElement.GetProperty("response").GetString());

        var fourth = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            sessionId,
            message = "Austin"
        });

        using var fourthJson = JsonDocument.Parse(await fourth.Content.ReadAsStringAsync());
        Assert.Equal("Any key constraints like budget or timeline?", fourthJson.RootElement.GetProperty("response").GetString());

        var fifth = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            sessionId,
            message = "budget is 20k"
        });

        using var fifthJson = JsonDocument.Parse(await fifth.Content.ReadAsStringAsync());
        Assert.Equal("Thanks — that gives me enough context. Please share your first name.", fifthJson.RootElement.GetProperty("response").GetString());

        var sixth = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            sessionId,
            message = "my name is Sam"
        });

        using var sixthJson = JsonDocument.Parse(await sixth.Content.ReadAsStringAsync());
        Assert.Equal("Great, and what’s the best contact method for follow-up?", sixthJson.RootElement.GetProperty("response").GetString());
    }

    [Fact]
    public async Task ChatSend_ContactIntent_OnFirstTurn_CurrentlyUsesGreeting()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var response = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "what is your contact number"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Hi! How can I help you today?", json.RootElement.GetProperty("response").GetString());
        Assert.False(json.RootElement.GetProperty("ticketCreated").GetBoolean());
    }

    [Fact]
    public async Task ChatSend_WithKnowledge_OnFirstTurn_CurrentlyUsesGreeting()
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
        Assert.Equal("Hi! How can I help you today?", json.RootElement.GetProperty("response").GetString());
        Assert.False(json.RootElement.GetProperty("ticketCreated").GetBoolean());
    }

    [Fact]
    public async Task ChatSend_RejectsWidgetKeyMismatchBetweenQueryAndBody()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var response = await _client!.PostAsJsonAsync($"/engage/chat/send?widgetKey={Uri.EscapeDataString(site.WidgetKey)}", new
        {
            widgetKey = "different-widget-key",
            message = "hello"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("widgetKey", out _));
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

    [Fact]
    public async Task ChatSend_PersistsCollectorSessionId_FromCookie_WhenRequestFieldMissing()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        const string collectorSessionId = "intentify_sid_cookie123";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/engage/chat/send");
        request.Headers.Add("Cookie", $"intentify_sid={collectorSessionId}");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                widgetKey = site.WidgetKey,
                message = "hello"
            }),
            Encoding.UTF8,
            "application/json");

        var response = await _client!.SendAsync(request);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var sessionId = Guid.Parse(json.RootElement.GetProperty("sessionId").GetString()!);

        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var sessions = database.GetCollection<BsonEngageSession>("EngageChatSessions");
        var session = await sessions.Find(item => item.Id == sessionId).FirstOrDefaultAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(session);
        Assert.Equal(collectorSessionId, session!.CollectorSessionId);
    }

    [Fact]
    public async Task ChatSend_PersistsCollectorSessionId_FromRequest()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var collectorSessionId = "intentify_sid_abc123";
        var response = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "hello",
            collectorSessionId
        });

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var sessionId = Guid.Parse(json.RootElement.GetProperty("sessionId").GetString()!);

        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var sessions = database.GetCollection<BsonEngageSession>("EngageChatSessions");
        var session = await sessions.Find(item => item.Id == sessionId).FirstOrDefaultAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(session);
        Assert.Equal(collectorSessionId, session!.CollectorSessionId);
    }

    [Fact]
    public async Task ChatSend_DoesNotOverwriteCollectorSessionId_WhenAlreadySet()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var firstResponse = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "first",
            collectorSessionId = "collector-a"
        });

        using var firstJson = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        var sessionId = firstJson.RootElement.GetProperty("sessionId").GetString();

        var secondResponse = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            sessionId,
            message = "second",
            collectorSessionId = "collector-b"
        });

        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var sessions = database.GetCollection<BsonEngageSession>("EngageChatSessions");
        var persisted = await sessions.Find(item => item.Id == Guid.Parse(sessionId!)).FirstOrDefaultAsync();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(persisted);
        Assert.Equal("collector-a", persisted!.CollectorSessionId);
    }

    [Fact]
    public async Task ChatSend_MissingSessionId_WithSameCollectorSessionId_CurrentlyCreatesNewSession()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        const string collectorSessionId = "collector-shared";

        var firstResponse = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "first",
            collectorSessionId
        });

        using var firstJson = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        var firstSessionId = firstJson.RootElement.GetProperty("sessionId").GetString();

        var secondResponse = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "second",
            collectorSessionId
        });

        using var secondJson = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
        var secondSessionId = secondJson.RootElement.GetProperty("sessionId").GetString();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotEqual(firstSessionId, secondSessionId);
    }

    [Fact]
    public async Task ChatSend_ReusesSession_WhenStillActive()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var firstResponse = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "first"
        });

        using var firstJson = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        var firstSessionId = firstJson.RootElement.GetProperty("sessionId").GetString();

        var secondResponse = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            sessionId = firstSessionId,
            message = "second"
        });

        using var secondJson = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
        var secondSessionId = secondJson.RootElement.GetProperty("sessionId").GetString();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(firstSessionId, secondSessionId);
    }

    [Fact]
    public async Task ChatSend_CreatesNewSession_WhenExistingSessionIsExpired()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var firstResponse = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "first"
        });

        using var firstJson = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        var firstSessionId = firstJson.RootElement.GetProperty("sessionId").GetString();

        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var sessions = database.GetCollection<BsonEngageSession>("EngageChatSessions");
        var firstSessionGuid = Guid.Parse(firstSessionId!);
        var update = Builders<BsonEngageSession>.Update.Set(item => item.UpdatedAtUtc, DateTime.UtcNow.AddMinutes(-31));
        await sessions.UpdateOneAsync(item => item.Id == firstSessionGuid, update);

        var secondResponse = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            sessionId = firstSessionId,
            message = "second"
        });

        using var secondJson = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
        var secondSessionId = secondJson.RootElement.GetProperty("sessionId").GetString();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotEqual(firstSessionId, secondSessionId);
    }

    [Fact]
    public async Task ChatSession_PersistsCollectorSessionId()
    {
        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var sessions = database.GetCollection<BsonEngageSession>("EngageChatSessions");

        var session = new BsonEngageSession
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            BotId = Guid.NewGuid(),
            WidgetKey = "widget-key",
            CollectorSessionId = "intentify-session-123",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await sessions.InsertOneAsync(session);

        var persisted = await sessions.Find(item => item.Id == session.Id).FirstOrDefaultAsync();

        Assert.NotNull(persisted);
        Assert.Equal(session.CollectorSessionId, persisted!.CollectorSessionId);
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
        var response = await _client!.PostAsJsonAsync("/auth/register", new RegisterRequest("Engage Tester", email, "password-123", "Default Org"));
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

    private sealed class BsonEngageSession
    {
        public Guid Id { get; init; }
        public Guid TenantId { get; init; }
        public Guid SiteId { get; init; }
        public Guid BotId { get; init; }
        public string WidgetKey { get; init; } = string.Empty;
        public string? CollectorSessionId { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }

    private sealed class BsonEngageBot
    {
        public Guid SiteId { get; init; }
        public string Name { get; init; } = string.Empty;
    }
}
