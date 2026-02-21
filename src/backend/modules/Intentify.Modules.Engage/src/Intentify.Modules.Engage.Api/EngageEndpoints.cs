using System.Security.Claims;
using Intentify.Modules.Engage.Application;
using Intentify.Modules.Sites.Application;
using Intentify.Shared.Validation;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.Engage.Api;

internal static class EngageEndpoints
{
    public static IResult WidgetScriptAsync(HttpContext context)
    {
        var script = """
(function(){
  var scriptTag = document.currentScript;
  var widgetKey = scriptTag && scriptTag.getAttribute('data-widget-key');
  if (!widgetKey) {
    console.warn('Intentify Engage widget: missing data-widget-key.');
    return;
  }

  var baseUrl;
  try {
    baseUrl = new URL(scriptTag.src, window.location.href).origin;
  } catch (e) {
    baseUrl = window.location.origin;
  }

  var storageKey = 'intentify_engage_session_' + widgetKey;
  var sessionId = localStorage.getItem(storageKey) || '';
  var assistantName = 'Assistant';

  function endpoint(path) { return baseUrl + path; }

  function readCookie(name) {
    var escapedName = name.replace(/[-[\]{}()*+?.,\^$|#\s]/g, '\\$&');
    var match = document.cookie.match(new RegExp('(?:^|; )' + escapedName + '=([^;]*)'));
    if (!match) {
      return null;
    }

    try {
      return decodeURIComponent(match[1]);
    } catch (e) {
      return match[1];
    }
  }

  var toggleButton = document.createElement('button');
  toggleButton.type = 'button';
  toggleButton.textContent = 'Chat';
  toggleButton.style.cssText = 'position:fixed;right:16px;bottom:16px;z-index:999999;padding:10px 14px;background:#111827;color:#fff;border:none;border-radius:999px;cursor:pointer;box-shadow:0 4px 12px rgba(0,0,0,.2);';

  var panel = document.createElement('div');
  panel.style.cssText = 'position:fixed;right:16px;bottom:68px;z-index:999999;width:300px;height:380px;background:#fff;border:1px solid #e5e7eb;border-radius:10px;box-shadow:0 10px 24px rgba(0,0,0,.18);display:none;font-family:Arial,sans-serif;';

  var messages = document.createElement('div');
  messages.style.cssText = 'height:300px;overflow:auto;padding:10px;font-size:13px;line-height:1.4;';

  var composer = document.createElement('div');
  composer.style.cssText = 'display:flex;gap:8px;padding:10px;border-top:1px solid #e5e7eb;';

  var input = document.createElement('input');
  input.type = 'text';
  input.placeholder = 'Type a message...';
  input.style.cssText = 'flex:1;padding:8px;border:1px solid #d1d5db;border-radius:6px;';

  var sendButton = document.createElement('button');
  sendButton.type = 'button';
  sendButton.textContent = 'Send';
  sendButton.style.cssText = 'padding:8px 10px;background:#2563eb;color:#fff;border:none;border-radius:6px;cursor:pointer;';

  composer.appendChild(input);
  composer.appendChild(sendButton);
  panel.appendChild(messages);
  panel.appendChild(composer);
  document.body.appendChild(toggleButton);
  document.body.appendChild(panel);

  function addMessage(role, text) {
    var row = document.createElement('div');
    row.style.display = 'flex';
    row.style.marginBottom = '8px';
    row.style.justifyContent = role === 'user' ? 'flex-end' : 'flex-start';

    var bubble = document.createElement('div');
    bubble.style.maxWidth = '85%';
    bubble.style.padding = '8px 10px';
    bubble.style.borderRadius = '10px';
    bubble.style.whiteSpace = 'pre-wrap';
    bubble.style.background = role === 'user' ? '#dbeafe' : '#f3f4f6';
    bubble.style.color = '#111827';

    var label = document.createElement('div');
    label.style.fontSize = '11px';
    label.style.fontWeight = '600';
    label.style.marginBottom = '4px';
    label.textContent = role === 'user' ? 'You' : assistantName;

    var content = document.createElement('div');
    content.textContent = text;

    bubble.appendChild(label);
    bubble.appendChild(content);
    row.appendChild(bubble);
    messages.appendChild(row);
    messages.scrollTop = messages.scrollHeight;
  }

  toggleButton.addEventListener('click', function() {
    panel.style.display = panel.style.display === 'none' ? 'block' : 'none';
  });

  sendButton.addEventListener('click', sendMessage);
  input.addEventListener('keydown', function(event) {
    if (event.key === 'Enter') {
      event.preventDefault();
      sendMessage();
    }
  });

  fetch(endpoint('/engage/widget/bootstrap?widgetKey=' + encodeURIComponent(widgetKey)))
    .then(function(response) { return response.ok ? response.json() : null; })
    .then(function(payload) {
      if (payload && (payload.botName || payload.displayName)) {
        assistantName = payload.botName || payload.displayName;
      }
    })
    .catch(function(){});

  function sendMessage() {
    var message = input.value.trim();
    if (!message) {
      return;
    }

    addMessage('user', message);
    input.value = '';

    fetch(endpoint('/engage/chat/send?widgetKey=' + encodeURIComponent(widgetKey)), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ widgetKey: widgetKey, sessionId: sessionId, message: message, collectorSessionId: readCookie('intentify_sid') })
    })
      .then(function(response) {
        if (!response.ok) {
          throw new Error('Chat request failed with status ' + response.status);
        }
        return response.json();
      })
      .then(function(payload) {
        sessionId = payload.sessionId || sessionId;
        if (sessionId) {
          localStorage.setItem(storageKey, sessionId);
        }
        addMessage('bot', payload.response || '');
      })
      .catch(function(error) {
        console.warn('Intentify Engage widget send failed:', error);
        addMessage('bot', 'Sorry, something went wrong. Please try again.');
      });
  }
})();
""";

        return Results.Text(script, "application/javascript");
    }

