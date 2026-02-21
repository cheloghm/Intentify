import { createCard, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const getSiteId = (site) => site?.siteId || site?.id || '';
const SELECTED_SITE_STORAGE_KEY = 'intentify.selectedSiteId';
const DEFAULT_PAGE_SIZE = 20;

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
    // ignore storage errors
  }
};

export const renderTicketsView = async (container, { apiClient, toast, query } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();

  const initialPage = Number.parseInt(query?.page || '1', 10);
  const state = {
    sites: [],
    siteId: query?.siteId || loadSelectedSiteId(),
    tickets: [],
    page: Number.isFinite(initialPage) && initialPage > 0 ? initialPage : 1,
    pageSize: DEFAULT_PAGE_SIZE,
    loadingSites: true,
    loadingTickets: false,
  };

  const page = document.createElement('div');
  page.style.display = 'flex';
  page.style.flexDirection = 'column';
  page.style.gap = '16px';
  page.style.width = '100%';
  page.style.maxWidth = '1000px';

  const controls = document.createElement('div');
  controls.style.display = 'flex';
  controls.style.flexWrap = 'wrap';
  controls.style.gap = '10px';
  controls.style.alignItems = 'flex-end';

  const siteField = document.createElement('label');
  siteField.style.display = 'flex';
  siteField.style.flexDirection = 'column';
  siteField.style.gap = '6px';

  const siteLabel = document.createElement('span');
  siteLabel.textContent = 'Site';
  siteLabel.style.fontSize = '13px';
  siteLabel.style.color = '#334155';

  const siteSelect = document.createElement('select');
  siteSelect.style.minWidth = '280px';
  siteSelect.style.padding = '8px 10px';
  siteSelect.style.borderRadius = '6px';
  siteSelect.style.border = '1px solid #cbd5e1';

  siteField.append(siteLabel, siteSelect);
  controls.append(siteField);

  const listBody = document.createElement('div');
  listBody.style.display = 'flex';
  listBody.style.flexDirection = 'column';
  listBody.style.gap = '10px';

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

  const updateHash = () => {
    const params = new URLSearchParams();
    if (state.siteId) {
      params.set('siteId', state.siteId);
    }
    if (state.page > 1) {
      params.set('page', String(state.page));
    }

    const queryString = params.toString();
    const nextHash = `#/tickets${queryString ? `?${queryString}` : ''}`;
    if (window.location.hash !== nextHash) {
      window.location.hash = nextHash;
    }
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
      option.textContent = site.domain || option.value;
      siteSelect.appendChild(option);
    });

    siteSelect.value = state.siteId || '';
  };

  const renderTickets = () => {
    listBody.innerHTML = '';

    if (state.loadingTickets) {
      listBody.textContent = 'Loading tickets...';
      listBody.style.color = '#64748b';
      pageInfo.textContent = `Page ${state.page}`;
      prevButton.disabled = true;
      nextButton.disabled = true;
      return;
    }

    if (!state.siteId) {
      listBody.textContent = 'Select a site to view tickets.';
      listBody.style.color = '#64748b';
      pageInfo.textContent = 'Page 1';
      prevButton.disabled = true;
      nextButton.disabled = true;
      return;
    }

    if (!state.tickets.length) {
      listBody.textContent = 'No tickets found for this site.';
      listBody.style.color = '#64748b';
      pageInfo.textContent = `Page ${state.page}`;
      prevButton.disabled = state.page <= 1;
      nextButton.disabled = true;
      return;
    }

    const table = document.createElement('table');
    table.className = 'ui-table';

    const headerRow = document.createElement('tr');
    ['ID', 'Subject', 'Status', 'Updated'].forEach((label) => {
      const th = document.createElement('th');
      th.textContent = label;
      headerRow.appendChild(th);
    });

    const thead = document.createElement('thead');
    thead.appendChild(headerRow);
    table.appendChild(thead);

    const tbody = document.createElement('tbody');
    state.tickets.forEach((ticket) => {
      const tr = document.createElement('tr');
      [
        toShortId(ticket.id),
        ticket.subject || '—',
        ticket.status || '—',
        formatDate(ticket.updatedAtUtc),
      ].forEach((value) => {
        const td = document.createElement('td');
        td.textContent = String(value);
        tr.appendChild(td);
      });
      tbody.appendChild(tr);
    });

    table.appendChild(tbody);
    listBody.appendChild(table);

    pageInfo.textContent = `Page ${state.page}`;
    prevButton.disabled = state.page <= 1;
    nextButton.disabled = state.tickets.length < state.pageSize;
  };

  const loadTickets = async () => {
    renderTickets();

    if (!state.siteId) {
      state.tickets = [];
      renderTickets();
      return;
    }

    state.loadingTickets = true;
    renderTickets();

    try {
      state.tickets = client.tickets?.listTickets
        ? await client.tickets.listTickets({ siteId: state.siteId, page: state.page, pageSize: state.pageSize })
        : await client.request(`/tickets?siteId=${encodeURIComponent(state.siteId)}&page=${state.page}&pageSize=${state.pageSize}`);
    } catch (error) {
      state.tickets = [];
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    } finally {
      state.loadingTickets = false;
      renderTickets();
    }
  };

  siteSelect.addEventListener('change', async () => {
    state.siteId = siteSelect.value;
    state.page = 1;
    saveSelectedSiteId(state.siteId);
    updateHash();
    await loadTickets();
  });

  prevButton.addEventListener('click', async () => {
    if (state.page <= 1 || state.loadingTickets) {
      return;
    }

    state.page -= 1;
    updateHash();
    await loadTickets();
  });

  nextButton.addEventListener('click', async () => {
    if (state.loadingTickets || state.tickets.length < state.pageSize) {
      return;
    }

    state.page += 1;
    updateHash();
    await loadTickets();
  });

  page.append(
    createCard({ title: 'Tickets filters', body: controls }),
    createCard({ title: 'Tickets', body: listBody }),
    pagination
  );

  container.appendChild(page);

  try {
    state.sites = await client.sites.list();
    setSiteOptions();
    if (!state.siteId && state.sites.length > 0) {
      state.siteId = getSiteId(state.sites[0]);
      siteSelect.value = state.siteId;
      saveSelectedSiteId(state.siteId);
      updateHash();
    }
    state.loadingSites = false;
    setSiteOptions();
  } catch (error) {
    state.loadingSites = false;
    setSiteOptions();
    notifier.show({ message: mapApiError(error).message, variant: 'danger' });
  }

  await loadTickets();
};
