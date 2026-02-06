using System.Net;
using System.Text.Json;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace Intentify.Shared.Web.Tests;

public sealed class SharedWebTests
{
    [Fact]
    public async Task CorrelationMiddleware_UsesIncomingHeader()
    {
        await using var app = await BuildApp(Environments.Development, builder =>
        {
            builder.UseMiddleware<CorrelationIdMiddleware>();
            builder.Run(context => context.Response.WriteAsync("ok"));
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "abc-123");

        var response = await app.GetTestClient().SendAsync(request);

        Assert.True(response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values));
        Assert.Equal("abc-123", Assert.Single(values));
    }

    [Fact]
    public async Task CorrelationMiddleware_GeneratesHeaderWhenMissing()
    {
        await using var app = await BuildApp(Environments.Development, builder =>
        {
            builder.UseMiddleware<CorrelationIdMiddleware>();
            builder.Run(context => context.Response.WriteAsync("ok"));
        });

        var response = await app.GetTestClient().GetAsync("/");

        Assert.True(response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values));
        var correlationId = Assert.Single(values);
        Assert.True(Guid.TryParse(correlationId, out _));
    }

    [Fact]
    public async Task ExceptionHandling_ReturnsSafeProblemDetailsInProduction()
    {
        await using var app = await BuildApp(Environments.Production, builder =>
        {
            builder.UseExceptionHandler(appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    var feature = context.Features.Get<IExceptionHandlerFeature>();
                    var exception = feature?.Error ?? new InvalidOperationException("Unknown error");
                    var problemDetails = ProblemDetailsHelpers.CreateExceptionProblemDetails(
                        exception,
                        context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment());

                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await context.Response.WriteAsJsonAsync(problemDetails);
                });
            });

            builder.Run(_ => throw new InvalidOperationException("Sensitive error"));
        });

        var response = await app.GetTestClient().GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("An unexpected error occurred.", json.RootElement.GetProperty("detail").GetString());
    }

    private static async Task<WebApplication> BuildApp(
        string environment,
        Action<IApplicationBuilder> configure)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environment
        });

        builder.WebHost.UseTestServer();

        var app = builder.Build();
        configure(app);
        await app.StartAsync();
        return app;
    }
}
