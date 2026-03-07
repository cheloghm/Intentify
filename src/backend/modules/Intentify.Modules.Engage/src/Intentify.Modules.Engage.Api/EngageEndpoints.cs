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

  function addBubble(role, bodyBuilder) {
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

    bubble.appendChild(label);
    bodyBuilder(bubble);
    row.appendChild(bubble);
    messages.appendChild(row);
    messages.scrollTop = messages.scrollHeight;
    return bubble;
  }

  function addMessage(role, text) {
    addBubble(role, function(bubble) {
      var content = document.createElement('div');
      content.textContent = text;
      bubble.appendChild(content);
    });
  }

  function fetchPromoDefinition(promoPublicKey) {
    return fetch(endpoint('/promos/public/' + encodeURIComponent(promoPublicKey)))
      .then(function(response) {
        if (!response.ok) {
          throw new Error('Promo lookup failed with status ' + response.status);
        }
        return response.json();
      });
  }

  function isSupportedQuestionType(type) {
    var normalized = (type || '').toLowerCase();
    return normalized === 'text' || normalized === 'email' || normalized === 'phone' || normalized === 'textarea' || normalized === 'checkbox';
  }

  function addPromoForm(payload) {
    var promoPublicKey = payload && payload.promoPublicKey;
    if (!promoPublicKey) {
      return;
    }

    fetchPromoDefinition(promoPublicKey)
      .then(function(promo) {
        var questions = Array.isArray(promo && promo.questions) ? promo.questions.filter(function(question) {
          return question && question.key && isSupportedQuestionType(question.type || 'text');
        }) : [];

        addBubble('bot', function(bubble) {
          var title = document.createElement('div');
          title.style.fontWeight = '600';
          title.style.marginBottom = '6px';
          title.textContent = (payload && payload.promoTitle) || (promo && promo.name) || 'Promo';
          bubble.appendChild(title);

          var descriptionValue = (payload && payload.promoDescription) || (promo && promo.description);
          if (descriptionValue) {
            var description = document.createElement('div');
            description.style.marginBottom = '8px';
            description.textContent = descriptionValue;
            bubble.appendChild(description);
          }

          var form = document.createElement('form');
          form.style.display = 'flex';
          form.style.flexDirection = 'column';
          form.style.gap = '6px';

          var controls = [];
          questions.sort(function(a, b) { return (a.order || 0) - (b.order || 0); }).forEach(function(question) {
            var wrapper = document.createElement('label');
            wrapper.style.display = 'flex';
            wrapper.style.flexDirection = 'column';
            wrapper.style.gap = '4px';

            var label = document.createElement('span');
            label.textContent = question.label || question.key;
            label.style.fontSize = '12px';
            wrapper.appendChild(label);

            var type = (question.type || 'text').toLowerCase();
            var control;
            if (type === 'textarea') {
              control = document.createElement('textarea');
              control.rows = 3;
            } else if (type === 'checkbox') {
              control = document.createElement('input');
              control.type = 'checkbox';
              wrapper.style.flexDirection = 'row';
              wrapper.style.alignItems = 'center';
              wrapper.style.gap = '6px';
            } else {
              control = document.createElement('input');
              control.type = type === 'phone' ? 'tel' : (type === 'email' ? 'email' : 'text');
            }

            if (type !== 'checkbox') {
              control.style.padding = '6px 8px';
              control.style.border = '1px solid #cbd5e1';
              control.style.borderRadius = '6px';
            }

            control.setAttribute('data-question-key', question.key);
            control.setAttribute('data-required', question.required ? 'true' : 'false');
            control.setAttribute('data-type', type);
            controls.push(control);

            if (type === 'checkbox') {
              wrapper.insertBefore(control, label);
            } else {
              wrapper.appendChild(control);
            }

            form.appendChild(wrapper);
          });

          var consentWrap = document.createElement('label');
          consentWrap.style.display = 'flex';
          consentWrap.style.alignItems = 'center';
          consentWrap.style.gap = '6px';
          var consentInput = document.createElement('input');
          consentInput.type = 'checkbox';
          consentInput.required = true;
          consentWrap.appendChild(consentInput);
          consentWrap.appendChild(document.createTextNode('I agree to submit this promo form.'));
          form.appendChild(consentWrap);

          var submit = document.createElement('button');
          submit.type = 'submit';
          submit.textContent = 'Submit';
          submit.style.padding = '8px 10px';
          submit.style.background = '#2563eb';
          submit.style.color = '#fff';
          submit.style.border = 'none';
          submit.style.borderRadius = '6px';
          submit.style.cursor = 'pointer';
          form.appendChild(submit);

          form.addEventListener('submit', function(event) {
            event.preventDefault();

            var answers = {};
            for (var i = 0; i < controls.length; i++) {
              var control = controls[i];
              var key = control.getAttribute('data-question-key');
              var required = control.getAttribute('data-required') === 'true';
              var type = control.getAttribute('data-type');
              var value = type === 'checkbox' ? (control.checked ? 'true' : '') : (control.value || '').trim();

              if (required && !value) {
                addMessage('bot', 'Please fill all required promo fields before submitting.');
                return;
              }

              if (value) {
                answers[key] = value;
              }
            }

            if (!consentInput.checked) {
              addMessage('bot', 'Please confirm consent before submitting.');
              return;
            }

            submit.disabled = true;
            submit.textContent = 'Submitting...';

            fetch(endpoint('/promos/public/' + encodeURIComponent(promoPublicKey) + '/entries'), {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({
                sessionId: readCookie('intentify_sid'),
                engageSessionId: sessionId || null,
                consentGiven: true,
                consentStatement: 'I agree to submit this promo form.',
                answers: answers
              })
            })
              .then(function(response) {
                if (!response.ok) {
                  throw new Error('Promo submit failed with status ' + response.status);
                }
                return response.json();
              })
              .then(function() {
                submit.disabled = true;
                submit.textContent = 'Submitted';
                addMessage('bot', 'Thanks! Your promo submission was received.');
              })
              .catch(function(error) {
                console.warn('Intentify promo submit failed:', error);
                submit.disabled = false;
                submit.textContent = 'Submit';
                addMessage('bot', 'Sorry, we could not submit the promo form. Please try again.');
              });
          });

          bubble.appendChild(form);
        });
      })
      .catch(function(error) {
        console.warn('Intentify promo render failed:', error);
        addMessage('bot', 'A promo is available, but we could not load the form right now.');
      });
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
        if (payload && payload.responseKind === 'promo' && payload.promoPublicKey) {
          addPromoForm(payload);
        }
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
        var queryWidgetKey = NormalizeOptional(widgetKey);
        var bodyWidgetKey = NormalizeOptional(request.WidgetKey);

        if (!string.IsNullOrWhiteSpace(queryWidgetKey)
            && !string.IsNullOrWhiteSpace(bodyWidgetKey)
            && !string.Equals(queryWidgetKey, bodyWidgetKey, StringComparison.Ordinal))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["widgetKey"] = ["Widget key mismatch between query and request body."]
            }));
        }

        var resolvedWidgetKey = bodyWidgetKey ?? queryWidgetKey;

        Guid? sessionId = null;
        var normalizedSessionId = NormalizeOptional(request.SessionId);
        if (!string.IsNullOrWhiteSpace(normalizedSessionId))
        {
            if (!Guid.TryParse(normalizedSessionId, out var parsedSessionId))
            {
                return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["sessionId"] = ["Session id is invalid."]
                }));
            }

            sessionId = parsedSessionId;
        }

        var resolvedCollectorSessionId = NormalizeOptional(request.CollectorSessionId)
            ?? NormalizeOptional(context.Request.Cookies["intentify_sid"]);

        var result = await handler.HandleAsync(new ChatSendCommand(resolvedWidgetKey, sessionId, request.Message, resolvedCollectorSessionId), context.RequestAborted);
        return result.Status switch
        {
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(new EngageChatSendResponse(
                result.Value!.SessionId.ToString("N"),
                result.Value.Response,
                result.Value.Confidence,
                result.Value.TicketCreated,
                result.Value.Sources.Select(item => new EngageCitationResponse(item.SourceId.ToString("N"), item.ChunkId.ToString("N"), item.ChunkIndex)).ToArray(),
                result.Value.ResponseKind,
                result.Value.PromoPublicKey,
                result.Value.PromoTitle,
                result.Value.PromoDescription,
                ToStage7DecisionResponse(result.Value.Stage7Decision)))
        };
    }


    public static async Task<IResult> GetBotAsync(string? siteId, HttpContext context, GetEngageBotHandler handler)
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

        var result = await handler.HandleAsync(new GetEngageBotQuery(tenantId.Value, parsedSiteId), context.RequestAborted);
        return result.Status switch
        {
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(new EngageBotResponse(result.Value!.BotId.ToString("N"), result.Value.Name))
        };
    }

    public static async Task<IResult> UpdateBotAsync(string? siteId, UpdateEngageBotRequest request, HttpContext context, UpdateEngageBotHandler handler)
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

        var result = await handler.HandleAsync(new UpdateEngageBotCommand(tenantId.Value, parsedSiteId, request.Name), context.RequestAborted);
        return result.Status switch
        {
            OperationStatus.ValidationFailed => Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            OperationStatus.NotFound => Results.NotFound(),
            _ => Results.Ok(new EngageBotResponse(result.Value!.BotId.ToString("N"), result.Value.Name))
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



    private static EngageAiDecisionResponse? ToStage7DecisionResponse(AiDecisionContract? decision)
    {
        if (decision is null)
        {
            return null;
        }

        return new EngageAiDecisionResponse(
            decision.SchemaVersion,
            decision.DecisionId,
            decision.ContextRef is null
                ? null
                : new EngageAiDecisionContextRefResponse(
                    decision.ContextRef.TenantId.ToString("N"),
                    decision.ContextRef.SiteId.ToString("N"),
                    decision.ContextRef.VisitorId?.ToString("N"),
                    decision.ContextRef.EngageSessionId?.ToString("N")),
            decision.OverallConfidence,
            decision.Recommendations?.Select(item => new EngageAiRecommendationResponse(
                item.Type.ToString(),
                item.Confidence,
                item.Rationale,
                item.EvidenceRefs?.Select(evidence => new EngageAiEvidenceRefResponse(evidence.Source, evidence.ReferenceId, evidence.Detail)).ToArray(),
                item.TargetRefs is null
                    ? null
                    : new EngageAiTargetRefsResponse(
                        item.TargetRefs.PromoId?.ToString("N"),
                        item.TargetRefs.PromoPublicKey,
                        item.TargetRefs.KnowledgeSourceId?.ToString("N"),
                        item.TargetRefs.TicketId?.ToString("N"),
                        item.TargetRefs.VisitorId?.ToString("N")),
                item.RequiresApproval,
                item.ProposedCommand)).ToArray(),
            decision.ValidationStatus.ToString(),
            decision.ValidationErrors,
            decision.AllowlistedActions?.Select(item => item.ToString()).ToArray(),
            decision.ShouldFallback,
            decision.FallbackReason,
            decision.NoActionMessage);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static Guid? TryGetTenantId(ClaimsPrincipal user)
    {
        var tenantIdValue = user.FindFirstValue("tenantId");
        return Guid.TryParse(tenantIdValue, out var tenantId) ? tenantId : null;
    }
}
