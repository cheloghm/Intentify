import { createCard, createInput, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';
import { API_BASE } from '../shared/config.js';

const copyToClipboard = async (value) => {
  if (navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(value);
    return;
  }

  const textarea = document.createElement('textarea');
  textarea.value = value;
  textarea.style.position = 'fixed';
  textarea.style.opacity = '0';
  document.body.appendChild(textarea);
  textarea.focus();
  textarea.select();
  document.execCommand('copy');
  textarea.remove();
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

export const renderInstallView = (container, { apiClient, toast, query } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();
  const siteId = query?.siteId;
  const domain = query?.domain || '';
  const storageKey = siteId ? `intentify:siteKeys:${siteId}` : '';

  if (!siteId) {
    const message = document.createElement('div');
    message.textContent = 'Missing site ID. Return to Sites and open Install again.';
    message.style.color = '#dc2626';
    message.style.fontSize = '14px';
    container.appendChild(message);
    return;
  }

  let storedKeys = null;
  if (storageKey) {
    try {
      const rawStored = sessionStorage.getItem(storageKey);
      if (rawStored) {
        const parsedStored = JSON.parse(rawStored);
        if (parsedStored && typeof parsedStored === 'object') {
          storedKeys = parsedStored;
        }
      }
    } catch (error) {
      storedKeys = null;
    }
  }

  const state = {
    siteKey: storedKeys?.siteKey || '',
    widgetKey: storedKeys?.widgetKey || '',
    copyMessage: '',
    status: null,
    statusError: '',
    verifyResult: null,
    verifyError: '',
    actionError: '',
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

  const instructions = document.createElement('div');
  instructions.style.display = 'flex';
  instructions.style.flexDirection = 'column';
  instructions.style.gap = '6px';
  const instructionTitle = document.createElement('div');
  instructionTitle.textContent = '1. Paste your site key';
  instructionTitle.style.fontWeight = '600';
  instructionTitle.style.fontSize = '14px';
  const instructionText = document.createElement('div');
  instructionText.textContent =
    'If you just created/regenerated keys, the site key is pre-filled. Otherwise paste it.';
  instructionText.style.fontSize = '13px';
  instructionText.style.color = '#64748b';
  instructions.append(instructionTitle, instructionText);

  const { wrapper: siteKeyWrapper, input: siteKeyInput } = createInput({
    label: 'Site key',
    placeholder: 'sk_live_••••••••',
  });
  if (state.siteKey) {
    siteKeyInput.value = state.siteKey;
  }

  const regenerateButton = createButton({ label: 'Regenerate keys' });
  regenerateButton.style.alignSelf = 'flex-start';

  const snippetTitle = document.createElement('div');
  snippetTitle.textContent = '2. Copy the snippet';
  snippetTitle.style.fontWeight = '600';
  snippetTitle.style.fontSize = '14px';

  const snippetDescription = document.createElement('div');
  snippetDescription.textContent = 'Add this tag to the <head> of your site.';
  snippetDescription.style.fontSize = '13px';
  snippetDescription.style.color = '#64748b';

  const testingTip = document.createElement('div');
  testingTip.textContent =
    'For local testing, add http://localhost:8088 to Allowed origins in Sites.';
  testingTip.style.fontSize = '13px';
  testingTip.style.color = '#64748b';

  const snippetBox = document.createElement('div');
  snippetBox.style.display = 'flex';
  snippetBox.style.flexDirection = 'column';
  snippetBox.style.gap = '10px';

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

  const copyStatus = document.createElement('div');
  copyStatus.style.fontSize = '12px';
  copyStatus.style.color = '#15803d';

  const actionErrorText = document.createElement('div');
  actionErrorText.style.fontSize = '13px';
  actionErrorText.style.color = '#dc2626';

  const updateSnippet = () => {
    const baseUrl = API_BASE.replace(/\/$/, '');
    const key = siteKeyInput.value.trim();
    state.siteKey = key;
    snippetValue.value = `<script async src="${baseUrl}/collector/tracker.js" data-site-key="${key}"></script>`;
    copyButton.disabled = !key;
    copyStatus.textContent = state.copyMessage;
    actionErrorText.textContent = state.actionError;
  };

  siteKeyInput.addEventListener('input', updateSnippet);
  updateSnippet();

  copyButton.addEventListener('click', async () => {
    try {
      await copyToClipboard(snippetValue.value);
      state.copyMessage = 'Copied';
      state.actionError = '';
      copyStatus.textContent = state.copyMessage;
      notifier.show({ message: 'Snippet copied to clipboard.', variant: 'success' });
      window.setTimeout(() => {
        state.copyMessage = '';
        copyStatus.textContent = '';
      }, 1500);
    } catch (error) {
      state.actionError = 'Unable to copy snippet.';
      actionErrorText.textContent = state.actionError;
      notifier.show({ message: 'Unable to copy snippet.', variant: 'danger' });
    }
  });

  regenerateButton.addEventListener('click', async () => {
    regenerateButton.disabled = true;
    regenerateButton.textContent = 'Regenerating...';
    state.actionError = '';
    actionErrorText.textContent = '';
    state.copyMessage = '';
    copyStatus.textContent = '';
    try {
      const response = await client.request(`/sites/${siteId}/keys/regenerate`, { method: 'POST' });
      state.siteKey = response?.siteKey || '';
      state.widgetKey = response?.widgetKey || '';
      siteKeyInput.value = state.siteKey;
      if (storageKey) {
        sessionStorage.setItem(
          storageKey,
          JSON.stringify({ siteKey: state.siteKey, widgetKey: state.widgetKey })
        );
      }
      updateSnippet();
      renderVerification();
      notifier.show({ message: 'Keys regenerated.', variant: 'success' });
    } catch (error) {
      const uiError = mapApiError(error);
      state.actionError = uiError.message;
      actionErrorText.textContent = state.actionError;
    } finally {
      regenerateButton.disabled = false;
      regenerateButton.textContent = 'Regenerate keys';
    }
  });

  snippetBox.append(snippetValue, copyButton, copyStatus, actionErrorText);

  const statusTitle = document.createElement('div');
  statusTitle.textContent = '3. Verify installation';
  statusTitle.style.fontWeight = '600';
  statusTitle.style.fontSize = '14px';

  const statusDescription = document.createElement('div');
  statusDescription.textContent = 'Current installation status for this site.';
  statusDescription.style.fontSize = '13px';
  statusDescription.style.color = '#64748b';

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
  statusConfigured.style.fontSize = '13px';
  statusInstalled.style.fontSize = '13px';
  statusFirstEvent.style.fontSize = '13px';
  statusCheckedAt.style.fontSize = '13px';
  statusError.style.fontSize = '13px';
  statusError.style.color = '#dc2626';

  const refreshStatusButton = createButton({ label: 'Refresh status', variant: 'primary' });

  statusRow.append(statusConfigured, statusInstalled, statusFirstEvent, statusCheckedAt, statusError);

  const updateStatusDisplay = () => {
    const status = state.status || {};
    statusConfigured.textContent = `Configured: ${status.isConfigured ? 'Yes' : 'No'}`;
    statusInstalled.textContent = `Installed: ${status.isInstalled ? 'Yes' : 'No'}`;
    statusFirstEvent.textContent = `First event received: ${
      status.firstEventReceivedAtUtc || 'Not yet'
    }`;
    statusCheckedAt.textContent = `Last checked: ${new Date().toISOString()}`;
    statusError.textContent = state.statusError;
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

  refreshStatusButton.addEventListener('click', loadInstallationStatus);

  const verifySection = document.createElement('div');
  verifySection.style.display = 'flex';
  verifySection.style.flexDirection = 'column';
  verifySection.style.gap = '8px';

  const verifyHelper = document.createElement('div');
  verifyHelper.style.fontSize = '13px';
  verifyHelper.style.color = '#64748b';

  const testStatusButton = createButton({ label: 'Test status' });
  testStatusButton.style.alignSelf = 'flex-start';

  const verifyErrorText = document.createElement('div');
  verifyErrorText.style.fontSize = '13px';
  verifyErrorText.style.color = '#dc2626';

  const verifyPre = document.createElement('pre');
  verifyPre.style.margin = '0';
  verifyPre.style.padding = '10px 12px';
  verifyPre.style.border = '1px solid #e2e8f0';
  verifyPre.style.borderRadius = '8px';
  verifyPre.style.background = '#f8fafc';
  verifyPre.style.fontSize = '12px';
  verifyPre.style.display = 'none';

  const renderVerification = () => {
    verifyPre.style.display = state.verifyResult ? 'block' : 'none';
    verifyPre.textContent = state.verifyResult ? JSON.stringify(state.verifyResult, null, 2) : '';
    verifyErrorText.textContent = state.verifyError;
    if (!state.widgetKey) {
      verifyHelper.textContent = 'Regenerate keys to get widget key for verification.';
      testStatusButton.style.display = 'none';
      return;
    }
    verifyHelper.textContent = `Widget key ready: ${state.widgetKey}`;
    testStatusButton.style.display = 'inline-block';
  };

  testStatusButton.addEventListener('click', async () => {
    if (!state.widgetKey) {
      return;
    }
    testStatusButton.disabled = true;
    testStatusButton.textContent = 'Testing...';
    state.verifyError = '';
    try {
      const response = await fetch(
        `${API_BASE.replace(/\/$/, '')}/sites/installation/status?widgetKey=${encodeURIComponent(
          state.widgetKey
        )}`
      );
      if (!response.ok) {
        throw new Error(`Request failed with status ${response.status}`);
      }
      state.verifyResult = await response.json();
    } catch (error) {
      state.verifyError = error.message || 'Unable to verify widget key status.';
    } finally {
      testStatusButton.disabled = false;
      testStatusButton.textContent = 'Test status';
      renderVerification();
    }
  });

  verifySection.append(verifyHelper, testStatusButton, verifyErrorText, verifyPre);
  renderVerification();

  const installBody = document.createElement('div');
  installBody.style.display = 'flex';
  installBody.style.flexDirection = 'column';
  installBody.style.gap = '14px';
  installBody.append(
    instructions,
    siteKeyWrapper,
    regenerateButton,
    snippetTitle,
    snippetDescription,
    testingTip,
    snippetBox
  );

  const installCard = createCard({
    title: 'Installation snippet',
    body: installBody,
  });

  const statusBody = document.createElement('div');
  statusBody.style.display = 'flex';
  statusBody.style.flexDirection = 'column';
  statusBody.style.gap = '10px';
  statusBody.append(statusTitle, statusDescription, refreshStatusButton, statusRow, verifySection);

  const statusCard = createCard({
    title: 'Verification',
    body: statusBody,
  });

  page.append(header, installCard, statusCard);
  container.appendChild(page);
  updateStatusDisplay();
  loadInstallationStatus();
};
