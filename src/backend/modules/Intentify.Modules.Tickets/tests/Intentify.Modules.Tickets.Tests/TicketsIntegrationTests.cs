using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Intentify.AppHost;
using Intentify.Modules.Auth.Api;
using Intentify.Modules.Tickets.Api;
using Intentify.Shared.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Intentify.Modules.Tickets.Tests;

public sealed class TicketsIntegrationTests : IAsyncLifetime
{
    private const string JwtIssuer = "intentify";
    private const string JwtAudience = "intentify-users";
    private const string JwtSigningKey = "test-signing-key-1234567890-EXTRA-KEY";

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
            ["Intentify:Jwt:Issuer"] = JwtIssuer,
            ["Intentify:Jwt:Audience"] = JwtAudience,
            ["Intentify:Jwt:SigningKey"] = JwtSigningKey,
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
    public async Task ListTickets_IsTenantScoped()
    {
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();

        var tokenA = CreateJwt(userAId, tenantAId);
        var tokenB = CreateJwt(userBId, tenantBId);

        var firstTicket = await CreateTicketAsync(tokenA, new CreateTicketRequest(Guid.NewGuid(), null, null, "Tenant A", "Ticket A", null));
        await CreateTicketAsync(tokenB, new CreateTicketRequest(Guid.NewGuid(), null, null, "Tenant B", "Ticket B", null));

        var firstTenantListResponse = await SendAuthorizedAsync(HttpMethod.Get, "/tickets?page=1&pageSize=10", tokenA);
        Assert.Equal(HttpStatusCode.OK, firstTenantListResponse.StatusCode);

        using var listJson = JsonDocument.Parse(await firstTenantListResponse.Content.ReadAsStringAsync());
        var items = listJson.RootElement;
        Assert.Single(items.EnumerateArray().Where(item => item.GetProperty("subject").GetString() == "Tenant A"));
        Assert.DoesNotContain(items.EnumerateArray(), item => item.GetProperty("subject").GetString() == "Tenant B");

        var forbiddenGet = await SendAuthorizedAsync(HttpMethod.Get, $"/tickets/{firstTicket.Id}", tokenB);
        Assert.Equal(HttpStatusCode.NotFound, forbiddenGet.StatusCode);
    }

    [Fact]
    public async Task TransitionStatus_RejectsInvalidTransitions()
    {
        var token = await RegisterUserAsync("transition");
        var ticket = await CreateTicketAsync(token, new CreateTicketRequest(Guid.NewGuid(), null, null, "Status", "Status transition", null));

        var invalidTransition = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/tickets/{ticket.Id}/status",
            token,
            JsonContent.Create(new TransitionTicketStatusRequest("Closed")));

        Assert.Equal(HttpStatusCode.BadRequest, invalidTransition.StatusCode);

        var toInProgress = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/tickets/{ticket.Id}/status",
            token,
            JsonContent.Create(new TransitionTicketStatusRequest("InProgress")));

        Assert.Equal(HttpStatusCode.OK, toInProgress.StatusCode);

        var toClosed = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/tickets/{ticket.Id}/status",
            token,
            JsonContent.Create(new TransitionTicketStatusRequest("Closed")));

        Assert.Equal(HttpStatusCode.BadRequest, toClosed.StatusCode);
    }

    private async Task<(Guid Id, string Subject)> CreateTicketAsync(string accessToken, CreateTicketRequest request)
    {
        var response = await SendAuthorizedAsync(HttpMethod.Post, "/tickets", accessToken, JsonContent.Create(request));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (json.RootElement.GetProperty("id").GetGuid(), json.RootElement.GetProperty("subject").GetString()!);
    }


    private static string CreateJwt(Guid userId, Guid tenantId)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString("N")),
            new("tenantId", tenantId.ToString("N"))
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> RegisterUserAsync(string prefix)
    {
        var email = $"{prefix}-{Guid.NewGuid():N}@intentify.local";
        var response = await _client!.PostAsJsonAsync("/auth/register", new RegisterRequest("Tickets Tester", email, "password-123"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        return payload!.AccessToken;
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string url, string accessToken, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = content;

        return await _client!.SendAsync(request);
    }
}
