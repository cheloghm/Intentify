import { createCard, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const button = (label, primary = false) => {
  const b = document.createElement('button');
  b.type = 'button';
  b.textContent = label;
  b.style.padding = '8px 12px';
  b.style.borderRadius = '6px';
  b.style.border = primary ? 'none' : '1px solid #cbd5e1';
  b.style.background = primary ? '#2563eb' : '#fff';
  b.style.color = primary ? '#fff' : '#1e293b';
  b.style.cursor = 'pointer';
  return b;
};

const getSiteId = (site) => site?.siteId || site?.id || '';
const getPromoId = (promo) => promo?.id || promo?.promoId || '';

export const renderPromosView = (container, { apiClient, toast } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();
  const state = { sites: [], promos: [], entries: [], siteId: '', selectedPromoId: '' };

  const page = document.createElement('div');
  page.style.display = 'flex';
  page.style.flexDirection = 'column';
  page.style.gap = '16px';
  page.style.width = '100%';
  page.style.maxWidth = '1000px';

  const header = document.createElement('h2');
  header.textContent = 'Promos';
  header.style.margin = '0';
  page.appendChild(header);

  const createBody = document.createElement('form');
  createBody.style.display = 'grid';
  createBody.style.gridTemplateColumns = '1fr 1fr';
  createBody.style.gap = '8px';

  const siteSelect = document.createElement('select');
  const nameInput = document.createElement('input');
  const descriptionInput = document.createElement('input');
  [siteSelect, nameInput, descriptionInput].forEach((el) => {
    el.style.padding = '8px 10px';
    el.style.border = '1px solid #cbd5e1';
    el.style.borderRadius = '6px';
  });
  nameInput.placeholder = 'Promo name';
  descriptionInput.placeholder = 'Description';
  const createBtn = button('Create promo', true);
  createBtn.type = 'submit';
  createBody.append(siteSelect, nameInput, descriptionInput, createBtn);

  const promosBody = document.createElement('div');
  promosBody.style.display = 'flex';
  promosBody.style.flexDirection = 'column';
  promosBody.style.gap = '8px';

  const entriesBody = document.createElement('div');
  entriesBody.style.display = 'flex';
  entriesBody.style.flexDirection = 'column';
  entriesBody.style.gap = '8px';

  const exportBtn = button('Export CSV');
  exportBtn.addEventListener('click', () => {
    if (!state.entries.length) {
      notifier.show({ message: 'No entries to export.', variant: 'warning' });
      return;
    }

    const headers = ['id', 'promoId', 'visitorId', 'firstPartyId', 'sessionId', 'email', 'name', 'createdAtUtc'];
    const rows = state.entries.map((entry) => headers.map((h) => JSON.stringify(entry[h] ?? '')).join(','));
    const csv = [headers.join(','), ...rows].join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'promo-entries.csv';
    a.click();
    URL.revokeObjectURL(url);
  });

  const renderSites = () => {
    siteSelect.innerHTML = '';
    const empty = document.createElement('option');
    empty.value = '';
    empty.textContent = state.sites.length ? 'Select site' : 'No sites';
    siteSelect.appendChild(empty);
    state.sites.forEach((site) => {
      const opt = document.createElement('option');
      opt.value = getSiteId(site);
      opt.textContent = site.domain || opt.value;
      siteSelect.appendChild(opt);
    });
    siteSelect.value = state.siteId;
  };

  const renderPromos = () => {
    promosBody.innerHTML = '';
    if (!state.promos.length) {
      const empty = document.createElement('div');
      empty.textContent = 'No promos found.';
      empty.style.color = '#64748b';
      promosBody.appendChild(empty);
      return;
    }

    state.promos.forEach((promo) => {
      const row = document.createElement('div');
      row.style.display = 'flex';
      row.style.justifyContent = 'space-between';
      row.style.alignItems = 'center';
      row.style.padding = '8px';
      row.style.border = '1px solid #e2e8f0';
      row.style.borderRadius = '6px';

      const left = document.createElement('div');
      left.textContent = `${promo.name} · ${promo.publicKey}`;
      const viewBtn = button('View entries');
      viewBtn.addEventListener('click', async () => {
        state.selectedPromoId = getPromoId(promo);
        await loadEntries();
      });

      row.append(left, viewBtn);
      promosBody.appendChild(row);
    });
  };

  const renderEntries = () => {
    entriesBody.innerHTML = '';
    entriesBody.appendChild(exportBtn);
    if (!state.entries.length) {
      const empty = document.createElement('div');
      empty.textContent = state.selectedPromoId ? 'No entries.' : 'Select a promo.';
      empty.style.color = '#64748b';
      entriesBody.appendChild(empty);
      return;
    }

    const table = document.createElement('table');
    table.style.width = '100%';
    table.style.borderCollapse = 'collapse';
    table.innerHTML = '<thead><tr><th>Email</th><th>Name</th><th>Visitor</th><th>Created</th></tr></thead>';
    const body = document.createElement('tbody');
    state.entries.forEach((entry) => {
      const tr = document.createElement('tr');
      tr.innerHTML = `<td>${entry.email || ''}</td><td>${entry.name || ''}</td><td>${entry.visitorId || ''}</td><td>${entry.createdAtUtc || ''}</td>`;
      Array.from(tr.children).forEach((td) => {
        td.style.borderTop = '1px solid #e2e8f0';
        td.style.padding = '6px';
      });
      body.appendChild(tr);
    });
    table.appendChild(body);
    entriesBody.appendChild(table);
  };

  const loadPromos = async () => {
    try {
      state.promos = await client.promos.list(state.siteId || undefined);
      renderPromos();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  const loadEntries = async () => {
    if (!state.selectedPromoId) return;
    try {
      state.entries = await client.promos.listEntries(state.selectedPromoId, 1, 100);
      renderEntries();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  siteSelect.addEventListener('change', async () => {
    state.siteId = siteSelect.value;
    await loadPromos();
  });

  createBody.addEventListener('submit', async (event) => {
    event.preventDefault();
    if (!siteSelect.value || !nameInput.value.trim()) {
      notifier.show({ message: 'Site and name are required.', variant: 'warning' });
      return;
    }
    try {
      await client.promos.create({ siteId: siteSelect.value, name: nameInput.value.trim(), description: descriptionInput.value.trim(), isActive: true });
      nameInput.value = '';
      descriptionInput.value = '';
      await loadPromos();
      notifier.show({ message: 'Promo created.', variant: 'success' });
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  });

  const init = async () => {
    try {
      state.sites = await client.sites.list();
      state.siteId = getSiteId(state.sites[0]);
      renderSites();
      await loadPromos();
      renderEntries();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  page.append(
    createCard({ title: 'Create promo', body: createBody }),
    createCard({ title: 'Promos', body: promosBody }),
    createCard({ title: 'Entries', body: entriesBody })
  );

  container.appendChild(page);
  init();
};
