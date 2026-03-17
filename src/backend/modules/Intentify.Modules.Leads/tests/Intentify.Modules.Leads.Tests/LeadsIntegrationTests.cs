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

namespace Intentify.Modules.Leads.Tests;

public sealed class LeadsIntegrationTests : IAsyncLifetime
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
    public async Task PromoEntry_CreatesLead()
    {
        var token = await RegisterUserAsync("lead-create");
        var tenantId = Guid.Parse((await GetCurrentUserAsync(token)).TenantId);
        var site = await CreateSiteAsync(token);
        var promo = await CreatePromoAsync(token, site.SiteId, "Lead Promo");

        var response = await _client!.PostAsJsonAsync($"/promos/public/{promo.PublicKey}/entries", new
        {
            email = "lead@example.com",
            name = "Lead One",
            consentGiven = true,
            consentStatement = "Agreed"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var leads = await GetLeadsFromDbAsync();
        var created = leads.Single(item => item.TenantId == tenantId && item.SiteId == Guid.Parse(site.SiteId));
        Assert.Equal("lead@example.com", created.PrimaryEmail);
    }

    [Fact]
    public async Task CrossSession_FirstPartyId_LinksToSingleLead()
    {
        var token = await RegisterUserAsync("lead-fp");
        var site = await CreateSiteAsync(token);
        var promo = await CreatePromoAsync(token, site.SiteId, "FP Promo");

        var first = await _client!.PostAsJsonAsync($"/promos/public/{promo.PublicKey}/entries", new
        {
            firstPartyId = "fp-123",
            sessionId = "sess-a",
            consentGiven = true,
            consentStatement = "Agreed"
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _client!.PostAsJsonAsync($"/promos/public/{promo.PublicKey}/entries", new
        {
            firstPartyId = "fp-123",
            sessionId = "sess-b",
            consentGiven = true,
            consentStatement = "Agreed"
        });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var leads = await GetLeadsFromDbAsync();
        Assert.Single(leads.Where(item => item.SiteId == Guid.Parse(site.SiteId) && item.FirstPartyId == "fp-123"));
    }

    [Fact]
    public async Task ConsentGated_VisitorEnrichment()
    {
        var token = await RegisterUserAsync("lead-consent");
        var tenantId = Guid.Parse((await GetCurrentUserAsync(token)).TenantId);
        var site = await CreateSiteAsync(token);

        var db = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var visitors = db.GetCollection<Visitor>(VisitorsMongoCollections.Visitors);
        var visitor = new Visitor
        {
            TenantId = tenantId,
            SiteId = Guid.Parse(site.SiteId),
            FirstPartyId = "fp-enrich",
            CreatedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow
        };
        await visitors.InsertOneAsync(visitor);

        var promo = await CreatePromoAsync(token, site.SiteId, "Consent Promo");
        var withConsent = await _client!.PostAsJsonAsync($"/promos/public/{promo.PublicKey}/entries", new
        {
            firstPartyId = "fp-enrich",
            email = "enrich@example.com",
            name = "Enriched Name",
            consentGiven = true,
            consentStatement = "Agreed"
        });
        Assert.Equal(HttpStatusCode.OK, withConsent.StatusCode);

        var enriched = await visitors.Find(item => item.Id == visitor.Id).FirstOrDefaultAsync();
        Assert.Equal("enrich@example.com", enriched!.PrimaryEmail);
        Assert.Equal("Enriched Name", enriched.DisplayName);
        Assert.NotNull(enriched.LastIdentifiedAtUtc);

        var withoutConsent = await _client!.PostAsJsonAsync($"/promos/public/{promo.PublicKey}/entries", new
        {
            firstPartyId = "fp-enrich",
            email = "new@example.com",
            name = "Another Name",
            consentGiven = false,
            consentStatement = "Denied"
        });
        Assert.Equal(HttpStatusCode.OK, withoutConsent.StatusCode);

        var unchanged = await visitors.Find(item => item.Id == visitor.Id).FirstOrDefaultAsync();
        Assert.Equal("enrich@example.com", unchanged!.PrimaryEmail);
        Assert.Equal("Enriched Name", unchanged.DisplayName);
    }

    [Fact]
    public async Task EmailDedupe_UsesSingleLeadPerSite()
    {
        var token = await RegisterUserAsync("lead-email");
        var site = await CreateSiteAsync(token);
        var promo = await CreatePromoAsync(token, site.SiteId, "Email Promo");

        var first = await _client!.PostAsJsonAsync($"/promos/public/{promo.PublicKey}/entries", new
        {
            email = "same@example.com",
            firstPartyId = "fp-1",
            consentGiven = true,
            consentStatement = "Agreed"
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _client!.PostAsJsonAsync($"/promos/public/{promo.PublicKey}/entries", new
        {
            email = "same@example.com",
            firstPartyId = "fp-2",
            consentGiven = true,
            consentStatement = "Agreed"
        });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var leads = await GetLeadsFromDbAsync();
        Assert.Single(leads.Where(item => item.SiteId == Guid.Parse(site.SiteId) && item.PrimaryEmail == "same@example.com"));
    }

    [Fact]
    public async Task TenantIsolation_SeparatesLeadsAcrossTenants()
    {
        var tokenA = await RegisterUserAsync("lead-tenant-a");
        var tokenB = await RegisterUserAsync("lead-tenant-b");

        var siteA = await CreateSiteAsync(tokenA);
        var siteB = await CreateSiteAsync(tokenB);

        var promoA = await CreatePromoAsync(tokenA, siteA.SiteId, "Tenant A Promo");
        var promoB = await CreatePromoAsync(tokenB, siteB.SiteId, "Tenant B Promo");

        var a = await _client!.PostAsJsonAsync($"/promos/public/{promoA.PublicKey}/entries", new
        {
            email = "shared@example.com",
            consentGiven = true,
            consentStatement = "Agreed"
        });
        Assert.Equal(HttpStatusCode.OK, a.StatusCode);

        var b = await _client!.PostAsJsonAsync($"/promos/public/{promoB.PublicKey}/entries", new
        {
            email = "shared@example.com",
            consentGiven = true,
            consentStatement = "Agreed"
        });
        Assert.Equal(HttpStatusCode.OK, b.StatusCode);

        var tenantAList = await SendAuthorizedAsync(HttpMethod.Get, "/leads?page=1&pageSize=50", tokenA);
        Assert.Equal(HttpStatusCode.OK, tenantAList.StatusCode);
        using var tenantAJson = JsonDocument.Parse(await tenantAList.Content.ReadAsStringAsync());
        Assert.DoesNotContain(tenantAJson.RootElement.EnumerateArray(), item => item.GetProperty("siteId").GetString() == siteB.SiteId);
    }

    private async Task<List<BsonLead>> GetLeadsFromDbAsync()
    {
        var db = new MongoClient(_mongo.ConnectionString).GetDatabase(_mongo.DatabaseName);
        var leads = db.GetCollection<BsonLead>("leads");
        return await leads.Find(_ => true).ToListAsync();
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
        var response = await SendAuthorizedAsync(HttpMethod.Post, "/sites", token, JsonContent.Create(new CreateSiteRequest($"lead-{Guid.NewGuid():N}.intentify.local")));
        var payload = await response.Content.ReadFromJsonAsync<CreateSiteResponse>();
        return payload!;
    }

    private async Task<string> RegisterUserAsync(string prefix)
    {
        var response = await _client!.PostAsJsonAsync("/auth/register", new RegisterRequest("Leads Tester", $"{prefix}-{Guid.NewGuid():N}@intentify.local", "password-123", "Default Org"));
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return payload!.AccessToken;
    }

    private async Task<CurrentUserResponse> GetCurrentUserAsync(string token)
    {
        var response = await SendAuthorizedAsync(HttpMethod.Get, "/auth/me", token);
        var payload = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        return payload!;
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string url, string token, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = content;
        return await _client!.SendAsync(request);
    }

    private sealed class BsonLead
    {
        public Guid TenantId { get; init; }
        public Guid SiteId { get; init; }
        public string? PrimaryEmail { get; init; }
        public string? FirstPartyId { get; init; }
    }
}
