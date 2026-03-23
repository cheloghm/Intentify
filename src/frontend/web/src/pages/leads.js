import { createCard, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const button = (label) => {
  const b = document.createElement('button');
  b.type = 'button';
  b.textContent = label;
  b.style.padding = '8px 12px';
  b.style.border = '1px solid #cbd5e1';
  b.style.borderRadius = '6px';
  b.style.background = '#fff';
  b.style.cursor = 'pointer';
  return b;
};

const getSiteId = (site) => site?.siteId || site?.id || '';

export const renderLeadsView = (container, { apiClient, toast } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();
  const state = { sites: [], siteId: '', leads: [], selected: null };

  const page = document.createElement('div');
  page.style.display = 'flex';
  page.style.flexDirection = 'column';
  page.style.gap = '16px';
  page.style.width = '100%';
  page.style.maxWidth = '1000px';

  const filters = document.createElement('div');
  filters.style.display = 'flex';
  filters.style.gap = '8px';
  const siteSelect = document.createElement('select');
  siteSelect.style.padding = '8px 10px';
  siteSelect.style.border = '1px solid #cbd5e1';
  siteSelect.style.borderRadius = '6px';
  filters.appendChild(siteSelect);

  const listBody = document.createElement('div');
  listBody.style.display = 'flex';
  listBody.style.flexDirection = 'column';
  listBody.style.gap = '8px';

  const detailBody = document.createElement('div');
  detailBody.style.color = '#334155';

  const renderSites = () => {
    siteSelect.innerHTML = '';
    const empty = document.createElement('option');
    empty.value = '';
    empty.textContent = 'All sites';
    siteSelect.appendChild(empty);
    state.sites.forEach((site) => {
      const option = document.createElement('option');
      option.value = getSiteId(site);
      option.textContent = site.domain || option.value;
      siteSelect.appendChild(option);
    });
    siteSelect.value = state.siteId;
  };

  const renderDetail = () => {
    detailBody.innerHTML = '';
    if (!state.selected) {
      detailBody.textContent = 'Select a lead.';
      return;
    }

    const lead = state.selected;
    const wrapper = document.createElement('div');
    wrapper.style.display = 'grid';
    wrapper.style.gap = '6px';
    wrapper.innerHTML = `
      <div><strong>Email:</strong> ${lead.primaryEmail || ''}</div>
      <div><strong>Name:</strong> ${lead.displayName || ''}</div>
      <div><strong>Phone:</strong> ${lead.phone || ''}</div>
      <div><strong>Preferred contact method:</strong> ${lead.preferredContactMethod || ''}</div>
      <div><strong>FirstPartyId:</strong> ${lead.firstPartyId || ''}</div>
      <div><strong>Linked Visitor:</strong> ${lead.linkedVisitorId || ''}</div>
    `;

    if (lead.linkedVisitorId) {
      const link = document.createElement('a');
      link.href = `#/visitors/${lead.linkedVisitorId}?siteId=${lead.siteId}`;
      link.textContent = 'Open visitor profile';
      wrapper.appendChild(link);
    }

    detailBody.appendChild(wrapper);
  };

  const loadLeadDetail = async (leadId) => {
    try {
      state.selected = await client.leads.get(leadId);
      renderDetail();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  const renderList = () => {
    listBody.innerHTML = '';
    if (!state.leads.length) {
      listBody.textContent = 'No leads found.';
      return;
    }

    state.leads.forEach((lead) => {
      const row = document.createElement('div');
      row.style.display = 'flex';
      row.style.justifyContent = 'space-between';
      row.style.alignItems = 'center';
      row.style.padding = '8px';
      row.style.border = '1px solid #e2e8f0';
      row.style.borderRadius = '6px';

      const summary = document.createElement('div');
      summary.textContent = `${lead.primaryEmail || '(no email)'} · ${lead.displayName || '(no name)'}`;
      const view = button('View');
      view.addEventListener('click', () => loadLeadDetail(lead.id));
      row.append(summary, view);
      listBody.appendChild(row);
    });
  };

  const loadLeads = async () => {
    try {
      state.leads = await client.leads.list(state.siteId || undefined, 1, 100);
      renderList();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  siteSelect.addEventListener('change', async () => {
    state.siteId = siteSelect.value;
    await loadLeads();
  });

  const init = async () => {
    try {
      state.sites = await client.sites.list();
      renderSites();
      await loadLeads();
      renderDetail();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  page.append(
    createCard({ title: 'Leads filters', body: filters }),
    createCard({ title: 'Leads', body: listBody }),
    createCard({ title: 'Lead detail', body: detailBody })
  );

  container.appendChild(page);
  init();
};
