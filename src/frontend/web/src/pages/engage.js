import { createCard, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';
import { API_BASE } from '../shared/config.js';

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

const getSiteId = (site) => site?.siteId || site?.id || '';

const conversationTimestampFields = ['updatedAtUtc', 'updatedAt', 'createdAtUtc', 'createdAt'];

const getConversationTimestamp = (conversation) => {
  for (const field of conversationTimestampFields) {
    const rawValue = conversation?.[field];
    if (!rawValue) {
      continue;
    }

    const timestamp = new Date(rawValue).getTime();
    if (!Number.isNaN(timestamp)) {
      return timestamp;
    }
  }

  return null;
};

const sortConversationsNewestFirst = (conversations) => {
  if (!Array.isArray(conversations) || !conversations.length) {
    return [];
  }

  return [...conversations].sort((left, right) => {
    const rightTimestamp = getConversationTimestamp(right);
    const leftTimestamp = getConversationTimestamp(left);

    if (rightTimestamp === null && leftTimestamp === null) {
      return 0;
    }

    if (rightTimestamp === null) {
      return 1;
    }

    if (leftTimestamp === null) {
      return -1;
    }

    return rightTimestamp - leftTimestamp;
  });
};

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

export const renderEngageView = (container, { apiClient, toast } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();

  const state = {
    sites: [],
    siteId: '',
    widgetKey: '',
    sessionId: '',
    transcript: [],
    conversations: [],
    selectedSessionId: '',
    selectedMessages: [],
    revealWidgetKey: false,
  };

  const page = document.createElement('div');
  page.style.display = 'flex';
  page.style.flexDirection = 'column';
  page.style.gap = '20px';
  page.style.width = '100%';
  page.style.maxWidth = '980px';

  const header = document.createElement('div');
  const title = document.createElement('h2');
  title.textContent = 'Engage';
  title.style.margin = '0';
  const subtitle = document.createElement('p');
  subtitle.textContent = 'Configure bot context, run test chats, and review conversations.';
  subtitle.style.margin = '6px 0 0';
  subtitle.style.color = '#64748b';
  header.append(title, subtitle);

  const configBody = document.createElement('div');
  configBody.style.display = 'grid';
  configBody.style.gridTemplateColumns = '1fr 1fr';
  configBody.style.gap = '10px';

  const siteField = document.createElement('label');
  siteField.style.display = 'flex';
  siteField.style.flexDirection = 'column';
  siteField.style.gap = '6px';

  const siteLabel = document.createElement('span');
  siteLabel.textContent = 'Site';
  siteLabel.style.fontSize = '13px';

  const siteSelect = document.createElement('select');
  siteSelect.style.padding = '8px 10px';
  siteSelect.style.borderRadius = '6px';
  siteSelect.style.border = '1px solid #cbd5e1';
  siteSelect.style.background = '#ffffff';

  siteField.append(siteLabel, siteSelect);

  const widgetWrap = document.createElement('div');
  widgetWrap.style.display = 'flex';
  widgetWrap.style.flexDirection = 'column';
  widgetWrap.style.gap = '6px';
  const widgetLabel = document.createElement('span');
  widgetLabel.textContent = 'Widget key';
  widgetLabel.style.fontSize = '13px';
  const widgetValue = document.createElement('div');
  widgetValue.style.padding = '8px 10px';
  widgetValue.style.borderRadius = '6px';
  widgetValue.style.border = '1px solid #e2e8f0';
  widgetValue.style.background = '#f8fafc';
  widgetValue.textContent = 'select site';
  widgetWrap.append(widgetLabel, widgetValue);

  configBody.append(siteField, widgetWrap);

  const installBody = document.createElement('div');
  installBody.style.display = 'flex';
  installBody.style.flexDirection = 'column';
  installBody.style.gap = '10px';

  const installKeyRow = document.createElement('div');
  installKeyRow.style.display = 'flex';
  installKeyRow.style.alignItems = 'center';
  installKeyRow.style.gap = '8px';

  const installKeyLabel = document.createElement('span');
  installKeyLabel.textContent = 'Widget key:';
  installKeyLabel.style.fontSize = '13px';
  installKeyLabel.style.color = '#475569';

  const installKeyValue = document.createElement('code');
  installKeyValue.style.fontSize = '12px';
  installKeyValue.style.padding = '4px 6px';
  installKeyValue.style.borderRadius = '4px';
  installKeyValue.style.background = '#f1f5f9';

  const toggleWidgetKeyButton = createButton({ label: 'Reveal' });
  const copyWidgetKeyButton = createButton({ label: 'Copy key' });
  installKeyRow.append(installKeyLabel, installKeyValue, toggleWidgetKeyButton, copyWidgetKeyButton);

  const snippetValue = document.createElement('textarea');
  snippetValue.readOnly = true;
  snippetValue.rows = 1;
  snippetValue.style.width = '100%';
  snippetValue.style.borderRadius = '8px';
  snippetValue.style.border = '1px solid #e2e8f0';
  snippetValue.style.padding = '10px 12px';
  snippetValue.style.fontFamily = 'ui-monospace, SFMono-Regular, SFMono-Regular, Menlo, monospace';
  snippetValue.style.fontSize = '12px';
  snippetValue.style.color = '#1e293b';
  snippetValue.style.background = '#f8fafc';

  const copySnippetButton = createButton({ label: 'Copy snippet' });
  copySnippetButton.style.alignSelf = 'flex-start';

  const installInstructions = document.createElement('ul');
  installInstructions.style.margin = '0';
  installInstructions.style.paddingLeft = '18px';
  installInstructions.style.color = '#475569';
  installInstructions.style.fontSize = '13px';

  const htmlInstruction = document.createElement('li');
  htmlInstruction.textContent = 'Paste before </body>';
  const wordpressInstruction = document.createElement('li');
  wordpressInstruction.textContent = 'WordPress: add to footer or a header/footer injection plugin';
  installInstructions.append(htmlInstruction, wordpressInstruction);

  const updateInstallCard = () => {
    const hasSite = !!state.siteId;
    const key = state.widgetKey ? state.widgetKey.trim() : '';
    const baseUrl = API_BASE.replace(/\/+$/, '');
    const masked = key ? '••••••' : '••••••';

    installKeyValue.textContent = state.revealWidgetKey && key ? key : masked;
    toggleWidgetKeyButton.disabled = !hasSite || !key;
    toggleWidgetKeyButton.textContent = state.revealWidgetKey ? 'Hide' : 'Reveal';
    copyWidgetKeyButton.disabled = !hasSite || !key;
    copySnippetButton.disabled = !hasSite || !key;
    snippetValue.value = `<script async src="${baseUrl}/engage/widget.js" data-widget-key="${key}"></script>`;
  };

  toggleWidgetKeyButton.addEventListener('click', () => {
    state.revealWidgetKey = !state.revealWidgetKey;
    updateInstallCard();
  });

  copyWidgetKeyButton.addEventListener('click', async () => {
    try {
      await copyToClipboardRobust(state.widgetKey || '');
      notifier.show({ message: 'Widget key copied.', variant: 'success' });
    } catch (error) {
      notifier.show({ message: 'Unable to copy widget key.', variant: 'danger' });
    }
  });

  copySnippetButton.addEventListener('click', async () => {
    try {
      await copyToClipboardRobust(snippetValue.value);
      notifier.show({ message: 'Snippet copied to clipboard.', variant: 'success' });
    } catch (error) {
      notifier.show({ message: 'Unable to copy snippet.', variant: 'danger' });
    }
  });

  installBody.append(installKeyRow, snippetValue, copySnippetButton, installInstructions);

  const chatBody = document.createElement('div');
  chatBody.style.display = 'flex';
  chatBody.style.flexDirection = 'column';
  chatBody.style.gap = '10px';

  const transcript = document.createElement('div');
  transcript.style.display = 'flex';
  transcript.style.flexDirection = 'column';
  transcript.style.gap = '8px';

  const chatForm = document.createElement('form');
  chatForm.style.display = 'flex';
  chatForm.style.gap = '8px';

  const chatInput = document.createElement('input');
  chatInput.type = 'text';
  chatInput.placeholder = 'Type a message';
  chatInput.style.flex = '1';
  chatInput.style.padding = '8px 10px';
  chatInput.style.borderRadius = '6px';
  chatInput.style.border = '1px solid #cbd5e1';

  const sendButton = createButton({ label: 'Send', variant: 'primary', type: 'submit' });
  sendButton.type = 'submit';

  chatForm.append(chatInput, sendButton);
  chatBody.append(transcript, chatForm);

  const conversationsBody = document.createElement('div');
  conversationsBody.style.display = 'flex';
  conversationsBody.style.flexDirection = 'column';
  conversationsBody.style.gap = '12px';

  const sessionsList = document.createElement('div');
  sessionsList.style.display = 'flex';
  sessionsList.style.flexDirection = 'column';
  sessionsList.style.gap = '8px';

  conversationsBody.appendChild(sessionsList);

  const modalOverlay = document.createElement('div');
  modalOverlay.style.position = 'fixed';
  modalOverlay.style.inset = '0';
  modalOverlay.style.background = 'rgba(15, 23, 42, 0.55)';
  modalOverlay.style.display = 'none';
  modalOverlay.style.alignItems = 'center';
  modalOverlay.style.justifyContent = 'center';
  modalOverlay.style.zIndex = '999999';
  modalOverlay.style.padding = '20px';

  const modal = document.createElement('div');
  modal.style.width = '100%';
  modal.style.maxWidth = '760px';
  modal.style.maxHeight = '85vh';
  modal.style.background = '#ffffff';
  modal.style.borderRadius = '12px';
  modal.style.border = '1px solid #e2e8f0';
  modal.style.display = 'flex';
  modal.style.flexDirection = 'column';
  modal.style.overflow = 'hidden';

  const modalHeader = document.createElement('div');
  modalHeader.style.display = 'flex';
  modalHeader.style.alignItems = 'center';
  modalHeader.style.justifyContent = 'space-between';
  modalHeader.style.gap = '8px';
  modalHeader.style.padding = '12px 14px';
  modalHeader.style.borderBottom = '1px solid #e2e8f0';

  const modalTitle = document.createElement('div');
  modalTitle.style.fontWeight = '600';
  modalTitle.style.color = '#0f172a';
  modalTitle.textContent = 'Conversation';

  const modalActions = document.createElement('div');
  modalActions.style.display = 'flex';
  modalActions.style.alignItems = 'center';
  modalActions.style.gap = '8px';

  const jumpOldestButton = createButton({ label: 'Jump to oldest' });
  const jumpLatestButton = createButton({ label: 'Jump to latest' });
  const closeModalButton = createButton({ label: 'Close' });

  modalActions.append(jumpOldestButton, jumpLatestButton, closeModalButton);
  modalHeader.append(modalTitle, modalActions);

  const modalMessages = document.createElement('div');
  modalMessages.style.padding = '12px 14px';
  modalMessages.style.overflowY = 'auto';
  modalMessages.style.display = 'flex';
  modalMessages.style.flexDirection = 'column';
  modalMessages.style.gap = '8px';

  modal.append(modalHeader, modalMessages);
  modalOverlay.appendChild(modal);
  document.body.appendChild(modalOverlay);

  const setSiteOptions = () => {
    siteSelect.innerHTML = '';
    const placeholder = document.createElement('option');
    placeholder.value = '';
    placeholder.textContent = state.sites.length ? 'Select a site' : 'No sites available';
    siteSelect.appendChild(placeholder);

    state.sites.forEach((site) => {
      const option = document.createElement('option');
      option.value = getSiteId(site);
      option.textContent = site.domain || getSiteId(site);
      siteSelect.appendChild(option);
    });

    siteSelect.value = state.siteId;
  };

  const renderTranscript = () => {
    transcript.innerHTML = '';
    if (!state.transcript.length) {
      const empty = document.createElement('div');
      empty.textContent = 'No messages yet.';
      empty.style.color = '#64748b';
      transcript.appendChild(empty);
      return;
    }

    state.transcript.forEach((entry) => {
      const bubble = document.createElement('div');
      bubble.style.alignSelf = entry.role === 'user' ? 'flex-end' : 'flex-start';
      bubble.style.maxWidth = '80%';
      bubble.style.padding = '8px 10px';
      bubble.style.borderRadius = '8px';
      bubble.style.background = entry.role === 'user' ? '#dbeafe' : '#f1f5f9';
      bubble.style.color = '#0f172a';
      bubble.style.whiteSpace = 'pre-wrap';
      bubble.textContent = entry.content;
      transcript.appendChild(bubble);
    });
  };

  const renderConversations = () => {
    sessionsList.innerHTML = '';
    if (!state.conversations.length) {
      const empty = document.createElement('div');
      empty.textContent = state.siteId ? 'No conversations yet.' : 'Select a site first.';
      empty.style.color = '#64748b';
      sessionsList.appendChild(empty);
      return;
    }

    state.conversations.forEach((conversation) => {
      const button = createButton({ label: conversation.sessionId });
      button.style.textAlign = 'left';
      if (conversation.sessionId === state.selectedSessionId) {
        button.style.background = '#eff6ff';
        button.style.borderColor = '#bfdbfe';
      }
      button.addEventListener('click', async () => {
        state.selectedSessionId = conversation.sessionId;
        await loadConversationMessages({ openModal: true });
      });
      sessionsList.appendChild(button);
    });
  };

  const renderConversationMessagesModal = () => {
    modalMessages.innerHTML = '';

    modalTitle.textContent = state.selectedSessionId
      ? `Conversation ${state.selectedSessionId}`
      : 'Conversation';

    if (!state.selectedMessages.length) {
      const empty = document.createElement('div');
      empty.textContent = 'No messages for this session.';
      empty.style.color = '#64748b';
      modalMessages.appendChild(empty);
      return;
    }

    state.selectedMessages.forEach((message) => {
      const row = document.createElement('div');
      row.style.border = '1px solid #e2e8f0';
      row.style.borderRadius = '8px';
      row.style.padding = '10px 12px';
      row.style.background = '#ffffff';

      const meta = document.createElement('div');
      meta.textContent = `${message.role} • ${new Date(message.createdAtUtc).toLocaleString()}`;
      meta.style.fontSize = '12px';
      meta.style.color = '#64748b';

      const content = document.createElement('div');
      content.textContent = message.content;
      content.style.marginTop = '6px';
      content.style.whiteSpace = 'pre-wrap';

      row.append(meta, content);
      modalMessages.appendChild(row);
    });
  };

  const closeModal = () => {
    modalOverlay.style.display = 'none';
  };

  const openModal = () => {
    modalOverlay.style.display = 'flex';
    modalMessages.scrollTop = modalMessages.scrollHeight;
  };

  closeModalButton.addEventListener('click', closeModal);
  modalOverlay.addEventListener('click', (event) => {
    if (event.target === modalOverlay) {
      closeModal();
    }
  });

  jumpOldestButton.addEventListener('click', () => {
    modalMessages.scrollTop = 0;
  });

  jumpLatestButton.addEventListener('click', () => {
    modalMessages.scrollTop = modalMessages.scrollHeight;
  });

  const loadConversations = async () => {
    if (!state.siteId) {
      state.conversations = [];
      state.selectedSessionId = '';
      state.selectedMessages = [];
      closeModal();
      renderConversations();
      return;
    }

    try {
      state.conversations = sortConversationsNewestFirst(
        await client.engage.getConversations(state.siteId)
      );
      const selectedExists = state.conversations.some(
        (conversation) => conversation.sessionId === state.selectedSessionId
      );
      if ((!state.selectedSessionId || !selectedExists) && state.conversations[0]) {
        state.selectedSessionId = state.conversations[0].sessionId;
      }
      if (!state.conversations[0]) {
        state.selectedSessionId = '';
      }
      renderConversations();
      if (state.selectedSessionId) {
        await loadConversationMessages();
      } else {
        state.selectedMessages = [];
      }
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  const loadConversationMessages = async ({ openModal: shouldOpenModal = false } = {}) => {
    if (!state.siteId || !state.selectedSessionId) {
      state.selectedMessages = [];
      return;
    }

    try {
      state.selectedMessages = (
        await client.engage.getConversationMessages(state.selectedSessionId, state.siteId)
      ).sort(
        (left, right) =>
          new Date(left.createdAtUtc).getTime() - new Date(right.createdAtUtc).getTime()
      );
      renderConversations();
      renderConversationMessagesModal();
      if (shouldOpenModal) {
        openModal();
      }
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  const loadSiteWidgetKey = async (siteId) => {
    if (!siteId) {
      state.widgetKey = '';
      widgetValue.textContent = 'select site';
      updateInstallCard();
      return;
    }

    try {
      const keys = await client.sites.getKeys(siteId);
      state.widgetKey = keys?.widgetKey || '';
      widgetValue.textContent = state.widgetKey || 'select site';
      updateInstallCard();
    } catch (error) {
      state.widgetKey = '';
      widgetValue.textContent = 'unavailable';
      updateInstallCard();
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  siteSelect.addEventListener('change', async () => {
    state.siteId = siteSelect.value;
    state.revealWidgetKey = false;
    state.selectedSessionId = '';
    state.selectedMessages = [];
    state.conversations = [];
    state.sessionId = '';
    state.transcript = [];
    closeModal();
    await loadSiteWidgetKey(state.siteId);
    renderTranscript();
    renderConversations();
    await loadConversations();
  });

  chatForm.addEventListener('submit', async (event) => {
    event.preventDefault();
    if (!state.widgetKey) {
      notifier.show({ message: 'Select a site with a widget key.', variant: 'warning' });
      return;
    }

    const message = chatInput.value.trim();
    if (!message) {
      return;
    }

    sendButton.disabled = true;
    sendButton.textContent = 'Sending...';

    chatInput.value = '';

    try {
      const response = await client.engage.sendChat(
        state.widgetKey,
        state.sessionId || null,
        message
      );
      state.sessionId = response.sessionId || state.sessionId;
      state.transcript.push({ role: 'user', content: message });
      state.transcript.push({ role: 'assistant', content: response.response || '' });
      renderTranscript();
      await loadConversations();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    } finally {
      sendButton.disabled = false;
      sendButton.textContent = 'Send';
    }
  });

  const loadSites = async () => {
    try {
      state.sites = await client.sites.list();
      if (state.sites[0]) {
        state.siteId = getSiteId(state.sites[0]);
      }
      setSiteOptions();
      await loadSiteWidgetKey(state.siteId);
      await loadConversations();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  page.append(
    header,
    createCard({ title: 'Bot Config', body: configBody }),
    createCard({ title: 'Install Engage', body: installBody }),
    createCard({ title: 'Test Chat', body: chatBody }),
    createCard({ title: 'Conversations', body: conversationsBody })
  );

  container.appendChild(page);

  renderTranscript();
  renderConversations();
  updateInstallCard();
  void loadSites();
};
