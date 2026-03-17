import { createBadge, createCard, createInput, createTable, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const formatDate = (value) => {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return String(value);
  return date.toLocaleString();
};

const formatNumber = (value) => {
  if (typeof value !== 'number' || Number.isNaN(value)) return '0';
  return value.toLocaleString();
};

const createSummaryGrid = () => {
  const grid = document.createElement('div');
  grid.style.display = 'grid';
  grid.style.gridTemplateColumns = 'repeat(auto-fit, minmax(180px, 1fr))';
  grid.style.gap = '12px';
  return grid;
};

const createMetricCard = ({ label, value, hint } = {}) => {
  const body = document.createElement('div');

  const valueNode = document.createElement('div');
  valueNode.style.fontSize = '24px';
  valueNode.style.fontWeight = '700';
  valueNode.style.color = '#0f172a';
  valueNode.textContent = value ?? '—';

  const hintNode = document.createElement('div');
  hintNode.style.marginTop = '6px';
  hintNode.style.color = '#64748b';
  hintNode.style.fontSize = '12px';
  hintNode.textContent = hint || '';

  body.append(valueNode, hintNode);

  return createCard({ title: label, body });
};

const createHealthBadge = (status) => {
  const normalized = String(status || '').toLowerCase();
  const variant = normalized === 'ok' ? 'success' : 'warning';
  return createBadge({ text: status || 'unknown', variant });
};

export const renderPlatformAdminView = (container, { apiClient, toast, query } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();

  const state = {
    loading: true,
    summary: null,
    operational: null,
    tenants: [],
    totalCount: 0,
    page: Number(query?.page) > 0 ? Number(query.page) : 1,
    pageSize: 25,
    search: typeof query?.search === 'string' ? query.search : '',
    error: '',
  };

  const root = document.createElement('div');
  root.style.display = 'flex';
  root.style.flexDirection = 'column';
  root.style.gap = '16px';
  root.style.width = '100%';
  root.style.maxWidth = '1180px';

  const header = document.createElement('div');
  const title = document.createElement('h2');
  title.textContent = 'Platform Admin';
  title.style.margin = '0';
  const subtitle = document.createElement('p');
  subtitle.style.margin = '6px 0 0 0';
  subtitle.style.color = '#475569';
  subtitle.textContent = 'Platform-level oversight across all tenants.';
  header.append(title, subtitle);

  const controls = document.createElement('div');
  controls.style.display = 'flex';
  controls.style.gap = '8px';
  controls.style.alignItems = 'center';

  const { wrapper: searchWrapper, input: searchInput } = createInput({
    label: 'Search tenants',
    placeholder: 'Tenant name or domain',
    value: state.search,
  });
  searchWrapper.style.minWidth = '320px';

  const searchButton = document.createElement('button');
  searchButton.type = 'button';
  searchButton.textContent = 'Search';
  searchButton.style.padding = '8px 12px';
  searchButton.style.border = '1px solid #cbd5e1';
  searchButton.style.borderRadius = '6px';
  searchButton.style.background = '#fff';

  const clearButton = document.createElement('button');
  clearButton.type = 'button';
  clearButton.textContent = 'Clear';
  clearButton.style.padding = '8px 12px';
  clearButton.style.border = '1px solid #cbd5e1';
  clearButton.style.borderRadius = '6px';
  clearButton.style.background = '#fff';

  controls.append(searchWrapper, searchButton, clearButton);

  const loadingText = document.createElement('div');
  loadingText.style.color = '#475569';
  loadingText.textContent = 'Loading platform data...';

  const errorText = document.createElement('div');
  errorText.style.color = '#b91c1c';
  errorText.style.display = 'none';

  const cardsContainer = createSummaryGrid();

  const tableContainer = document.createElement('div');
  tableContainer.style.display = 'flex';
  tableContainer.style.flexDirection = 'column';
  tableContainer.style.gap = '10px';

  const pagination = document.createElement('div');
  pagination.style.display = 'flex';
  pagination.style.justifyContent = 'space-between';
  pagination.style.alignItems = 'center';

  const prevButton = document.createElement('button');
  prevButton.type = 'button';
  prevButton.textContent = 'Previous';
  prevButton.style.padding = '8px 12px';
  prevButton.style.border = '1px solid #cbd5e1';
  prevButton.style.borderRadius = '6px';
  prevButton.style.background = '#fff';

  const nextButton = document.createElement('button');
  nextButton.type = 'button';
  nextButton.textContent = 'Next';
  nextButton.style.padding = '8px 12px';
  nextButton.style.border = '1px solid #cbd5e1';
  nextButton.style.borderRadius = '6px';
  nextButton.style.background = '#fff';

  const pageInfo = document.createElement('span');
  pageInfo.style.color = '#475569';
  pageInfo.style.fontSize = '13px';

  pagination.append(prevButton, pageInfo, nextButton);

  tableContainer.append(pagination);

  const renderCards = () => {
    cardsContainer.innerHTML = '';

    if (!state.summary) {
      return;
    }

    const entries = [
      ['Tenants', formatNumber(state.summary.totalTenants)],
      ['Sites', formatNumber(state.summary.totalSites)],
      ['Visitors', formatNumber(state.summary.totalVisitors)],
      ['Engage Sessions', formatNumber(state.summary.totalEngageSessions)],
      ['Engage Messages', formatNumber(state.summary.totalEngageMessages)],
      ['Tickets', formatNumber(state.summary.totalTickets)],
      ['Promos', formatNumber(state.summary.totalPromos)],
      ['Promo Entries', formatNumber(state.summary.totalPromoEntries)],
      ['Intelligence Records', formatNumber(state.summary.totalIntelligenceTrendRecords)],
      ['Knowledge Sources', formatNumber(state.summary.totalKnowledgeSources)],
    ];

    entries.forEach(([label, value]) => {
      cardsContainer.appendChild(createMetricCard({ label, value }));
    });

    const healthBody = document.createElement('div');
    healthBody.appendChild(createHealthBadge(state.summary.healthStatus));

    if (state.operational) {
      const details = document.createElement('div');
      details.style.marginTop = '10px';
      details.style.color = '#475569';
      details.style.fontSize = '12px';
      details.textContent = `Knowledge indexed: ${formatNumber(state.operational.indexedKnowledgeSources)} • failed: ${formatNumber(state.operational.failedKnowledgeSources)}`;
      healthBody.appendChild(details);
    }

    cardsContainer.appendChild(createCard({ title: 'Health Status', body: healthBody }));
  };

  const buildTenantTable = () => {
    if (!state.tenants.length) {
      const empty = document.createElement('div');
      empty.style.color = '#64748b';
      empty.textContent = 'No tenants found.';
      return empty;
    }

    const table = createTable({
      columns: [
        { key: 'tenantName', label: 'Tenant' },
        { key: 'domain', label: 'Domain' },
        { key: 'plan', label: 'Plan' },
        { key: 'createdAt', label: 'Created' },
        { key: 'lastActivityAtUtc', label: 'Last Activity' },
        { key: 'sites', label: 'Sites' },
        { key: 'visitors', label: 'Visitors' },
        { key: 'tickets', label: 'Tickets' },
        { key: 'promos', label: 'Promos' },
        { key: 'actions', label: 'Actions' },
      ],
      rows: state.tenants.map((item) => ({
        tenantName: item.tenantName || '',
        domain: item.domain || '',
        plan: item.plan || '',
        createdAt: formatDate(item.createdAt),
        lastActivityAtUtc: formatDate(item.usage?.lastActivityAtUtc),
        sites: formatNumber(item.usage?.siteCount || 0),
        visitors: formatNumber(item.usage?.visitorsCount || 0),
        tickets: formatNumber(item.usage?.ticketsCount || 0),
        promos: formatNumber(item.usage?.promosCount || 0),
        actions: 'View',
      })),
    });

    const rows = table.querySelectorAll('tbody tr');
    rows.forEach((row, index) => {
      const actionCell = row.lastElementChild;
      if (!actionCell) return;
      actionCell.textContent = '';

      const link = document.createElement('a');
      link.href = `#/platform-admin/tenant/${state.tenants[index].tenantId}`;
      link.textContent = 'View';
      link.style.color = '#2563eb';
      link.style.textDecoration = 'none';
      link.style.fontWeight = '500';
      actionCell.appendChild(link);
    });

    return table;
  };

  const renderTenants = () => {
    const existing = tableContainer.querySelector('.platform-admin-table-host');
    if (existing) {
      existing.remove();
    }

    const host = document.createElement('div');
    host.className = 'platform-admin-table-host';
    host.appendChild(buildTenantTable());
    tableContainer.appendChild(host);

    const start = state.totalCount === 0 ? 0 : (state.page - 1) * state.pageSize + 1;
    const end = Math.min(state.page * state.pageSize, state.totalCount);
    pageInfo.textContent = `Showing ${start}-${end} of ${state.totalCount}`;

    prevButton.disabled = state.page <= 1;
    nextButton.disabled = end >= state.totalCount;
  };

  const syncHashQuery = () => {
    const params = new URLSearchParams();
    params.set('page', String(state.page));
    if (state.search) {
      params.set('search', state.search);
    }
    window.location.hash = `#/platform-admin?${params.toString()}`;
  };

  const load = async () => {
    state.loading = true;
    state.error = '';
    loadingText.style.display = 'block';
    errorText.style.display = 'none';

    try {
      const [summary, operational, tenantResponse] = await Promise.all([
        client.platformAdmin.getSummary(),
        client.platformAdmin.getOperationalSummary(),
        client.platformAdmin.listTenants({
          page: state.page,
          pageSize: state.pageSize,
          search: state.search || undefined,
        }),
      ]);

      state.summary = summary;
      state.operational = operational;
      state.tenants = Array.isArray(tenantResponse.items) ? tenantResponse.items : [];
      state.totalCount = Number(tenantResponse.totalCount) || 0;

      renderCards();
      renderTenants();
    } catch (error) {
      const uiError = mapApiError(error);
      state.error = uiError.message;
      notifier.show({ message: uiError.message, variant: 'danger' });
      errorText.textContent = uiError.message;
      errorText.style.display = 'block';
    } finally {
      state.loading = false;
      loadingText.style.display = 'none';
    }
  };

  searchButton.addEventListener('click', async () => {
    state.search = searchInput.value.trim();
    state.page = 1;
    syncHashQuery();
    await load();
  });

  clearButton.addEventListener('click', async () => {
    searchInput.value = '';
    state.search = '';
    state.page = 1;
    syncHashQuery();
    await load();
  });

  searchInput.addEventListener('keydown', async (event) => {
    if (event.key !== 'Enter') return;
    event.preventDefault();
    state.search = searchInput.value.trim();
    state.page = 1;
    syncHashQuery();
    await load();
  });

  prevButton.addEventListener('click', async () => {
    if (state.page <= 1) return;
    state.page -= 1;
    syncHashQuery();
    await load();
  });

  nextButton.addEventListener('click', async () => {
    const maxPage = Math.max(1, Math.ceil(state.totalCount / state.pageSize));
    if (state.page >= maxPage) return;
    state.page += 1;
    syncHashQuery();
    await load();
  });

  root.append(header, controls, loadingText, errorText, cardsContainer, tableContainer);
  container.appendChild(root);
  load();
};

export const renderPlatformAdminTenantDetailView = (container, { apiClient, toast, params } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();
  const tenantId = params?.tenantId;

  const root = document.createElement('div');
  root.style.display = 'flex';
  root.style.flexDirection = 'column';
  root.style.gap = '16px';
  root.style.width = '100%';
  root.style.maxWidth = '1100px';

  const breadcrumb = document.createElement('a');
  breadcrumb.href = '#/platform-admin';
  breadcrumb.textContent = '← Back to Platform Overview';
  breadcrumb.style.color = '#2563eb';
  breadcrumb.style.textDecoration = 'none';
  breadcrumb.style.fontWeight = '500';

  const loadingText = document.createElement('div');
  loadingText.style.color = '#475569';
  loadingText.textContent = 'Loading tenant detail...';

  const content = document.createElement('div');
  content.style.display = 'flex';
  content.style.flexDirection = 'column';
  content.style.gap = '12px';

  root.append(breadcrumb, loadingText, content);
  container.appendChild(root);

  const renderDetail = (detail) => {
    const metadataRows = [
      { field: 'Tenant Name', value: detail.tenantName || '' },
      { field: 'Domain', value: detail.domain || '' },
      { field: 'Plan', value: detail.plan || '' },
      { field: 'Industry', value: detail.industry || '' },
      { field: 'Category', value: detail.category || '' },
      { field: 'Created', value: formatDate(detail.createdAt) },
      { field: 'Updated', value: formatDate(detail.updatedAt) },
      { field: 'Last Activity', value: formatDate(detail.usage?.lastActivityAtUtc) },
    ];

    const metadataTable = createTable({
      columns: [
        { key: 'field', label: 'Field' },
        { key: 'value', label: 'Value' },
      ],
      rows: metadataRows,
    });

    const usageGrid = createSummaryGrid();
    const usageEntries = [
      ['Sites', detail.usage?.siteCount],
      ['Visitors', detail.usage?.visitorsCount],
      ['Engage Sessions', detail.usage?.engageSessionsCount],
      ['Engage Messages', detail.usage?.engageMessagesCount],
      ['Tickets', detail.usage?.ticketsCount],
      ['Promos', detail.usage?.promosCount],
      ['Promo Entries', detail.usage?.promoEntriesCount],
      ['Intelligence Records', detail.usage?.intelligenceRecordCount],
      ['Ads Campaigns', detail.usage?.adsCampaignCount],
      ['Knowledge Sources', detail.usage?.knowledgeSourcesCount],
      ['Knowledge Indexed', detail.usage?.knowledgeIndexedCount],
      ['Knowledge Failed', detail.usage?.knowledgeFailedCount],
    ];

    usageEntries.forEach(([label, value]) => {
      usageGrid.appendChild(createMetricCard({ label, value: formatNumber(Number(value) || 0) }));
    });

    const sites = Array.isArray(detail.sites) ? detail.sites : [];
    const sitesTable = createTable({
      columns: [
        { key: 'domain', label: 'Domain' },
        { key: 'siteId', label: 'Site ID' },
        { key: 'createdAtUtc', label: 'Created' },
        { key: 'updatedAtUtc', label: 'Updated' },
        { key: 'firstEventReceivedAtUtc', label: 'First Event Received' },
      ],
      rows: sites.map((site) => ({
        domain: site.domain || '',
        siteId: site.siteId || '',
        createdAtUtc: formatDate(site.createdAtUtc),
        updatedAtUtc: formatDate(site.updatedAtUtc),
        firstEventReceivedAtUtc: formatDate(site.firstEventReceivedAtUtc),
      })),
    });

    const activity = detail.recentActivity || {};
    const activityRows = [
      { marker: 'Sites', value: formatDate(activity.lastSiteActivityAtUtc) },
      { marker: 'Visitors', value: formatDate(activity.lastVisitorActivityAtUtc) },
      { marker: 'Engage Sessions', value: formatDate(activity.lastEngageSessionActivityAtUtc) },
      { marker: 'Tickets', value: formatDate(activity.lastTicketActivityAtUtc) },
      { marker: 'Promos', value: formatDate(activity.lastPromoActivityAtUtc) },
      { marker: 'Promo Entries', value: formatDate(activity.lastPromoEntryActivityAtUtc) },
      { marker: 'Intelligence', value: formatDate(activity.lastIntelligenceActivityAtUtc) },
      { marker: 'Ads', value: formatDate(activity.lastAdsActivityAtUtc) },
      { marker: 'Knowledge', value: formatDate(activity.lastKnowledgeActivityAtUtc) },
    ];

    const activityTable = createTable({
      columns: [
        { key: 'marker', label: 'Module' },
        { key: 'value', label: 'Last Activity' },
      ],
      rows: activityRows,
    });

    content.replaceChildren(
      createCard({ title: 'Tenant Metadata', body: metadataTable }),
      createCard({ title: 'Usage Summary', body: usageGrid }),
      createCard({ title: 'Sites', body: sites.length ? sitesTable : document.createTextNode('No sites found.') }),
      createCard({ title: 'Recent Activity', body: activityTable })
    );
  };

  const load = async () => {
    if (!tenantId) {
      loadingText.style.display = 'none';
      content.textContent = 'Tenant id is missing.';
      return;
    }

    try {
      const detail = await client.platformAdmin.getTenantDetail(tenantId);
      renderDetail(detail);
    } catch (error) {
      const uiError = mapApiError(error);
      notifier.show({ message: uiError.message, variant: 'danger' });
      content.textContent = uiError.status === 404 ? 'Tenant not found.' : uiError.message;
    } finally {
      loadingText.style.display = 'none';
    }
  };

  load();
};
