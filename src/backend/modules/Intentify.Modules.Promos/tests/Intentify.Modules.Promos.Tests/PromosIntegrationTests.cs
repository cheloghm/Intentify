using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Intentify.AppHost;
using Intentify.Modules.Auth.Api;
using Intentify.Modules.Sites.Api;
using Intentify.Modules.Visitors.Domain;
using Intentify.Shared.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Xunit;

namespace Intentify.Modules.Promos.Tests;

public sealed class PromosIntegrationTests : IAsyncLifetime
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
        if (_app is not null) await _app.DisposeAsync();
        await _mongo.DisposeAsync();
    }

    [Fact]
    public async Task ValidPromoKey_CreatesEntry_AndConsentLog()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);
        var promo = await CreatePromoAsync(token, site.SiteId, "Waitlist");

        var response = await _client!.PostAsJsonAsync($"/promos/public/{promo.PublicKey}/entries", new
        {
            email = "user@example.com",
            name = "User",
            consentGiven = true,
            consentStatement = "I agree"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var db = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var entries = db.GetCollection<BsonPromoEntry>("promo_entries");
        var logs = db.GetCollection<BsonPromoConsentLog>("promo_consent_logs");
        var entry = await entries.Find(item => item.PromoId == promo.Id).FirstOrDefaultAsync();
        Assert.NotNull(entry);
        Assert.Equal(Guid.Parse(site.SiteId), entry!.SiteId);
        Assert.Null(entry.EngageSessionId);
        var log = await logs.Find(item => item.PromoEntryId == entry.Id).FirstOrDefaultAsync();
        Assert.NotNull(log);
        Assert.True(log!.ConsentGiven);
    }



    [Fact]
    public async Task PublicEntry_PersistsEngageSessionId_WhenProvided()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);
        var promo = await CreatePromoAsync(token, site.SiteId, "Engage Link Promo");
        var engageSessionId = Guid.NewGuid();

        var response = await _client!.PostAsJsonAsync($"/promos/public/{promo.PublicKey}/entries", new
        {
            email = "user@example.com",
            consentGiven = true,
            consentStatement = "I agree",
            engageSessionId = engageSessionId.ToString("N")
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var db = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var entries = db.GetCollection<BsonPromoEntry>("promo_entries");
        var entry = await entries.Find(item => item.PromoId == promo.Id).SortByDescending(i => i.CreatedAtUtc).FirstOrDefaultAsync();
        Assert.NotNull(entry);
        Assert.Equal(engageSessionId, entry!.EngageSessionId);
        Assert.Equal(Guid.Parse(site.SiteId), entry.SiteId);
    }

    [Fact]
    public async Task PublicPromoEndpoint_ReturnsActivePromoQuestions()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(site.SiteId), "siteId");
        content.Add(new StringContent("Widget Promo"), "name");
        content.Add(new StringContent("Widget description"), "description");
        content.Add(new StringContent("[{\"key\":\"email\",\"label\":\"Email\",\"type\":\"email\",\"required\":true,\"order\":1},{\"key\":\"agree\",\"label\":\"Agree\",\"type\":\"checkbox\",\"required\":false,\"order\":2}]"), "questions");
        var createResponse = await SendAuthorizedAsync(HttpMethod.Post, "/promos", token, content);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        using var createJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var publicKey = createJson.RootElement.GetProperty("publicKey").GetString();

        var publicResponse = await _client!.GetAsync($"/promos/public/{publicKey}");
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);

        using var json = JsonDocument.Parse(await publicResponse.Content.ReadAsStringAsync());
        Assert.Equal(publicKey, json.RootElement.GetProperty("publicKey").GetString());
        Assert.Equal("Widget Promo", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("Widget description", json.RootElement.GetProperty("description").GetString());

        var questions = json.RootElement.GetProperty("questions");
        Assert.Equal(2, questions.GetArrayLength());
        Assert.Equal("email", questions[0].GetProperty("key").GetString());
        Assert.Equal("email", questions[0].GetProperty("type").GetString());
        Assert.True(questions[0].GetProperty("required").GetBoolean());
    }

    [Fact]
    public async Task EntriesByVisitor_ReturnsLinkedPromoSubmissions()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);
        var tenantId = Guid.Parse((await GetCurrentUserAsync(token)).TenantId);
        var siteGuid = Guid.Parse(site.SiteId);

        var db = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var visitors = db.GetCollection<Visitor>(VisitorsMongoCollections.Visitors);
        var visitor = new Visitor
        {
            TenantId = tenantId,
            SiteId = siteGuid,
            FirstPartyId = "fp-visitor-profile",
            CreatedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow
        };
        await visitors.InsertOneAsync(visitor);

        var promo = await CreatePromoAsync(token, site.SiteId, "Visitor Promo");

        var createEntryResponse = await _client!.PostAsJsonAsync($"/promos/public/{promo.PublicKey}/entries", new
        {
            visitorId = visitor.Id.ToString("N"),
            email = "visitor@example.com",
            name = "Visitor Name",
            consentGiven = true,
            consentStatement = "I agree",
            answers = new Dictionary<string, string> { ["email"] = "visitor@example.com" }
        });
        Assert.Equal(HttpStatusCode.OK, createEntryResponse.StatusCode);

        var response = await SendAuthorizedAsync(HttpMethod.Get, $"/promos/entries/by-visitor?siteId={site.SiteId}&visitorId={visitor.Id:N}&page=1&pageSize=50", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        Assert.True(json.RootElement.GetArrayLength() >= 1);

        var item = json.RootElement[0];
        Assert.Equal(promo.Id.ToString("N"), item.GetProperty("promoId").GetString());
        Assert.Equal("Visitor Promo", item.GetProperty("promoName").GetString());
        Assert.Equal("visitor@example.com", item.GetProperty("email").GetString());
        Assert.Equal(site.SiteId, item.GetProperty("siteId").GetString());
    }

    [Fact]
    public async Task InvalidPromoKey_IsRejected()
    {
        var response = await _client!.PostAsJsonAsync($"/promos/public/{Guid.NewGuid():N}/entries", new
        {
            consentGiven = true,
            consentStatement = "I agree"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Entry_UsesVisitorResolutionPrecedence_VisitorId_OverFirstPartyAndSession()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);
        var tenantId = Guid.Parse((await GetCurrentUserAsync(token)).TenantId);
        var siteGuid = Guid.Parse(site.SiteId);

        var db = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var visitors = db.GetCollection<Visitor>(VisitorsMongoCollections.Visitors);

        var explicitVisitor = new Visitor
        {
            TenantId = tenantId,
            SiteId = siteGuid,
            FirstPartyId = "fp-explicit",
            CreatedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
            Sessions = [new VisitorSession { SessionId = "sess-explicit", FirstSeenAtUtc = DateTime.UtcNow, LastSeenAtUtc = DateTime.UtcNow }]
        };

        var firstPartyVisitor = new Visitor
        {
            TenantId = tenantId,
            SiteId = siteGuid,
            FirstPartyId = "fp-shared",
            CreatedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
            Sessions = [new VisitorSession { SessionId = "sess-shared", FirstSeenAtUtc = DateTime.UtcNow, LastSeenAtUtc = DateTime.UtcNow }]
        };

        await visitors.InsertManyAsync([explicitVisitor, firstPartyVisitor]);

        var promo = await CreatePromoAsync(token, site.SiteId, "Precedence Promo");
        var response = await _client!.PostAsJsonAsync($"/promos/public/{promo.PublicKey}/entries", new
        {
            visitorId = explicitVisitor.Id.ToString("N"),
            firstPartyId = "fp-shared",
            sessionId = "sess-shared",
            consentGiven = true,
            consentStatement = "I agree"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var entries = db.GetCollection<BsonPromoEntry>("promo_entries");
        var entry = await entries.Find(item => item.PromoId == promo.Id).SortByDescending(i => i.CreatedAtUtc).FirstOrDefaultAsync();
        Assert.NotNull(entry);
        Assert.Equal(explicitVisitor.Id, entry!.VisitorId);
    }

    [Fact]
    public async Task Entry_WithFirstPartyId_LinksToExistingVisitor()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);
        var db = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var visitors = db.GetCollection<Visitor>(VisitorsMongoCollections.Visitors);
        var visitor = new Visitor
        {
            TenantId = Guid.Parse((await GetCurrentUserAsync(token)).TenantId),
            SiteId = Guid.Parse(site.SiteId),
            FirstPartyId = "fp-promos",
            CreatedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow
        };
        await visitors.InsertOneAsync(visitor);

        var promo = await CreatePromoAsync(token, site.SiteId, "Identity Promo");
        var response = await _client!.PostAsJsonAsync($"/promos/public/{promo.PublicKey}/entries", new
        {
            firstPartyId = "fp-promos",
            consentGiven = true,
            consentStatement = "I agree"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var entries = db.GetCollection<BsonPromoEntry>("promo_entries");
        var entry = await entries.Find(item => item.PromoId == promo.Id).SortByDescending(i => i.CreatedAtUtc).FirstOrDefaultAsync();
        Assert.NotNull(entry);
        Assert.Equal(visitor!.Id, entry!.VisitorId);
    }


    [Fact]
    public async Task CreatePromo_WithFlyerAndQuestions_Persists()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(site.SiteId), "siteId");
        content.Add(new StringContent("Summer Promo"), "name");
        content.Add(new StringContent("Summer description"), "description");
        content.Add(new StringContent("true"), "isActive");
        content.Add(new StringContent("[{\"key\":\"email\",\"label\":\"Email\",\"type\":\"email\",\"required\":true,\"order\":0}]"), "questions");
        content.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("flyer-content")), "flyer", "flyer.txt");

        var response = await SendAuthorizedAsync(HttpMethod.Post, "/promos", token, content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var promoId = json.RootElement.GetProperty("id").GetGuid();

        var db = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var promos = db.GetCollection<BsonPromo>("promos");
        var promo = await promos.Find(item => item.Id == promoId).FirstOrDefaultAsync();
        Assert.NotNull(promo);
        Assert.Equal("flyer.txt", promo!.FlyerFileName);
        Assert.True(promo.FlyerBytes?.Length > 0);
        Assert.Single(promo.Questions);
        Assert.Equal("email", promo.Questions[0].Key);
    }

    [Fact]
    public async Task PublicEntry_EnforcesRequiredAnswers_AndPersistsAnswers()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(site.SiteId), "siteId");
        content.Add(new StringContent("Qualified Leads"), "name");
        content.Add(new StringContent("[{\"key\":\"email\",\"label\":\"Email\",\"type\":\"email\",\"required\":true,\"order\":0}]"), "questions");
        var createResponse = await SendAuthorizedAsync(HttpMethod.Post, "/promos", token, content);
        using var createJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var publicKey = createJson.RootElement.GetProperty("publicKey").GetString();

        var invalidResponse = await _client!.PostAsJsonAsync($"/promos/public/{publicKey}/entries", new
        {
            consentGiven = true,
            consentStatement = "I agree",
            answers = new Dictionary<string, string>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

        var validResponse = await _client!.PostAsJsonAsync($"/promos/public/{publicKey}/entries", new
        {
            consentGiven = true,
            consentStatement = "I agree",
            answers = new Dictionary<string, string> { ["email"] = "user@example.com" }
        });

        Assert.Equal(HttpStatusCode.OK, validResponse.StatusCode);

        var db = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var entries = db.GetCollection<BsonPromoEntry>("promo_entries");
        var entry = await entries.Find(_ => true).SortByDescending(item => item.CreatedAtUtc).FirstOrDefaultAsync();
        Assert.NotNull(entry);
        Assert.NotNull(entry!.Answers);
        Assert.Equal("user@example.com", entry.Answers!["email"]);
    }

    [Fact]
    public async Task ListAndDetailEndpoints_StillReturnPromoAndEntries()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);
        var promo = await CreatePromoAsync(token, site.SiteId, "List Detail Promo");

        var entryResponse = await _client!.PostAsJsonAsync($"/promos/public/{promo.PublicKey}/entries", new
        {
            email = "list-detail@example.com",
            consentGiven = true,
            consentStatement = "I agree"
        });
        Assert.Equal(HttpStatusCode.OK, entryResponse.StatusCode);

        var listResponse = await SendAuthorizedAsync(HttpMethod.Get, $"/promos?siteId={site.SiteId}", token);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, listJson.RootElement.ValueKind);
        Assert.Contains(listJson.RootElement.EnumerateArray(), item => item.GetProperty("id").GetString() == promo.Id.ToString("N"));

        var detailResponse = await SendAuthorizedAsync(HttpMethod.Get, $"/promos/{promo.Id:N}", token);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        using var detailJson = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync());
        var detailRoot = detailJson.RootElement;
        Assert.Equal(promo.Id, detailRoot.GetProperty("promo").GetProperty("id").GetGuid());
        Assert.True(detailRoot.GetProperty("entries").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task ExportCsv_ReturnsFixedAndDynamicColumns()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(site.SiteId), "siteId");
        content.Add(new StringContent("Export Promo"), "name");
        content.Add(new StringContent("[{\"key\":\"favorite\",\"label\":\"Favorite\",\"type\":\"text\",\"required\":false,\"order\":0}]"), "questions");
        var createResponse = await SendAuthorizedAsync(HttpMethod.Post, "/promos", token, content);
        using var createJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var promoId = createJson.RootElement.GetProperty("id").GetString();
        var publicKey = createJson.RootElement.GetProperty("publicKey").GetString();

        var engageSessionId = Guid.NewGuid();
        var entryResponse = await _client!.PostAsJsonAsync($"/promos/public/{publicKey}/entries", new
        {
            email = "user@example.com",
            consentGiven = true,
            consentStatement = "I agree",
            engageSessionId = engageSessionId.ToString("N"),
            answers = new Dictionary<string, string> { ["favorite"] = "Blue" }
        });
        Assert.Equal(HttpStatusCode.OK, entryResponse.StatusCode);

        var csvResponse = await SendAuthorizedAsync(HttpMethod.Get, $"/promos/{promoId}/export.csv", token);
        Assert.Equal(HttpStatusCode.OK, csvResponse.StatusCode);
        var csv = await csvResponse.Content.ReadAsStringAsync();
        Assert.Contains("siteId", csv);
        Assert.Contains("engageSessionId", csv);
        Assert.Contains(Guid.Parse(site.SiteId).ToString("N"), csv);
        Assert.Contains(engageSessionId.ToString("N"), csv);
        Assert.Contains("q_favorite", csv);
        Assert.Contains("Blue", csv);
    }



    private async Task<CurrentUserResponse> GetCurrentUserAsync(string token)
    {
        var response = await SendAuthorizedAsync(HttpMethod.Get, "/auth/me", token);
        var payload = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        return payload!;
    }

    private async Task<(Guid Id, string PublicKey)> CreatePromoAsync(string token, string siteId, string name)
    {
        var response = await SendAuthorizedAsync(HttpMethod.Post, "/promos", token, JsonContent.Create(new { siteId, name, description = "desc", isActive = true }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (json.RootElement.GetProperty("id").GetGuid(), json.RootElement.GetProperty("publicKey").GetString()!);
    }

    private async Task<CreateSiteResponse> CreateSiteAsync(string token)
    {
        var response = await SendAuthorizedAsync(HttpMethod.Post, "/sites", token, JsonContent.Create(new CreateSiteRequest($"promo-{Guid.NewGuid():N}.intentify.local")));
        var payload = await response.Content.ReadFromJsonAsync<CreateSiteResponse>();
        return payload!;
    }

    private async Task<string> RegisterUserAsync()
    {
        var response = await _client!.PostAsJsonAsync("/auth/register", new RegisterRequest("Promos Tester", $"promo-{Guid.NewGuid():N}@intentify.local", "password-123"));
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return payload!.AccessToken;
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string url, string token, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = content;
        return await _client!.SendAsync(request);
    }

    private sealed class BsonPromo
    {
        public Guid Id { get; init; }
        public string? FlyerFileName { get; init; }
        public byte[]? FlyerBytes { get; init; }
        public List<BsonPromoQuestion> Questions { get; init; } = [];
    }

    private sealed class BsonPromoQuestion
    {
        public string Key { get; init; } = string.Empty;
    }

    private sealed class BsonPromoEntry
    {
        public Guid Id { get; init; }
        public Guid PromoId { get; init; }
        public Guid SiteId { get; init; }
        public Guid? EngageSessionId { get; init; }
        public Guid? VisitorId { get; init; }
        public Dictionary<string, string>? Answers { get; init; }
        public DateTime CreatedAtUtc { get; init; }
    }

    private sealed class BsonPromoConsentLog
    {
        public Guid PromoEntryId { get; init; }
        public bool ConsentGiven { get; init; }
    }
}
