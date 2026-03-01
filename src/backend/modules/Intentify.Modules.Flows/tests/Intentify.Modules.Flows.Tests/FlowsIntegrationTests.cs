using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Intentify.AppHost;
using Intentify.Modules.Auth.Api;
using Intentify.Modules.Flows.Application;
using System.IdentityModel.Tokens.Jwt;
using Intentify.Modules.Sites.Api;
using Intentify.Shared.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Intentify.Modules.Flows.Tests;

public sealed class FlowsIntegrationTests : IAsyncLifetime
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
    public async Task CreateFlow_ThenExecuteTrigger_StoresRun()
    {
        var token = await RegisterUserAsync();
        var site = await CreateSiteAsync(token);

        var createResponse = await SendAuthorizedAsync(HttpMethod.Post, "/flows", token, JsonContent.Create(new
        {
            siteId = site.SiteId,
            name = "Log intelligence trend updates",
            trigger = new
            {
                triggerType = "IntelligenceTrendsUpdated",
                filters = new Dictionary<string, string> { ["window"] = "7d" }
            },
            conditions = new object[] { },
            actions = new[] { new { actionType = "LogRun", @params = new Dictionary<string, string>() } }
        }));

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var flow = await createResponse.Content.ReadFromJsonAsync<FlowDetailDto>();
        Assert.NotNull(flow);

        var executeService = _app!.Services.GetRequiredService<ExecuteFlowsForTriggerService>();
        var executeResult = await executeService.HandleAsync(new ExecuteFlowsTriggerCommand(
            GetTenantId(token),
            Guid.Parse(site.SiteId),
            "IntelligenceTrendsUpdated",
            new Dictionary<string, string> { ["window"] = "7d" },
            new Dictionary<string, string> { ["message"] = "hello" }));

        if (!executeResult.IsSuccess)
        {
            throw new InvalidOperationException("Expected execute success.");
        }

        var runsResponse = await SendAuthorizedAsync(HttpMethod.Get, $"/flows/{flow.Id}/runs", token);
        Assert.Equal(HttpStatusCode.OK, runsResponse.StatusCode);
        var runs = await runsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<FlowRunDto>>();
        Assert.NotNull(runs);
        Assert.NotEmpty(runs);
    }

    private static Guid GetTenantId(string token)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var tenantId = jwt.Claims.First(c => c.Type == "tenantId").Value;
        return Guid.Parse(tenantId);
    }

    private async Task<string> RegisterUserAsync()
    {
        var email = $"flows-{Guid.NewGuid():N}@intentify.local";
        var response = await _client!.PostAsJsonAsync("/auth/register", new RegisterRequest("Flows Tester", email, "password-123"));
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return payload!.AccessToken;
    }

    private async Task<CreateSiteResponse> CreateSiteAsync(string accessToken)
    {
        var domain = $"flows-{Guid.NewGuid():N}.intentify.local";
        var response = await SendAuthorizedAsync(HttpMethod.Post, "/sites", accessToken, JsonContent.Create(new CreateSiteRequest(domain)));
        var payload = await response.Content.ReadFromJsonAsync<CreateSiteResponse>();
        return payload!;
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string url, string accessToken, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = content;

        return await _client!.SendAsync(request);
    }
}
