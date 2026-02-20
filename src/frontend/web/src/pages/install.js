import { createCard, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';
import { API_BASE } from '../shared/config.js';

const ORIGIN_HELPER_TEXT = 'Paste the website origin (scheme + host + port). No paths.';

const copyToClipboardRobust = async (value) => {
  if (navigator.clipboard?.writeText) {
    try {
      await navigator.clipboard.writeText(value);
      return;
    } catch (error) {
      // Fallback below.
    }
  }

  const textarea = document.createElement('textarea');
  textarea.value = value;
  textarea.style.position = 'fixed';
  textarea.style.opacity = '0';
  document.body.appendChild(textarea);
  textarea.focus();
  textarea.select();
  const copied = document.execCommand('copy');
  textarea.remove();

  if (!copied) {
    throw new Error('Copy command failed.');
  }
};

const createButton = ({ label, variant = 'default', type = 'button' } = {}) => {
  const button = document.createElement('button');
  button.type = type;
  button.textContent = label;
  button.style.padding = '8px 12px';
  button.style.borderRadius = '6px';
  button.style.border = variant === 'primary' ? 'none' : '1px solid #e2e8f0';
  button.style.background = variant === 'primary' ? '#2563eb' : '#ffffff';
  button.style.color = variant === 'primary' ? '#ffffff' : '#1e293b';
  button.style.cursor = 'pointer';
  button.style.fontSize = '13px';
  return button;
};

const normalizeOrigin = (value) => {
  const input = typeof value === 'string' ? value.trim() : '';
  if (!input) {
    return { value: '', error: ORIGIN_HELPER_TEXT };
  }

  const withoutTrailingSlash = input.replace(/\/+$/, '');
  let normalized = withoutTrailingSlash;

  if (withoutTrailingSlash.includes('/')) {
    try {
      const parsed = new URL(withoutTrailingSlash);
      normalized = `${parsed.protocol}//${parsed.host}`;
    } catch (error) {
      normalized = withoutTrailingSlash;
    }
  }

  if (!/^https?:\/\//i.test(normalized)) {
    return { value: '', error: ORIGIN_HELPER_TEXT };
  }

  try {
    const parsed = new URL(normalized);
    if (!parsed.hostname) {
      return { value: '', error: ORIGIN_HELPER_TEXT };
    }
    return { value: `${parsed.protocol}//${parsed.host}`, error: '' };
  } catch (error) {
    return { value: '', error: ORIGIN_HELPER_TEXT };
  }
};

const normalizeOrigins = (origins) => {
  const unique = new Map();
  origins.forEach((origin) => {
    const normalizedResult = normalizeOrigin(origin);
    if (!normalizedResult.value) {
      return;
    }

    const key = normalizedResult.value.toLowerCase();
    if (!unique.has(key)) {
      unique.set(key, normalizedResult.value);
    }
  });
  return Array.from(unique.values());
};

const loadCachedKeys = (siteId) => {
  if (!siteId) {
    return null;
  }

  try {
    const raw = localStorage.getItem(`intentify.siteKeys.${siteId}`);
    if (!raw) {
      return null;
    }

    const parsed = JSON.parse(raw);
    if (
      parsed &&
      typeof parsed.siteKey === 'string' &&
      typeof parsed.widgetKey === 'string' &&
      parsed.siteKey &&
      parsed.widgetKey
    ) {
      return parsed;
    }
  } catch (error) {
    return null;
  }

  return null;
};

const saveCachedKeys = (siteId, { siteKey, widgetKey }) => {
  if (!siteId || !siteKey || !widgetKey) {
    return;
  }

  localStorage.setItem(
    `intentify.siteKeys.${siteId}`,
    JSON.stringify({
      siteKey,
      widgetKey,
      cachedAtUtc: new Date().toISOString(),
    })
  );
};

const getSiteId = (site) => site?.siteId || site?.id || '';

export const renderInstallView = (container, { apiClient, toast, query } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();
  const siteId = query?.siteId;
  const domain = query?.domain || '';
  const querySiteKey = typeof query?.siteKey === 'string' ? query.siteKey.trim() : '';
  const queryWidgetKey = typeof query?.widgetKey === 'string' ? query.widgetKey.trim() : '';

  if (!siteId) {
    const message = document.createElement('div');
    message.textContent = 'Missing site ID. Return to Sites and open Install again.';
    message.style.color = '#dc2626';
    message.style.fontSize = '14px';
    container.appendChild(message);
    return;
  }

  const cachedKeys = !querySiteKey || !queryWidgetKey ? loadCachedKeys(siteId) : null;

  const state = {
    site: null,
    siteLoading: false,
    origins: [],
    originInput: '',
    originInputError: '',
    savingOrigins: false,
    originsError: '',
    rawSiteKey: querySiteKey || cachedKeys?.siteKey || '',
    rawWidgetKey: queryWidgetKey || cachedKeys?.widgetKey || '',
    keysLoading: false,
    revealSiteKey: false,
    copyMessage: '',
    status: null,
    statusError: '',
    actionError: '',
    sendingTest: false,
  };

  const page = document.createElement('div');
  page.style.display = 'flex';
  page.style.flexDirection = 'column';
  page.style.gap = '20px';
  page.style.width = '100%';
  page.style.maxWidth = '840px';

  const header = document.createElement('div');
  const title = document.createElement('h2');
  title.textContent = 'Install';
  title.style.margin = '0';
  const subtitle = document.createElement('p');
  subtitle.textContent = domain
    ? `Finish installing tracking for ${domain}.`
    : 'Finish installing tracking for your site.';
  subtitle.style.margin = '6px 0 0';
  subtitle.style.color = '#64748b';
  header.append(title, subtitle);

  const stepsList = document.createElement('div');
  stepsList.style.display = 'flex';
  stepsList.style.gap = '10px';
  ['1. Allowed origins', '2. Snippet', '3. Verify'].forEach((label) => {
    const badge = document.createElement('div');
    badge.textContent = label;
    badge.style.fontSize = '12px';
    badge.style.padding = '6px 10px';
    badge.style.border = '1px solid #e2e8f0';
    badge.style.borderRadius = '999px';
    badge.style.color = '#475569';
    stepsList.appendChild(badge);
  });

  const step1Body = document.createElement('div');
  step1Body.style.display = 'flex';
  step1Body.style.flexDirection = 'column';
  step1Body.style.gap = '10px';

  const originsList = document.createElement('div');
  originsList.style.display = 'flex';
  originsList.style.flexDirection = 'column';
  originsList.style.gap = '8px';

  const originInput = document.createElement('input');
  originInput.type = 'text';
  originInput.placeholder = 'https://app.example.com';
  originInput.style.padding = '8px 10px';
  originInput.style.borderRadius = '6px';
  originInput.style.border = '1px solid #cbd5e1';

  const helperText = document.createElement('div');
  helperText.style.fontSize = '12px';
  helperText.style.color = '#64748b';
  helperText.textContent = ORIGIN_HELPER_TEXT;

  const originInputError = document.createElement('div');
  originInputError.style.fontSize = '12px';
  originInputError.style.color = '#dc2626';

  const originActions = document.createElement('div');
  originActions.style.display = 'flex';
  originActions.style.flexWrap = 'wrap';
  originActions.style.gap = '8px';

  const addOriginButton = createButton({ label: 'Add origin' });
  const addDashboardOriginButton = createButton({ label: 'Add current origin (Dashboard origin)' });
  const addLocalhostButton = createButton({ label: 'Add localhost current port' });
  const saveOriginsButton = createButton({ label: 'Save origins', variant: 'primary' });
  const originsErrorText = document.createElement('div');
  originsErrorText.style.fontSize = '13px';
  originsErrorText.style.color = '#dc2626';

  originActions.append(
    addOriginButton,
    addDashboardOriginButton,
    addLocalhostButton,
    saveOriginsButton
  );

  step1Body.append(originsList, originInput, helperText, originInputError, originActions, originsErrorText);

  const renderOrigins = () => {
    originsList.innerHTML = '';
    if (!state.origins.length) {
      const empty = document.createElement('div');
      empty.textContent = 'No origins configured yet.';
      empty.style.color = '#94a3b8';
      empty.style.fontSize = '13px';
      originsList.appendChild(empty);
    } else {
      state.origins.forEach((origin, index) => {
        const row = document.createElement('div');
        row.style.display = 'flex';
        row.style.justifyContent = 'space-between';
        row.style.alignItems = 'center';
        row.style.gap = '10px';
        row.style.border = '1px solid #e2e8f0';
        row.style.borderRadius = '6px';
        row.style.padding = '8px 10px';

        const text = document.createElement('span');
        text.textContent = origin;
        text.style.fontSize = '13px';

        const removeButton = createButton({ label: 'Remove' });
        removeButton.addEventListener('click', () => {
          state.origins = state.origins.filter((_, i) => i !== index);
          renderOrigins();
        });

        row.append(text, removeButton);
        originsList.appendChild(row);
      });
    }

    originInput.value = state.originInput;
    originInputError.textContent = state.originInputError;
    saveOriginsButton.disabled = state.savingOrigins || state.siteLoading;
    saveOriginsButton.textContent = state.savingOrigins ? 'Saving...' : 'Save origins';
    originsErrorText.textContent = state.originsError;
  };

  const addOrigin = (rawOrigin) => {
    const normalizedResult = normalizeOrigin(rawOrigin);
    if (!normalizedResult.value) {
      state.originInputError = normalizedResult.error;
      renderOrigins();
      return;
    }

    const existing = state.origins.find(
      (origin) => origin.toLowerCase() === normalizedResult.value.toLowerCase()
    );
    if (existing) {
      notifier.show({ message: 'Origin already listed.', variant: 'warning' });
      return;
    }

    state.originInputError = '';
    state.origins.push(normalizedResult.value);
    state.originInput = '';
    renderOrigins();
  };

  originInput.addEventListener('input', () => {
    state.originInput = originInput.value;
    state.originInputError = '';
  });

  addOriginButton.addEventListener('click', () => {
    addOrigin(state.originInput);
  });

  addDashboardOriginButton.addEventListener('click', () => {
    addOrigin(window.location.origin);
  });

  addLocalhostButton.addEventListener('click', () => {
    if (window.location.hostname !== 'localhost') {
      state.originInputError = ORIGIN_HELPER_TEXT;
      renderOrigins();
      return;
    }
    addOrigin(`http://localhost:${window.location.port || 80}`);
  });

  saveOriginsButton.addEventListener('click', async () => {
    state.savingOrigins = true;
    state.originsError = '';
    renderOrigins();

    try {
      const payloadOrigins = normalizeOrigins(state.origins);
      const response = await client.request(`/sites/${siteId}/origins`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ allowedOrigins: payloadOrigins }),
      });
      state.origins = response?.allowedOrigins || payloadOrigins;
      state.site = {
        ...(state.site || {}),
        ...response,
        allowedOrigins: state.origins,
      };
      notifier.show({ message: 'Allowed origins updated.', variant: 'success' });
    } catch (error) {
      const uiError = mapApiError(error);
      state.originsError = uiError.message;
      notifier.show({ message: uiError.message, variant: 'danger' });
    } finally {
      state.savingOrigins = false;
      renderOrigins();
    }
  });

  const step2Body = document.createElement('div');
  step2Body.style.display = 'flex';
  step2Body.style.flexDirection = 'column';
  step2Body.style.gap = '10px';

  const siteKeyRow = document.createElement('div');
  siteKeyRow.style.display = 'flex';
  siteKeyRow.style.alignItems = 'center';
  siteKeyRow.style.gap = '8px';

  const siteKeyLabel = document.createElement('span');
  siteKeyLabel.textContent = 'Site key:';
  siteKeyLabel.style.fontSize = '13px';
  siteKeyLabel.style.color = '#475569';

  const siteKeyValue = document.createElement('code');
  siteKeyValue.style.fontSize = '12px';
  siteKeyValue.style.padding = '4px 6px';
  siteKeyValue.style.borderRadius = '4px';
  siteKeyValue.style.background = '#f1f5f9';

  const revealButton = createButton({ label: 'Reveal' });
  const generateKeysButton = createButton({ label: 'Generate keys for install' });
  generateKeysButton.style.alignSelf = 'flex-start';

  siteKeyRow.append(siteKeyLabel, siteKeyValue, revealButton);

  const snippetValue = document.createElement('textarea');
  snippetValue.readOnly = true;
  snippetValue.rows = 3;
  snippetValue.style.width = '100%';
  snippetValue.style.borderRadius = '8px';
  snippetValue.style.border = '1px solid #e2e8f0';
  snippetValue.style.padding = '10px 12px';
  snippetValue.style.fontFamily = 'ui-monospace, SFMono-Regular, SFMono-Regular, Menlo, monospace';
  snippetValue.style.fontSize = '12px';
  snippetValue.style.color = '#1e293b';
  snippetValue.style.background = '#f8fafc';

  const copyButton = createButton({ label: 'Copy snippet' });
  copyButton.style.alignSelf = 'flex-start';

  const snippetHint = document.createElement('div');
  snippetHint.style.fontSize = '12px';
  snippetHint.style.color = '#64748b';

  const copyStatus = document.createElement('div');
  copyStatus.style.fontSize = '12px';
  copyStatus.style.color = '#15803d';

  const actionErrorText = document.createElement('div');
  actionErrorText.style.fontSize = '13px';
  actionErrorText.style.color = '#dc2626';

  const updateSnippet = () => {
    const baseUrl = API_BASE.replace(/\/$/, '');
    const key = state.rawSiteKey ? state.rawSiteKey.trim() : '';
    snippetValue.value = `<script async src="${baseUrl}/collector/tracker.js" data-site-key="${key}"></script>`;

    const masked = key ? '••••••' : '••••••';
    siteKeyValue.textContent = state.revealSiteKey && key ? key : masked;
    revealButton.disabled = !key;
    revealButton.textContent = state.revealSiteKey ? 'Hide' : 'Reveal';

    const missingKey = !key;
    copyButton.disabled = missingKey || state.keysLoading;
    generateKeysButton.style.display = missingKey ? 'inline-block' : 'none';
    generateKeysButton.disabled = state.keysLoading;
    generateKeysButton.textContent = state.keysLoading ? 'Generating...' : 'Generate keys for install';

    snippetHint.textContent = state.keysLoading
      ? 'Generating keys...'
      : missingKey
      ? 'Generate keys for install to populate data-site-key automatically.'
      : '';

    copyStatus.textContent = state.copyMessage;
    actionErrorText.textContent = state.actionError;
  };

  const updateKeys = ({ siteKey, widgetKey } = {}) => {
    state.rawSiteKey = siteKey || '';
    state.rawWidgetKey = widgetKey || '';
    updateSnippet();
  };

  const regenerateKeys = async (activeSiteId) => {
    if (client.sites?.regenerateKeys) {
      return client.sites.regenerateKeys(activeSiteId);
    }

    return client.request(`/sites/${activeSiteId}/keys/regenerate`, { method: 'POST' });
  };

  revealButton.addEventListener('click', () => {
    state.revealSiteKey = !state.revealSiteKey;
    updateSnippet();
  });

  generateKeysButton.addEventListener('click', async () => {
    state.keysLoading = true;
    state.actionError = '';
    updateSnippet();
    try {
      const response = await regenerateKeys(siteId);
      const siteKey = response?.siteKey || '';
      const widgetKey = response?.widgetKey || '';
      updateKeys({ siteKey, widgetKey });
      saveCachedKeys(siteId, { siteKey, widgetKey });
      notifier.show({ message: 'Keys generated.', variant: 'success' });
    } catch (error) {
      const uiError = mapApiError(error);
      state.actionError = uiError.message;
      notifier.show({ message: uiError.message, variant: 'danger' });
    } finally {
      state.keysLoading = false;
      updateSnippet();
    }
  });

  copyButton.addEventListener('click', async () => {
    try {
      await copyToClipboardRobust(snippetValue.value);
      state.copyMessage = 'Copied';
      state.actionError = '';
      updateSnippet();
      notifier.show({ message: 'Snippet copied to clipboard.', variant: 'success' });
      window.setTimeout(() => {
        state.copyMessage = '';
        copyStatus.textContent = '';
      }, 1500);
    } catch (error) {
      state.actionError = 'Unable to copy snippet.';
      updateSnippet();
      notifier.show({ message: 'Unable to copy snippet.', variant: 'danger' });
    }
  });

  step2Body.append(siteKeyRow, generateKeysButton, snippetValue, copyButton, snippetHint, copyStatus, actionErrorText);

  const step3Body = document.createElement('div');
  step3Body.style.display = 'flex';
  step3Body.style.flexDirection = 'column';
  step3Body.style.gap = '10px';

  const statusRow = document.createElement('div');
  statusRow.style.display = 'flex';
  statusRow.style.flexDirection = 'column';
  statusRow.style.alignItems = 'flex-start';
  statusRow.style.gap = '8px';
  statusRow.style.padding = '10px 12px';
  statusRow.style.border = '1px solid #e2e8f0';
  statusRow.style.borderRadius = '8px';
  statusRow.style.background = '#ffffff';

  const statusConfigured = document.createElement('div');
  const statusInstalled = document.createElement('div');
  const statusFirstEvent = document.createElement('div');
  const statusCheckedAt = document.createElement('div');
  const statusError = document.createElement('div');
  statusError.style.color = '#dc2626';

  const refreshStatusButton = createButton({ label: 'Refresh status', variant: 'primary' });
  const sendTestEventButton = createButton({ label: 'Send test event' });
  const verifyErrorText = document.createElement('div');
  verifyErrorText.style.fontSize = '13px';
  verifyErrorText.style.color = '#dc2626';

  statusRow.append(statusConfigured, statusInstalled, statusFirstEvent, statusCheckedAt, statusError);

  const updateStatusDisplay = () => {
    const status = state.status || {};
    statusConfigured.textContent = `Configured: ${status.isConfigured ? 'Yes' : 'No'}`;
    statusInstalled.textContent = `Installed: ${status.isInstalled ? 'Yes' : 'No'}`;
    statusFirstEvent.textContent = `First event received: ${status.firstEventReceivedAtUtc || 'Not yet'}`;
    statusCheckedAt.textContent = `Last checked: ${new Date().toISOString()}`;
    statusError.textContent = state.statusError;
    verifyErrorText.textContent = state.actionError;
    sendTestEventButton.disabled = !state.rawSiteKey || state.sendingTest;
    sendTestEventButton.textContent = state.sendingTest ? 'Sending...' : 'Send test event';
  };

  const loadInstallationStatus = async () => {
    refreshStatusButton.disabled = true;
    refreshStatusButton.textContent = 'Loading...';
    state.statusError = '';
    try {
      state.status = await client.request(`/sites/${siteId}/installation-status`);
    } catch (error) {
      const uiError = mapApiError(error);
      state.statusError = uiError.message;
    } finally {
      refreshStatusButton.disabled = false;
      refreshStatusButton.textContent = 'Refresh status';
      updateStatusDisplay();
    }
  };

  const sendTestEvent = async () => {
    if (!state.rawSiteKey) {
      return;
    }

    state.sendingTest = true;
    state.actionError = '';
    updateStatusDisplay();

    try {
      await client.request('/collector/events', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          siteKey: state.rawSiteKey,
          type: 'pageview',
          url: window.location.href || 'intentify://install-test',
          referrer: window.location.origin || null,
          tsUtc: new Date().toISOString(),
          data: {
            source: 'install-test',
          },
        }),
      });
      notifier.show({ message: 'Test event sent.', variant: 'success' });
    } catch (error) {
      const uiError = mapApiError(error);
      state.actionError = uiError.message;
      notifier.show({ message: uiError.message, variant: 'danger' });
    } finally {
      state.sendingTest = false;
      updateStatusDisplay();
    }
  };

  refreshStatusButton.addEventListener('click', loadInstallationStatus);
  sendTestEventButton.addEventListener('click', sendTestEvent);

  step3Body.append(refreshStatusButton, sendTestEventButton, statusRow, verifyErrorText);

  const card = createCard({
    title: 'Installation flow',
    body: (() => {
      const wrapper = document.createElement('div');
      wrapper.style.display = 'flex';
      wrapper.style.flexDirection = 'column';
      wrapper.style.gap = '16px';

      const step1Card = createCard({ title: 'Step 1: Allowed origins', body: step1Body });
      const step2Card = createCard({ title: 'Step 2: Snippet', body: step2Body });
      const step3Card = createCard({ title: 'Step 3: Verify', body: step3Body });
      wrapper.append(step1Card, step2Card, step3Card);
      return wrapper;
    })(),
  });

  page.append(header, stepsList, card);
  container.appendChild(page);

  const loadSite = async () => {
    state.siteLoading = true;
    try {
      const sites = await client.request('/sites');
      const site = (Array.isArray(sites) ? sites : []).find((item) => getSiteId(item) === siteId);
      state.site = site || null;
      state.origins = normalizeOrigins(site?.allowedOrigins || []);
    } catch (error) {
      const uiError = mapApiError(error);
      state.originsError = uiError.message;
    } finally {
      state.siteLoading = false;
      renderOrigins();
    }
  };

  renderOrigins();
  updateSnippet();
  updateStatusDisplay();
  loadInstallationStatus();
  loadSite();

  if (state.rawSiteKey && state.rawWidgetKey) {
    updateKeys({ siteKey: state.rawSiteKey, widgetKey: state.rawWidgetKey });
  }
};
