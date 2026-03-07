import { createCard, createInput, createTable, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const DEFAULT_FILTERS = {
  siteId: '',
  category: 'general',
  location: 'US',
  timeWindow: '7d',
  audienceType: '',
  provider: '',
  keyword: '',
};

const TIME_WINDOW_OPTIONS = ['24h', '7d', '30d', '90d'];
const AUDIENCE_OPTIONS = ['', 'B2B', 'B2C'];

const createSelectField = ({ label, value, options }) => {
  const wrapper = document.createElement('label');
  wrapper.className = 'ui-input';

  const labelText = document.createElement('span');
  labelText.textContent = label;

  const select = document.createElement('select');
  select.className = 'ui-input__field';

  options.forEach((option) => {
    const optionNode = document.createElement('option');
    if (typeof option === 'string') {
      optionNode.value = option;
      optionNode.textContent = option || 'Any';
    } else {
      optionNode.value = option.value;
      optionNode.textContent = option.label;
    }

    if (optionNode.value === value) {
      optionNode.selected = true;
    }

    select.appendChild(optionNode);
  });

  wrapper.append(labelText, select);
  return { wrapper, select };
};

const formatTimestamp = (value) => {
  if (!value) {
    return '—';
  }

  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? '—' : parsed.toLocaleString();
};

const createSummaryRows = (dashboard) => {
  const list = document.createElement('div');
  list.style.display = 'grid';
  list.style.gridTemplateColumns = 'repeat(auto-fit, minmax(180px, 1fr))';
  list.style.gap = '12px';

  const items = [
    { label: 'Site', value: dashboard.siteId || '—' },
    { label: 'Provider', value: dashboard.provider || '—' },
    { label: 'Audience', value: dashboard.audienceType || 'Any' },
    { label: 'Total Items', value: dashboard.totalItems ?? 0 },
    { label: 'Average Score', value: dashboard.summary?.averageScore ?? 0 },
    { label: 'Max Score', value: dashboard.summary?.maxScore ?? 0 },
    { label: 'Refreshed At', value: formatTimestamp(dashboard.refreshedAtUtc) },
  ];

  items.forEach(({ label, value }) => {
    const item = document.createElement('div');
    item.style.background = '#f8fafc';
    item.style.border = '1px solid #e2e8f0';
    item.style.borderRadius = '8px';
    item.style.padding = '10px 12px';

    const labelNode = document.createElement('div');
    labelNode.style.fontSize = '12px';
    labelNode.style.color = '#64748b';
    labelNode.textContent = label;

    const valueNode = document.createElement('div');
    valueNode.style.fontWeight = '600';
    valueNode.style.color = '#0f172a';
    valueNode.textContent = String(value);

    item.append(labelNode, valueNode);
    list.appendChild(item);
  });

  return list;
};

export const renderIntelligenceView = (container, { apiClient, toast } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();

  const state = {
    filters: { ...DEFAULT_FILTERS },
    sites: [],
  };

  const page = document.createElement('div');
  page.style.display = 'flex';
  page.style.flexDirection = 'column';
  page.style.gap = '16px';
  page.style.width = '100%';
  page.style.maxWidth = '1100px';

  const statusText = document.createElement('div');
  statusText.style.color = '#334155';

  const filterGrid = document.createElement('div');
  filterGrid.style.display = 'grid';
  filterGrid.style.gridTemplateColumns = 'repeat(auto-fit, minmax(180px, 1fr))';
  filterGrid.style.gap = '12px';

  const keywordField = createInput({ label: 'Keyword', value: state.filters.keyword, placeholder: 'e.g. lead generation' });
  const categoryField = createInput({ label: 'Category', value: state.filters.category });
  const locationField = createInput({ label: 'Location', value: state.filters.location });
  const providerField = createInput({ label: 'Provider', value: state.filters.provider, placeholder: 'Google' });
  const timeWindowField = createSelectField({
    label: 'Time Window',
    value: state.filters.timeWindow,
    options: TIME_WINDOW_OPTIONS,
  });
  const audienceTypeField = createSelectField({
    label: 'Audience Type',
    value: state.filters.audienceType,
    options: AUDIENCE_OPTIONS,
  });
  const siteField = createSelectField({
    label: 'Site',
    value: state.filters.siteId,
    options: [{ value: '', label: 'Loading sites...' }],
  });

  const actions = document.createElement('div');
  actions.style.display = 'flex';
  actions.style.gap = '8px';
  actions.style.marginTop = '8px';

  const applyButton = document.createElement('button');
  applyButton.type = 'button';
  applyButton.textContent = 'Apply filters';
  applyButton.style.padding = '10px 14px';
  applyButton.style.borderRadius = '6px';
  applyButton.style.border = 'none';
  applyButton.style.background = '#2563eb';
  applyButton.style.color = '#fff';
  applyButton.style.cursor = 'pointer';

  const resetButton = document.createElement('button');
  resetButton.type = 'button';
  resetButton.textContent = 'Reset';
  resetButton.style.padding = '10px 14px';
  resetButton.style.borderRadius = '6px';
  resetButton.style.border = '1px solid #cbd5e1';
  resetButton.style.background = '#fff';
  resetButton.style.color = '#0f172a';
  resetButton.style.cursor = 'pointer';

  actions.append(applyButton, resetButton);

  const summaryBody = document.createElement('div');
  summaryBody.style.color = '#334155';

  const trendsBody = document.createElement('div');
  trendsBody.style.color = '#334155';

  const setLoading = (isLoading) => {
    applyButton.disabled = isLoading;
    resetButton.disabled = isLoading;
    applyButton.textContent = isLoading ? 'Loading...' : 'Apply filters';
  };

  const readFiltersFromInputs = () => ({
    siteId: siteField.select.value,
    category: categoryField.input.value.trim(),
    location: locationField.input.value.trim(),
    timeWindow: timeWindowField.select.value,
    audienceType: audienceTypeField.select.value,
    provider: providerField.input.value.trim(),
    keyword: keywordField.input.value.trim(),
  });

  const syncSiteOptions = () => {
    siteField.select.innerHTML = '';

    if (!state.sites.length) {
      const option = document.createElement('option');
      option.value = '';
      option.textContent = 'No sites available';
      siteField.select.appendChild(option);
      return;
    }

    state.sites.forEach((site) => {
      const option = document.createElement('option');
      option.value = site.siteId || site.id;
      option.textContent = site.domain || option.value;
      if (option.value === state.filters.siteId) {
        option.selected = true;
      }
      siteField.select.appendChild(option);
    });
  };

  const renderSummary = (dashboard) => {
    summaryBody.replaceChildren(
      createSummaryRows(dashboard),
    );
  };

  const renderTrends = (dashboard) => {
    const items = Array.isArray(dashboard.topItems) ? dashboard.topItems : [];
    if (!items.length) {
      const empty = document.createElement('div');
      empty.style.color = '#475569';
      empty.textContent = 'No trends found for the selected filters.';
      trendsBody.replaceChildren(empty);
      return;
    }

    const table = createTable({
      columns: [
        { key: 'queryOrTopic', label: 'Query / Topic' },
        { key: 'score', label: 'Score' },
        { key: 'rank', label: 'Rank' },
        { key: 'provider', label: 'Provider' },
      ],
      rows: items.map((item) => ({
        queryOrTopic: item.queryOrTopic,
        score: item.score,
        rank: item.rank ?? '—',
        provider: item.provider || dashboard.provider || '—',
      })),
    });

    trendsBody.replaceChildren(table);
  };

  const loadDashboard = async () => {
    const filters = readFiltersFromInputs();
    state.filters = { ...state.filters, ...filters };

    if (!state.filters.siteId) {
      statusText.textContent = 'No site found. Create a site to load intelligence data.';
      summaryBody.textContent = '';
      trendsBody.textContent = '';
      return;
    }

    setLoading(true);
    statusText.textContent = 'Loading intelligence dashboard...';

    try {
      const dashboard = await client.intelligence.dashboard({
        siteId: state.filters.siteId,
        category: state.filters.category,
        location: state.filters.location,
        timeWindow: state.filters.timeWindow,
        audienceType: state.filters.audienceType,
        provider: state.filters.provider,
        keyword: state.filters.keyword,
      });

      statusText.textContent = `Showing trends for ${state.filters.category} in ${state.filters.location} (${state.filters.timeWindow}).`;
      renderSummary(dashboard);
      renderTrends(dashboard);
    } catch (error) {
      const uiError = mapApiError(error);
      if (uiError.status === 401 || uiError.status === 403) {
        statusText.textContent = 'You are not authorized to view intelligence data. Please sign in again.';
      } else {
        statusText.textContent = 'Unable to load intelligence dashboard.';
        notifier.show({ message: uiError.message, variant: 'danger' });
      }

      summaryBody.textContent = '';
      trendsBody.textContent = '';
    } finally {
      setLoading(false);
    }
  };

  const resetFilters = () => {
    const preservedSiteId = state.filters.siteId;
    state.filters = { ...DEFAULT_FILTERS, siteId: preservedSiteId };

    categoryField.input.value = state.filters.category;
    locationField.input.value = state.filters.location;
    timeWindowField.select.value = state.filters.timeWindow;
    audienceTypeField.select.value = state.filters.audienceType;
    providerField.input.value = state.filters.provider;
    keywordField.input.value = state.filters.keyword;

    loadDashboard();
  };

  applyButton.addEventListener('click', loadDashboard);
  resetButton.addEventListener('click', resetFilters);

  filterGrid.append(
    siteField.wrapper,
    categoryField.wrapper,
    locationField.wrapper,
    timeWindowField.wrapper,
    audienceTypeField.wrapper,
    providerField.wrapper,
    keywordField.wrapper,
  );

  page.append(
    createCard({
      title: 'Intelligence Dashboard',
      body: (() => {
        const body = document.createElement('div');
        body.style.display = 'flex';
        body.style.flexDirection = 'column';
        body.style.gap = '12px';
        body.append(statusText, filterGrid, actions);
        return body;
      })(),
    }),
    createCard({ title: 'Summary', body: summaryBody }),
    createCard({ title: 'Top Trends', body: trendsBody }),
  );

  container.appendChild(page);

  const initialize = async () => {
    try {
      const sites = await client.sites.list();
      state.sites = Array.isArray(sites) ? sites : [];
      state.filters.siteId = state.sites[0]?.siteId || state.sites[0]?.id || '';
      syncSiteOptions();
      if (state.filters.siteId) {
        siteField.select.value = state.filters.siteId;
      }
      await loadDashboard();
    } catch (error) {
      const uiError = mapApiError(error);
      if (uiError.status === 401 || uiError.status === 403) {
        statusText.textContent = 'You are not authorized to view intelligence data. Please sign in again.';
      } else {
        statusText.textContent = 'Unable to load intelligence data.';
        notifier.show({ message: uiError.message, variant: 'danger' });
      }
      summaryBody.textContent = '';
      trendsBody.textContent = '';
    }
  };

  initialize();
};
