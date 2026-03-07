import { createCard, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const SELECTED_SITE_STORAGE_KEY = 'intentify.selectedSiteId';

const formatDate = (value) => {
  if (!value) return '—';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString();
};

const createSummaryValue = (label, value) => {
  const block = document.createElement('div');
  block.style.padding = '10px 12px';
  block.style.border = '1px solid #e2e8f0';
  block.style.borderRadius = '8px';
  block.style.background = '#ffffff';

  const labelNode = document.createElement('div');
  labelNode.textContent = label;
  labelNode.style.fontSize = '12px';
  labelNode.style.color = '#64748b';

  const valueNode = document.createElement('div');
  valueNode.textContent = value ?? '—';
  valueNode.style.fontSize = '15px';
  valueNode.style.fontWeight = '600';
  valueNode.style.color = '#0f172a';

  block.append(labelNode, valueNode);
  return block;
};

const mapPath = (timelineItem) => {
  const url = timelineItem?.url;
  if (!url) return '—';
  try {
    const parsedUrl = new URL(url);
    return `${parsedUrl.pathname || '/'}${parsedUrl.search || ''}${parsedUrl.hash || ''}` || url;
  } catch {
    return url;
  }
};

const mapReferrer = (timelineItem) => {
  const referrer = timelineItem?.referrer;
  if (!referrer) return 'Direct / None';
  try {
    return new URL(referrer).hostname || referrer;
  } catch {
    return referrer;
  }
};

const sortByOccurredDesc = (items) =>
  [...items].sort((left, right) => new Date(right?.occurredAtUtc || 0).getTime() - new Date(left?.occurredAtUtc || 0).getTime());

const normalizeSessionId = (value) =>
  typeof value === 'string' && value.trim() ? value.trim() : 'sessionless';

const getEventData = (timelineItem) => {
  const raw = timelineItem?.metadataSummary ?? timelineItem?.data;
  if (!raw) return null;
  if (typeof raw === 'string') {
    try {
      const parsed = JSON.parse(raw);
      return parsed && typeof parsed === 'object' ? parsed : null;
    } catch {
      return null;
    }
  }
  return typeof raw === 'object' ? raw : null;
};

const getCollectorSessionId = (timelineItem) => {
  return normalizeSessionId(timelineItem?.sessionId ?? timelineItem?.SessionId);
};

const getDateKey = (value) => {
  const date = new Date(value || 0);
  if (Number.isNaN(date.getTime())) return 'Unknown date';
  return date.toISOString().slice(0, 10);
};

const formatDateHeading = (key) => {
  const date = new Date(`${key}T00:00:00.000Z`);
  if (Number.isNaN(date.getTime())) return key;
  return date.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
};

const parseNumber = (value) => {
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value === 'string') {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }
  return null;
};

const formatDuration = (secondsValue) => {
  const seconds = parseNumber(secondsValue);
  if (seconds === null) return '—';
  const totalSeconds = Math.max(0, Math.round(seconds));
  return `${Math.floor(totalSeconds / 60)}m ${totalSeconds % 60}s`;
};

const getTimelineDetails = (item) => {
  const data = getEventData(item);
  if (!data) return '—';

  const type = String(item?.type || '').toLowerCase();
  if (type === 'time_on_page') {
    const duration = formatDuration(data.seconds);
    if (duration === '—') return '—';
    const reason = typeof data.reason === 'string' ? data.reason.trim() : '';
    return reason ? `${duration} (${reason})` : duration;
  }

  if (type === 'scroll_depth') {
    const percent = parseNumber(data.percent ?? data.value);
    return percent === null ? '—' : `${Math.round(percent)}%`;
  }

  if (type === 'pageview') return data.title || data.pageTitle || '—';

  if (type === 'click' || type === 'outbound_click') {
    const text = typeof data.text === 'string' ? data.text.trim() : '';
    const id = typeof data.id === 'string' ? data.id.trim() : '';
    const tag = typeof data.tag === 'string' ? data.tag.trim() : '';
    if (text && id) return `${text} (#${id})`;
    return text || (id ? `#${id}` : '') || data.href || tag || '—';
  }

  return '—';
};

