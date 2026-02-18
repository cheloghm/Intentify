using System.Security.Claims;
using Intentify.Modules.Engage.Application;
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

  function endpoint(path) { return baseUrl + path; }

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
    row.style.marginBottom = '8px';
    row.innerHTML = '<strong>' + (role === 'user' ? 'You' : 'Bot') + ':</strong> ' + text;
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

  fetch(endpoint('/engage/widget/bootstrap?widgetKey=' + encodeURIComponent(widgetKey))).catch(function(){});

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
      body: JSON.stringify({ widgetKey: widgetKey, sessionId: sessionId, message: message })
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

    public static async Task<IResult> WidgetBootstrapAsync(string widgetKey, WidgetBootstrapHandler handler, HttpContext context)
    {
        var result = await handler.HandleAsync(new WidgetBootstrapQuery(widgetKey), context.RequestAborted);
        return result.Status switch
        {
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(new WidgetBootstrapResponse(result.Value!.SiteId.ToString("N"), result.Value.Domain))
        };
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

        var result = await handler.HandleAsync(new ChatSendCommand(resolvedWidgetKey, sessionId, request.Message), context.RequestAborted);
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

    public static async Task<IResult> ListConversationsAsync(string? siteId, HttpContext context, ListConversationsHandler handler)
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

        var results = await handler.HandleAsync(new ListConversationsQuery(tenantId.Value, parsedSiteId), context.RequestAborted);
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
