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
        var log = await logs.Find(item => item.PromoEntryId == entry!.Id).FirstOrDefaultAsync();
        Assert.NotNull(log);
        Assert.True(log!.ConsentGiven);
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

    private sealed class BsonPromoEntry
    {
        public Guid Id { get; init; }
        public Guid PromoId { get; init; }
        public Guid? VisitorId { get; init; }
        public DateTime CreatedAtUtc { get; init; }
    }

    private sealed class BsonPromoConsentLog
    {
        public Guid PromoEntryId { get; init; }
        public bool ConsentGiven { get; init; }
    }
}
