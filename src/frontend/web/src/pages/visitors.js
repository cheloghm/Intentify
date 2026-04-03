import { createApiClient, mapApiError } from '../shared/apiClient.js';
import { createToastManager } from '../shared/ui/index.js';

const formatDate = (value) => {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
};

const toShortId = (value) => {
  if (!value || value.length < 8) return value || '—';
  return `${value.slice(0, 8)}…`;
};

const getSiteId = (site) => site?.siteId || site?.id || '';
const SELECTED_SITE_STORAGE_KEY = 'intentify.selectedSiteId';

const loadSelectedSiteId = () => {
  try { return localStorage.getItem(SELECTED_SITE_STORAGE_KEY) || ''; } catch { return ''; }
};

const saveSelectedSiteId = (siteId) => {
  try {
    if (!siteId) { localStorage.removeItem(SELECTED_SITE_STORAGE_KEY); return; }
    localStorage.setItem(SELECTED_SITE_STORAGE_KEY, siteId);
  } catch { /* ignore */ }
};

const PAGE_SIZE = 10;

export const renderVisitorsView = async (container, { apiClient, toast, query } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();

  const state = {
    loadingSites: true,
    loadingVisitors: false,
    sites: [],
    siteId: query?.siteId || '',
    visitors: [],
    counts: null,
    onlineNow: null,
    pageAnalytics: null,
    currentPage: 1,
  };

  // ── Root ──────────────────────────────────────────────────────────────
  const page = document.createElement('div');
  page.className = 'stack';
  page.style.maxWidth = '1100px';
  page.style.width = '100%';

  // ── Page header ───────────────────────────────────────────────────────
  const pageHeader = document.createElement('div');
  pageHeader.className = 'page-header';

  const titleGroup = document.createElement('div');
  const titleEl = document.createElement('h1');
  titleEl.className = 'page-title';
  titleEl.textContent = 'Visitors';
  const subtitleEl = document.createElement('div');
  subtitleEl.className = 'page-subtitle';
  subtitleEl.textContent = 'Track and understand your website visitors in real time';
  titleGroup.append(titleEl, subtitleEl);

  const controls = document.createElement('div');
  controls.style.display = 'flex';
  controls.style.alignItems = 'center';
  controls.style.gap = '8px';

  const siteSelect = document.createElement('select');
  siteSelect.className = 'form-select';
  siteSelect.style.minWidth = '220px';

  const refreshBtn = document.createElement('button');
  refreshBtn.className = 'btn btn-secondary';
  refreshBtn.textContent = '↻  Refresh';

  controls.append(siteSelect, refreshBtn);
  pageHeader.append(titleGroup, controls);

  // ── Metric cards ──────────────────────────────────────────────────────
  const metricsGrid = document.createElement('div');
  metricsGrid.className = 'grid-4';

  const makeMetricCard = (icon, label, iconBg) => {
    const card = document.createElement('div');
    card.className = 'metric-card';

    const iconEl = document.createElement('div');
    iconEl.className = 'metric-icon';
    iconEl.textContent = icon;
    iconEl.style.background = iconBg;

    const labelEl = document.createElement('div');
    labelEl.className = 'metric-label';
    labelEl.textContent = label;

    const valueEl = document.createElement('div');
    valueEl.className = 'metric-value';
    valueEl.textContent = '—';

    card.append(iconEl, labelEl, valueEl);
    return { card, valueEl };
  };

  const mTotal   = makeMetricCard('👥', 'Total Visitors',   'var(--brand-primary-light)');
  const mOnline  = makeMetricCard('🟢', 'Online Now',       'var(--color-success-light)');
  const mAvgTime = makeMetricCard('⏱', 'Avg Time on Page', 'var(--color-warning-light)');
  const mReturn  = makeMetricCard('↩', 'Return Rate',      'var(--color-info-light)');
  metricsGrid.append(mTotal.card, mOnline.card, mAvgTime.card, mReturn.card);

  // ── Charts row ────────────────────────────────────────────────────────
  const chartsRow = document.createElement('div');
  chartsRow.className = 'grid-2';

  // Traffic line chart
  const trafficCard = document.createElement('div');
  trafficCard.className = 'card';

  const trafficHeader = document.createElement('div');
  trafficHeader.className = 'card-header';
  const trafficTitleGroup = document.createElement('div');
  const trafficTitle = document.createElement('div');
  trafficTitle.className = 'card-title';
  trafficTitle.textContent = 'Visitor Traffic';
  const trafficSub = document.createElement('div');
  trafficSub.className = 'card-subtitle';
  trafficSub.textContent = 'Last 7 days';
  trafficTitleGroup.append(trafficTitle, trafficSub);
  trafficHeader.append(trafficTitleGroup);

  const chartWrap = document.createElement('div');
  chartWrap.style.position = 'relative';
  chartWrap.style.height = '200px';
  const chartCanvas = document.createElement('canvas');
  chartWrap.appendChild(chartCanvas);
  trafficCard.append(trafficHeader, chartWrap);

  // Top pages bar chart
  const topPagesCard = document.createElement('div');
  topPagesCard.className = 'card';

  const topPagesHeader = document.createElement('div');
  topPagesHeader.className = 'card-header';
  const topPagesTitleGroup = document.createElement('div');
  const topPagesTitle = document.createElement('div');
  topPagesTitle.className = 'card-title';
  topPagesTitle.textContent = 'Top Pages';
  const topPagesSub = document.createElement('div');
  topPagesSub.className = 'card-subtitle';
  topPagesSub.textContent = 'Last 7 days · by pageviews';
  topPagesTitleGroup.append(topPagesTitle, topPagesSub);
  topPagesHeader.append(topPagesTitleGroup);

  const topPagesBody = document.createElement('div');
  topPagesBody.className = 'stack';
  topPagesBody.style.gap = '10px';
  topPagesCard.append(topPagesHeader, topPagesBody);

  chartsRow.append(trafficCard, topPagesCard);

  // ── Visitors table card ───────────────────────────────────────────────
  const tableCard = document.createElement('div');
  tableCard.className = 'card';

  const tableCardHeader = document.createElement('div');
  tableCardHeader.className = 'card-header';
  const tableTitleGroup = document.createElement('div');
  const tableTitleEl = document.createElement('div');
  tableTitleEl.className = 'card-title';
  tableTitleEl.textContent = 'Visitors';
  const tableCountEl = document.createElement('div');
  tableCountEl.className = 'card-subtitle';
  tableCountEl.textContent = 'Select a site above';
  tableTitleGroup.append(tableTitleEl, tableCountEl);
  tableCardHeader.append(tableTitleGroup);

  const tableWrapper = document.createElement('div');
  tableWrapper.className = 'table-wrapper';

  const paginationEl = document.createElement('div');
  paginationEl.className = 'pagination';
  const paginationInfo = document.createElement('div');
  const paginationControls = document.createElement('div');
  paginationControls.className = 'pagination-controls';
  paginationEl.append(paginationInfo, paginationControls);

  tableCard.append(tableCardHeader, tableWrapper, paginationEl);

  // Error
  const errorEl = document.createElement('div');
  errorEl.style.color = 'var(--color-danger)';
  errorEl.style.fontSize = '13px';

  // ── Render helpers ────────────────────────────────────────────────────
  const getOnlineSet = () => {
    const s = new Set();
    (state.onlineNow?.visitors || []).forEach((v) => s.add(v.visitorId));
    return s;
  };

  const updateMetrics = () => {
    const total = state.visitors.length;
    const onlineCount = state.onlineNow?.count ?? 0;

    const pages = state.pageAnalytics?.pages;
    let avgTime = '—';
    if (Array.isArray(pages) && pages.length) {
      const avg = pages.reduce((s, p) => s + (p.avgTimeOnPageSeconds || 0), 0) / pages.length;
      avgTime = `${Math.round(avg)}s`;
    }

    let returnRate = '—';
    if (total > 0) {
      const returning = state.visitors.filter((v) => (v.sessionsCount || 0) > 1).length;
      returnRate = `${Math.round((returning / total) * 100)}%`;
    }

    mTotal.valueEl.textContent   = total || '—';
    mOnline.valueEl.textContent  = onlineCount || '—';
    mAvgTime.valueEl.textContent = avgTime;
    mReturn.valueEl.textContent  = returnRate;
  };

  const renderTopPages = () => {
    topPagesBody.innerHTML = '';
    const pages = state.pageAnalytics?.pages;

    if (!pages || !pages.length) {
      const empty = document.createElement('div');
      empty.style.padding = '24px 0';
      empty.style.textAlign = 'center';
      empty.style.color = 'var(--color-text-muted)';
      empty.style.fontSize = '13px';
      empty.textContent = state.siteId ? 'No page data yet.' : 'Select a site to see top pages.';
      topPagesBody.appendChild(empty);
      return;
    }

    const maxViews = Math.max(...pages.map((p) => p.pageViews || 0));
    pages.slice(0, 8).forEach((p) => {
      const row = document.createElement('div');

      const labelRow = document.createElement('div');
      labelRow.style.display = 'flex';
      labelRow.style.justifyContent = 'space-between';
      labelRow.style.fontSize = '12px';
      labelRow.style.marginBottom = '4px';

      const path = document.createElement('span');
      path.style.color = 'var(--color-text-secondary)';
      path.style.overflow = 'hidden';
      path.style.textOverflow = 'ellipsis';
      path.style.whiteSpace = 'nowrap';
      path.style.maxWidth = '75%';
      path.textContent = p.pageUrl || '—';

      const count = document.createElement('span');
      count.style.color = 'var(--color-text-muted)';
      count.style.fontWeight = '500';
      count.textContent = p.pageViews ?? 0;
      labelRow.append(path, count);

      const track = document.createElement('div');
      track.style.height = '6px';
      track.style.background = 'var(--color-surface-raised)';
      track.style.borderRadius = '999px';
      track.style.overflow = 'hidden';

      const fill = document.createElement('div');
      fill.style.height = '100%';
      fill.style.width = `${maxViews > 0 ? Math.round(((p.pageViews || 0) / maxViews) * 100) : 0}%`;
      fill.style.background = 'var(--brand-primary)';
      fill.style.borderRadius = '999px';
      track.appendChild(fill);

      row.append(labelRow, track);
      topPagesBody.appendChild(row);
    });
  };

  const renderTable = () => {
    tableWrapper.innerHTML = '';
    paginationInfo.textContent = '';
    paginationControls.innerHTML = '';

    if (state.loadingVisitors) {
      const loading = document.createElement('div');
      loading.className = 'empty-state';
      loading.textContent = 'Loading visitors…';
      tableWrapper.appendChild(loading);
      return;
    }

    const onlineSet = getOnlineSet();
    const total = state.visitors.length;
    const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
    const page = Math.min(state.currentPage, totalPages);
    const start = (page - 1) * PAGE_SIZE;
    const end = Math.min(start + PAGE_SIZE, total);
    const slice = state.visitors.slice(start, end);

    tableCountEl.textContent = total
      ? `${total} visitor${total !== 1 ? 's' : ''}`
      : 'No visitors found';

    if (!total) {
      const empty = document.createElement('div');
      empty.className = 'empty-state';
      const icon = document.createElement('div');
      icon.className = 'empty-state-icon';
      icon.textContent = '👤';
      const emptyTitle = document.createElement('div');
      emptyTitle.className = 'empty-state-title';
      emptyTitle.textContent = state.siteId ? 'No visitors yet' : 'Select a site';
      const emptyDesc = document.createElement('div');
      emptyDesc.className = 'empty-state-desc';
      emptyDesc.textContent = state.siteId
        ? 'No events received yet. Install your snippet and verify.'
        : 'Choose a site from the dropdown above.';
      empty.append(icon, emptyTitle, emptyDesc);
      tableWrapper.appendChild(empty);
      return;
    }

    const table = document.createElement('table');
    table.className = 'data-table';

    const thead = document.createElement('thead');
    const headRow = document.createElement('tr');
    ['Visitor', 'Last Seen', 'Sessions', 'Pages Visited', 'Engagement', 'Status', ''].forEach((label) => {
      const th = document.createElement('th');
      th.textContent = label;
      headRow.appendChild(th);
    });
    thead.appendChild(headRow);
    table.appendChild(thead);

    const tbody = document.createElement('tbody');
    slice.forEach((visitor) => {
      const tr = document.createElement('tr');
      tr.style.cursor = 'pointer';

      const buildParams = () => {
        const p = new URLSearchParams();
        p.set('siteId', state.siteId);
        p.set('lastSeenAtUtc', visitor.lastSeenAtUtc || '');
        p.set('sessionsCount', String(visitor.sessionsCount ?? ''));
        p.set('totalPagesVisited', String(visitor.totalPagesVisited ?? ''));
        p.set('lastSessionEngagementScore', String(visitor.lastSessionEngagementScore ?? ''));
        return p.toString();
      };

      tr.addEventListener('click', () => {
        window.location.hash = `#/visitors/${visitor.visitorId}?${buildParams()}`;
      });

      const isOnline = onlineSet.has(visitor.visitorId);

      const idTd = document.createElement('td');
      const idSpan = document.createElement('span');
      idSpan.className = 'text-primary';
      idSpan.textContent = toShortId(visitor.visitorId);
      idTd.appendChild(idSpan);

      const lastSeenTd = document.createElement('td');
      lastSeenTd.textContent = formatDate(visitor.lastSeenAtUtc);

      const sessionsTd = document.createElement('td');
      sessionsTd.textContent = visitor.sessionsCount ?? '—';

      const pagesTd = document.createElement('td');
      pagesTd.textContent = visitor.totalPagesVisited ?? '—';

      const engageTd = document.createElement('td');
      engageTd.textContent = visitor.lastSessionEngagementScore ?? '—';

      const statusTd = document.createElement('td');
      const badge = document.createElement('span');
      badge.className = isOnline ? 'badge badge-success' : 'badge badge-neutral';
      badge.textContent = isOnline ? '● Online' : 'Offline';
      statusTd.appendChild(badge);

      const actionTd = document.createElement('td');
      const viewLink = document.createElement('a');
      viewLink.href = `#/visitors/${visitor.visitorId}?${buildParams()}`;
      viewLink.className = 'btn btn-sm btn-secondary';
      viewLink.textContent = 'View';
      viewLink.addEventListener('click', (e) => e.stopPropagation());
      actionTd.appendChild(viewLink);

      tr.append(idTd, lastSeenTd, sessionsTd, pagesTd, engageTd, statusTd, actionTd);
      tbody.appendChild(tr);
    });

    table.appendChild(tbody);
    tableWrapper.appendChild(table);

    // Pagination
    paginationInfo.textContent = `Showing ${start + 1}–${end} of ${total} visitor${total !== 1 ? 's' : ''}`;

    if (totalPages > 1) {
      const prevBtn = document.createElement('button');
      prevBtn.className = 'page-btn';
      prevBtn.textContent = '← Prev';
      prevBtn.disabled = page <= 1;
      prevBtn.addEventListener('click', () => { state.currentPage = page - 1; renderTable(); });

      const pageInfoSpan = document.createElement('span');
      pageInfoSpan.style.padding = '0 6px';
      pageInfoSpan.style.fontSize = '12px';
      pageInfoSpan.style.color = 'var(--color-text-muted)';
      pageInfoSpan.textContent = `${page} / ${totalPages}`;

      const nextBtn = document.createElement('button');
      nextBtn.className = 'page-btn';
      nextBtn.textContent = 'Next →';
      nextBtn.disabled = page >= totalPages;
      nextBtn.addEventListener('click', () => { state.currentPage = page + 1; renderTable(); });

      paginationControls.append(prevBtn, pageInfoSpan, nextBtn);
    }
  };

  const setSiteOptions = () => {
    siteSelect.innerHTML = '';
    const placeholder = document.createElement('option');
    placeholder.value = '';
    placeholder.textContent = state.loadingSites ? 'Loading sites…' : 'Select a site';
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
    errorEl.textContent = '';
    if (!state.siteId) {
      state.visitors = [];
      state.counts = null;
      state.onlineNow = null;
      state.pageAnalytics = null;
      state.currentPage = 1;
      updateMetrics();
      renderTopPages();
      renderTable();
      return;
    }

    state.loadingVisitors = true;
    state.currentPage = 1;
    renderTable();

    try {
      const [visitors, counts, onlineNow, pageAnalytics] = await Promise.all([
        client.visitors?.list
          ? client.visitors.list(state.siteId, 1, 50)
          : client.request(`/visitors?siteId=${encodeURIComponent(state.siteId)}&page=1&pageSize=50`),
        client.visitors?.visitCounts
          ? client.visitors.visitCounts(state.siteId)
          : client.request(`/visitors/visits/counts?siteId=${encodeURIComponent(state.siteId)}`),
        client.visitors?.onlineNow
          ? client.visitors.onlineNow(state.siteId, 5, 20)
          : client.request(`/visitors/online-now?siteId=${encodeURIComponent(state.siteId)}&windowMinutes=5&limit=20`),
        client.visitors?.pageAnalytics
          ? client.visitors.pageAnalytics(state.siteId, 7, 10)
          : client.request(`/visitors/analytics/pages?siteId=${encodeURIComponent(state.siteId)}&days=7&limit=10`),
      ]);
      state.visitors      = Array.isArray(visitors) ? visitors : [];
      state.counts        = counts || null;
      state.onlineNow     = onlineNow || null;
      state.pageAnalytics = pageAnalytics || null;
    } catch (error) {
      const uiError = mapApiError(error);
      errorEl.textContent = uiError.message;
      notifier.show({ message: uiError.message, variant: 'danger' });
      state.visitors = [];
      state.counts = null;
      state.onlineNow = null;
      state.pageAnalytics = null;
    } finally {
      state.loadingVisitors = false;
      updateMetrics();
      renderTopPages();
      renderTable();
    }
  };

  siteSelect.addEventListener('change', () => {
    state.siteId = siteSelect.value;
    saveSelectedSiteId(state.siteId);
    window.location.hash = state.siteId
      ? `#/visitors?siteId=${encodeURIComponent(state.siteId)}`
      : '#/visitors';
  });
  refreshBtn.addEventListener('click', loadVisitors);

  // ── Assemble DOM ──────────────────────────────────────────────────────
  page.append(pageHeader, metricsGrid, chartsRow, tableCard, errorEl);
  container.appendChild(page);

  // Init Chart.js after canvas is in DOM
  requestAnimationFrame(() => {
    if (!window.Chart) return;
    new window.Chart(chartCanvas, {
      type: 'line',
      data: {
        labels: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
        datasets: [{
          label: 'Visitors',
          data: [12, 19, 8, 25, 31, 18, 24],
          borderColor: '#6366f1',
          backgroundColor: 'rgba(99,102,241,0.08)',
          fill: true,
          tension: 0.4,
          pointBackgroundColor: '#6366f1',
          pointRadius: 4,
          pointHoverRadius: 6,
        }],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
          tooltip: { backgroundColor: '#0f172a', padding: 10, cornerRadius: 8 },
        },
        scales: {
          y: {
            beginAtZero: true,
            grid: { color: '#f1f5f9' },
            ticks: { color: '#94a3b8', font: { size: 11 } },
          },
          x: {
            grid: { display: false },
            ticks: { color: '#94a3b8', font: { size: 11 } },
          },
        },
      },
    });
  });

  // Initial empty renders
  updateMetrics();
  renderTopPages();
  renderTable();
  setSiteOptions();

  // Load sites, then visitors
  try {
    const sites = await client.request('/sites');
    state.sites = Array.isArray(sites) ? sites : [];
    const siteIds = new Set(state.sites.map(getSiteId).filter(Boolean));
    const localId = loadSelectedSiteId();

    if (state.siteId && siteIds.has(state.siteId)) {
      saveSelectedSiteId(state.siteId);
    } else if (localId && siteIds.has(localId)) {
      state.siteId = localId;
    } else if (state.sites.length) {
      state.siteId = getSiteId(state.sites[0]);
      saveSelectedSiteId(state.siteId);
    } else {
      state.siteId = '';
      saveSelectedSiteId('');
    }
  } catch (error) {
    errorEl.textContent = mapApiError(error).message;
  } finally {
    state.loadingSites = false;
    setSiteOptions();
  }

  await loadVisitors();
};