    public static async Task<IResult> WidgetBootstrapAsync(string widgetKey, WidgetBootstrapHandler handler, IEngageBotRepository botRepository, ISiteRepository siteRepository, HttpContext context)
    {
        var result = await handler.HandleAsync(new WidgetBootstrapQuery(widgetKey), context.RequestAborted);
        return result.Status switch
        {
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound => Results.NotFound(),
            _ => await BuildWidgetBootstrapOkAsync(result.Value!, widgetKey, botRepository, siteRepository, context)
        };
    }

    private static async Task<IResult> BuildWidgetBootstrapOkAsync(
        WidgetBootstrapResult result,
        string widgetKey,
        IEngageBotRepository botRepository,
        ISiteRepository siteRepository,
        HttpContext context)
    {
        var site = await siteRepository.GetByWidgetKeyAsync(widgetKey, context.RequestAborted);
        var displayName = "Assistant";
        var botName = "Assistant";

        if (site is not null)
        {
            var bot = await botRepository.GetOrCreateForSiteAsync(site.TenantId, site.Id, context.RequestAborted);
            var resolvedName = string.IsNullOrWhiteSpace(bot.Name) ? bot.DisplayName : bot.Name;
            if (!string.IsNullOrWhiteSpace(resolvedName))
            {
                displayName = resolvedName;
                botName = resolvedName;
            }
        }

        return Results.Ok(new WidgetBootstrapResponse(result.SiteId.ToString("N"), result.Domain, displayName, botName));
    }

    public static async Task<IResult> ChatSendAsync(
        EngageChatSendRequest request,
        string? widgetKey,
        ChatSendHandler handler,
        HttpContext context)
    {
        var resolvedWidgetKey = string.IsNullOrWhiteSpace(widgetKey) ? request.WidgetKey : widgetKey.Trim();

        Guid? sessionId = null;
        if (!string.IsNullOrWhiteSpace(request.SessionId))
        {
            if (!Guid.TryParse(request.SessionId, out var parsedSessionId))
            {
                return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["sessionId"] = ["Session id is invalid."]
                }));
            }

            sessionId = parsedSessionId;
        }

        var result = await handler.HandleAsync(new ChatSendCommand(resolvedWidgetKey, sessionId, request.Message, request.CollectorSessionId), context.RequestAborted);
        return result.Status switch
        {
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(new EngageChatSendResponse(
                result.Value!.SessionId.ToString("N"),
                result.Value.Response,
                result.Value.Confidence,
                result.Value.TicketCreated,
                result.Value.Sources.Select(item => new EngageCitationResponse(item.SourceId.ToString("N"), item.ChunkId.ToString("N"), item.ChunkIndex)).ToArray()))
        };
    }

    public static async Task<IResult> ListConversationsAsync(string? siteId, string? collectorSessionId, HttpContext context, ListConversationsHandler handler)
    {
        if (string.IsNullOrWhiteSpace(siteId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is required."]
            }));
        }

        if (!Guid.TryParse(siteId, out var parsedSiteId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is invalid."]
            }));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var results = await handler.HandleAsync(new ListConversationsQuery(tenantId.Value, parsedSiteId, collectorSessionId), context.RequestAborted);
        return Results.Ok(results.Select(item => new ConversationSummaryResponse(item.SessionId.ToString("N"), item.CreatedAtUtc, item.UpdatedAtUtc)).ToArray());
    }

    public static async Task<IResult> GetConversationMessagesAsync(string sessionId, string? siteId, HttpContext context, GetConversationMessagesHandler handler)
    {
        if (!Guid.TryParse(sessionId, out var parsedSessionId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["sessionId"] = ["Session id is invalid."]
            }));
        }

        if (string.IsNullOrWhiteSpace(siteId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is required."]
            }));
        }

        if (!Guid.TryParse(siteId, out var parsedSiteId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site id is invalid."]
            }));
        }

        var tenantId = TryGetTenantId(context.User);
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(new GetConversationMessagesQuery(tenantId.Value, parsedSiteId, parsedSessionId), context.RequestAborted);
        return result.Status switch
        {
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(result.Value!.Select(item => new ConversationMessageResponse(
                item.MessageId.ToString("N"),
                item.Role,
                item.Content,
                item.CreatedAtUtc,
                item.Confidence,
                item.Citations.Select(c => new EngageCitationResponse(c.SourceId.ToString("N"), c.ChunkId.ToString("N"), c.ChunkIndex)).ToArray())).ToArray())
        };
    }

    private static Guid? TryGetTenantId(ClaimsPrincipal user)
    {
        var tenantIdValue = user.FindFirstValue("tenantId");
        return Guid.TryParse(tenantIdValue, out var tenantId) ? tenantId : null;
    }
}
