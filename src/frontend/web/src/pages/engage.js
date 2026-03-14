import { createButton, createCard, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const getSiteId = (site) => site?.siteId || site?.id || '';

const conversationTimestampFields = [
  'updatedAtUtc',
  'updatedAt',
  'createdAtUtc',
  'createdAt',
  'lastActivityAtUtc',
  'lastActivityAt',
];

const getConversationTimestamp = (conversation) => {
  for (const field of conversationTimestampFields) {
    const rawValue = conversation?.[field];
    if (!rawValue) {
      continue;
    }

    const value = new Date(rawValue).getTime();
    if (!Number.isNaN(value)) {
      return value;
    }
  }

  return null;
};

const sortConversationsNewestFirst = (conversations) => {
  if (!Array.isArray(conversations) || !conversations.length) {
    return [];
  }

  const hasSortableTimestamp = conversations.some((conversation) =>
    conversationTimestampFields.some((field) => {
      const rawValue = conversation?.[field];
      if (!rawValue) {
        return false;
      }

      return !Number.isNaN(new Date(rawValue).getTime());
    })
  );

  if (!hasSortableTimestamp) {
    return conversations;
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


const formatConfidence = (value) => {
  const numeric = Number(value);
  if (!Number.isFinite(numeric)) {
    return 'n/a';
  }

  return `${Math.round(numeric * 100)}%`;
};

const buildRecommendationTargetSummary = (targetRefs) => {
  if (!targetRefs || typeof targetRefs !== 'object') {
    return '';
  }

  const targets = [
    targetRefs.promoId ? `promoId: ${targetRefs.promoId}` : null,
    targetRefs.promoPublicKey ? `promoKey: ${targetRefs.promoPublicKey}` : null,
    targetRefs.knowledgeSourceId ? `knowledgeSourceId: ${targetRefs.knowledgeSourceId}` : null,
    targetRefs.ticketId ? `ticketId: ${targetRefs.ticketId}` : null,
    targetRefs.visitorId ? `visitorId: ${targetRefs.visitorId}` : null,
  ].filter(Boolean);

  return targets.join(' · ');
};

const getValidRecommendations = (stage7Decision) => {
  if (!stage7Decision || stage7Decision.validationStatus !== 'Valid') {
    return [];
  }

  if (!Array.isArray(stage7Decision.recommendations)) {
    return [];
  }

  return stage7Decision.recommendations.filter((item) => {
    if (!item || typeof item !== 'object') {
      return false;
    }

    if (typeof item.type !== 'string' || !item.type.trim()) {
      return false;
    }

    if (typeof item.rationale !== 'string' || !item.rationale.trim()) {
      return false;
    }

    return true;
  });
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
    botName: 'Assistant',
    primaryColor: '#2563eb',
    launcherVisible: true,
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


  const botNameField = document.createElement('label');
  botNameField.style.display = 'flex';
  botNameField.style.flexDirection = 'column';
  botNameField.style.gap = '6px';

  const botNameLabel = document.createElement('span');
  botNameLabel.textContent = 'Bot name';
  botNameLabel.style.fontSize = '13px';

  const botNameInput = document.createElement('input');
  botNameInput.type = 'text';
  botNameInput.placeholder = 'Assistant';
  botNameInput.maxLength = 50;
  botNameInput.style.padding = '8px 10px';
  botNameInput.style.borderRadius = '6px';
  botNameInput.style.border = '1px solid #cbd5e1';

  const saveBotNameButton = createButton({ label: 'Save name', variant: 'primary' });
  saveBotNameButton.style.width = 'fit-content';

  botNameField.append(botNameLabel, botNameInput, saveBotNameButton);

  const primaryColorField = document.createElement('label');
  primaryColorField.style.display = 'flex';
  primaryColorField.style.flexDirection = 'column';
  primaryColorField.style.gap = '6px';

  const primaryColorLabel = document.createElement('span');
  primaryColorLabel.textContent = 'Primary color';
  primaryColorLabel.style.fontSize = '13px';

  const primaryColorInput = document.createElement('input');
  primaryColorInput.type = 'color';
  primaryColorInput.value = state.primaryColor;
  primaryColorInput.style.height = '38px';
  primaryColorInput.style.padding = '4px';
  primaryColorInput.style.borderRadius = '6px';
  primaryColorInput.style.border = '1px solid #cbd5e1';

  const launcherVisibleWrap = document.createElement('label');
  launcherVisibleWrap.style.display = 'flex';
  launcherVisibleWrap.style.alignItems = 'center';
  launcherVisibleWrap.style.gap = '8px';

  const launcherVisibleInput = document.createElement('input');
  launcherVisibleInput.type = 'checkbox';
  launcherVisibleInput.checked = state.launcherVisible;

  const launcherVisibleText = document.createElement('span');
  launcherVisibleText.textContent = 'Show launcher';
  launcherVisibleText.style.fontSize = '13px';

  launcherVisibleWrap.append(launcherVisibleInput, launcherVisibleText);

  const saveAppearanceButton = createButton({ label: 'Save appearance', variant: 'primary' });
  saveAppearanceButton.style.width = 'fit-content';

  primaryColorField.append(primaryColorLabel, primaryColorInput, launcherVisibleWrap, saveAppearanceButton);

  configBody.append(siteField, widgetWrap, botNameField, primaryColorField);

  const installBody = document.createElement('div');
  installBody.style.display = 'flex';
  installBody.style.flexDirection = 'column';
  installBody.style.gap = '10px';

  const installMessage = document.createElement('p');
  installMessage.textContent = 'Engage now loads through the unified Intentify SDK snippet. Use the Install page to copy the single snippet and manage allowed origins.';
  installMessage.style.margin = '0';
  installMessage.style.fontSize = '13px';
  installMessage.style.color = '#475569';

  const updateInstallCard = () => {};

  installBody.append(installMessage);

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

  const jumpLatestButton = createButton({ label: 'Jump to latest' });
  const closeModalButton = createButton({ label: 'Close' });

  modalActions.append(jumpLatestButton, closeModalButton);
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
const label = document.createElement('div');
      label.style.fontSize = '11px';
      label.style.fontWeight = '600';
      label.style.marginBottom = '4px';
      label.textContent = entry.role === 'user' ? 'You' : (state.botName || 'Assistant');

      const content = document.createElement('div');
      content.textContent = entry.content;
      content.style.whiteSpace = 'pre-wrap';

      bubble.append(label, content);

      if (entry.role === 'assistant') {
        const recommendations = getValidRecommendations(entry.stage7Decision);
        if (recommendations.length > 0) {
          const recommendationPanel = document.createElement('div');
          recommendationPanel.style.marginTop = '8px';
          recommendationPanel.style.paddingTop = '8px';
          recommendationPanel.style.borderTop = '1px solid #cbd5e1';
          recommendationPanel.style.display = 'flex';
          recommendationPanel.style.flexDirection = 'column';
          recommendationPanel.style.gap = '6px';

          const recommendationTitle = document.createElement('div');
          recommendationTitle.textContent = 'Recommendations';
          recommendationTitle.style.fontSize = '11px';
          recommendationTitle.style.fontWeight = '600';
          recommendationTitle.style.color = '#334155';
          recommendationPanel.appendChild(recommendationTitle);

          recommendations.forEach((recommendation) => {
            const item = document.createElement('div');
            item.style.padding = '6px 8px';
            item.style.border = '1px solid #cbd5e1';
            item.style.borderRadius = '6px';
            item.style.background = '#ffffff';
            item.style.display = 'flex';
            item.style.flexDirection = 'column';
            item.style.gap = '4px';

            const meta = document.createElement('div');
            meta.style.fontSize = '11px';
            meta.style.fontWeight = '600';
            meta.style.color = '#0f172a';
            meta.textContent = `${recommendation.type} · Confidence ${formatConfidence(recommendation.confidence)} · ${recommendation.requiresApproval ? 'Approval required' : 'No approval required'}`;

            const rationale = document.createElement('div');
            rationale.style.fontSize = '12px';
            rationale.style.color = '#1e293b';
            rationale.textContent = recommendation.rationale;

            item.append(meta, rationale);

            const targetSummary = buildRecommendationTargetSummary(recommendation.targetRefs);
            if (targetSummary) {
              const target = document.createElement('div');
              target.style.fontSize = '11px';
              target.style.color = '#475569';
              target.textContent = `Target: ${targetSummary}`;
              item.appendChild(target);
            }

            recommendationPanel.appendChild(item);
          });

          bubble.appendChild(recommendationPanel);
        }
      }

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
      const isUser = message.role === 'user';
      const row = document.createElement('div');
      row.style.display = 'flex';
      row.style.justifyContent = isUser ? 'flex-end' : 'flex-start';

      const bubble = document.createElement('div');
      bubble.style.maxWidth = '82%';
      bubble.style.padding = '10px 12px';
      bubble.style.borderRadius = '10px';
      bubble.style.background = isUser ? '#dbeafe' : '#f1f5f9';
      bubble.style.color = '#0f172a';
      bubble.style.border = isUser ? '1px solid #bfdbfe' : '1px solid #e2e8f0';

      const meta = document.createElement('div');
      const roleLabel = isUser ? 'You' : (state.botName || 'Assistant');
      meta.textContent = `${roleLabel} • ${new Date(message.createdAtUtc).toLocaleString()}`;
      meta.style.fontSize = '12px';
      meta.style.color = '#64748b';

      const content = document.createElement('div');
      content.textContent = message.content;
      content.style.marginTop = '6px';
      content.style.whiteSpace = 'pre-wrap';

      bubble.append(meta, content);
      row.appendChild(bubble);
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


  const loadBotName = async (siteId) => {
    if (!siteId) {
      state.botName = 'Assistant';
      state.primaryColor = '#2563eb';
      state.launcherVisible = true;
      botNameInput.value = '';
      primaryColorInput.value = state.primaryColor;
      launcherVisibleInput.checked = state.launcherVisible;
      renderTranscript();
      return;
    }

    try {
      const bot = await client.engage.getBot(siteId);
      state.botName = bot?.name || 'Assistant';
      state.primaryColor = bot?.primaryColor || '#2563eb';
      state.launcherVisible = typeof bot?.launcherVisible === 'boolean' ? bot.launcherVisible : true;
      botNameInput.value = state.botName;
      primaryColorInput.value = state.primaryColor;
      launcherVisibleInput.checked = state.launcherVisible;
      renderTranscript();
    } catch (error) {
      state.botName = 'Assistant';
      state.primaryColor = '#2563eb';
      state.launcherVisible = true;
      botNameInput.value = '';
      primaryColorInput.value = state.primaryColor;
      launcherVisibleInput.checked = state.launcherVisible;
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  saveAppearanceButton.addEventListener('click', async () => {
    if (!state.siteId) {
      notifier.show({ message: 'Select a site first.', variant: 'warning' });
      return;
    }

    saveAppearanceButton.disabled = true;
    try {
      const updated = await client.engage.updateBot(state.siteId, botNameInput.value.trim() || state.botName || 'Assistant', {
        primaryColor: primaryColorInput.value,
        launcherVisible: launcherVisibleInput.checked,
      });
      state.botName = updated?.name || botNameInput.value.trim() || state.botName || 'Assistant';
      state.primaryColor = updated?.primaryColor || primaryColorInput.value || '#2563eb';
      state.launcherVisible = typeof updated?.launcherVisible === 'boolean' ? updated.launcherVisible : launcherVisibleInput.checked;
      botNameInput.value = state.botName;
      primaryColorInput.value = state.primaryColor;
      launcherVisibleInput.checked = state.launcherVisible;
      notifier.show({ message: 'Widget appearance saved.', variant: 'success' });
      renderTranscript();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    } finally {
      saveAppearanceButton.disabled = false;
    }
  });

  saveBotNameButton.addEventListener('click', async () => {
    if (!state.siteId) {
      notifier.show({ message: 'Select a site first.', variant: 'warning' });
      return;
    }

    const name = botNameInput.value.trim();
    if (name.length < 1 || name.length > 50) {
      notifier.show({ message: 'Bot name must be 1-50 characters.', variant: 'warning' });
      return;
    }

    saveBotNameButton.disabled = true;
    try {
      const updated = await client.engage.updateBot(state.siteId, name, {
        primaryColor: primaryColorInput.value,
        launcherVisible: launcherVisibleInput.checked,
      });
      state.botName = updated?.name || name;
      state.primaryColor = updated?.primaryColor || primaryColorInput.value || '#2563eb';
      state.launcherVisible = typeof updated?.launcherVisible === 'boolean' ? updated.launcherVisible : launcherVisibleInput.checked;
      botNameInput.value = state.botName;
      primaryColorInput.value = state.primaryColor;
      launcherVisibleInput.checked = state.launcherVisible;
      notifier.show({ message: 'Bot settings saved.', variant: 'success' });
      renderTranscript();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    } finally {
      saveBotNameButton.disabled = false;
    }
  });

  siteSelect.addEventListener('change', async () => {
    state.siteId = siteSelect.value;
    state.selectedSessionId = '';
    state.selectedMessages = [];
    state.conversations = [];
    state.sessionId = '';
    state.transcript = [];
    closeModal();
    await loadSiteWidgetKey(state.siteId);
    await loadBotName(state.siteId);
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
      state.transcript.push({
        role: 'assistant',
        content: response.response || '',
        stage7Decision: response.stage7Decision || null,
      });
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
      await loadBotName(state.siteId);
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