const getTimeOnSite = (events = []) => {
  const totalSeconds = events.reduce((sum, item) => {
    if (String(item?.type || '').toLowerCase() !== 'time_on_page') return sum;
    const data = getEventData(item);
    const seconds = parseNumber(data?.seconds ?? data?.durationSeconds ?? data?.duration ?? item?.seconds);
    return seconds === null ? sum : sum + seconds;
  }, 0);

  return totalSeconds > 0 ? formatDuration(totalSeconds) : '—';
};

export const renderVisitorProfileView = async (
  container,
  { apiClient, toast, params = {}, query = {} } = {}
) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();
  const visitorId = params.visitorId;

  let selectedSiteId = '';
  try {
    selectedSiteId = localStorage.getItem(SELECTED_SITE_STORAGE_KEY) || '';
  } catch {
    selectedSiteId = '';
  }
  const siteId = query.siteId || selectedSiteId || '';

  const page = document.createElement('div');
  page.style.display = 'flex';
  page.style.flexDirection = 'column';
  page.style.gap = '20px';
  page.style.width = '100%';
  page.style.maxWidth = '980px';

  const backLink = document.createElement('a');
  backLink.textContent = '← Back to Visitors';
  backLink.href = siteId ? `#/visitors?siteId=${encodeURIComponent(siteId)}` : '#/visitors';
  backLink.style.color = '#2563eb';
  backLink.style.textDecoration = 'none';

  const header = document.createElement('div');
  const title = document.createElement('h2');
  title.textContent = 'Visitor profile';
  title.style.margin = '0';
  const subtitle = document.createElement('p');
  subtitle.textContent = visitorId || '—';
  subtitle.style.margin = '6px 0 0';
  subtitle.style.color = '#64748b';
  header.append(title, subtitle);

  const summaryGrid = document.createElement('div');
  summaryGrid.style.display = 'grid';
  summaryGrid.style.gridTemplateColumns = 'repeat(3, minmax(0, 1fr))';
  summaryGrid.style.gap = '10px';

  const sessionList = document.createElement('div');
  sessionList.style.display = 'flex';
  sessionList.style.gap = '8px';
  sessionList.style.flexWrap = 'wrap';

  const recentSessionsBody = document.createElement('div');
  recentSessionsBody.style.display = 'flex';
  recentSessionsBody.style.flexDirection = 'column';
  recentSessionsBody.style.gap = '10px';

  const timelineBody = document.createElement('div');
  timelineBody.style.display = 'flex';
  timelineBody.style.flexDirection = 'column';
  timelineBody.style.gap = '10px';

  const timelineControls = document.createElement('div');
  timelineControls.style.display = 'flex';
  timelineControls.style.gap = '10px';
  timelineControls.style.flexWrap = 'wrap';

  const typeFilter = document.createElement('select');
  typeFilter.style.padding = '8px 10px';
  typeFilter.style.borderRadius = '6px';
  typeFilter.style.border = '1px solid #cbd5e1';

  const searchInput = document.createElement('input');
  searchInput.type = 'search';
  searchInput.placeholder = 'Search path or details';
  searchInput.style.padding = '8px 10px';
  searchInput.style.borderRadius = '6px';
  searchInput.style.border = '1px solid #cbd5e1';
  searchInput.style.minWidth = '240px';

  timelineControls.append(typeFilter, searchInput);

  const conversationsBody = document.createElement('div');
  conversationsBody.style.display = 'flex';
  conversationsBody.style.flexDirection = 'column';
  conversationsBody.style.gap = '8px';

  const state = {
    detail: null,
    events: [],
    sessions: [],
    selectedCollectorSessionId: '',
    typeFilter: 'all',
    search: '',
    conversations: [],
    selectedConversationId: '',
    selectedMessages: [],
  };

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
  modalHeader.style.justifyContent = 'space-between';
  modalHeader.style.padding = '12px 14px';
  modalHeader.style.borderBottom = '1px solid #e2e8f0';
  const modalTitle = document.createElement('div');
  modalTitle.style.fontWeight = '600';
  modalTitle.textContent = 'Conversation';
  const closeModalButton = document.createElement('button');
  closeModalButton.type = 'button';
  closeModalButton.textContent = 'Close';
  const modalMessages = document.createElement('div');
  modalMessages.style.padding = '12px 14px';
  modalMessages.style.overflowY = 'auto';
  modalMessages.style.display = 'flex';
  modalMessages.style.flexDirection = 'column';
  modalMessages.style.gap = '8px';
  modalHeader.append(modalTitle, closeModalButton);
  modal.append(modalHeader, modalMessages);
  modalOverlay.appendChild(modal);
  document.body.appendChild(modalOverlay);

  const closeModal = () => {
    modalOverlay.style.display = 'none';
  };

  closeModalButton.addEventListener('click', closeModal);
  modalOverlay.addEventListener('click', (event) => {
    if (event.target === modalOverlay) closeModal();
  });

  const fillSummary = () => {
    const detail = state.detail;
    const detailSessions = Array.isArray(detail?.recentSessions) ? detail.recentSessions : [];
    const totalTimeOnSiteSeconds = detailSessions.reduce(
      (sum, session) => sum + (Number.isFinite(session?.timeOnSiteSeconds) ? session.timeOnSiteSeconds : 0),
      0
    );

    summaryGrid.innerHTML = '';
    summaryGrid.append(
      createSummaryValue('First seen', formatDate(detail?.firstSeenAtUtc)),
      createSummaryValue('Last seen', formatDate(detail?.lastSeenAtUtc || state.events[0]?.occurredAtUtc)),
      createSummaryValue('Visit count', detail?.visitCount ?? '—'),
      createSummaryValue('Total pages visited', detail?.totalPagesVisited ?? '—'),
      createSummaryValue('Display name', detail?.displayName || '—'),
      createSummaryValue('Primary email', detail?.primaryEmail || '—'),
      createSummaryValue('Phone', detail?.phone || '—'),
      createSummaryValue('User agent', detail?.userAgent || '—'),
      createSummaryValue('Language', detail?.language || '—'),
      createSummaryValue('Platform', detail?.platform || '—'),
      createSummaryValue('Time on site', totalTimeOnSiteSeconds > 0 ? formatDuration(totalTimeOnSiteSeconds) : getTimeOnSite(state.events)),
      createSummaryValue('Recent events', String(state.events.length))
    );
  };

  const getEventsForSelectedSession = () => {
    if (!state.selectedCollectorSessionId) return state.events;
    return state.events.filter((item) => getCollectorSessionId(item) === state.selectedCollectorSessionId);
  };

  const renderTimeline = (items = []) => {
    timelineBody.innerHTML = '';

    if (!items.length) {
      const empty = document.createElement('div');
      empty.textContent = 'No events found for this visitor.';
      empty.style.color = '#64748b';
      timelineBody.appendChild(empty);
      return;
    }

    const groupedByDate = new Map();
    items.forEach((item) => {
      const key = getDateKey(item?.occurredAtUtc);
      if (!groupedByDate.has(key)) {
        groupedByDate.set(key, []);
      }
      groupedByDate.get(key).push(item);
    });

    const sortedKeys = Array.from(groupedByDate.keys()).sort((left, right) => right.localeCompare(left));

    sortedKeys.forEach((key) => {
      const section = document.createElement('section');
      section.style.display = 'flex';
      section.style.flexDirection = 'column';
      section.style.gap = '8px';

      const heading = document.createElement('div');
      heading.textContent = formatDateHeading(key);
      heading.style.fontWeight = '600';
      heading.style.color = '#0f172a';
      section.appendChild(heading);

      const table = document.createElement('table');
      table.className = 'ui-table';

      const thead = document.createElement('thead');
      const row = document.createElement('tr');
      ['Timestamp', 'Event', 'Path / URL', 'Referrer', 'Details'].forEach((label) => {
        const th = document.createElement('th');
        th.textContent = label;
        row.appendChild(th);
      });
      thead.appendChild(row);
      table.appendChild(thead);

      const tbody = document.createElement('tbody');
      groupedByDate.get(key).forEach((item) => {
        const tr = document.createElement('tr');
        [formatDate(item.occurredAtUtc), item.type || '—', mapPath(item), mapReferrer(item), getTimelineDetails(item)].forEach((value) => {
          const td = document.createElement('td');
          td.textContent = value;
          tr.appendChild(td);
        });
        tbody.appendChild(tr);
      });

      table.appendChild(tbody);
      section.appendChild(table);
      timelineBody.appendChild(section);
    });
  };

  const renderConversationModal = () => {
    modalMessages.innerHTML = '';
    modalTitle.textContent = state.selectedConversationId ? `Conversation ${state.selectedConversationId}` : 'Conversation';

    if (!state.selectedMessages.length) {
      const empty = document.createElement('div');
      empty.textContent = 'No messages for this conversation.';
      empty.style.color = '#64748b';
      modalMessages.appendChild(empty);
      return;
    }

    state.selectedMessages.forEach((message) => {
      const row = document.createElement('div');
      row.style.border = '1px solid #e2e8f0';
      row.style.borderRadius = '8px';
      row.style.padding = '10px 12px';

      const meta = document.createElement('div');
      meta.textContent = `${message.role} • ${formatDate(message.createdAtUtc)}`;
      meta.style.fontSize = '12px';
      meta.style.color = '#64748b';

      const content = document.createElement('div');
      content.textContent = message.content || '—';
      content.style.marginTop = '6px';
      content.style.whiteSpace = 'pre-wrap';

      row.append(meta, content);
      modalMessages.appendChild(row);
    });
  };

  const loadConversationMessages = async (conversationId) => {
    if (!conversationId || !siteId) return;
    try {
      state.selectedConversationId = conversationId;
      state.selectedMessages = await client.engage.getConversationMessages(conversationId, siteId);
      renderConversationModal();
      modalOverlay.style.display = 'flex';
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  const renderConversations = () => {
    conversationsBody.innerHTML = '';

    if (!state.selectedCollectorSessionId) {
      const empty = document.createElement('div');
      empty.textContent = 'Select a visitor session to view matching Engage chats.';
      empty.style.color = '#64748b';
      conversationsBody.appendChild(empty);
      return;
    }

    if (!state.conversations.length) {
      const empty = document.createElement('div');
      empty.textContent = 'No Engage conversations for this visitor session.';
      empty.style.color = '#64748b';
      conversationsBody.appendChild(empty);
      return;
    }

    state.conversations.forEach((conversation) => {
      const row = document.createElement('div');
      row.style.display = 'flex';
      row.style.justifyContent = 'space-between';
      row.style.alignItems = 'center';
      row.style.border = '1px solid #e2e8f0';
      row.style.borderRadius = '8px';
      row.style.padding = '8px 10px';

      const label = document.createElement('div');
      label.textContent = conversation.sessionId || '—';
      label.style.fontFamily = 'ui-monospace, SFMono-Regular, Menlo, monospace';
      label.style.fontSize = '12px';

      const button = document.createElement('button');
      button.type = 'button';
      button.textContent = 'Open';
      button.addEventListener('click', () => loadConversationMessages(conversation.sessionId));

      row.append(label, button);
      conversationsBody.appendChild(row);
    });
  };

  const loadConversationsForSelectedSession = async () => {
    if (!siteId || !state.selectedCollectorSessionId) {
      state.conversations = [];
      renderConversations();
      return;
    }

    try {
      state.conversations = await client.engage.getConversations(siteId, state.selectedCollectorSessionId);
      renderConversations();
    } catch (error) {
      state.conversations = [];
      renderConversations();
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  const setTypeOptions = (events = []) => {
    const typeValues = Array.from(new Set(events.map((item) => String(item?.type || '').toLowerCase()).filter(Boolean))).sort();
    typeFilter.innerHTML = '';

    const allOption = document.createElement('option');
    allOption.value = 'all';
    allOption.textContent = 'All event types';
    typeFilter.appendChild(allOption);

    typeValues.forEach((eventType) => {
      const option = document.createElement('option');
      option.value = eventType;
      option.textContent = eventType;
      typeFilter.appendChild(option);
    });

    typeFilter.value = state.typeFilter;
  };

  const getFilteredTimeline = () => {
    const searchTerm = state.search.trim().toLowerCase();
    return getEventsForSelectedSession().filter((item) => {
      const itemType = String(item?.type || '').toLowerCase();
      if (state.typeFilter !== 'all' && itemType !== state.typeFilter) return false;
      if (!searchTerm) return true;
      return `${mapPath(item)} ${getTimelineDetails(item)}`.toLowerCase().includes(searchTerm);
    });
  };

  const refreshTimeline = () => {
    const selectedEvents = getEventsForSelectedSession();
    setTypeOptions(selectedEvents);
    renderTimeline(getFilteredTimeline());
  };

  const renderSessionList = () => {
    sessionList.innerHTML = '';
    if (!state.sessions.length) {
      const empty = document.createElement('div');
      empty.textContent = 'No recent sessions.';
      empty.style.color = '#64748b';
      sessionList.appendChild(empty);
      return;
    }

    const allButton = document.createElement('button');
    allButton.type = 'button';
    allButton.textContent = 'All sessions';
    allButton.style.padding = '6px 10px';
    allButton.style.borderRadius = '999px';
    allButton.style.border = '1px solid #cbd5e1';
    allButton.style.background = !state.selectedCollectorSessionId ? '#dbeafe' : '#ffffff';
    allButton.addEventListener('click', async () => {
      state.selectedCollectorSessionId = '';
      refreshTimeline();
      await loadConversationsForSelectedSession();
      renderSessionList();
    });
    sessionList.appendChild(allButton);

    state.sessions.forEach((session) => {
      const sessionId = normalizeSessionId(session.sessionId);
      const button = document.createElement('button');
      button.type = 'button';
      button.textContent = `${sessionId} • ${formatDate(session.lastSeenAtUtc || session.lastOccurredAtUtc)}`;
      button.style.padding = '6px 10px';
      button.style.borderRadius = '999px';
      button.style.border = '1px solid #cbd5e1';
      button.style.background = state.selectedCollectorSessionId === sessionId ? '#dbeafe' : '#ffffff';
      button.addEventListener('click', async () => {
        state.selectedCollectorSessionId = sessionId;
        refreshTimeline();
        await loadConversationsForSelectedSession();
        renderSessionList();
      });
      sessionList.appendChild(button);
    });
  };

  const renderRecentSessions = () => {
    recentSessionsBody.innerHTML = '';

    if (!state.sessions.length) {
      const empty = document.createElement('div');
      empty.textContent = 'No recent sessions.';
      empty.style.color = '#64748b';
      recentSessionsBody.appendChild(empty);
      return;
    }

    const table = document.createElement('table');
    table.className = 'ui-table';
    const thead = document.createElement('thead');
    const headRow = document.createElement('tr');
    ['Session', 'First seen', 'Last seen', 'Pages', 'Time', 'Engagement', 'Last path'].forEach((label) => {
      const th = document.createElement('th');
      th.textContent = label;
      headRow.appendChild(th);
    });
    thead.appendChild(headRow);
    table.appendChild(thead);

    const tbody = document.createElement('tbody');
    state.sessions.forEach((session) => {
      const tr = document.createElement('tr');
      const values = [
        normalizeSessionId(session.sessionId),
        formatDate(session.firstSeenAtUtc),
        formatDate(session.lastSeenAtUtc || session.lastOccurredAtUtc),
        session.pagesVisited ?? '—',
        session.timeOnSiteSeconds !== undefined ? formatDuration(session.timeOnSiteSeconds) : '—',
        session.engagementScore ?? '—',
        session.lastPath || '—',
      ];

      values.forEach((value) => {
        const td = document.createElement('td');
        td.textContent = String(value);
        tr.appendChild(td);
      });

      tbody.appendChild(tr);
    });

    table.appendChild(tbody);
    recentSessionsBody.appendChild(table);
  };

  const summaryCard = createCard({ title: 'Summary', body: summaryGrid });

  const recentSessionsCard = createCard({ title: 'Recent sessions', body: recentSessionsBody });

  const timelineCard = createCard({
    title: 'Recent activity by date',
    body: (() => {
      const wrapper = document.createElement('div');
      wrapper.style.display = 'flex';
      wrapper.style.flexDirection = 'column';
      wrapper.style.gap = '10px';
      wrapper.append(sessionList, timelineControls, timelineBody);
      return wrapper;
    })(),
  });

  const conversationsCard = createCard({ title: 'Engage conversations', body: conversationsBody });

  typeFilter.addEventListener('change', () => {
    state.typeFilter = typeFilter.value;
    refreshTimeline();
  });

  searchInput.addEventListener('input', () => {
    state.search = searchInput.value;
    refreshTimeline();
  });

  page.append(backLink, header, summaryCard, recentSessionsCard, timelineCard, conversationsCard);
  container.appendChild(page);

  if (!visitorId || !siteId) {
    fillSummary();
    renderRecentSessions();
    renderSessionList();
    setTypeOptions([]);
    renderTimeline();
    renderConversations();
    return;
  }

  fillSummary();
  renderRecentSessions();
  renderSessionList();
  timelineBody.textContent = 'Loading timeline...';
  recentSessionsBody.textContent = 'Loading sessions...';

  const [detailResult, timelineResult] = await Promise.allSettled([
    client.visitors?.detail
      ? client.visitors.detail(visitorId, siteId)
      : client.request(`/visitors/${visitorId}?siteId=${encodeURIComponent(siteId)}`),
    client.visitors?.timeline
      ? client.visitors.timeline(visitorId, 200, siteId)
      : client.request(`/visitors/${visitorId}/timeline?siteId=${encodeURIComponent(siteId)}&limit=200`),
  ]);

  if (detailResult.status === 'fulfilled') {
    state.detail = detailResult.value || null;
  } else {
    state.detail = null;
    notifier.show({ message: mapApiError(detailResult.reason).message, variant: 'danger' });
  }

  if (timelineResult.status === 'fulfilled') {
    state.events = Array.isArray(timelineResult.value) ? sortByOccurredDesc(timelineResult.value) : [];
  } else {
    state.events = [];
    notifier.show({ message: mapApiError(timelineResult.reason).message, variant: 'danger' });
  }

  const detailSessions = Array.isArray(state.detail?.recentSessions) ? state.detail.recentSessions : [];
  if (detailSessions.length) {
    state.sessions = [...detailSessions].sort(
      (left, right) => new Date(right?.lastSeenAtUtc || 0).getTime() - new Date(left?.lastSeenAtUtc || 0).getTime()
    );
  } else {
    const grouped = new Map();
    state.events.forEach((item) => {
      const sessionId = getCollectorSessionId(item);
      const existing = grouped.get(sessionId);
      if (!existing) {
        grouped.set(sessionId, { sessionId, lastOccurredAtUtc: item?.occurredAtUtc });
      }
    });

    state.sessions = Array.from(grouped.values()).sort(
      (left, right) => new Date(right.lastOccurredAtUtc || 0).getTime() - new Date(left.lastOccurredAtUtc || 0).getTime()
    );
  }

  const validSessionIds = new Set(state.sessions.map((session) => normalizeSessionId(session.sessionId)));
  if (state.selectedCollectorSessionId && !validSessionIds.has(state.selectedCollectorSessionId)) {
    state.selectedCollectorSessionId = '';
  }

  fillSummary();
  renderRecentSessions();
  renderSessionList();
  refreshTimeline();
  await loadConversationsForSelectedSession();
};
