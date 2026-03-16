using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    }

    [Fact]
    public async Task ChatSend_WithKnowledge_WhenAiNotConfigured_UsesTicketFirstContactPrompt_ForRealQuestion()
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
        Assert.Equal("Thanks — I can connect you with our team. Please share your name and best email.", json.RootElement.GetProperty("response").GetString());
        Assert.True(json.RootElement.GetProperty("ticketCreated").GetBoolean());
        if (json.RootElement.TryGetProperty("stage7Decision", out var stage7Decision))
        {
            Assert.Equal(JsonValueKind.Null, stage7Decision.ValueKind);
        }

        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var handoffTickets = database.GetCollection<BsonEngageTicket>("EngageHandoffTickets");
        var handoffCount = await handoffTickets.CountDocumentsAsync(item => item.SessionId == Guid.Parse(sessionId!));
        Assert.Equal(1, handoffCount);

        var tickets = database.GetCollection<BsonTicket>("tickets");
        var createdTicket = await tickets.Find(item => item.EngageSessionId == Guid.Parse(sessionId!)).FirstOrDefaultAsync();
        Assert.NotNull(createdTicket);
        Assert.Equal(site.SiteId, createdTicket!.SiteId.ToString());
        Assert.Equal("Engage handoff: AiUnavailable", createdTicket.Subject);
        Assert.Contains("what is your return policy?", createdTicket.Description);
        Assert.Contains("[Engage handoff package]", createdTicket.Description);
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
        if (json.RootElement.TryGetProperty("stage7Decision", out var stage7Decision))
        {
            Assert.Equal(JsonValueKind.Null, stage7Decision.ValueKind);
        }

        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var handoffTickets = database.GetCollection<BsonEngageTicket>("EngageHandoffTickets");
        var handoffCount = await handoffTickets.CountDocumentsAsync(item => item.SessionId == sessionId);
        Assert.Equal(0, handoffCount);

        var tickets = database.GetCollection<BsonTicket>("tickets");
        var createdTicket = await tickets.Find(item => item.EngageSessionId == sessionId).FirstOrDefaultAsync();
        Assert.Null(createdTicket);
    }

    [Fact]
    public async Task ChatSend_WithKnowledge_AndStage7ValidDecision_IncludesRecommendations()
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
            services.AddSingleton<IChatCompletionClient>(new FakeChatCompletionClient(prompt =>
            {
                if (prompt.Contains("Stage 7 AI decision assistant.", StringComparison.Ordinal))
                {
                    return Result<string>.Success(
                        """
                        {
                          "schemaVersion": "stage7.v1",
                          "decisionId": "decision-1",
                          "overallConfidence": 0.8,
                          "recommendations": [
                            {
                              "type": "SuggestKnowledge",
                              "confidence": 0.8,
                              "rationale": "Knowledge chunk directly addresses the question.",
                              "evidenceRefs": [{ "source": "Knowledge", "referenceId": "chunk-1", "detail": "return policy" }],
                              "targetRefs": null,
                              "requiresApproval": false,
                              "proposedCommand": null
                            }
                          ],
                          "shouldFallback": false,
                          "fallbackReason": null,
                          "noActionMessage": null
                        }
                        """);
                }

                return Result<string>.Success("Return policy is 30 days with original receipt.");
            }));
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
        Assert.Equal("Return policy is 30 days with original receipt.", json.RootElement.GetProperty("response").GetString());
        Assert.True(json.RootElement.TryGetProperty("stage7Decision", out var stage7Decision));
        Assert.Equal("stage7.v1", stage7Decision.GetProperty("schemaVersion").GetString());
        Assert.Equal("Valid", stage7Decision.GetProperty("validationStatus").GetString());
        Assert.Equal("SuggestKnowledge", stage7Decision.GetProperty("recommendations")[0].GetProperty("type").GetString());
    }

    [Fact]
    public async Task ChatSend_WithKnowledge_AndStage7InvalidOutput_DoesNotBreakResponse()
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
            services.AddSingleton<IChatCompletionClient>(new FakeChatCompletionClient(prompt =>
            {
                if (prompt.Contains("Stage 7 AI decision assistant.", StringComparison.Ordinal))
                {
                    return Result<string>.Success("not-json");
                }

                return Result<string>.Success("Return policy is 30 days with original receipt.");
            }));
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
        Assert.Equal("Return policy is 30 days with original receipt.", json.RootElement.GetProperty("response").GetString());
        if (json.RootElement.TryGetProperty("stage7Decision", out var stage7Decision))
        {
            Assert.Equal(JsonValueKind.Null, stage7Decision.ValueKind);
        }
    }

    [Fact]
    public async Task ChatSend_WithoutPromoTrigger_DoesNotReturnPromoMetadata()
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
        Assert.True(json.RootElement.TryGetProperty("response", out _));

        if (json.RootElement.TryGetProperty("responseKind", out var responseKind))
        {
            Assert.True(responseKind.ValueKind is JsonValueKind.Null or JsonValueKind.String);
            if (responseKind.ValueKind == JsonValueKind.String)
            {
                Assert.NotEqual("promo", responseKind.GetString());
            }
        }

        if (json.RootElement.TryGetProperty("promoPublicKey", out var promoPublicKey))
        {
            Assert.True(promoPublicKey.ValueKind is JsonValueKind.Null or JsonValueKind.String);
            if (promoPublicKey.ValueKind == JsonValueKind.String)
            {
                Assert.True(string.IsNullOrWhiteSpace(promoPublicKey.GetString()));
            }
        }
    }

    [Fact]
    public async Task ChatSend_ManualPromoTrigger_ReturnsPromoMetadata()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var response = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "/promo promo-public-key-123"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("promo", json.RootElement.GetProperty("responseKind").GetString());
        Assert.Equal("promo-public-key-123", json.RootElement.GetProperty("promoPublicKey").GetString());
        Assert.Equal("Please complete this short promo form.", json.RootElement.GetProperty("response").GetString());
        Assert.False(json.RootElement.GetProperty("ticketCreated").GetBoolean());
        Assert.Equal(0, json.RootElement.GetProperty("sources").GetArrayLength());
    }

    [Fact]
    public async Task ChatSend_WithoutKnowledge_UsesLayeredFallback_WithoutAutoTicket()
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
        Assert.Equal("I can help with that — are you asking about ordering, the menu, booking, or something else?", json.RootElement.GetProperty("response").GetString());
        Assert.False(json.RootElement.GetProperty("ticketCreated").GetBoolean());

        var secondResponse = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            sessionId,
            message = "can you help me"
        });

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        using var secondJson = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
        Assert.Equal(sessionId, secondJson.RootElement.GetProperty("sessionId").GetString());
        Assert.Equal("I can help with that — are you asking about ordering, the menu, booking, or something else?", secondJson.RootElement.GetProperty("response").GetString());
        Assert.False(secondJson.RootElement.GetProperty("ticketCreated").GetBoolean());

        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var handoffTickets = database.GetCollection<BsonEngageTicket>("EngageHandoffTickets");
        var parsedSessionId = Guid.Parse(sessionId!);
        var handoffCount = await handoffTickets.CountDocumentsAsync(item => item.SessionId == parsedSessionId);
        Assert.Equal(0, handoffCount);

        var tickets = database.GetCollection<BsonTicket>("tickets");
        var ticketsCount = await tickets.CountDocumentsAsync(item => item.EngageSessionId == parsedSessionId);
        Assert.Equal(0, ticketsCount);
    }

    [Fact]
    public async Task ChatSend_FallbackStyleHandoff_DoesNotAutoTicket_VagueLowConfidenceMessage()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var updateBot = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/engage/bot?siteId={site.SiteId}",
            token,
            JsonContent.Create(new { name = "Assistant", fallbackStyle = "handoff" }));
        Assert.Equal(HttpStatusCode.OK, updateBot.StatusCode);

        var response = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "tell me stuff"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var sessionId = Guid.Parse(json.RootElement.GetProperty("sessionId").GetString()!);
        Assert.False(json.RootElement.GetProperty("ticketCreated").GetBoolean());

        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var handoffTickets = database.GetCollection<BsonEngageTicket>("EngageHandoffTickets");
        var handoffCount = await handoffTickets.CountDocumentsAsync(item => item.SessionId == sessionId);
        Assert.Equal(0, handoffCount);

        var tickets = database.GetCollection<BsonTicket>("tickets");
        var createdTicket = await tickets.Find(item => item.EngageSessionId == sessionId).FirstOrDefaultAsync();
        Assert.Null(createdTicket);
    }

    [Fact]
    public async Task ChatSend_FallbackStyleHandoff_Escalates_ForRealQuestion()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var updateBot = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/engage/bot?siteId={site.SiteId}",
            token,
            JsonContent.Create(new { name = "Assistant", fallbackStyle = "handoff" }));
        Assert.Equal(HttpStatusCode.OK, updateBot.StatusCode);

        var response = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "what is your cancellation policy?"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var sessionId = Guid.Parse(json.RootElement.GetProperty("sessionId").GetString()!);
        Assert.True(json.RootElement.GetProperty("ticketCreated").GetBoolean());

        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var tickets = database.GetCollection<BsonTicket>("tickets");
        var createdTicket = await tickets.Find(item => item.EngageSessionId == sessionId).FirstOrDefaultAsync();
        Assert.NotNull(createdTicket);
    }

    [Fact]
    public async Task ChatSend_PromptAllowlist_UsesSafeDefaults_ForUnexpectedStoredPersonality()
    {
        var capturedPrompt = string.Empty;

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
            services.AddSingleton<IChatCompletionClient>(new FakeChatCompletionClient(prompt =>
            {
                capturedPrompt = prompt;
                return Result<string>.Success("Return policy is 30 days with original receipt.");
            }));
        });

        await using var app = AppHostApplication.Build(builder);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var token = await RegisterUserAsync(client);
        var site = await CreateSiteAsync(client, token);
        await AddKnowledgeAsync(client, token, site.SiteId, "Return policy is 30 days with original receipt.");

        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var bots = database.GetCollection<BsonDocument>("EngageBots");
        await bots.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("siteId", Guid.Parse(site.SiteId)),
            Builders<BsonDocument>.Update
                .Set("tone", "!!!unexpected-tone!!!")
                .Set("verbosity", "!!!unexpected-verbosity!!!"));

        var response = await client.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "what is your return policy?"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("- Keep a warm tone.", capturedPrompt, StringComparison.Ordinal);
        Assert.Contains("- Keep verbosity balanced.", capturedPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("knowledge base", capturedPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChatSend_LowSupportOrderFlow_AsksTargetedClarification()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var first = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "how can i order?"
        });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var sessionId = firstJson.RootElement.GetProperty("sessionId").GetString();

        var second = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            sessionId,
            message = "cocktail"
        });

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        using var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal("Sure — are you looking for recommendations, prices, or how to place an order?", secondJson.RootElement.GetProperty("response").GetString());

        var third = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            sessionId,
            message = "something that contains gin"
        });

        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
        using var thirdJson = JsonDocument.Parse(await third.Content.ReadAsStringAsync());
        Assert.Equal("I can help narrow that down. Do you want something refreshing, citrusy, or more spirit-forward?", thirdJson.RootElement.GetProperty("response").GetString());
        Assert.DoesNotContain("knowledge base", thirdJson.RootElement.GetProperty("response").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reliable answer", thirdJson.RootElement.GetProperty("response").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChatSend_AmbiguousShortPrompt_ReturnsClarification()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var response = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "help"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Happy to help — are you looking for hours, location, contact details, or services?", json.RootElement.GetProperty("response").GetString());
        Assert.False(json.RootElement.GetProperty("ticketCreated").GetBoolean());
    }

    [Fact]
    public async Task ChatSend_ContactIntent_UsesBusinessAwareFallback()
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
        Assert.Equal("I can help with contact details — are you looking for a phone number, email, or contact form?", json.RootElement.GetProperty("response").GetString());
        Assert.False(json.RootElement.GetProperty("ticketCreated").GetBoolean());
    }

    [Fact]
    public async Task ChatSend_TypoContactIntent_UsesBusinessAwareFallback()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var response = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "Contct dtails?"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("I can help with contact details — are you looking for a phone number, email, or contact form?", json.RootElement.GetProperty("response").GetString());
        Assert.False(json.RootElement.GetProperty("ticketCreated").GetBoolean());
    }

    [Fact]
    public async Task ChatSend_OrganizationNameQuestion_ReturnsHelpfulClarification()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var response = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "The name of your org?"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("If you’re asking about our organization, I can help with the business name, contact details, hours, location, or services. Which one do you need?", json.RootElement.GetProperty("response").GetString());
        Assert.False(json.RootElement.GetProperty("ticketCreated").GetBoolean());
    }

    [Fact]
    public async Task ChatSend_InformationalErrorQuery_DoesNotAutoEscalate()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var response = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "what error codes do you support?"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("ticketCreated").GetBoolean());
        Assert.NotEqual("Sorry about that — I’ll get someone to help. What’s your name and best email?", json.RootElement.GetProperty("response").GetString());
    }

    [Fact]
    public async Task ChatSend_ExplicitHumanHelpRequest_StillEscalates()
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
        Assert.True(json.RootElement.GetProperty("ticketCreated").GetBoolean());
        Assert.Equal("Sorry about that — I’ll get someone to help. What’s your name and best email?", json.RootElement.GetProperty("response").GetString());
    }

    [Fact]
    public async Task ChatSend_CapturesContactDetails_AsLead_WhenHumanHelpFlowTriggered()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var helpResponse = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "the checkout form isn't working"
        });

        Assert.Equal(HttpStatusCode.OK, helpResponse.StatusCode);
        using var helpJson = JsonDocument.Parse(await helpResponse.Content.ReadAsStringAsync());
        var sessionId = helpJson.RootElement.GetProperty("sessionId").GetString();
        Assert.Equal("Sorry about that — I’ll get someone to help. What’s your name and best email?", helpJson.RootElement.GetProperty("response").GetString());

        var detailsResponse = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            sessionId,
            message = "my name is Pat Example, pat@example.com"
        });

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        using var detailsJson = JsonDocument.Parse(await detailsResponse.Content.ReadAsStringAsync());
        Assert.Equal("Thanks — I’ve got your details. Our team will contact you shortly.", detailsJson.RootElement.GetProperty("response").GetString());

        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var tickets = database.GetCollection<BsonTicket>("tickets");
        var handoffTicket = await tickets.Find(item => item.EngageSessionId == Guid.Parse(sessionId!)).FirstOrDefaultAsync();
        Assert.NotNull(handoffTicket);
        Assert.Equal("Engage handoff: NeedsHumanHelp", handoffTicket!.Subject);
        Assert.Contains("[Engage handoff package]", handoffTicket.Description);
        Assert.Contains("Recent transcript:", handoffTicket.Description);

        var leads = database.GetCollection<BsonLead>("leads");
        var createdLead = await leads.Find(item => item.SiteId == Guid.Parse(site.SiteId) && item.PrimaryEmail == "pat@example.com").FirstOrDefaultAsync();

        Assert.NotNull(createdLead);
        Assert.Equal("Pat Example", createdLead!.DisplayName);
    }

    [Fact]
    public async Task ChatSend_DistilledContext_ExcludesCurrentUserTurn()
    {
        string? capturedPrompt = null;

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
            services.AddSingleton<IChatCompletionClient>(new FakeChatCompletionClient(prompt =>
            {
                if (prompt.Contains("You are an Engage support assistant.", StringComparison.Ordinal))
                {
                    capturedPrompt = prompt;
                    return Result<string>.Success("Return policy is 30 days with original receipt.");
                }

                return Result<string>.Success("{}");
            }));
        });

        await using var app = AppHostApplication.Build(builder);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var token = await RegisterUserAsync(client);
        var site = await CreateSiteAsync(client, token);
        await AddKnowledgeAsync(client, token, site.SiteId, "Return policy is 30 days with original receipt.");

        var firstResponse = await client.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "what is your return policy?"
        });

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        using var firstJson = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        var sessionId = firstJson.RootElement.GetProperty("sessionId").GetString();

        var secondMessage = "do you have weekend hours?";
        var secondResponse = await client.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            sessionId,
            message = secondMessage
        });

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(capturedPrompt);

        var match = Regex.Match(
            capturedPrompt!,
            "Distilled prior user context:\\n(?<distilled>[\\s\\S]*?)\\n\\nUser question:",
            RegexOptions.CultureInvariant);

        Assert.True(match.Success);
        Assert.DoesNotContain(secondMessage, match.Groups["distilled"].Value, StringComparison.OrdinalIgnoreCase);
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
    public async Task ChatSend_ResumesSession_WhenSessionIdMissing_AndCollectorSessionMatches()
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
        Assert.Equal(firstSessionId, secondSessionId);
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
    public async Task ChatSend_CreatesNewSession_WhenCollectorLinkedSessionIsExpired_AndSessionIdMissing()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        const string collectorSessionId = "collector-expired";

        var firstResponse = await _client!.PostAsJsonAsync("/engage/chat/send", new
        {
            widgetKey = site.WidgetKey,
            message = "first",
            collectorSessionId
        });

        using var firstJson = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        var firstSessionId = firstJson.RootElement.GetProperty("sessionId").GetString();

        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var sessions = database.GetCollection<BsonEngageSession>("EngageChatSessions");
        var firstSessionGuid = Guid.Parse(firstSessionId!);
        var expireUpdate = Builders<BsonEngageSession>.Update.Set(item => item.UpdatedAtUtc, DateTime.UtcNow.AddMinutes(-31));
        await sessions.UpdateOneAsync(item => item.Id == firstSessionGuid, expireUpdate);

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


    private async Task<CurrentUserResponse> GetCurrentUserAsync(string token)
    {
        var response = await SendAuthorizedAsync(HttpMethod.Get, "/auth/me", token);
        var payload = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        return payload!;
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
        var response = await client.PostAsJsonAsync("/auth/register", new RegisterRequest("Engage Tester", email, "password-123", "Default Org"));
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

    private sealed class BsonEngageTicket
    {
        public Guid SessionId { get; init; }
    }

    private sealed class BsonTicket
    {
        public Guid SiteId { get; init; }
        public Guid EngageSessionId { get; init; }
        public Guid? VisitorId { get; init; }
        public string Subject { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
    }

    private sealed class BsonLead
    {
        public Guid SiteId { get; init; }
        public string? PrimaryEmail { get; init; }
        public string? DisplayName { get; init; }
    }

    private sealed class FakeChatCompletionClient : IChatCompletionClient
    {
        private readonly Func<string, Result<string>> _handler;

        public FakeChatCompletionClient(string response)
            : this(_ => Result<string>.Success(response))
        {
        }

        public FakeChatCompletionClient(Func<string, Result<string>> handler)
        {
            _handler = handler;
        }

        public Task<Result<string>> CompleteAsync(string prompt, CancellationToken ct)
        {
            return Task.FromResult(_handler(prompt));
        }
    }
}
