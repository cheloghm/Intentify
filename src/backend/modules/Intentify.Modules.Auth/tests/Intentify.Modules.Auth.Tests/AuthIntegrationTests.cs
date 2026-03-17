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
    private Guid _tenantId;
    private Guid _adminUserId;
    private Guid _managerUserId;
    private Guid _regularUserId;
    private Guid _otherTenantUserId;
    private Guid _superAdminUserId;

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
        var request = new RegisterRequest("New Tester", "newtester@intentify.local", "password-456", "New Tester Org");

        var response = await _client!.PostAsJsonAsync("/auth/register", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsClientError()
    {
        var request = new RegisterRequest("Dup Tester", "tester@intentify.local", "password-456", "Dup Org");

        var response = await _client!.PostAsJsonAsync("/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await AssertValidationErrorAsync(response, "email");
    }

    [Fact]
    public async Task Register_InvalidEmail_ReturnsValidationError()
    {
        var request = new RegisterRequest("Bad Email", "invalid-email", "password-456", "Bad Email Org");

        var response = await _client!.PostAsJsonAsync("/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await AssertValidationErrorAsync(response, "email");
    }

    [Fact]
    public async Task Register_WeakPassword_ReturnsValidationError()
    {
        var request = new RegisterRequest("Weak Password", "weakpass@intentify.local", "short1", "Weak Org");

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

    [Fact]
    public async Task ListUsers_AdminAllowed_UserForbidden()
    {
        var adminToken = await LoginAndGetTokenAsync("admin@intentify.local", "password-123");
        var userToken = await LoginAndGetTokenAsync("tester@intentify.local", "password-123");

        using var adminRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/users");
        adminRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var adminResponse = await _client!.SendAsync(adminRequest);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);

        using var userRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/users");
        userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        var userResponse = await _client.SendAsync(userRequest);
        Assert.Equal(HttpStatusCode.Forbidden, userResponse.StatusCode);
    }

    [Fact]
    public async Task ListInvites_AdminAllowed_UserForbidden()
    {
        var adminToken = await LoginAndGetTokenAsync("admin@intentify.local", "password-123");
        var userToken = await LoginAndGetTokenAsync("tester@intentify.local", "password-123");
        var createResponse = await PostInviteAsync(adminToken, "pending@intentify.local", AuthRoles.User);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        using var adminRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/invites");
        adminRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var adminResponse = await _client!.SendAsync(adminRequest);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);

        var invites = await adminResponse.Content.ReadFromJsonAsync<TenantInviteResponse[]>();
        Assert.NotNull(invites);
        Assert.Contains(invites!, invite => invite.Email == "pending@intentify.local" && invite.RevokedAtUtc is null);

        using var userRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/invites");
        userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        var userResponse = await _client.SendAsync(userRequest);
        Assert.Equal(HttpStatusCode.Forbidden, userResponse.StatusCode);
    }

    [Fact]
    public async Task RevokeInvite_RevokesPendingInvite()
    {
        var adminToken = await LoginAndGetTokenAsync("admin@intentify.local", "password-123");
        var createResponse = await PostInviteAsync(adminToken, "revoke-me@intentify.local", AuthRoles.User);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var invite = await createResponse.Content.ReadFromJsonAsync<CreateInviteResponse>();
        Assert.NotNull(invite);

        var listed = await GetInvitesAsync(adminToken);
        var targetInvite = listed.Single(item => item.Token == invite!.Token);

        using var revokeRequest = new HttpRequestMessage(HttpMethod.Delete, $"/auth/invites/{targetInvite.Id:N}");
        revokeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var revokeResponse = await _client!.SendAsync(revokeRequest);
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/invites");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var listResponse = await _client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var invites = await listResponse.Content.ReadFromJsonAsync<TenantInviteResponse[]>();
        Assert.NotNull(invites);
        var revoked = invites!.Single(item => item.InviteId == targetInvite.Id.ToString("N"));
        Assert.NotNull(revoked.RevokedAtUtc);
    }

    [Fact]
    public async Task InviteMatrix_EnforcesHierarchy()
    {
        var adminToken = await LoginAndGetTokenAsync("admin@intentify.local", "password-123");
        var managerToken = await LoginAndGetTokenAsync("manager@intentify.local", "password-123");

        var adminInviteManager = await PostInviteAsync(adminToken, "new-manager@intentify.local", AuthRoles.Manager);
        Assert.Equal(HttpStatusCode.OK, adminInviteManager.StatusCode);

        var adminInviteAdmin = await PostInviteAsync(adminToken, "new-admin@intentify.local", AuthRoles.Admin);
        Assert.Equal(HttpStatusCode.Forbidden, adminInviteAdmin.StatusCode);

        var managerInviteUser = await PostInviteAsync(managerToken, "new-user@intentify.local", AuthRoles.User);
        Assert.Equal(HttpStatusCode.OK, managerInviteUser.StatusCode);

        var managerInviteManager = await PostInviteAsync(managerToken, "new-manager-2@intentify.local", AuthRoles.Manager);
        Assert.Equal(HttpStatusCode.Forbidden, managerInviteManager.StatusCode);
    }

    [Fact]
    public async Task ChangeRole_BlocksSelfAndLastAdmin()
    {
        var adminToken = await LoginAndGetTokenAsync("admin@intentify.local", "password-123");

        using var selfRequest = new HttpRequestMessage(HttpMethod.Put, $"/auth/users/{_adminUserId}/role")
        {
            Content = JsonContent.Create(new ChangeTenantUserRoleRequest(AuthRoles.Manager))
        };
        selfRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var selfResponse = await _client!.SendAsync(selfRequest);
        Assert.Equal(HttpStatusCode.Forbidden, selfResponse.StatusCode);

        using var demoteRequest = new HttpRequestMessage(HttpMethod.Put, $"/auth/users/{_managerUserId}/role")
        {
            Content = JsonContent.Create(new ChangeTenantUserRoleRequest(AuthRoles.User))
        };
        demoteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var demoteResponse = await _client.SendAsync(demoteRequest);
        Assert.Equal(HttpStatusCode.NoContent, demoteResponse.StatusCode);
    }

    [Fact]
    public async Task ChangeRole_EnforcesTenantIsolationAndHierarchy()
    {
        var managerToken = await LoginAndGetTokenAsync("manager@intentify.local", "password-123");

        using var promoteRequest = new HttpRequestMessage(HttpMethod.Put, $"/auth/users/{_regularUserId}/role")
        {
            Content = JsonContent.Create(new ChangeTenantUserRoleRequest(AuthRoles.Manager))
        };
        promoteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var promoteResponse = await _client!.SendAsync(promoteRequest);
        Assert.Equal(HttpStatusCode.Forbidden, promoteResponse.StatusCode);

        using var otherTenantRequest = new HttpRequestMessage(HttpMethod.Put, $"/auth/users/{_otherTenantUserId}/role")
        {
            Content = JsonContent.Create(new ChangeTenantUserRoleRequest(AuthRoles.User))
        };
        otherTenantRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var otherTenantResponse = await _client.SendAsync(otherTenantRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, otherTenantResponse.StatusCode);
    }

    [Fact]
    public async Task RemoveUser_BlocksSelfAndLastAdmin_AndSupportsSoftDelete()
    {
        var adminToken = await LoginAndGetTokenAsync("admin@intentify.local", "password-123");
        var managerToken = await LoginAndGetTokenAsync("manager@intentify.local", "password-123");

        using var selfDeleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/auth/users/{_adminUserId}");
        selfDeleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var selfDeleteResponse = await _client!.SendAsync(selfDeleteRequest);
        Assert.Equal(HttpStatusCode.Forbidden, selfDeleteResponse.StatusCode);

        using var managerDeleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/auth/users/{_regularUserId}");
        managerDeleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var managerDeleteResponse = await _client.SendAsync(managerDeleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, managerDeleteResponse.StatusCode);

        var removedUserLogin = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("tester@intentify.local", "password-123"));
        Assert.Equal(HttpStatusCode.Unauthorized, removedUserLogin.StatusCode);
    }

    [Fact]
    public async Task LastAdmin_CannotBeDemotedOrRemoved()
    {
        var superAdminToken = await LoginAndGetTokenAsync("super@intentify.local", "password-123");

        using var demoteRequest = new HttpRequestMessage(HttpMethod.Put, $"/auth/users/{_adminUserId}/role")
        {
            Content = JsonContent.Create(new ChangeTenantUserRoleRequest(AuthRoles.Manager))
        };
        demoteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superAdminToken);
        var demoteResponse = await _client!.SendAsync(demoteRequest);
        Assert.Equal(HttpStatusCode.Forbidden, demoteResponse.StatusCode);

        using var removeRequest = new HttpRequestMessage(HttpMethod.Delete, $"/auth/users/{_adminUserId}");
        removeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superAdminToken);
        var removeResponse = await _client.SendAsync(removeRequest);
        Assert.Equal(HttpStatusCode.Forbidden, removeResponse.StatusCode);
    }

    [Fact]
    public async Task AcceptInvite_RejectsUnsupportedRole()
    {
        MongoConventions.Register();
        var client = new MongoClient(_mongo.ConnectionString);
        var database = client.GetDatabase(_mongo.DatabaseName);
        var invitations = database.GetCollection<Invitation>(AuthMongoCollections.Invitations);

        var token = Convert.ToHexString(Guid.NewGuid().ToByteArray());
        await invitations.InsertOneAsync(new Invitation
        {
            TenantId = _tenantId,
            CreatedByUserId = _adminUserId,
            Email = "unsafe@intentify.local",
            Role = AuthRoles.SuperAdmin,
            Token = token,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        var response = await _client!.PostAsJsonAsync("/auth/invites/accept", new AcceptInviteRequest(
            token,
            "Unsafe User",
            "unsafe@intentify.local",
            "password-123"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task SeedUserAsync()
    {
        MongoConventions.Register();

        var client = new MongoClient(_mongo.ConnectionString);
        var database = client.GetDatabase(_mongo.DatabaseName);

        var tenants = database.GetCollection<Tenant>(AuthMongoCollections.Tenants);
        var users = database.GetCollection<User>(AuthMongoCollections.Users);

        var tenantId = Guid.NewGuid();
        _tenantId = tenantId;
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

        _regularUserId = user.Id;

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "admin@intentify.local",
            PasswordHash = hasher.HashPassword("password-123"),
            DisplayName = "Admin",
            Roles = new[] { AuthRoles.Admin }
        };
        _adminUserId = adminUser.Id;

        var managerUser = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "manager@intentify.local",
            PasswordHash = hasher.HashPassword("password-123"),
            DisplayName = "Manager",
            Roles = new[] { AuthRoles.Manager }
        };
        _managerUserId = managerUser.Id;

        var superAdminUser = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "super@intentify.local",
            PasswordHash = hasher.HashPassword("password-123"),
            DisplayName = "Super Admin",
            Roles = new[] { AuthRoles.SuperAdmin }
        };
        _superAdminUserId = superAdminUser.Id;

        var otherTenantId = Guid.NewGuid();
        var otherTenant = new Tenant
        {
            Id = otherTenantId,
            Name = "Other Tenant",
            Domain = "other.local",
            Plan = "dev",
            Industry = "software",
            Category = "test"
        };

        var otherTenantUser = new User
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            Email = "other-admin@intentify.local",
            PasswordHash = hasher.HashPassword("password-123"),
            DisplayName = "Other Admin",
            Roles = new[] { AuthRoles.Admin }
        };
        _otherTenantUserId = otherTenantUser.Id;

        await tenants.InsertOneAsync(tenant);
        await tenants.InsertOneAsync(otherTenant);
        await users.InsertOneAsync(user);
        await users.InsertOneAsync(adminUser);
        await users.InsertOneAsync(managerUser);
        await users.InsertOneAsync(superAdminUser);
        await users.InsertOneAsync(otherTenantUser);
    }

    private async Task<string> LoginAndGetTokenAsync(string email, string password)
    {
        var response = await _client!.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        return payload!.AccessToken;
    }

    private async Task<HttpResponseMessage> PostInviteAsync(string token, string email, string role)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/invites")
        {
            Content = JsonContent.Create(new CreateInviteRequest(email, role))
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client!.SendAsync(request);
    }

    private async Task<IReadOnlyCollection<Invitation>> GetInvitesAsync(string token)
    {
        MongoConventions.Register();
        var client = new MongoClient(_mongo.ConnectionString);
        var database = client.GetDatabase(_mongo.DatabaseName);
        var invitations = database.GetCollection<Invitation>(AuthMongoCollections.Invitations);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/invites");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client!.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await invitations.Find(_ => true).ToListAsync();
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
