import { createCard, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const SELECTED_SITE_STORAGE_KEY = 'intentify.selectedSiteId';

const formatDate = (value) => {
  if (!value) {
    return '—';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '—';
  }

  return date.toLocaleString();
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
  if (!url) {
    return '—';
  }

  try {
    const parsedUrl = new URL(url);
    const path = parsedUrl.pathname || '/';
    const search = parsedUrl.search || '';
    const hash = parsedUrl.hash || '';
    const combined = `${path}${search}${hash}`;
    return combined || url;
  } catch (error) {
    return url;
  }
};

const mapReferrer = (timelineItem) => {
  const referrer = timelineItem?.referrer;
  if (!referrer) {
    return 'Direct / None';
  }

  try {
    return new URL(referrer).hostname || referrer;
  } catch (error) {
    return referrer;
  }
};

const sortByOccurredDesc = (items) => {
  return [...items].sort((left, right) => {
    const leftMs = new Date(left?.occurredAtUtc || 0).getTime();
    const rightMs = new Date(right?.occurredAtUtc || 0).getTime();
    return rightMs - leftMs;
  });
};

const getEventData = (timelineItem) => {
  const raw = timelineItem?.metadataSummary ?? timelineItem?.data;
  if (!raw) {
    return null;
  }

  if (typeof raw === 'string') {
    try {
      const parsed = JSON.parse(raw);
      return parsed && typeof parsed === 'object' ? parsed : null;
    } catch (error) {
      return null;
    }
  }

  return typeof raw === 'object' ? raw : null;
};

const parseNumber = (value) => {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value;
  }

  if (typeof value === 'string') {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }

  return null;
};

const formatDuration = (secondsValue) => {
  const seconds = parseNumber(secondsValue);
  if (seconds === null) {
    return '—';
  }

  const totalSeconds = Math.max(0, Math.round(seconds));
  const minutes = Math.floor(totalSeconds / 60);
  const remainingSeconds = totalSeconds % 60;
  return `${minutes}m ${remainingSeconds}s`;
};

const getTimelineDetails = (item) => {
  const data = getEventData(item);
  if (!data) {
    return '—';
  }

  const type = String(item?.type || '').toLowerCase();
  if (type === 'time_on_page') {
    const duration = formatDuration(data.seconds);
    if (duration === '—') {
      return '—';
    }

    const reason = typeof data.reason === 'string' ? data.reason.trim() : '';
    return reason ? `${duration} (${reason})` : duration;
  }

  if (type === 'scroll_depth') {
    const percent = parseNumber(data.percent ?? data.value);
    return percent === null ? '—' : `${Math.round(percent)}%`;
  }

  if (type === 'pageview') {
    return data.title || data.pageTitle || '—';
  }

  if (type === 'click' || type === 'outbound_click') {
    const text = typeof data.text === 'string' ? data.text.trim() : '';
    const id = typeof data.id === 'string' ? data.id.trim() : '';
    const tag = typeof data.tag === 'string' ? data.tag.trim() : '';
    if (text && id) {
      return `${text} (#${id})`;
    }
    return text || (id ? `#${id}` : '') || data.href || tag || '—';
  }

  return '—';
};

const getTimeOnSite = (events = []) => {
  const totalSeconds = events.reduce((sum, item) => {
    if (String(item?.type || '').toLowerCase() !== 'time_on_page') {
      return sum;
    }

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
  } catch (error) {
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

  const state = {
    events: [],
    typeFilter: 'all',
    search: '',
  };

  const fillSummary = (events = []) => {
    const sessionsFromTimeline = new Set(
      events.map((item) => item?.metadataSummary?.sessionId).filter(Boolean)
    ).size;

    const pagesVisited = query.totalPagesVisited || events.length || '—';

    summaryGrid.innerHTML = '';
    summaryGrid.append(
      createSummaryValue('Last seen', formatDate(query.lastSeenAtUtc || events[0]?.occurredAtUtc)),
      createSummaryValue('Sessions count', query.sessionsCount || (sessionsFromTimeline || '—')),
      createSummaryValue('Pages visited', String(pagesVisited)),
      createSummaryValue('Time on site', getTimeOnSite(events)),
      createSummaryValue('Engagement score', query.lastSessionEngagementScore || '—'),
      createSummaryValue('Recent events', String(events.length))
    );
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
    items.forEach((item) => {
      const tr = document.createElement('tr');
      const values = [
        formatDate(item.occurredAtUtc),
        item.type || '—',
        mapPath(item),
        mapReferrer(item),
        getTimelineDetails(item),
      ];

      values.forEach((value) => {
        const td = document.createElement('td');
        td.textContent = value;
        tr.appendChild(td);
      });

      tbody.appendChild(tr);
    });

    table.appendChild(tbody);
    timelineBody.appendChild(table);
  };

  const summaryCard = createCard({
    title: 'Summary',
    body: summaryGrid,
  });

  const timelineCard = createCard({
    title: 'Timeline',
    body: (() => {
      const wrapper = document.createElement('div');
      wrapper.style.display = 'flex';
      wrapper.style.flexDirection = 'column';
      wrapper.style.gap = '10px';
      wrapper.append(timelineControls, timelineBody);
      return wrapper;
    })(),
  });

  const getFilteredTimeline = () => {
    const searchTerm = state.search.trim().toLowerCase();
    return state.events.filter((item) => {
      const itemType = String(item?.type || '').toLowerCase();
      if (state.typeFilter !== 'all' && itemType !== state.typeFilter) {
        return false;
      }

      if (!searchTerm) {
        return true;
      }

      const haystack = `${mapPath(item)} ${getTimelineDetails(item)}`.toLowerCase();
      return haystack.includes(searchTerm);
    });
  };

  const refreshTimeline = () => {
    renderTimeline(getFilteredTimeline());
  };

  const setTypeOptions = (events = []) => {
    const typeValues = Array.from(
      new Set(
        events
          .map((item) => String(item?.type || '').toLowerCase())
          .filter(Boolean)
      )
    ).sort();

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

  typeFilter.addEventListener('change', () => {
    state.typeFilter = typeFilter.value;
    refreshTimeline();
  });

  searchInput.addEventListener('input', () => {
    state.search = searchInput.value;
    refreshTimeline();
  });

  page.append(backLink, header, summaryCard, timelineCard);
  container.appendChild(page);

  if (!visitorId || !siteId) {
    fillSummary();
    renderTimeline();
    return;
  }

  timelineBody.textContent = 'Loading timeline...';

  try {
    const timeline = client.visitors?.timeline
      ? await client.visitors.timeline(visitorId, 200, siteId)
      : await client.request(
          `/visitors/${visitorId}/timeline?siteId=${encodeURIComponent(siteId)}&limit=200`
        );

    const sortedTimeline = Array.isArray(timeline) ? sortByOccurredDesc(timeline) : [];
    state.events = sortedTimeline;
    setTypeOptions(sortedTimeline);
    fillSummary(sortedTimeline);
    refreshTimeline();
  } catch (error) {
    const uiError = mapApiError(error);
    notifier.show({ message: uiError.message, variant: 'danger' });
    state.events = [];
    setTypeOptions([]);
    fillSummary();
    refreshTimeline();
  }
};
