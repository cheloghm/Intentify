using System.Text;
using Intentify.Modules.LinkHub.Application;

namespace Intentify.Modules.LinkHub.Api;

internal static class LinkHubPublicPage
{
    private static readonly Dictionary<string, string> PlatformIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["instagram"] = "📸", ["facebook"] = "👥", ["tiktok"] = "🎵", ["x"]         = "🐦",
        ["linkedin"]  = "💼", ["youtube"]  = "▶️", ["snapchat"] = "👻", ["pinterest"] = "📌",
        ["whatsapp"]  = "💬", ["website"]  = "🌐", ["email"]    = "✉️", ["custom"]    = "🔗",
    };

    public static string BuildHtml(LinkHubProfileResult profile, string baseUrl)
    {
        var isDark      = string.Equals(profile.Theme, "dark", StringComparison.OrdinalIgnoreCase);
        var cardBg      = isDark ? "rgba(30,41,59,0.85)" : "rgba(255,255,255,0.85)";
        var textPrimary = isDark ? "#f1f5f9" : "#0f172a";
        var textSecond  = isDark ? "#94a3b8" : "#64748b";
        var brandColor  = Esc(profile.BrandColor ?? "#6366f1");
        var slug        = Esc(profile.Slug);
        var displayName = Esc(profile.DisplayName);
        var bio         = Esc(profile.Bio ?? string.Empty);
        var baseUrlEsc  = Esc(baseUrl);

        // ── Background ────────────────────────────────────────────────────────
        var bgType  = (profile.BackgroundType ?? "color").ToLowerInvariant();
        var bgVal   = profile.BackgroundValue ?? "#ffffff";
        string bodyBgCss;
        if (bgType == "gradient")
            bodyBgCss = $"background:{Esc(bgVal)}";
        else if (bgType == "image")
            bodyBgCss = $"background-image:url('{Esc(bgVal)}');background-size:cover;background-position:center;background-attachment:fixed";
        else
            bodyBgCss = $"background-color:{Esc(bgVal)}";

        // ── Avatar HTML ───────────────────────────────────────────────────────
        string avatarHtml;
        if (!string.IsNullOrWhiteSpace(profile.ProfilePictureUrl))
        {
            avatarHtml =
                $"<div class=\"lhp-avatar\" style=\"background:transparent;padding:0;overflow:hidden\">" +
                $"<img src=\"{Esc(profile.ProfilePictureUrl)}\" " +
                $"style=\"width:80px;height:80px;border-radius:50%;object-fit:cover;border:3px solid {brandColor}\" " +
                $"onerror=\"this.parentElement.innerHTML='<span style=font-size:36px>{Esc(profile.AvatarEmoji ?? "👤")}</span>'\" />" +
                $"</div>";
        }
        else if (!string.IsNullOrWhiteSpace(profile.AvatarEmoji))
        {
            avatarHtml = $"<div class=\"lhp-avatar\" style=\"background:{brandColor}\">{Esc(profile.AvatarEmoji)}</div>";
        }
        else
        {
            var initials = !string.IsNullOrWhiteSpace(profile.AvatarInitials)
                ? Esc(profile.AvatarInitials)
                : Esc(profile.DisplayName.Length > 0 ? profile.DisplayName[..Math.Min(2, profile.DisplayName.Length)].ToUpperInvariant() : "?");
            avatarHtml = $"<div class=\"lhp-avatar\" style=\"background:{brandColor};font-size:28px\">{initials}</div>";
        }

        // ── Tracker + Widget scripts ──────────────────────────────────────────
        var trackerScript = string.IsNullOrWhiteSpace(profile.SiteKey)
            ? ""
            : $"<script src=\"{baseUrlEsc}/api/collector/tracker.js\" data-site-key=\"{Esc(profile.SiteKey)}\" defer></script>";

        var widgetScript = profile.EngageBotEnabled && !string.IsNullOrWhiteSpace(profile.WidgetKey)
            ? $"<script src=\"{baseUrlEsc}/api/collector/sdk.js\" data-widget-key=\"{Esc(profile.WidgetKey)}\" defer></script>"
            : "";

        // ── Links HTML ────────────────────────────────────────────────────────
        var linksHtml   = new StringBuilder();
        var activeLinks = profile.Links.Where(l => l.IsActive).OrderBy(l => l.Order).ToList();

        if (activeLinks.Count == 0)
        {
            linksHtml.Append($"<p style=\"color:{textSecond};text-align:center;font-size:13px;margin:24px 0\">No links added yet.</p>");
        }
        else
        {
            foreach (var l in activeLinks)
            {
                var displayMode = (l.DisplayMode ?? "icon-label").ToLowerInvariant();

                // Resolve icon/favicon
                string iconHtml;
                if ((l.Platform is "website" or "custom") &&
                    Uri.TryCreate(l.Url, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == "https" || uri.Scheme == "http"))
                {
                    var faviconUrl = Esc($"https://www.google.com/s2/favicons?sz=64&domain={uri.Host}");
                    iconHtml = $"<img src=\"{faviconUrl}\" width=\"20\" height=\"20\" " +
                               "style=\"border-radius:3px;flex-shrink:0\" onerror=\"this.style.display='none'\" />";
                }
                else
                {
                    var emoji = PlatformIcons.TryGetValue(l.Platform ?? "", out var pi) ? pi : (l.IconEmoji ?? "🔗");
                    iconHtml = $"<span class=\"lhp-link-icon\">{emoji}</span>";
                }

                var label = Esc(l.Label);
                var url   = Esc(l.Url);
                var lid   = Esc(l.Id);

                if (displayMode == "icon-only")
                {
                    linksHtml.Append(
                        $"<button class=\"lhp-link lhp-link-icon-only\" onclick=\"trackClick('{lid}','{url}')\" type=\"button\" title=\"{label}\">" +
                        iconHtml +
                        "</button>\n");
                }
                else if (displayMode == "label-only")
                {
                    linksHtml.Append(
                        $"<button class=\"lhp-link\" onclick=\"trackClick('{lid}','{url}')\" type=\"button\">" +
                        $"<span class=\"lhp-link-label\">{label}</span>" +
                        "</button>\n");
                }
                else // icon-label (default)
                {
                    linksHtml.Append(
                        $"<button class=\"lhp-link\" onclick=\"trackClick('{lid}','{url}')\" type=\"button\">" +
                        iconHtml +
                        $"<span class=\"lhp-link-label\">{label}</span>" +
                        "</button>\n");
                }
            }
        }

        var bioHtml = string.IsNullOrWhiteSpace(bio) ? "" : $"<div class=\"lhp-bio\">{bio}</div>";

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.AppendLine($"  <title>{displayName} | Hven Link Hub</title>");
        sb.AppendLine($"  <meta name=\"description\" content=\"{bio}\">");
        sb.AppendLine("  <style>");
        sb.AppendLine("    *{box-sizing:border-box;margin:0;padding:0}");
        sb.AppendLine($"    body{{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',system-ui,sans-serif;{bodyBgCss};min-height:100vh;display:flex;flex-direction:column;align-items:center;padding:40px 16px 80px}}");
        sb.AppendLine($"    .lhp-card{{width:100%;max-width:480px;display:flex;flex-direction:column;align-items:center;gap:0;background:{cardBg};border-radius:20px;padding:32px 24px;backdrop-filter:blur(8px)}}");
        sb.AppendLine($"    .lhp-avatar{{width:80px;height:80px;border-radius:50%;background:{brandColor};display:flex;align-items:center;justify-content:center;font-size:36px;margin-bottom:14px;color:#fff;font-weight:700;flex-shrink:0}}");
        sb.AppendLine($"    .lhp-name{{font-size:22px;font-weight:800;color:{textPrimary};text-align:center;letter-spacing:-.02em;margin-bottom:6px}}");
        sb.AppendLine($"    .lhp-bio{{font-size:14px;color:{textSecond};text-align:center;line-height:1.6;margin-bottom:24px;max-width:340px}}");
        sb.AppendLine("    .lhp-links{width:100%;display:flex;flex-direction:column;gap:10px}");
        sb.AppendLine($"    .lhp-link{{width:100%;padding:14px 20px;border-radius:12px;background:{brandColor};color:#fff;border:none;cursor:pointer;font-size:15px;font-weight:600;display:flex;align-items:center;gap:12px;transition:transform .12s,opacity .12s;font-family:inherit}}");
        sb.AppendLine("    .lhp-link-icon-only{width:auto;min-width:52px;justify-content:center;padding:12px 14px}");
        sb.AppendLine("    .lhp-link:hover{opacity:.9;transform:translateY(-1px)}");
        sb.AppendLine("    .lhp-link:active{transform:translateY(0);opacity:1}");
        sb.AppendLine("    .lhp-link-icon{font-size:20px;flex-shrink:0;line-height:1}");
        sb.AppendLine("    .lhp-link-label{flex:1;text-align:center}");
        sb.AppendLine($"    .lhp-footer{{margin-top:32px;font-size:11px;color:{textSecond};text-align:center}}");
        sb.AppendLine($"    .lhp-footer a{{color:{textSecond};text-decoration:none;opacity:.7}}");
        sb.AppendLine("    .lhp-footer a:hover{opacity:1}");
        sb.AppendLine("    @keyframes fadeIn{from{opacity:0;transform:translateY(8px)}to{opacity:1;transform:translateY(0)}}");
        sb.AppendLine("    .lhp-card{animation:fadeIn .4s ease forwards}");
        sb.AppendLine("  </style>");
        sb.AppendLine($"  {trackerScript}");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"lhp-card\">");
        sb.AppendLine($"    {avatarHtml}");
        sb.AppendLine($"    <div class=\"lhp-name\">{displayName}</div>");
        sb.AppendLine($"    {bioHtml}");
        sb.AppendLine("    <div class=\"lhp-links\">");
        sb.Append(linksHtml);
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"lhp-footer\"><a href=\"https://hven.io\" target=\"_blank\" rel=\"noopener\">Powered by Hven</a></div>");
        sb.AppendLine("  </div>");
        sb.AppendLine($"  {widgetScript}");
        sb.AppendLine("  <script>");
        sb.AppendLine($"    var HUB_SLUG = '{slug}';");
        sb.AppendLine($"    var BASE_URL = '{baseUrlEsc}';");
        sb.AppendLine("    function trackClick(linkId, url) {");
        sb.AppendLine("      var body = JSON.stringify({ linkId: linkId, referrer: document.referrer || '', userAgent: navigator.userAgent || '' });");
        sb.AppendLine("      var req = new XMLHttpRequest();");
        sb.AppendLine("      req.open('POST', BASE_URL + '/api/hub/' + HUB_SLUG + '/click', true);");
        sb.AppendLine("      req.setRequestHeader('Content-Type', 'application/json');");
        sb.AppendLine("      req.onreadystatechange = function() { if (req.readyState === 4) { window.location.href = url; } };");
        sb.AppendLine("      req.timeout = 1500;");
        sb.AppendLine("      req.ontimeout = function() { window.location.href = url; };");
        sb.AppendLine("      req.send(body);");
        sb.AppendLine("      return false;");
        sb.AppendLine("    }");
        sb.AppendLine("    (function() {");
        sb.AppendLine("      var vr = new XMLHttpRequest();");
        sb.AppendLine("      vr.open('POST', BASE_URL + '/api/hub/' + HUB_SLUG + '/view', true);");
        sb.AppendLine("      vr.setRequestHeader('Content-Type', 'application/json');");
        sb.AppendLine("      vr.send(JSON.stringify({ referrer: document.referrer || '', userAgent: navigator.userAgent || '' }));");
        sb.AppendLine("    })();");
        sb.AppendLine("  </script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string Esc(string? s) =>
        (s ?? string.Empty)
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#x27;");
}
