using Intentify.Modules.Sites.Application;
using Microsoft.AspNetCore.Cors.Infrastructure;

namespace Intentify.AppHost;

internal sealed class DynamicCorsPolicyProvider : ICorsPolicyProvider
{
    private readonly ISiteRepository _siteRepository;
    private readonly string[] _dashboardOrigins;

    public DynamicCorsPolicyProvider(ISiteRepository siteRepository, string[] dashboardOrigins)
    {
        _siteRepository = siteRepository;
        _dashboardOrigins = dashboardOrigins;
    }

    public async Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        if (!context.Request.Headers.TryGetValue("Origin", out var requestOriginValues))
        {
            return null;
        }

        var requestOrigin = NormalizeOrigin(requestOriginValues.ToString());
        if (string.IsNullOrWhiteSpace(requestOrigin))
        {
            return null;
        }

        if (context.Request.Path.StartsWithSegments("/collector", StringComparison.OrdinalIgnoreCase))
        {
            var siteKey = context.Request.Query["siteKey"].ToString();
            if (!string.IsNullOrWhiteSpace(siteKey))
            {
                var site = await _siteRepository.GetBySiteKeyAsync(siteKey.Trim(), context.RequestAborted);
                var allowedOrigins = site?.AllowedOrigins
                    .Select(NormalizeOrigin)
                    .Where(static origin => !string.IsNullOrWhiteSpace(origin))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (allowedOrigins is { Length: > 0 } &&
                    allowedOrigins.Contains(requestOrigin, StringComparer.OrdinalIgnoreCase))
                {
                    return new CorsPolicyBuilder()
                        .WithOrigins(requestOrigin)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .Build();
                }

                return null;
            }
        }

        var widgetKey = context.Request.Query["widgetKey"].ToString();
        if (!string.IsNullOrWhiteSpace(widgetKey))
        {
            if (_dashboardOrigins.Contains(requestOrigin, StringComparer.OrdinalIgnoreCase))
            {
                return new CorsPolicyBuilder()
                    .WithOrigins(requestOrigin)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .Build();
            }

            var site = await _siteRepository.GetByWidgetKeyAsync(widgetKey.Trim(), context.RequestAborted);
            var allowedOrigins = site?.AllowedOrigins
                .Select(NormalizeOrigin)
                .Where(static origin => !string.IsNullOrWhiteSpace(origin))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (allowedOrigins is { Length: > 0 } &&
                allowedOrigins.Contains(requestOrigin, StringComparer.OrdinalIgnoreCase))
            {
                return new CorsPolicyBuilder()
                    .WithOrigins(requestOrigin)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .Build();
            }

            return null;
        }

        if (_dashboardOrigins.Contains(requestOrigin, StringComparer.OrdinalIgnoreCase))
        {
            return new CorsPolicyBuilder()
                .WithOrigins(requestOrigin)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .Build();
        }

        return null;
    }

    private static string NormalizeOrigin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim().TrimEnd('/');
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }
}
