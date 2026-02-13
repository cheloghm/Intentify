import { createCard, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const createButton = ({ label } = {}) => {
  const button = document.createElement('button');
  button.type = 'button';
  button.textContent = label;
  button.style.padding = '8px 12px';
  button.style.borderRadius = '6px';
  button.style.border = '1px solid #e2e8f0';
  button.style.background = '#ffffff';
  button.style.color = '#1e293b';
  button.style.cursor = 'pointer';
  button.style.fontSize = '13px';
  return button;
};

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

const toShortId = (value) => {
  if (!value || value.length < 8) {
    return value || '—';
  }

  return `${value.slice(0, 8)}…`;
};

const getSiteId = (site) => site?.siteId || site?.id || '';
const SELECTED_SITE_STORAGE_KEY = 'intentify.selectedSiteId';

const loadSelectedSiteId = () => {
  try {
    return localStorage.getItem(SELECTED_SITE_STORAGE_KEY) || '';
  } catch (error) {
    return '';
  }
};

const saveSelectedSiteId = (siteId) => {
  try {
    if (!siteId) {
      localStorage.removeItem(SELECTED_SITE_STORAGE_KEY);
      return;
    }
    localStorage.setItem(SELECTED_SITE_STORAGE_KEY, siteId);
  } catch (error) {
    // ignore storage failures
  }
};

export const renderVisitorsView = async (container, { apiClient, toast, query } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();

  const state = {
    loadingSites: true,
    loadingVisitors: false,
    loadingCounts: false,
    sites: [],
    siteId: query?.siteId || '',
    visitors: [],
    counts: null,
    error: '',
  };

  const page = document.createElement('div');
  page.style.display = 'flex';
  page.style.flexDirection = 'column';
  page.style.gap = '20px';
  page.style.width = '100%';
  page.style.maxWidth = '980px';

  const header = document.createElement('div');
  const title = document.createElement('h2');
  title.textContent = 'Visitors';
  title.style.margin = '0';
  const subtitle = document.createElement('p');
  subtitle.textContent = 'Review visitors and recent activity per site.';
  subtitle.style.margin = '6px 0 0';
  subtitle.style.color = '#64748b';
  header.append(title, subtitle);

  const controls = document.createElement('div');
  controls.style.display = 'flex';
  controls.style.flexWrap = 'wrap';
  controls.style.gap = '10px';
  controls.style.alignItems = 'flex-end';

  const siteField = document.createElement('label');
  siteField.style.display = 'flex';
  siteField.style.flexDirection = 'column';
  siteField.style.gap = '6px';
  const siteFieldLabel = document.createElement('span');
  siteFieldLabel.textContent = 'Site';
  siteFieldLabel.style.fontSize = '13px';
  siteFieldLabel.style.color = '#334155';

  const siteSelect = document.createElement('select');
  siteSelect.style.minWidth = '280px';
  siteSelect.style.padding = '8px 10px';
  siteSelect.style.borderRadius = '6px';
  siteSelect.style.border = '1px solid #cbd5e1';
  siteSelect.style.background = '#ffffff';

  const refreshButton = createButton({ label: 'Refresh' });

  siteField.append(siteFieldLabel, siteSelect);
  controls.append(siteField, refreshButton);

  const errorText = document.createElement('div');
  errorText.style.color = '#dc2626';
  errorText.style.fontSize = '13px';

  const countsRow = document.createElement('div');
  countsRow.style.display = 'grid';
  countsRow.style.gridTemplateColumns = 'repeat(3, minmax(0, 1fr))';
  countsRow.style.gap = '10px';

  const listBody = document.createElement('div');
  listBody.style.display = 'flex';
  listBody.style.flexDirection = 'column';
  listBody.style.gap = '10px';

  const renderCounts = () => {
    countsRow.innerHTML = '';
    const counts = state.counts || {};
    const windows = [
      { label: 'Last 7 days', value: counts.last7 },
      { label: 'Last 30 days', value: counts.last30 },
      { label: 'Last 90 days', value: counts.last90 },
    ];

    windows.forEach((windowItem) => {
      const cell = document.createElement('div');
      cell.style.padding = '10px 12px';
      cell.style.border = '1px solid #e2e8f0';
      cell.style.borderRadius = '8px';
      cell.style.background = '#ffffff';

      const label = document.createElement('div');
      label.textContent = windowItem.label;
      label.style.fontSize = '12px';
      label.style.color = '#64748b';

      const value = document.createElement('div');
      value.textContent = String(windowItem.value ?? '—');
      value.style.fontSize = '20px';
      value.style.fontWeight = '600';
      value.style.color = '#0f172a';

      cell.append(label, value);
      countsRow.appendChild(cell);
    });
  };

  const renderVisitors = () => {
    listBody.innerHTML = '';

    if (state.loadingVisitors) {
      const loading = document.createElement('div');
      loading.textContent = 'Loading visitors...';
      loading.style.color = '#64748b';
      listBody.appendChild(loading);
      return;
    }

    if (!state.visitors.length) {
      const empty = document.createElement('div');
      empty.textContent = state.siteId
        ? 'No visitors yet for this site.'
        : 'Select a site to view visitors.';
      empty.style.color = '#64748b';
      listBody.appendChild(empty);
      return;
    }

    const table = document.createElement('table');
    table.className = 'ui-table';

    const headerRow = document.createElement('tr');
    ['Visitor', 'Last seen', 'Sessions', 'Pages visited', 'Engagement', ''].forEach((label) => {
      const th = document.createElement('th');
      th.textContent = label;
      headerRow.appendChild(th);
    });

    const thead = document.createElement('thead');
    thead.appendChild(headerRow);
    table.appendChild(thead);

    const tbody = document.createElement('tbody');
    state.visitors.forEach((visitor) => {
      const tr = document.createElement('tr');

      const cells = [
        toShortId(visitor.visitorId),
        formatDate(visitor.lastSeenAtUtc),
        visitor.sessionsCount ?? '—',
        visitor.totalPagesVisited ?? '—',
        visitor.lastSessionEngagementScore ?? '—',
      ];

      cells.forEach((value) => {
        const td = document.createElement('td');
        td.textContent = String(value);
        tr.appendChild(td);
      });

      const actionTd = document.createElement('td');
      const link = document.createElement('a');
      const params = new URLSearchParams();
      params.set('siteId', state.siteId);
      params.set('lastSeenAtUtc', visitor.lastSeenAtUtc || '');
      params.set('sessionsCount', String(visitor.sessionsCount ?? ''));
      params.set('totalPagesVisited', String(visitor.totalPagesVisited ?? ''));
      params.set('lastSessionEngagementScore', String(visitor.lastSessionEngagementScore ?? ''));
      link.href = `#/visitors/${visitor.visitorId}?${params.toString()}`;
      link.textContent = 'View';
      link.style.color = '#2563eb';
      link.style.textDecoration = 'none';
      actionTd.appendChild(link);
      tr.appendChild(actionTd);

      tbody.appendChild(tr);
    });

    table.appendChild(tbody);
    listBody.appendChild(table);
  };

  const setSiteOptions = () => {
    siteSelect.innerHTML = '';

    const placeholder = document.createElement('option');
    placeholder.value = '';
    placeholder.textContent = state.loadingSites ? 'Loading sites...' : 'Select a site';
    siteSelect.appendChild(placeholder);

    state.sites.forEach((site) => {
      const option = document.createElement('option');
      option.value = getSiteId(site);
      option.textContent = site.domain || getSiteId(site);
      siteSelect.appendChild(option);
    });

    siteSelect.value = state.siteId || '';
  };

  const loadVisitors = async () => {
    state.error = '';
    errorText.textContent = '';

    if (!state.siteId) {
      state.visitors = [];
      state.counts = null;
      renderCounts();
      renderVisitors();
      return;
    }

    state.loadingVisitors = true;
    state.loadingCounts = true;
    renderVisitors();

    try {
      const [visitors, counts] = await Promise.all([
        client.visitors?.list
          ? client.visitors.list(state.siteId, 1, 50)
          : client.request(`/visitors?siteId=${encodeURIComponent(state.siteId)}&page=1&pageSize=50`),
        client.visitors?.visitCounts
          ? client.visitors.visitCounts(state.siteId)
          : client.request(`/visitors/visits/counts?siteId=${encodeURIComponent(state.siteId)}`),
      ]);

      state.visitors = Array.isArray(visitors) ? visitors : [];
      state.counts = counts || null;
    } catch (error) {
      const uiError = mapApiError(error);
      state.error = uiError.message;
      errorText.textContent = state.error;
      notifier.show({ message: uiError.message, variant: 'danger' });
      state.visitors = [];
      state.counts = null;
    } finally {
      state.loadingVisitors = false;
      state.loadingCounts = false;
      renderCounts();
      renderVisitors();
    }
  };

  siteSelect.addEventListener('change', () => {
    state.siteId = siteSelect.value;
    saveSelectedSiteId(state.siteId);
    if (state.siteId) {
      window.location.hash = `#/visitors?siteId=${encodeURIComponent(state.siteId)}`;
      return;
    }
    window.location.hash = '#/visitors';
  });

  refreshButton.addEventListener('click', loadVisitors);

  const countsCard = createCard({
    title: 'Visit counts',
    body: countsRow,
  });

  const visitorsCard = createCard({
    title: 'Visitors list',
    body: listBody,
  });

  page.append(header, controls, errorText, countsCard, visitorsCard);
  container.appendChild(page);

  renderCounts();
  renderVisitors();
  setSiteOptions();

  try {
    const sites = await client.request('/sites');
    state.sites = Array.isArray(sites) ? sites : [];
    const siteIds = new Set(state.sites.map((site) => getSiteId(site)).filter(Boolean));
    const localSiteId = loadSelectedSiteId();

    if (state.siteId && siteIds.has(state.siteId)) {
      saveSelectedSiteId(state.siteId);
    } else if (localSiteId && siteIds.has(localSiteId)) {
      state.siteId = localSiteId;
    } else if (state.sites.length) {
      state.siteId = getSiteId(state.sites[0]);
      saveSelectedSiteId(state.siteId);
    } else {
      state.siteId = '';
      saveSelectedSiteId('');
    }
  } catch (error) {
    const uiError = mapApiError(error);
    state.error = uiError.message;
    errorText.textContent = state.error;
  } finally {
    state.loadingSites = false;
    setSiteOptions();
  }

  await loadVisitors();
};
