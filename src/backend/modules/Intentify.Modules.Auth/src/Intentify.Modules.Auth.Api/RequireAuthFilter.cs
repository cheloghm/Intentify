using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Intentify.Shared.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Intentify.Modules.Auth.Api;

public sealed class RequireAuthFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var token = TryGetBearerToken(httpContext.Request);
        if (string.IsNullOrWhiteSpace(token))
        {
            return Results.Unauthorized();
        }

        var validator = httpContext.RequestServices.GetRequiredService<JwtTokenValidator>();
        var options = httpContext.RequestServices.GetRequiredService<IOptions<JwtOptions>>().Value;
        var validationResult = validator.Validate(token, options);
        if (!validationResult.IsSuccess || validationResult.Value is null)
        {
            return Results.Unauthorized();
        }

        httpContext.User = validationResult.Value;
        EnsureNameIdentifier(httpContext.User);

        return await next(context);
    }

    private static string? TryGetBearerToken(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Authorization", out var values))
        {
            return null;
        }

        var header = values.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return header["Bearer ".Length..].Trim();
    }

    private static void EnsureNameIdentifier(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        if (identity.HasClaim(claim => claim.Type == ClaimTypes.NameIdentifier))
        {
            return;
        }

        var subject = identity.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrWhiteSpace(subject))
        {
            return;
        }

        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, subject));
    }
}
