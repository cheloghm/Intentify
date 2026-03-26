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
  var uiStateStorageKey = 'intentify_engage_ui_state_' + widgetKey;
  var sessionId = localStorage.getItem(storageKey) || '';
  var isPanelOpen = false;
  try {
    isPanelOpen = localStorage.getItem(uiStateStorageKey) === 'open';
  } catch (e) {}
  var assistantName = 'Assistant';
  var primaryColor = '#2563eb';
  var launcherVisible = true;
  var isSending = false;
  var isHydrating = false;
  var typingIndicatorRow = null;
  var contactDetailsPrompt = 'Sorry about that — I’ll get someone to help. What’s your name and best email?';
  var collectorSessionWaitMs = 1200;
  var collectorSessionMaxWaitMs = 2200;
  var hydrateRequestId = 0;
  var sendNonce = 0;

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


  function clearStoredSession() {
    sessionId = '';
    try {
      localStorage.removeItem(storageKey);
    } catch (e) {}
  }

  var toggleButton = document.createElement('button');
  toggleButton.type = 'button';
  toggleButton.textContent = 'Chat';
  toggleButton.style.cssText = 'position:fixed;right:16px;bottom:16px;z-index:999999;padding:10px 14px;background:#111827;color:#fff;border:none;border-radius:999px;cursor:pointer;box-shadow:0 4px 12px rgba(0,0,0,.2);';

  var panel = document.createElement('div');
  panel.style.cssText = 'position:fixed;right:16px;bottom:68px;z-index:999999;width:320px;height:400px;background:#fff;border:1px solid #e5e7eb;border-radius:12px;box-shadow:0 14px 30px rgba(15,23,42,.22);display:none;font-family:Arial,sans-serif;overflow:hidden;';

  var messages = document.createElement('div');
  messages.style.cssText = 'height:320px;overflow:auto;padding:12px;font-size:13px;line-height:1.45;background:#f8fafc;';

  var composer = document.createElement('div');
  composer.style.cssText = 'display:flex;gap:8px;padding:10px;border-top:1px solid #e5e7eb;background:#fff;';

  var input = document.createElement('input');
  input.type = 'text';
  input.placeholder = 'Type a message...';
  input.style.cssText = 'flex:1;padding:8px;border:1px solid #d1d5db;border-radius:8px;';

  var sendButton = document.createElement('button');
  sendButton.type = 'button';
  sendButton.textContent = 'Send';
  sendButton.style.cssText = 'padding:8px 12px;background:#2563eb;color:#fff;border:none;border-radius:8px;cursor:pointer;font-weight:600;';

  composer.appendChild(input);
  composer.appendChild(sendButton);
  panel.appendChild(messages);
  panel.appendChild(composer);
  document.body.appendChild(toggleButton);
  document.body.appendChild(panel);

  function applyTheme() {
    var safePrimaryColor = /^#[0-9a-fA-F]{6}$/.test(primaryColor) ? primaryColor : '#2563eb';
    toggleButton.style.background = safePrimaryColor;
    sendButton.style.background = safePrimaryColor;
    panel.style.borderColor = safePrimaryColor;
    input.style.borderColor = '#cbd5e1';

    if (!launcherVisible) {
      toggleButton.style.display = 'none';
      panel.style.display = 'none';
      return;
    }

    toggleButton.style.display = 'block';
  }

  function setSendingState(pending) {
    isSending = pending;
    input.disabled = pending;
    sendButton.disabled = pending;
    sendButton.textContent = pending ? 'Sending...' : 'Send';
  }

  function persistPanelState(open) {
    isPanelOpen = open;
    try {
      localStorage.setItem(uiStateStorageKey, open ? 'open' : 'closed');
    } catch (e) {}
  }

  function renderPanelState() {
    panel.style.display = isPanelOpen ? 'block' : 'none';
  }

  function canSafelyFocusInput() {
    if (!launcherVisible || !isPanelOpen || panel.style.display === 'none' || isSending || isHydrating) {
      return false;
    }

    var active = document.activeElement;
    if (!active || active === document.body) {
      return true;
    }

    if (active === input || active === toggleButton) {
      return true;
    }

    if (panel.contains(active)) {
      return false;
    }

    return false;
  }

  function restoreInputFocus() {
    if (!canSafelyFocusInput()) {
      return;
    }

    setTimeout(function() {
      if (!canSafelyFocusInput()) {
        return;
      }

      try {
        input.focus({ preventScroll: true });
      } catch (error) {
        input.focus();
      }
    }, 0);
  }

  function addBubble(role, bodyBuilder) {
    var row = document.createElement('div');
    row.style.display = 'flex';
    row.style.marginBottom = '8px';
    row.style.justifyContent = role === 'user' ? 'flex-end' : 'flex-start';

    var bubble = document.createElement('div');
    bubble.style.maxWidth = '85%';
    bubble.style.padding = '9px 11px';
    bubble.style.borderRadius = role === 'user' ? '12px 12px 4px 12px' : '12px 12px 12px 4px';
    bubble.style.whiteSpace = 'pre-wrap';
    bubble.style.background = role === 'user' ? '#dbeafe' : '#ffffff';
    bubble.style.border = role === 'user' ? '1px solid #bfdbfe' : '1px solid #e2e8f0';
    bubble.style.color = '#111827';
    bubble.style.boxShadow = role === 'user' ? 'none' : '0 2px 8px rgba(15,23,42,.06)';

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
    return row;
  }

  function removeTypingIndicator() {
    if (typingIndicatorRow && typingIndicatorRow.parentNode) {
      typingIndicatorRow.parentNode.removeChild(typingIndicatorRow);
    }
    typingIndicatorRow = null;
  }

  function showTypingIndicator() {
    removeTypingIndicator();
    typingIndicatorRow = document.createElement('div');
    typingIndicatorRow.style.display = 'flex';
    typingIndicatorRow.style.marginBottom = '8px';
    typingIndicatorRow.style.justifyContent = 'flex-start';

    var indicator = document.createElement('div');
    indicator.textContent = assistantName + ' is typing…';
    indicator.style.fontSize = '12px';
    indicator.style.color = '#475569';
    indicator.style.padding = '6px 10px';
    indicator.style.borderRadius = '999px';
    indicator.style.background = '#e2e8f0';

    typingIndicatorRow.appendChild(indicator);
    messages.appendChild(typingIndicatorRow);
    messages.scrollTop = messages.scrollHeight;
  }

  function addMessage(role, text) {
    return addBubble(role, function(bubble) {
      var content = document.createElement('div');
      content.textContent = text;
      bubble.appendChild(content);
    });
  }

  function addContactDetailsForm() {
    addBubble('bot', function(bubble) {
      var form = document.createElement('form');
      form.style.display = 'flex';
      form.style.flexDirection = 'column';
      form.style.gap = '6px';

      var nameInput = document.createElement('input');
      nameInput.type = 'text';
      nameInput.placeholder = 'Name';
      nameInput.style.cssText = 'padding:6px 8px;border:1px solid #cbd5e1;border-radius:6px;';
      form.appendChild(nameInput);

      var emailInput = document.createElement('input');
      emailInput.type = 'email';
      emailInput.required = true;
      emailInput.placeholder = 'Email';
      emailInput.style.cssText = 'padding:6px 8px;border:1px solid #cbd5e1;border-radius:6px;';
      form.appendChild(emailInput);

      var consentWrap = document.createElement('label');
      consentWrap.style.display = 'flex';
      consentWrap.style.alignItems = 'center';
      consentWrap.style.gap = '6px';
      var consentInput = document.createElement('input');
      consentInput.type = 'checkbox';
      consentInput.required = true;
      consentWrap.appendChild(consentInput);
      consentWrap.appendChild(document.createTextNode('I agree to share my contact details.'));
      form.appendChild(consentWrap);

      var submit = document.createElement('button');
      submit.type = 'submit';
      submit.textContent = 'Share details';
      submit.style.cssText = 'padding:8px 10px;background:' + primaryColor + ';color:#fff;border:none;border-radius:6px;cursor:pointer;';
      form.appendChild(submit);

      form.addEventListener('submit', function(event) {
        event.preventDefault();

        if (!consentInput.checked) {
          addMessage('bot', 'Please confirm consent before sharing details.');
          return;
        }

        var email = (emailInput.value || '').trim();
        if (!email) {
          addMessage('bot', 'Please provide your email address.');
          return;
        }

        var name = (nameInput.value || '').trim();
        submit.disabled = true;
        submit.textContent = 'Sending...';
        sendChatMessage(name ? ('my name is ' + name + ', ' + email) : email)
          .finally(function() {
            submit.disabled = false;
            submit.textContent = 'Share details';
          });
      });

      bubble.appendChild(form);
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
          submit.style.background = primaryColor;
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


  function hydrateConversation() {
    if (!sessionId) {
      return Promise.resolve();
    }

    removeTypingIndicator();
    isHydrating = true;
    input.disabled = true;
    sendButton.disabled = true;

    var requestId = ++hydrateRequestId;
    return fetch(endpoint('/engage/widget/conversations/' + encodeURIComponent(sessionId) + '/messages?widgetKey=' + encodeURIComponent(widgetKey)))
      .then(function(response) {
        if (response.status === 404) {
          clearStoredSession();
          if (!isSending && requestId === hydrateRequestId) {
            messages.innerHTML = '';
          }
          return null;
        }

        if (!response.ok) {
          throw new Error('Conversation history request failed with status ' + response.status);
        }

        return response.json();
      })
      .then(function(history) {
        if (requestId !== hydrateRequestId) {
          return;
        }
        if (!Array.isArray(history)) {
          return;
        }

        messages.innerHTML = '';
        history.forEach(function(item) {
          var role = ((item && item.role) || '').toLowerCase() === 'assistant' ? 'bot' : 'user';
          addMessage(role, (item && item.content) || '');
        });
      })
      .catch(function(error) {
        console.warn('Intentify Engage widget history hydrate failed:', error);
      })
      .finally(function() {
        isHydrating = false;
        input.disabled = false;
        sendButton.disabled = false;
        restoreInputFocus();
      });
  }

  toggleButton.addEventListener('click', function() {
    var willOpen = !isPanelOpen;
    persistPanelState(willOpen);
    renderPanelState();
    if (willOpen) {
      restoreInputFocus();
    }
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
      if (payload && typeof payload.primaryColor === 'string') {
        primaryColor = payload.primaryColor;
      }
      if (payload && typeof payload.launcherVisible === 'boolean') {
        launcherVisible = payload.launcherVisible;
      }
      applyTheme();
    })
    .catch(function(){});

  applyTheme();
  renderPanelState();
  hydrateConversation();
  restoreInputFocus();

  function sendMessage() {
    if (isSending || isHydrating) {
      return;
    }

    var message = input.value.trim();
    if (!message) {
      return;
    }

    input.value = '';

    sendChatMessage(message);
  }

  function sendChatMessage(message) {
    var requestNonce = ++sendNonce;
    var optimisticUserRow = addMessage('user', message);
    var shouldRestoreFocusAfterSend = true;
    setSendingState(true);
    showTypingIndicator();
    return waitForCollectorSessionId()
      .then(function(collectorSessionId) {
        return fetch(endpoint('/engage/chat/send?widgetKey=' + encodeURIComponent(widgetKey)), {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ widgetKey: widgetKey, sessionId: sessionId, message: message, collectorSessionId: collectorSessionId })
        });
      })
      .then(function(response) {
        if (!response.ok) {
          if (response.status === 404) {
            clearStoredSession();
          }
          throw new Error('Chat request failed with status ' + response.status);
        }
        return response.json();
      })
      .then(function(payload) {
        if (requestNonce !== sendNonce) {
          return;
        }
        removeTypingIndicator();
        sessionId = payload.sessionId || sessionId;
        if (sessionId) {
          localStorage.setItem(storageKey, sessionId);
        }
        return renderAssistantPayload(payload).then(function(restoreFocus) {
          shouldRestoreFocusAfterSend = restoreFocus;
        });
      })
      .catch(function(error) {
        console.warn('Intentify Engage widget send failed:', error);
        removeTypingIndicator();
        if (optimisticUserRow && optimisticUserRow.parentNode) {
          optimisticUserRow.parentNode.removeChild(optimisticUserRow);
        }
        if (!input.value) {
          input.value = message;
        }
        addMessage('bot', 'Sorry, something went wrong. Please try again.');
      })
      .finally(function() {
        setSendingState(false);
        if (shouldRestoreFocusAfterSend) {
          restoreInputFocus();
        }
      });
  }

  function renderAssistantPayload(payload) {
    var restoreFocus = true;
    addMessage('bot', payload && payload.response ? payload.response : '');

    if (payload && payload.response === contactDetailsPrompt) {
      addContactDetailsForm();
      restoreFocus = false;
    }
    if (payload && payload.responseKind === 'promo' && payload.promoPublicKey) {
      addPromoForm(payload);
      restoreFocus = false;
    }

    if (!payload || !payload.secondaryResponse) {
      return Promise.resolve(restoreFocus);
    }

    showTypingIndicator();
    return new Promise(function(resolve) {
      setTimeout(function() {
        removeTypingIndicator();
        addMessage('bot', payload.secondaryResponse);
        resolve(restoreFocus);
      }, 700);
    });
  }

  function waitForCollectorSessionId() {
    var existing = readCookie('intentify_sid');
    if (existing) {
      return Promise.resolve(existing);
    }

    return new Promise(function(resolve) {
      var startedAt = Date.now();
      function poll() {
        var current = readCookie('intentify_sid');
        if (current || Date.now() - startedAt >= collectorSessionWaitMs) {
          if (!current) {
            current = ensureCollectorSessionId();
          }
          resolve(current || null);
          return;
        }

        if (Date.now() - startedAt >= collectorSessionMaxWaitMs) {
          resolve(ensureCollectorSessionId());
          return;
        }

        setTimeout(poll, 100);
      }

      poll();
    });
  }

  function ensureCollectorSessionId() {
    var existing = readCookie('intentify_sid');
    if (existing) {
      return existing;
    }

    var generated = generateSessionId();
    if (!generated) {
      return null;
    }

    var secureFlag = window.location.protocol === 'https:' ? '; Secure' : '';
    document.cookie = 'intentify_sid=' + encodeURIComponent(generated) + '; Max-Age=1800; Path=/; SameSite=Lax' + secureFlag;
    return generated;
  }

  function generateSessionId() {
    if (!window.crypto || !window.crypto.getRandomValues) {
      return null;
    }

    var bytes = new Uint8Array(16);
    window.crypto.getRandomValues(bytes);
    var output = '';
    for (var i = 0; i < bytes.length; i += 1) {
      output += bytes[i].toString(16).padStart(2, '0');
    }
    return output;
  }
})();
