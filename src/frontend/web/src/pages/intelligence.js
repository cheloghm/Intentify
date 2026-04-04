import { createCard, createInput, createTable, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const DEFAULT_FILTERS = {
  siteId: '',
  category: '',
  location: '',
  timeWindow: '7d',
  audienceType: '',
  provider: '',
  keyword: '',
};

const TIME_WINDOW_OPTIONS = ['24h', '7d', '30d', '90d'];
const AUDIENCE_OPTIONS = ['', 'B2B', 'B2C'];

const createSelectField = ({ label, value, options }) => {
  const wrapper = document.createElement('label');
  wrapper.style.cssText = 'display:flex;flex-direction:column;gap:4px;font-size:12px;font-weight:500;color:var(--color-text-muted);';

  const labelText = document.createElement('span');
  labelText.textContent = label;

  const select = document.createElement('select');
  select.className = 'form-select';

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

const createMetricCard = (label, value, icon = '') => {
  const card = document.createElement('div');
  card.className = 'metric-card';
  if (icon) {
    const iconEl = document.createElement('div');
    iconEl.className = 'metric-icon';
    iconEl.style.background = 'var(--brand-primary-light)';
    iconEl.textContent = icon;
    card.appendChild(iconEl);
  }
  const labelEl = document.createElement('div');
  labelEl.className = 'metric-label';
  labelEl.textContent = label;
  const valueEl = document.createElement('div');
  valueEl.className = 'metric-value';
  valueEl.textContent = String(value ?? '—');
  card.append(labelEl, valueEl);
  return card;
};

const createSummaryRows = (dashboard) => {
  const grid = document.createElement('div');
  grid.className = 'grid-4';

  grid.append(
    createMetricCard('Total Trend Items', dashboard.totalItems ?? 0, '📊'),
    createMetricCard('Average Score', dashboard.summary?.averageScore ?? 0, '⭐'),
    createMetricCard('Top Ranked Items', dashboard.summary?.rankedItemsCount ?? 0, '🏆'),
    createMetricCard('Date Range', dashboard.timeWindow || '—', '📅'),
  );

  return grid;
};

const parseCsv = (value) =>
  value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);

const stringifyList = (values) => (Array.isArray(values) ? values.join(', ') : '');

export const renderIntelligenceView = (container, { apiClient, toast } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();

  const state = {
    filters: { ...DEFAULT_FILTERS },
    sites: [],
    loading: false,
    profileLoading: false,
  };

  const page = document.createElement('div');
  page.style.display = 'flex';
  page.style.flexDirection = 'column';
  page.style.gap = '16px';
  page.style.width = '100%';
  page.style.maxWidth = '1100px';

  const pageHeader = document.createElement('div');
  pageHeader.className = 'page-header';
  const headerLeft = document.createElement('div');
  const pageTitle = document.createElement('h2');
  pageTitle.className = 'page-title';
  pageTitle.textContent = 'Market Intelligence';
  const pageSubtitle = document.createElement('p');
  pageSubtitle.className = 'page-subtitle';
  pageSubtitle.textContent = 'Search trends and keyword insights for your market';
  headerLeft.append(pageTitle, pageSubtitle);
  pageHeader.appendChild(headerLeft);

  const profileStatusText = document.createElement('div');
  profileStatusText.style.color = '#475569';
  profileStatusText.style.fontSize = '13px';

  const profileGrid = document.createElement('div');
  profileGrid.style.display = 'grid';
  profileGrid.style.gridTemplateColumns = 'repeat(auto-fit, minmax(220px, 1fr))';
  profileGrid.style.gap = '12px';

  const profileNameField = createInput({ label: 'Profile Name', placeholder: 'Business profile name' });
  const profileIndustryField = createInput({ label: 'Industry Category', placeholder: 'e.g. Retail' });
  const profileAudienceField = createSelectField({
    label: 'Primary Audience Type',
    value: '',
    options: AUDIENCE_OPTIONS,
  });
  const profileLocationsField = createInput({ label: 'Target Locations', placeholder: 'US, CA' });
  const profileProductsField = createInput({ label: 'Products / Services', placeholder: 'Consulting, Audits' });
  const profileWatchTopicsField = createInput({ label: 'Watch Topics', placeholder: 'Optional, comma separated' });
  const profileSeasonalPrioritiesField = createInput({ label: 'Seasonal Priorities', placeholder: 'Optional, comma separated' });
  const profileRefreshIntervalField = createInput({ label: 'Refresh Interval Minutes', type: 'number', placeholder: 'Optional' });

  const profileIsActiveWrapper = document.createElement('label');
  profileIsActiveWrapper.className = 'ui-input';
  const profileIsActiveText = document.createElement('span');
  profileIsActiveText.textContent = 'Profile Active';
  const profileIsActiveField = document.createElement('input');
  profileIsActiveField.type = 'checkbox';
  profileIsActiveField.checked = true;
  profileIsActiveWrapper.append(profileIsActiveText, profileIsActiveField);

  const profileActions = document.createElement('div');
  profileActions.style.display = 'flex';
  profileActions.style.gap = '8px';

  const saveProfileButton = document.createElement('button');
  saveProfileButton.type = 'button';
  saveProfileButton.textContent = 'Save profile';
  saveProfileButton.style.padding = '10px 14px';
  saveProfileButton.style.borderRadius = '6px';
  saveProfileButton.style.border = 'none';
  saveProfileButton.style.background = '#2563eb';
  saveProfileButton.style.color = '#fff';
  saveProfileButton.style.cursor = 'pointer';

  profileActions.append(saveProfileButton);

  profileGrid.append(
    profileNameField.wrapper,
    profileIndustryField.wrapper,
    profileAudienceField.wrapper,
    profileLocationsField.wrapper,
    profileProductsField.wrapper,
    profileWatchTopicsField.wrapper,
    profileSeasonalPrioritiesField.wrapper,
    profileRefreshIntervalField.wrapper,
    profileIsActiveWrapper,
  );

  const statusText = document.createElement('div');
  statusText.style.color = '#334155';

  const filterGrid = document.createElement('div');
  filterGrid.style.cssText = 'display:flex;flex-wrap:wrap;gap:12px;align-items:flex-end;';

  const makeFilterInput = (label, placeholder, initialValue = '') => {
    const wrapper = document.createElement('label');
    wrapper.style.cssText = 'display:flex;flex-direction:column;gap:4px;font-size:12px;font-weight:500;color:var(--color-text-muted);flex:1;min-width:140px;';
    const labelEl = document.createElement('span');
    labelEl.textContent = label;
    const input = document.createElement('input');
    input.type = 'text';
    input.className = 'form-input';
    input.placeholder = placeholder;
    input.value = initialValue;
    wrapper.append(labelEl, input);
    return { wrapper, input };
  };

  const keywordField = makeFilterInput('Keyword', 'e.g. lead generation', state.filters.keyword);
  const categoryField = makeFilterInput('Category', 'Profile default', state.filters.category);
  const locationField = makeFilterInput('Location', 'Profile default', state.filters.location);
  const providerField = makeFilterInput('Provider', 'Google', state.filters.provider);
  const timeWindowField = createSelectField({
    label: 'Time Window',
    value: state.filters.timeWindow,
    options: TIME_WINDOW_OPTIONS,
  });
  timeWindowField.wrapper.style.flex = '0 0 auto';
  const audienceTypeField = createSelectField({
    label: 'Audience Type',
    value: state.filters.audienceType,
    options: AUDIENCE_OPTIONS,
  });
  audienceTypeField.wrapper.style.flex = '0 0 auto';
  const siteField = createSelectField({
    label: 'Site',
    value: state.filters.siteId,
    options: [{ value: '', label: 'Loading sites...' }],
  });
  siteField.wrapper.style.flex = '0 0 160px';

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

  const siteInsightsBody = document.createElement('div');
  siteInsightsBody.style.color = '#334155';
  siteInsightsBody.style.whiteSpace = 'pre-wrap';

  const trendsBody = document.createElement('div');
  trendsBody.style.color = '#334155';

  const setLoading = (isLoading) => {
    state.loading = isLoading;
    applyButton.disabled = isLoading;
    resetButton.disabled = isLoading;
    applyButton.textContent = isLoading ? 'Loading...' : 'Apply filters';
  };

  const setProfileLoading = (isLoading) => {
    state.profileLoading = isLoading;
    saveProfileButton.disabled = isLoading;
    saveProfileButton.textContent = isLoading ? 'Saving...' : 'Save profile';
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

  const readProfileFromInputs = () => {
    const refreshValue = profileRefreshIntervalField.input.value.trim();

    return {
      profileName: profileNameField.input.value.trim(),
      industryCategory: profileIndustryField.input.value.trim(),
      primaryAudienceType: profileAudienceField.select.value,
      targetLocations: parseCsv(profileLocationsField.input.value),
      primaryProductsOrServices: parseCsv(profileProductsField.input.value),
      watchTopics: parseCsv(profileWatchTopicsField.input.value),
      seasonalPriorities: parseCsv(profileSeasonalPrioritiesField.input.value),
      isActive: Boolean(profileIsActiveField.checked),
      refreshIntervalMinutes: refreshValue ? Number(refreshValue) : null,
    };
  };

  const applyProfileToInputs = (profile) => {
    profileNameField.input.value = profile?.profileName || '';
    profileIndustryField.input.value = profile?.industryCategory || '';
    profileAudienceField.select.value = profile?.primaryAudienceType || '';
    profileLocationsField.input.value = stringifyList(profile?.targetLocations);
    profileProductsField.input.value = stringifyList(profile?.primaryProductsOrServices);
    profileWatchTopicsField.input.value = stringifyList(profile?.watchTopics);
    profileSeasonalPrioritiesField.input.value = stringifyList(profile?.seasonalPriorities);
    profileRefreshIntervalField.input.value = profile?.refreshIntervalMinutes ? String(profile.refreshIntervalMinutes) : '';
    profileIsActiveField.checked = profile?.isActive ?? true;
  };

  const applyProfileDefaultsToFilters = (profile) => {
    if (!profile) {
      return;
    }

    if (!categoryField.input.value && profile.industryCategory) {
      categoryField.input.value = profile.industryCategory;
    }

    if (!locationField.input.value
      && Array.isArray(profile.targetLocations)
      && profile.targetLocations.length) {
      locationField.input.value = profile.targetLocations[0];
    }

    if (!audienceTypeField.select.value && profile.primaryAudienceType) {
      audienceTypeField.select.value = profile.primaryAudienceType;
    }
  };

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

  const loadProfile = async () => {
    const siteId = siteField.select.value || state.filters.siteId;
    if (!siteId) {
      applyProfileToInputs(null);
      profileStatusText.textContent = 'Select a site to manage profile defaults.';
      return;
    }

    profileStatusText.textContent = 'Loading profile...';

    try {
      const profile = await client.intelligence.getProfile(siteId);
      applyProfileToInputs(profile);
      applyProfileDefaultsToFilters(profile);
      profileStatusText.textContent = 'Profile loaded.';
    } catch (error) {
      const uiError = mapApiError(error);
      if (uiError.status === 404) {
        applyProfileToInputs(null);
        profileStatusText.textContent = 'No profile saved for this site yet.';
        return;
      }

      profileStatusText.textContent = 'Unable to load profile.';
      notifier.show({ message: uiError.message, variant: 'danger' });
    }
  };

  const saveProfile = async () => {
    const siteId = siteField.select.value || state.filters.siteId;
    if (!siteId) {
      notifier.show({ message: 'Select a site before saving profile.', variant: 'warning' });
      return;
    }

    setProfileLoading(true);
    profileStatusText.textContent = 'Saving profile...';

    try {
      const payload = readProfileFromInputs();
      const saved = await client.intelligence.upsertProfile(siteId, payload);
      applyProfileToInputs(saved);
      applyProfileDefaultsToFilters(saved);
      profileStatusText.textContent = 'Profile saved.';
      notifier.show({ message: 'Intelligence profile saved.', variant: 'success' });
    } catch (error) {
      const uiError = mapApiError(error);
      profileStatusText.textContent = 'Unable to save profile.';
      notifier.show({ message: uiError.message, variant: 'danger' });
    } finally {
      setProfileLoading(false);
    }
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
      empty.className = 'empty-state';
      const emptyDesc = document.createElement('div');
      emptyDesc.className = 'empty-state-desc';
      emptyDesc.textContent = 'No trends found for the selected filters.';
      empty.appendChild(emptyDesc);
      trendsBody.replaceChildren(empty);
      return;
    }

    const tableWrap = document.createElement('div');
    tableWrap.className = 'table-wrapper';
    const table = document.createElement('table');
    table.className = 'data-table';

    const thead = document.createElement('thead');
    const headRow = document.createElement('tr');
    ['Keyword / Topic', 'Score', 'Rank', 'Provider', 'Category'].forEach((col) => {
      const th = document.createElement('th');
      th.textContent = col;
      headRow.appendChild(th);
    });
    thead.appendChild(headRow);

    const tbody = document.createElement('tbody');
    items.forEach((item) => {
      const tr = document.createElement('tr');

      const topicCell = document.createElement('td');
      topicCell.className = 'text-primary';
      topicCell.textContent = item.queryOrTopic || '—';

      const scoreCell = document.createElement('td');
      const scoreVal = Number(item.score) || 0;
      const scoreWrap = document.createElement('div');
      scoreWrap.style.cssText = 'display:flex;align-items:center;gap:8px;min-width:80px;';
      const scoreNum = document.createElement('span');
      scoreNum.style.cssText = 'font-size:12px;font-weight:500;min-width:28px;';
      scoreNum.textContent = scoreVal;
      const barTrack = document.createElement('div');
      barTrack.style.cssText = 'flex:1;background:var(--color-border);border-radius:4px;height:4px;';
      const barFill = document.createElement('div');
      const pct = Math.min(100, Math.max(0, scoreVal));
      barFill.style.cssText = `width:${pct}%;background:var(--brand-primary);height:4px;border-radius:4px;`;
      barTrack.appendChild(barFill);
      scoreWrap.append(scoreNum, barTrack);
      scoreCell.appendChild(scoreWrap);

      const rankCell = document.createElement('td');
      rankCell.textContent = item.rank != null ? String(item.rank) : '—';

      const providerCell = document.createElement('td');
      const providerVal = item.provider || dashboard.provider || '—';
      if (providerVal !== '—') {
        const badge = document.createElement('span');
        badge.className = 'badge badge-info';
        badge.textContent = providerVal;
        providerCell.appendChild(badge);
      } else {
        providerCell.textContent = '—';
      }

      const categoryCell = document.createElement('td');
      categoryCell.style.color = 'var(--color-text-muted)';
      categoryCell.textContent = item.category || dashboard.category || '—';

      tr.append(topicCell, scoreCell, rankCell, providerCell, categoryCell);
      tbody.appendChild(tr);
    });

    table.append(thead, tbody);
    tableWrap.appendChild(table);
    trendsBody.replaceChildren(tableWrap);
  };

  const loadDashboard = async () => {
    const filters = readFiltersFromInputs();
    state.filters = { ...state.filters, ...filters };

    if (!state.filters.siteId) {
      statusText.textContent = 'No site found. Create a site to load intelligence data.';
      summaryBody.textContent = '';
      siteInsightsBody.textContent = '';
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

      statusText.textContent = `Showing trends for ${dashboard.category} in ${dashboard.location} (${dashboard.timeWindow}).`;
      renderSummary(dashboard);
      renderTrends(dashboard);

      try {
        const siteSummary = await client.intelligence.siteSummary({
          siteId: state.filters.siteId,
          category: state.filters.category,
          location: state.filters.location,
          timeWindow: state.filters.timeWindow,
          audienceType: state.filters.audienceType,
          provider: state.filters.provider,
          keyword: state.filters.keyword,
        });

        const source = siteSummary.usedAi ? 'AI-assisted' : 'Deterministic';
        siteInsightsBody.textContent = `${siteSummary.summary}\n\n(${source} summary • ${formatTimestamp(siteSummary.generatedAtUtc)})`;
      } catch (summaryError) {
        siteInsightsBody.textContent = 'Site insights summary is unavailable right now.';
      }
    } catch (error) {
      const uiError = mapApiError(error);
      if (uiError.status === 401 || uiError.status === 403) {
        statusText.textContent = 'You are not authorized to view intelligence data. Please sign in again.';
      } else {
        statusText.textContent = 'Unable to load intelligence dashboard.';
        notifier.show({ message: uiError.message, variant: 'danger' });
      }

      summaryBody.textContent = '';
      siteInsightsBody.textContent = '';
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
  saveProfileButton.addEventListener('click', saveProfile);
  siteField.select.addEventListener('change', async () => {
    state.filters.siteId = siteField.select.value;
    await loadProfile();
    await loadDashboard();
  });

  filterGrid.append(
    siteField.wrapper,
    keywordField.wrapper,
    categoryField.wrapper,
    locationField.wrapper,
    timeWindowField.wrapper,
    audienceTypeField.wrapper,
    providerField.wrapper,
  );

  page.append(
    pageHeader,
    createCard({
      title: 'Intelligence Profile',
      body: (() => {
        const body = document.createElement('div');
        body.style.display = 'flex';
        body.style.flexDirection = 'column';
        body.style.gap = '12px';
        body.append(profileStatusText, profileGrid, profileActions);
        return body;
      })(),
    }),
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
    createCard({ title: 'Site Insights Summary', body: siteInsightsBody }),
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
      await loadProfile();
      await loadDashboard();
    } catch (error) {
      const uiError = mapApiError(error);
      if (uiError.status === 401 || uiError.status === 403) {
        statusText.textContent = 'You are not authorized to view intelligence data. Please sign in again.';
      } else {
        statusText.textContent = 'Unable to load intelligence data.';
        profileStatusText.textContent = 'Unable to load profile data.';
        notifier.show({ message: uiError.message, variant: 'danger' });
      }
      summaryBody.textContent = '';
      siteInsightsBody.textContent = '';
      trendsBody.textContent = '';
    }
  };

  initialize();
};
