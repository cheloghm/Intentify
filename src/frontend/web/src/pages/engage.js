import { createCard, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

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

  chatForm.append(chatInput, sendButton);
  chatBody.append(transcript, chatForm);

  const conversationsBody = document.createElement('div');
  conversationsBody.style.display = 'grid';
  conversationsBody.style.gridTemplateColumns = '280px 1fr';
  conversationsBody.style.gap = '12px';

  const sessionsList = document.createElement('div');
  sessionsList.style.display = 'flex';
  sessionsList.style.flexDirection = 'column';
  sessionsList.style.gap = '8px';

  const messagesPanel = document.createElement('div');
  messagesPanel.style.display = 'flex';
  messagesPanel.style.flexDirection = 'column';
  messagesPanel.style.gap = '8px';

  conversationsBody.append(sessionsList, messagesPanel);

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
        await loadConversationMessages();
      });
      sessionsList.appendChild(button);
    });
  };

  const renderConversationMessages = () => {
    messagesPanel.innerHTML = '';
    if (!state.selectedSessionId) {
      const empty = document.createElement('div');
      empty.textContent = 'Select a conversation to view messages.';
      empty.style.color = '#64748b';
      messagesPanel.appendChild(empty);
      return;
    }

    if (!state.selectedMessages.length) {
      const empty = document.createElement('div');
      empty.textContent = 'No messages for this session.';
      empty.style.color = '#64748b';
      messagesPanel.appendChild(empty);
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
      messagesPanel.appendChild(row);
    });
  };

  const loadConversations = async () => {
    if (!state.siteId) {
      state.conversations = [];
      state.selectedSessionId = '';
      state.selectedMessages = [];
      renderConversations();
      renderConversationMessages();
      return;
    }

    try {
      state.conversations = await client.engage.listConversations(state.siteId);
      if (!state.selectedSessionId && state.conversations[0]) {
        state.selectedSessionId = state.conversations[0].sessionId;
      }
      renderConversations();
      await loadConversationMessages();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  const loadConversationMessages = async () => {
    if (!state.selectedSessionId) {
      state.selectedMessages = [];
      renderConversationMessages();
      return;
    }

    try {
      state.selectedMessages = await client.engage.getConversationMessages(state.selectedSessionId);
      renderConversations();
      renderConversationMessages();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  siteSelect.addEventListener('change', async () => {
    state.siteId = siteSelect.value;
    const site = state.sites.find((item) => getSiteId(item) === state.siteId);
    state.widgetKey = site?.widgetKey || '';
    widgetValue.textContent = state.widgetKey || 'select site';
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

    state.transcript.push({ role: 'user', content: message });
    renderTranscript();
    chatInput.value = '';

    try {
      const response = await client.engage.sendChat({
        widgetKey: state.widgetKey,
        sessionId: state.sessionId || null,
        message,
      });
      state.sessionId = response.sessionId || state.sessionId;
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
        state.widgetKey = state.sites[0].widgetKey || '';
      }
      setSiteOptions();
      widgetValue.textContent = state.widgetKey || 'select site';
      await loadConversations();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  page.append(
    header,
    createCard({ title: 'Bot Config', body: configBody }),
    createCard({ title: 'Test Chat', body: chatBody }),
    createCard({ title: 'Conversations', body: conversationsBody })
  );

  container.appendChild(page);

  renderTranscript();
  renderConversations();
  renderConversationMessages();
  void loadSites();
};
