import { createCard, createInput, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';
import { API_BASE } from '../shared/config.js';

const POLL_INTERVAL_MS = 2000;
const POLL_TIMEOUT_MS = 60000;

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

const wait = (duration) => new Promise((resolve) => setTimeout(resolve, duration));

export const renderInstallView = (container, { apiClient, toast, query } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();
  const siteId = query?.siteId || '';
  const domain = query?.domain || '';

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
    'Keys are shown once after creation. Paste the site key below to generate the snippet.';
  instructionText.style.fontSize = '13px';
  instructionText.style.color = '#64748b';
  instructions.append(instructionTitle, instructionText);

  const { wrapper: siteKeyWrapper, input: siteKeyInput } = createInput({
    label: 'Site key',
    placeholder: 'sk_live_••••••••',
  });

  const snippetTitle = document.createElement('div');
  snippetTitle.textContent = '2. Copy the snippet';
  snippetTitle.style.fontWeight = '600';
  snippetTitle.style.fontSize = '14px';

  const snippetDescription = document.createElement('div');
  snippetDescription.textContent = 'Add this tag to the <head> of your site.';
  snippetDescription.style.fontSize = '13px';
  snippetDescription.style.color = '#64748b';

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

  const updateSnippet = () => {
    const baseUrl = API_BASE.replace(/\/$/, '');
    const key = siteKeyInput.value.trim() || 'YOUR_SITE_KEY';
    snippetValue.value = `<script async src="${baseUrl}/collector/tracker.js" data-site-key="${key}"></script>`;
    copyButton.disabled = !siteKeyInput.value.trim();
  };

  siteKeyInput.addEventListener('input', updateSnippet);
  updateSnippet();

  copyButton.addEventListener('click', async () => {
    try {
      await copyToClipboard(snippetValue.value);
      notifier.show({ message: 'Snippet copied to clipboard.', variant: 'success' });
    } catch (error) {
      notifier.show({ message: 'Unable to copy snippet.', variant: 'danger' });
    }
  });

  snippetBox.append(snippetValue, copyButton);

  const statusTitle = document.createElement('div');
  statusTitle.textContent = '3. Verify installation';
  statusTitle.style.fontWeight = '600';
  statusTitle.style.fontSize = '14px';

  const statusDescription = document.createElement('div');
  statusDescription.textContent = 'We will check for the first event from your site.';
  statusDescription.style.fontSize = '13px';
  statusDescription.style.color = '#64748b';

  const statusRow = document.createElement('div');
  statusRow.style.display = 'flex';
  statusRow.style.alignItems = 'center';
  statusRow.style.justifyContent = 'space-between';
  statusRow.style.gap = '12px';
  statusRow.style.padding = '10px 12px';
  statusRow.style.border = '1px solid #e2e8f0';
  statusRow.style.borderRadius = '8px';
  statusRow.style.background = '#ffffff';

  const statusText = document.createElement('div');
  statusText.style.fontSize = '13px';
  statusText.style.color = '#475569';
  statusText.textContent = siteId
    ? 'Ready to verify installation.'
    : 'Missing site ID. Return to Sites and open Install again.';

  const verifyButton = createButton({ label: 'Verify installation', variant: 'primary' });
  verifyButton.disabled = !siteId;

  statusRow.append(statusText, verifyButton);

  let pollInProgress = false;

  const setStatus = (message, variant = 'neutral') => {
    statusText.textContent = message;
    if (variant === 'success') {
      statusText.style.color = '#15803d';
    } else if (variant === 'warning') {
      statusText.style.color = '#b45309';
    } else if (variant === 'danger') {
      statusText.style.color = '#dc2626';
    } else {
      statusText.style.color = '#475569';
    }
  };

  const pollInstallationStatus = async () => {
    if (!siteId || pollInProgress) {
      return;
    }

    pollInProgress = true;
    verifyButton.disabled = true;
    verifyButton.textContent = 'Checking...';

    const startTime = Date.now();

    while (Date.now() - startTime < POLL_TIMEOUT_MS) {
      if (!container.isConnected) {
        pollInProgress = false;
        return;
      }

      try {
        const status = await client.request(`/sites/${siteId}/installation-status`);
        const isConfigured = Boolean(status?.isConfigured);
        const isInstalled = Boolean(status?.isInstalled);

        if (!isConfigured) {
          setStatus('Not configured. Add allowed origins in the Sites page.', 'warning');
        } else if (isInstalled) {
          setStatus('Installed ✅', 'success');
          pollInProgress = false;
          verifyButton.disabled = false;
          verifyButton.textContent = 'Verify installation';
          return;
        } else {
          setStatus('Waiting for first event…', 'neutral');
        }
      } catch (error) {
        const uiError = mapApiError(error);
        setStatus(uiError.message, 'danger');
      }

      await wait(POLL_INTERVAL_MS);
    }

    setStatus('Still waiting for installation. Check again in a moment.', 'warning');
    pollInProgress = false;
    verifyButton.disabled = false;
    verifyButton.textContent = 'Verify installation';
  };

  verifyButton.addEventListener('click', pollInstallationStatus);

  const installBody = document.createElement('div');
  installBody.style.display = 'flex';
  installBody.style.flexDirection = 'column';
  installBody.style.gap = '14px';
  installBody.append(instructions, siteKeyWrapper, snippetTitle, snippetDescription, snippetBox);

  const installCard = createCard({
    title: 'Installation snippet',
    body: installBody,
  });

  const statusBody = document.createElement('div');
  statusBody.style.display = 'flex';
  statusBody.style.flexDirection = 'column';
  statusBody.style.gap = '10px';
  statusBody.append(statusTitle, statusDescription, statusRow);

  const statusCard = createCard({
    title: 'Verification',
    body: statusBody,
  });

  page.append(header, installCard, statusCard);
  container.appendChild(page);

  if (siteId) {
    pollInstallationStatus();
  }
};
