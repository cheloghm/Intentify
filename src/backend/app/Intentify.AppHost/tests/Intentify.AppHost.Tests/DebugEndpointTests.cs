using System.Net;
using Intentify.AppHost;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Intentify.AppHost.Tests;

public sealed class DebugEndpointTests
{
    [Fact]
    public async Task DebugEndpoint_IsMappedInDevelopment()
    {
        Environment.SetEnvironmentVariable(DebugEndpoints.DebugSecretEnvironmentVariable, "test-secret");

        try
        {
            await using var app = await BuildApp(Environments.Development);

            using var request = new HttpRequestMessage(HttpMethod.Get, "/debug");
            request.Headers.Add(DebugEndpoints.DebugSecretHeaderName, "test-secret");

            var response = await app.GetTestClient().SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable(DebugEndpoints.DebugSecretEnvironmentVariable, null);
        }
    }

    [Fact]
    public async Task DebugEndpoint_IsNotMappedInProduction()
    {
        await using var app = await BuildApp(Environments.Production);

        var response = await app.GetTestClient().GetAsync("/debug");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<WebApplication> BuildApp(string environment)
    {
        var builder = AppHostApplication.CreateBuilder([], environment);
        builder.WebHost.UseTestServer();

        var app = AppHostApplication.Build(builder);
        await app.StartAsync();
        return app;
    }
}
