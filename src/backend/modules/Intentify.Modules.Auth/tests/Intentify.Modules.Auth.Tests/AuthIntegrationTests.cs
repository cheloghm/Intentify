using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Intentify.AppHost;
using Intentify.Modules.Auth.Api;
using Intentify.Modules.Auth.Domain;
using Intentify.Shared.Data.Mongo;
using Intentify.Shared.Security;
using Intentify.Shared.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Xunit;

namespace Intentify.Modules.Auth.Tests;

public sealed class AuthIntegrationTests : IAsyncLifetime
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

        await SeedUserAsync();
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
    public async Task Login_ReturnsJwt()
    {
        var request = new LoginRequest("tester@intentify.local", "password-123");

        var response = await _client!.PostAsJsonAsync("/auth/login", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));
    }

    [Fact]
    public async Task Register_ReturnsJwt()
    {
        var request = new RegisterRequest("New Tester", "newtester@intentify.local", "password-456");

        var response = await _client!.PostAsJsonAsync("/auth/register", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsClientError()
    {
        var request = new RegisterRequest("Dup Tester", "tester@intentify.local", "password-456");

        var response = await _client!.PostAsJsonAsync("/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await AssertValidationErrorAsync(response, "email");
    }

    [Fact]
    public async Task Register_InvalidEmail_ReturnsValidationError()
    {
        var request = new RegisterRequest("Bad Email", "invalid-email", "password-456");

        var response = await _client!.PostAsJsonAsync("/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await AssertValidationErrorAsync(response, "email");
    }

    [Fact]
    public async Task Register_WeakPassword_ReturnsValidationError()
    {
        var request = new RegisterRequest("Weak Password", "weakpass@intentify.local", "short1");

        var response = await _client!.PostAsJsonAsync("/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await AssertValidationErrorAsync(response, "password");
    }

    [Fact]
    public async Task Login_InvalidEmail_ReturnsValidationError()
    {
        var request = new LoginRequest("invalid-email", "password-123");

        var response = await _client!.PostAsJsonAsync("/auth/login", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await AssertValidationErrorAsync(response, "email");
    }

    [Fact]
    public async Task Login_WeakPassword_ReturnsValidationError()
    {
        var request = new LoginRequest("tester@intentify.local", "short1");

        var response = await _client!.PostAsJsonAsync("/auth/login", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await AssertValidationErrorAsync(response, "password");
    }

    [Fact]
    public async Task ProtectedEndpoint_RequiresAuth()
    {
        var response = await _client!.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CurrentUser_ReturnsDisplayName()
    {
        var loginRequest = new LoginRequest("tester@intentify.local", "password-123");
        var loginResponse = await _client!.PostAsJsonAsync("/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginPayload);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginPayload!.AccessToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(payload);
        Assert.Equal("Tester", payload!.DisplayName);
    }

    private async Task SeedUserAsync()
    {
        MongoConventions.Register();

        var client = new MongoClient(_mongo.ConnectionString);
        var database = client.GetDatabase(_mongo.DatabaseName);

        var tenants = database.GetCollection<Tenant>(AuthMongoCollections.Tenants);
        var users = database.GetCollection<User>(AuthMongoCollections.Users);

        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Intentify Test",
            Domain = "intentify.local",
            Plan = "dev",
            Industry = "software",
            Category = "test"
        };

        var hasher = new PasswordHasher();
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "tester@intentify.local",
            PasswordHash = hasher.HashPassword("password-123"),
            DisplayName = "Tester",
            Roles = new[] { AuthRoles.User }
        };

        await tenants.InsertOneAsync(tenant);
        await users.InsertOneAsync(user);
    }

    private static async Task AssertValidationErrorAsync(HttpResponseMessage response, string fieldName)
    {
        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        Assert.Equal("Validation failed", root.GetProperty("title").GetString());
        var errors = root.GetProperty("errors");
        Assert.True(errors.TryGetProperty(fieldName, out _));
    }
}
