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
    opportunityAnalytics: null,
    botName: 'Assistant',
    primaryColor: '#2563eb',
    launcherVisible: true,
    tone: 'warm',
    verbosity: 'balanced',
    fallbackStyle: 'refine',
    businessDescription: '',
    industry: '',
    servicesDescription: '',
    geographicFocus: '',
    personalityDescriptor: '',
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

  const personalityField = document.createElement('label');
  personalityField.style.display = 'flex';
  personalityField.style.flexDirection = 'column';
  personalityField.style.gap = '6px';

  const personalityLabel = document.createElement('span');
  personalityLabel.textContent = 'Bot personality';
  personalityLabel.style.fontSize = '13px';

  const toneInput = document.createElement('select');
  toneInput.style.padding = '8px 10px';
  toneInput.style.borderRadius = '6px';
  toneInput.style.border = '1px solid #cbd5e1';
  [['warm', 'Warm'], ['professional', 'Professional'], ['casual', 'Casual']].forEach(([value, label]) => {
    const option = document.createElement('option');
    option.value = value;
    option.textContent = label;
    toneInput.appendChild(option);
  });

  const verbosityInput = document.createElement('select');
  verbosityInput.style.padding = '8px 10px';
  verbosityInput.style.borderRadius = '6px';
  verbosityInput.style.border = '1px solid #cbd5e1';
  [['brief', 'Brief'], ['balanced', 'Balanced'], ['detailed', 'Detailed']].forEach(([value, label]) => {
    const option = document.createElement('option');
    option.value = value;
    option.textContent = label;
    verbosityInput.appendChild(option);
  });

  const fallbackStyleInput = document.createElement('select');
  fallbackStyleInput.style.padding = '8px 10px';
  fallbackStyleInput.style.borderRadius = '6px';
  fallbackStyleInput.style.border = '1px solid #cbd5e1';
  [['refine', 'Refine first'], ['handoff', 'Offer handoff first']].forEach(([value, label]) => {
    const option = document.createElement('option');
    option.value = value;
    option.textContent = label;
    fallbackStyleInput.appendChild(option);
  });

  const savePersonalityButton = createButton({ label: 'Save personality', variant: 'primary' });
  savePersonalityButton.style.width = 'fit-content';

  personalityField.append(personalityLabel, toneInput, verbosityInput, fallbackStyleInput, savePersonalityButton);

  const saveAppearanceButton = createButton({ label: 'Save appearance', variant: 'primary' });
  saveAppearanceButton.style.width = 'fit-content';

  primaryColorField.append(primaryColorLabel, primaryColorInput, launcherVisibleWrap, saveAppearanceButton);

  configBody.append(siteField, widgetWrap, botNameField, primaryColorField, personalityField);

  // Business context fields
  const businessBody = document.createElement('div');
  businessBody.style.display = 'grid';
  businessBody.style.gridTemplateColumns = '1fr 1fr';
  businessBody.style.gap = '10px';

  const makeField = (labelText, el) => {
    const wrap = document.createElement('label');
    wrap.style.display = 'flex';
    wrap.style.flexDirection = 'column';
    wrap.style.gap = '6px';
    const lbl = document.createElement('span');
    lbl.textContent = labelText;
    lbl.style.fontSize = '13px';
    lbl.style.fontWeight = '500';
    el.className = 'form-input';
    wrap.append(lbl, el);
    return wrap;
  };

  const businessDescriptionInput = document.createElement('textarea');
  businessDescriptionInput.rows = 3;
  businessDescriptionInput.placeholder = 'Describe your business…';

  const industryInput = document.createElement('input');
  industryInput.type = 'text';
  industryInput.placeholder = 'e.g. SaaS, Real Estate, Healthcare';

  const servicesDescriptionInput = document.createElement('textarea');
  servicesDescriptionInput.rows = 2;
  servicesDescriptionInput.placeholder = 'Key products or services offered…';

  const geographicFocusInput = document.createElement('input');
  geographicFocusInput.type = 'text';
  geographicFocusInput.placeholder = 'e.g. United States, EMEA';

  const personalityDescriptorInput = document.createElement('input');
  personalityDescriptorInput.type = 'text';
  personalityDescriptorInput.placeholder = 'e.g. friendly, concise, formal';

  const saveBusinessButton = document.createElement('button');
  saveBusinessButton.type = 'button';
  saveBusinessButton.textContent = 'Save business context';
  saveBusinessButton.className = 'btn btn-primary btn-sm';
  saveBusinessButton.style.width = 'fit-content';
  saveBusinessButton.style.gridColumn = '1 / -1';

  businessBody.append(
    makeField('Business Description', businessDescriptionInput),
    makeField('Industry', industryInput),
    makeField('Services Description', servicesDescriptionInput),
    makeField('Geographic Focus', geographicFocusInput),
    makeField('Personality Descriptor', personalityDescriptorInput),
    saveBusinessButton,
  );

  // ── Digest Email section ──────────────────────────────────────────────
  const digestBody = document.createElement('div');
  digestBody.style.display = 'flex';
  digestBody.style.flexDirection = 'column';
  digestBody.style.gap = '12px';

  const digestEnabledRow = document.createElement('div');
  digestEnabledRow.style.cssText = 'display:flex;align-items:center;gap:8px;';
  const digestEnabledInput = document.createElement('input');
  digestEnabledInput.type = 'checkbox';
  digestEnabledInput.id = 'digest-enabled';
  const digestEnabledLabel = document.createElement('label');
  digestEnabledLabel.htmlFor = 'digest-enabled';
  digestEnabledLabel.textContent = 'Enable digest email';
  digestEnabledLabel.style.fontSize = '14px';
  digestEnabledRow.append(digestEnabledInput, digestEnabledLabel);

  const digestRecipientsWrap = document.createElement('label');
  digestRecipientsWrap.style.display = 'flex';
  digestRecipientsWrap.style.flexDirection = 'column';
  digestRecipientsWrap.style.gap = '4px';
  const digestRecipientsLabel = document.createElement('span');
  digestRecipientsLabel.textContent = 'Recipients (comma-separated emails)';
  digestRecipientsLabel.style.cssText = 'font-size:13px;font-weight:500;';
  const digestRecipientsInput = document.createElement('input');
  digestRecipientsInput.type = 'text';
  digestRecipientsInput.className = 'form-input';
  digestRecipientsInput.placeholder = 'e.g. alice@example.com, bob@example.com';
  digestRecipientsWrap.append(digestRecipientsLabel, digestRecipientsInput);

  const digestFrequencyWrap = document.createElement('label');
  digestFrequencyWrap.style.display = 'flex';
  digestFrequencyWrap.style.flexDirection = 'column';
  digestFrequencyWrap.style.gap = '4px';
  const digestFrequencyLabel = document.createElement('span');
  digestFrequencyLabel.textContent = 'Frequency';
  digestFrequencyLabel.style.cssText = 'font-size:13px;font-weight:500;';
  const digestFrequencyInput = document.createElement('select');
  digestFrequencyInput.className = 'form-select';
  [['weekly', 'Weekly'], ['daily', 'Daily']].forEach(([val, lbl]) => {
    const opt = document.createElement('option');
    opt.value = val;
    opt.textContent = lbl;
    digestFrequencyInput.appendChild(opt);
  });
  digestFrequencyWrap.append(digestFrequencyLabel, digestFrequencyInput);

  const toggleDigestFields = () => {
    const show = digestEnabledInput.checked;
    digestRecipientsWrap.style.display = show ? 'flex' : 'none';
    digestFrequencyWrap.style.display = show ? 'flex' : 'none';
  };
  digestEnabledInput.addEventListener('change', toggleDigestFields);
  toggleDigestFields();

  const digestButtonRow = document.createElement('div');
  digestButtonRow.style.cssText = 'display:flex;gap:8px;flex-wrap:wrap;';

  const saveDigestButton = document.createElement('button');
  saveDigestButton.type = 'button';
  saveDigestButton.textContent = 'Save digest settings';
  saveDigestButton.className = 'btn btn-primary btn-sm';

  const sendTestDigestButton = document.createElement('button');
  sendTestDigestButton.type = 'button';
  sendTestDigestButton.textContent = 'Send Test Digest';
  sendTestDigestButton.className = 'btn btn-secondary btn-sm';

  digestButtonRow.append(saveDigestButton, sendTestDigestButton);
  digestBody.append(digestEnabledRow, digestRecipientsWrap, digestFrequencyWrap, digestButtonRow);

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

  const analyticsBody = document.createElement('div');
  analyticsBody.style.display = 'flex';
  analyticsBody.style.flexDirection = 'column';
  analyticsBody.style.gap = '8px';

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
        const businessRows = [
          entry.opportunityLabel ? `Opportunity: ${entry.opportunityLabel}` : null,
          Number.isFinite(Number(entry.intentScore)) ? `Intent score: ${entry.intentScore}` : null,
          entry.preferredContactMethod ? `Preferred contact method: ${entry.preferredContactMethod}` : null,
          entry.conversationSummary ? `Summary: ${entry.conversationSummary}` : null,
          entry.suggestedFollowUp ? `Suggested follow-up: ${entry.suggestedFollowUp}` : null,
        ].filter(Boolean);

        if (businessRows.length > 0) {
          const businessMeta = document.createElement('div');
          businessMeta.style.marginTop = '8px';
          businessMeta.style.paddingTop = '8px';
          businessMeta.style.borderTop = '1px solid #cbd5e1';
          businessMeta.style.fontSize = '11px';
          businessMeta.style.color = '#334155';
          businessMeta.style.whiteSpace = 'pre-wrap';
          businessMeta.textContent = businessRows.join('\n');
          bubble.appendChild(businessMeta);
        }

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
      empty.className = 'empty-state';
      empty.innerHTML = `<div class="empty-state-icon">💬</div><div class="empty-state-title">${state.siteId ? 'No conversations yet' : 'Select a site'}</div>`;
      sessionsList.appendChild(empty);
      return;
    }

    const tableWrap = document.createElement('div');
    tableWrap.className = 'table-wrapper';
    const table = document.createElement('table');
    table.className = 'data-table';

    const thead = document.createElement('thead');
    thead.innerHTML = '<tr><th>Session</th><th>Date</th><th>Confidence</th><th>Lead</th><th>Ticket</th></tr>';
    table.appendChild(thead);

    const tbody = document.createElement('tbody');
    state.conversations.forEach((conv) => {
      const tr = document.createElement('tr');
      tr.style.cursor = 'pointer';
      if (conv.sessionId === state.selectedSessionId) {
        tr.style.background = 'var(--brand-primary-light)';
      }

      const shortId = conv.sessionId ? `${conv.sessionId.substring(0, 8)}\u2026` : '\u2014';
      const ts = getConversationTimestamp(conv);
      const dateStr = ts ? new Date(ts).toLocaleDateString() : '\u2014';
      const conf = Number(conv.averageConfidence ?? conv.confidence);
      const hasConf = Number.isFinite(conf) && conf > 0;
      const confVariant = conf >= 0.8 ? 'success' : conf >= 0.5 ? 'warning' : 'danger';
      const confHtml = hasConf
        ? `<span class="badge badge-${confVariant}">${Math.round(conf * 100)}%</span>`
        : '\u2014';
      const leadHtml = conv.hasLead || conv.leadId || conv.leadCreated
        ? '<span class="badge badge-success">✓</span>'
        : '<span style="color:var(--color-text-muted)">—</span>';
      const ticketHtml = conv.hasTicket || conv.ticketId || conv.ticketCreated
        ? '<span class="badge badge-success">✓</span>'
        : '<span style="color:var(--color-text-muted)">—</span>';

      tr.innerHTML = `
        <td class="text-primary" style="font-family:monospace;font-size:12px;">${shortId}</td>
        <td>${dateStr}</td>
        <td>${confHtml}</td>
        <td>${leadHtml}</td>
        <td>${ticketHtml}</td>
      `;
      tr.addEventListener('click', async () => {
        state.selectedSessionId = conv.sessionId;
        await loadConversationMessages({ openModal: true });
      });
      tbody.appendChild(tr);
    });

    table.appendChild(tbody);
    tableWrap.appendChild(table);
    sessionsList.appendChild(tableWrap);
  };

  const renderOpportunityAnalytics = () => {
    analyticsBody.innerHTML = '';

    if (!state.siteId) {
      const empty = document.createElement('div');
      empty.textContent = 'Select a site to view opportunity analytics.';
      empty.style.color = '#64748b';
      analyticsBody.appendChild(empty);
      return;
    }

    if (!state.opportunityAnalytics) {
      const empty = document.createElement('div');
      empty.textContent = 'No analytics available yet.';
      empty.style.color = '#64748b';
      analyticsBody.appendChild(empty);
      return;
    }

    const analytics = state.opportunityAnalytics;
    const summary = document.createElement('div');
    summary.className = 'grid-4';

    [
      ['Total Commercial', analytics.totalCommercialOpportunities ?? 0],
      ['Commercial', analytics.commercialCount ?? 0],
      ['Support', analytics.supportCount ?? 0],
      ['General', analytics.generalCount ?? 0],
      ['High Intent', analytics.highIntentCount ?? 0],
      ['Email Pref.', analytics.preferredContactMethodDistribution?.email ?? 0],
      ['Phone Pref.', analytics.preferredContactMethodDistribution?.phone ?? 0],
      ['Unknown Pref.', analytics.preferredContactMethodDistribution?.unknown ?? 0],
    ].forEach(([label, value]) => {
      const card = document.createElement('div');
      card.className = 'metric-card';
      card.innerHTML = `<div class="metric-label">${label}</div><div class="metric-value">${value}</div>`;
      summary.appendChild(card);
    });

    analyticsBody.appendChild(summary);

    const overTime = Array.isArray(analytics.opportunitiesOverTime) ? analytics.opportunitiesOverTime : [];
    if (overTime.length > 0) {
      const trend = document.createElement('div');
      trend.style.fontSize = '12px';
      trend.style.color = '#334155';
      trend.style.whiteSpace = 'pre-wrap';
      trend.textContent = `Opportunities over time: ${overTime
        .map((row) => `${new Date(row.dateUtc).toLocaleDateString()}: ${row.count}`)
        .join(' | ')}`;
      analyticsBody.appendChild(trend);
    }
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

      if (!isUser && Number.isFinite(Number(message.confidence))) {
        const confidence = document.createElement('div');
        confidence.style.marginTop = '6px';
        confidence.style.fontSize = '11px';
        confidence.style.color = '#475569';
        confidence.textContent = `Confidence: ${formatConfidence(message.confidence)}`;
        bubble.appendChild(confidence);
      }

      if (!isUser && Array.isArray(message.citations) && message.citations.length > 0) {
        const citationsWrap = document.createElement('details');
        citationsWrap.style.marginTop = '6px';

        const citationsSummary = document.createElement('summary');
        citationsSummary.textContent = `Citations (${message.citations.length})`;
        citationsSummary.style.cursor = 'pointer';
        citationsSummary.style.fontSize = '11px';
        citationsSummary.style.color = '#334155';
        citationsWrap.appendChild(citationsSummary);

        const citationsList = document.createElement('div');
        citationsList.style.marginTop = '4px';
        citationsList.style.fontSize = '11px';
        citationsList.style.color = '#475569';
        citationsList.style.display = 'flex';
        citationsList.style.flexDirection = 'column';
        citationsList.style.gap = '3px';

        message.citations.forEach((citation) => {
          const item = document.createElement('div');
          item.textContent = `${citation.sourceId} · chunk ${citation.chunkIndex}`;
          citationsList.appendChild(item);
        });

        citationsWrap.appendChild(citationsList);
        bubble.appendChild(citationsWrap);
      }
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

  const loadOpportunityAnalytics = async () => {
    if (!state.siteId) {
      state.opportunityAnalytics = null;
      renderOpportunityAnalytics();
      return;
    }

    try {
      state.opportunityAnalytics = await client.engage.getOpportunityAnalytics(state.siteId);
    } catch (error) {
      state.opportunityAnalytics = null;
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }

    renderOpportunityAnalytics();
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
      state.tone = 'warm';
      state.verbosity = 'balanced';
      state.fallbackStyle = 'refine';
      state.businessDescription = '';
      state.industry = '';
      state.servicesDescription = '';
      state.geographicFocus = '';
      state.personalityDescriptor = '';
      botNameInput.value = '';
      primaryColorInput.value = state.primaryColor;
      launcherVisibleInput.checked = state.launcherVisible;
      toneInput.value = state.tone;
      verbosityInput.value = state.verbosity;
      fallbackStyleInput.value = state.fallbackStyle;
      businessDescriptionInput.value = '';
      industryInput.value = '';
      servicesDescriptionInput.value = '';
      geographicFocusInput.value = '';
      personalityDescriptorInput.value = '';
      renderTranscript();
      return;
    }

    try {
      const bot = await client.engage.getBot(siteId);
      state.botName = bot?.name || 'Assistant';
      state.primaryColor = bot?.primaryColor || '#2563eb';
      state.launcherVisible = typeof bot?.launcherVisible === 'boolean' ? bot.launcherVisible : true;
      state.tone = bot?.tone || 'warm';
      state.verbosity = bot?.verbosity || 'balanced';
      state.fallbackStyle = bot?.fallbackStyle || 'refine';
      state.businessDescription = bot?.businessDescription || '';
      state.industry = bot?.industry || '';
      state.servicesDescription = bot?.servicesDescription || '';
      state.geographicFocus = bot?.geographicFocus || '';
      state.personalityDescriptor = bot?.personalityDescriptor || '';
      state.digestEmailEnabled = bot?.digestEmailEnabled ?? false;
      state.digestEmailRecipients = bot?.digestEmailRecipients || '';
      state.digestEmailFrequency = bot?.digestEmailFrequency || 'weekly';
      digestEnabledInput.checked = state.digestEmailEnabled;
      digestRecipientsInput.value = state.digestEmailRecipients;
      digestFrequencyInput.value = state.digestEmailFrequency;
      toggleDigestFields();
      botNameInput.value = state.botName;
      primaryColorInput.value = state.primaryColor;
      launcherVisibleInput.checked = state.launcherVisible;
      toneInput.value = state.tone;
      verbosityInput.value = state.verbosity;
      fallbackStyleInput.value = state.fallbackStyle;
      businessDescriptionInput.value = state.businessDescription;
      industryInput.value = state.industry;
      servicesDescriptionInput.value = state.servicesDescription;
      geographicFocusInput.value = state.geographicFocus;
      personalityDescriptorInput.value = state.personalityDescriptor;
      renderTranscript();
    } catch (error) {
      state.botName = 'Assistant';
      state.primaryColor = '#2563eb';
      state.launcherVisible = true;
      state.tone = 'warm';
      state.verbosity = 'balanced';
      state.fallbackStyle = 'refine';
      state.businessDescription = '';
      state.industry = '';
      state.servicesDescription = '';
      state.geographicFocus = '';
      state.personalityDescriptor = '';
      botNameInput.value = '';
      primaryColorInput.value = state.primaryColor;
      launcherVisibleInput.checked = state.launcherVisible;
      toneInput.value = state.tone;
      verbosityInput.value = state.verbosity;
      fallbackStyleInput.value = state.fallbackStyle;
      businessDescriptionInput.value = '';
      industryInput.value = '';
      servicesDescriptionInput.value = '';
      geographicFocusInput.value = '';
      personalityDescriptorInput.value = '';
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
        tone: toneInput.value,
        verbosity: verbosityInput.value,
        fallbackStyle: fallbackStyleInput.value,
        businessDescription: businessDescriptionInput.value.trim(),
        industry: industryInput.value.trim(),
        servicesDescription: servicesDescriptionInput.value.trim(),
        geographicFocus: geographicFocusInput.value.trim(),
        personalityDescriptor: personalityDescriptorInput.value.trim(),
      });
      state.botName = updated?.name || botNameInput.value.trim() || state.botName || 'Assistant';
      state.primaryColor = updated?.primaryColor || primaryColorInput.value || '#2563eb';
      state.launcherVisible = typeof updated?.launcherVisible === 'boolean' ? updated.launcherVisible : launcherVisibleInput.checked;
      state.tone = updated?.tone || toneInput.value || 'warm';
      state.verbosity = updated?.verbosity || verbosityInput.value || 'balanced';
      state.fallbackStyle = updated?.fallbackStyle || fallbackStyleInput.value || 'refine';
      botNameInput.value = state.botName;
      primaryColorInput.value = state.primaryColor;
      launcherVisibleInput.checked = state.launcherVisible;
      toneInput.value = state.tone;
      verbosityInput.value = state.verbosity;
      fallbackStyleInput.value = state.fallbackStyle;
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
        tone: toneInput.value,
        verbosity: verbosityInput.value,
        fallbackStyle: fallbackStyleInput.value,
        businessDescription: businessDescriptionInput.value.trim(),
        industry: industryInput.value.trim(),
        servicesDescription: servicesDescriptionInput.value.trim(),
        geographicFocus: geographicFocusInput.value.trim(),
        personalityDescriptor: personalityDescriptorInput.value.trim(),
      });
      state.botName = updated?.name || name;
      state.primaryColor = updated?.primaryColor || primaryColorInput.value || '#2563eb';
      state.launcherVisible = typeof updated?.launcherVisible === 'boolean' ? updated.launcherVisible : launcherVisibleInput.checked;
      state.tone = updated?.tone || toneInput.value || 'warm';
      state.verbosity = updated?.verbosity || verbosityInput.value || 'balanced';
      state.fallbackStyle = updated?.fallbackStyle || fallbackStyleInput.value || 'refine';
      botNameInput.value = state.botName;
      primaryColorInput.value = state.primaryColor;
      launcherVisibleInput.checked = state.launcherVisible;
      toneInput.value = state.tone;
      verbosityInput.value = state.verbosity;
      fallbackStyleInput.value = state.fallbackStyle;
      notifier.show({ message: 'Bot settings saved.', variant: 'success' });
      renderTranscript();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    } finally {
      saveBotNameButton.disabled = false;
    }
  });


  savePersonalityButton.addEventListener('click', async () => {
    if (!state.siteId) {
      notifier.show({ message: 'Select a site first.', variant: 'warning' });
      return;
    }

    savePersonalityButton.disabled = true;
    try {
      const updated = await client.engage.updateBot(state.siteId, botNameInput.value.trim() || state.botName || 'Assistant', {
        primaryColor: primaryColorInput.value,
        launcherVisible: launcherVisibleInput.checked,
        tone: toneInput.value,
        verbosity: verbosityInput.value,
        fallbackStyle: fallbackStyleInput.value,
        businessDescription: businessDescriptionInput.value.trim(),
        industry: industryInput.value.trim(),
        servicesDescription: servicesDescriptionInput.value.trim(),
        geographicFocus: geographicFocusInput.value.trim(),
        personalityDescriptor: personalityDescriptorInput.value.trim(),
      });
      state.tone = updated?.tone || toneInput.value || 'warm';
      state.verbosity = updated?.verbosity || verbosityInput.value || 'balanced';
      state.fallbackStyle = updated?.fallbackStyle || fallbackStyleInput.value || 'refine';
      toneInput.value = state.tone;
      verbosityInput.value = state.verbosity;
      fallbackStyleInput.value = state.fallbackStyle;
      notifier.show({ message: 'Bot personality saved.', variant: 'success' });
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    } finally {
      savePersonalityButton.disabled = false;
    }
  });

  saveBusinessButton.addEventListener('click', async () => {
    if (!state.siteId) {
      notifier.show({ message: 'Select a site first.', variant: 'warning' });
      return;
    }

    saveBusinessButton.disabled = true;
    try {
      await client.engage.updateBot(state.siteId, botNameInput.value.trim() || state.botName || 'Assistant', {
        primaryColor: primaryColorInput.value,
        launcherVisible: launcherVisibleInput.checked,
        tone: toneInput.value,
        verbosity: verbosityInput.value,
        fallbackStyle: fallbackStyleInput.value,
        businessDescription: businessDescriptionInput.value.trim(),
        industry: industryInput.value.trim(),
        servicesDescription: servicesDescriptionInput.value.trim(),
        geographicFocus: geographicFocusInput.value.trim(),
        personalityDescriptor: personalityDescriptorInput.value.trim(),
      });
      state.businessDescription = businessDescriptionInput.value.trim();
      state.industry = industryInput.value.trim();
      state.servicesDescription = servicesDescriptionInput.value.trim();
      state.geographicFocus = geographicFocusInput.value.trim();
      state.personalityDescriptor = personalityDescriptorInput.value.trim();
      notifier.show({ message: 'Business context saved.', variant: 'success' });
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    } finally {
      saveBusinessButton.disabled = false;
    }
  });

  saveDigestButton.addEventListener('click', async () => {
    if (!state.siteId) {
      notifier.show({ message: 'Select a site first.', variant: 'warning' });
      return;
    }
    saveDigestButton.disabled = true;
    try {
      await client.engage.updateBot(state.siteId, botNameInput.value.trim() || state.botName || 'Assistant', {
        primaryColor: primaryColorInput.value,
        launcherVisible: launcherVisibleInput.checked,
        tone: toneInput.value,
        verbosity: verbosityInput.value,
        fallbackStyle: fallbackStyleInput.value,
        businessDescription: businessDescriptionInput.value.trim(),
        industry: industryInput.value.trim(),
        servicesDescription: servicesDescriptionInput.value.trim(),
        geographicFocus: geographicFocusInput.value.trim(),
        personalityDescriptor: personalityDescriptorInput.value.trim(),
        digestEmailEnabled: digestEnabledInput.checked,
        digestEmailRecipients: digestRecipientsInput.value.trim(),
        digestEmailFrequency: digestFrequencyInput.value,
      });
      state.digestEmailEnabled = digestEnabledInput.checked;
      state.digestEmailRecipients = digestRecipientsInput.value.trim();
      state.digestEmailFrequency = digestFrequencyInput.value;
      notifier.show({ message: 'Digest settings saved.', variant: 'success' });
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    } finally {
      saveDigestButton.disabled = false;
    }
  });

  sendTestDigestButton.addEventListener('click', async () => {
    if (!state.siteId) {
      notifier.show({ message: 'Select a site first.', variant: 'warning' });
      return;
    }
    sendTestDigestButton.disabled = true;
    sendTestDigestButton.textContent = 'Generating…';
    try {
      const result = await client.engage.sendDigest(state.siteId);
      // Show result in a simple modal
      const overlay = document.createElement('div');
      overlay.style.cssText = 'position:fixed;inset:0;background:rgba(0,0,0,0.45);z-index:1000;display:flex;align-items:center;justify-content:center;padding:24px;';
      const dialog = document.createElement('div');
      dialog.className = 'card';
      dialog.style.cssText = 'width:100%;max-width:680px;max-height:85vh;overflow-y:auto;padding:24px;';
      const hdr = document.createElement('div');
      hdr.style.cssText = 'display:flex;align-items:center;justify-content:space-between;margin-bottom:16px;';
      const hTitle = document.createElement('h3');
      hTitle.style.cssText = 'margin:0;font-size:15px;font-weight:600;';
      hTitle.textContent = 'Digest Preview';
      const closeBtn = document.createElement('button');
      closeBtn.className = 'btn btn-secondary btn-sm';
      closeBtn.textContent = '✕';
      closeBtn.addEventListener('click', () => overlay.remove());
      overlay.addEventListener('click', (e) => { if (e.target === overlay) overlay.remove(); });
      hdr.append(hTitle, closeBtn);
      const pre = document.createElement('pre');
      pre.style.cssText = 'background:var(--color-surface);border:1px solid var(--color-border);border-radius:6px;padding:16px;font-size:12px;overflow-x:auto;white-space:pre-wrap;word-break:break-word;';
      pre.textContent = JSON.stringify(result, null, 2);
      dialog.append(hdr, pre);
      overlay.appendChild(dialog);
      document.body.appendChild(overlay);
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    } finally {
      sendTestDigestButton.disabled = false;
      sendTestDigestButton.textContent = 'Send Test Digest';
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
    await loadOpportunityAnalytics();
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
        opportunityLabel: response.opportunityLabel || null,
        intentScore: response.intentScore,
        preferredContactMethod: response.preferredContactMethod || null,
        conversationSummary: response.conversationSummary || null,
        suggestedFollowUp: response.suggestedFollowUp || null,
      });
      renderTranscript();
      await loadConversations();
      await loadOpportunityAnalytics();
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
      await loadOpportunityAnalytics();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  page.append(
    header,
    createCard({ title: 'Bot Config', body: configBody }),
    createCard({ title: 'Business Context', body: businessBody }),
    createCard({ title: 'Digest Email', body: digestBody }),
    createCard({ title: 'Opportunity Analytics', body: analyticsBody }),
    createCard({ title: 'Conversations', body: conversationsBody }),
    createCard({ title: 'Test Chat', body: chatBody }),
    createCard({ title: 'Install Engage', body: installBody })
  );

  container.appendChild(page);

  renderTranscript();
  renderConversations();
  renderOpportunityAnalytics();
  updateInstallCard();
  void loadSites();
};
