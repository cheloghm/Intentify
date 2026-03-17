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
const getCampaignId = (campaign) => campaign?.id || campaign?.campaignId || '';

export const renderAdsView = (container, { apiClient, toast } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();
  const state = {
    sites: [],
    campaigns: [],
    siteId: '',
    selectedCampaignId: '',
    selectedCampaign: null,
    report: null,
    placements: [],
  };

  const page = document.createElement('div');
  page.style.display = 'flex';
  page.style.flexDirection = 'column';
  page.style.gap = '16px';
  page.style.width = '100%';
  page.style.maxWidth = '1000px';

  const header = document.createElement('h2');
  header.textContent = 'Ads';
  header.style.margin = '0';
  page.appendChild(header);

  const createForm = document.createElement('form');
  createForm.style.display = 'grid';
  createForm.style.gridTemplateColumns = '1fr 1fr auto';
  createForm.style.gap = '8px';

  const siteSelect = document.createElement('select');
  const createNameInput = document.createElement('input');
  const createObjectiveInput = document.createElement('input');
  const createActiveLabel = document.createElement('label');
  const createActiveCheckbox = document.createElement('input');
  createActiveCheckbox.type = 'checkbox';
  createActiveCheckbox.checked = false;
  createActiveLabel.style.display = 'flex';
  createActiveLabel.style.alignItems = 'center';
  createActiveLabel.style.gap = '6px';
  createActiveLabel.append(createActiveCheckbox, document.createTextNode('Is active'));

  [siteSelect, createNameInput, createObjectiveInput].forEach((el) => {
    el.style.padding = '8px 10px';
    el.style.border = '1px solid #cbd5e1';
    el.style.borderRadius = '6px';
  });

  createNameInput.placeholder = 'Campaign name';
  createObjectiveInput.placeholder = 'Objective (optional)';

  const createBtn = button('Create campaign', true);
  createBtn.type = 'submit';
  createForm.append(siteSelect, createNameInput, createObjectiveInput, createActiveLabel, createBtn);

  const campaignsBody = document.createElement('div');
  campaignsBody.style.display = 'flex';
  campaignsBody.style.flexDirection = 'column';
  campaignsBody.style.gap = '8px';

  const detailBody = document.createElement('div');
  detailBody.style.display = 'flex';
  detailBody.style.flexDirection = 'column';
  detailBody.style.gap = '8px';

  const placementsBody = document.createElement('div');
  placementsBody.style.display = 'flex';
  placementsBody.style.flexDirection = 'column';
  placementsBody.style.gap = '8px';

  const reportBody = document.createElement('div');
  reportBody.style.display = 'flex';
  reportBody.style.flexDirection = 'column';
  reportBody.style.gap = '8px';

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

  const loadCampaignReport = async () => {
    if (!state.selectedCampaignId) {
      state.report = null;
      renderReport();
      return;
    }

    try {
      state.report = await client.ads.getReport(state.selectedCampaignId);
      renderReport();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  const renderCampaigns = () => {
    campaignsBody.innerHTML = '';

    if (!state.siteId) {
      campaignsBody.textContent = 'Select a site to view campaigns.';
      campaignsBody.style.color = '#64748b';
      return;
    }

    if (!state.campaigns.length) {
      campaignsBody.textContent = 'No campaigns found.';
      campaignsBody.style.color = '#64748b';
      return;
    }

    state.campaigns.forEach((campaign) => {
      const row = document.createElement('div');
      row.style.display = 'flex';
      row.style.justifyContent = 'space-between';
      row.style.alignItems = 'center';
      row.style.padding = '8px';
      row.style.border = '1px solid #e2e8f0';
      row.style.borderRadius = '6px';

      const summary = document.createElement('div');
      summary.textContent = `${campaign.name || '(unnamed)'} · ${campaign.isActive ? 'Active' : 'Inactive'}`;

      const viewBtn = button('View');
      viewBtn.addEventListener('click', async () => {
        state.selectedCampaignId = getCampaignId(campaign);
        try {
          state.selectedCampaign = await client.ads.getCampaign(state.selectedCampaignId);
          state.placements = [...(state.selectedCampaign?.placements || [])];
          renderDetail();
          renderPlacements();
          await loadCampaignReport();
        } catch (error) {
          notifier.show({ message: mapApiError(error).message, variant: 'danger' });
        }
      });

      row.append(summary, viewBtn);
      campaignsBody.appendChild(row);
    });
  };

  const renderDetail = () => {
    detailBody.innerHTML = '';

    if (!state.selectedCampaign) {
      detailBody.textContent = 'Select a campaign.';
      detailBody.style.color = '#64748b';
      return;
    }

    const campaign = state.selectedCampaign;

    const form = document.createElement('form');
    form.style.display = 'grid';
    form.style.gridTemplateColumns = '1fr 1fr auto auto';
    form.style.gap = '8px';

    const nameInput = document.createElement('input');
    nameInput.value = campaign.name || '';
    const objectiveInput = document.createElement('input');
    objectiveInput.value = campaign.objective || '';
    const activeLabel = document.createElement('label');
    activeLabel.style.display = 'flex';
    activeLabel.style.alignItems = 'center';
    activeLabel.style.gap = '6px';
    const activeCheckbox = document.createElement('input');
    activeCheckbox.type = 'checkbox';
    activeCheckbox.checked = Boolean(campaign.isActive);
    activeLabel.append(activeCheckbox, document.createTextNode('Is active'));

    [nameInput, objectiveInput].forEach((el) => {
      el.style.padding = '8px 10px';
      el.style.border = '1px solid #cbd5e1';
      el.style.borderRadius = '6px';
    });

    nameInput.placeholder = 'Campaign name';
    objectiveInput.placeholder = 'Objective';

    const saveBtn = button('Save', true);
    saveBtn.type = 'submit';

    const toggleBtn = button(campaign.isActive ? 'Deactivate' : 'Activate');
    toggleBtn.addEventListener('click', async () => {
      try {
        if (campaign.isActive) {
          state.selectedCampaign = await client.ads.deactivateCampaign(state.selectedCampaignId);
        } else {
          state.selectedCampaign = await client.ads.activateCampaign(state.selectedCampaignId);
        }
        await loadCampaigns();
        renderDetail();
      } catch (error) {
        notifier.show({ message: mapApiError(error).message, variant: 'danger' });
      }
    });

    form.addEventListener('submit', async (event) => {
      event.preventDefault();
      if (!nameInput.value.trim()) {
        notifier.show({ message: 'Name is required.', variant: 'warning' });
        return;
      }

      try {
        state.selectedCampaign = await client.ads.updateCampaign(state.selectedCampaignId, {
          siteId: state.selectedCampaign.siteId,
          name: nameInput.value.trim(),
          objective: objectiveInput.value.trim() || null,
          isActive: activeCheckbox.checked,
          startsAtUtc: state.selectedCampaign.startsAtUtc || null,
          endsAtUtc: state.selectedCampaign.endsAtUtc || null,
          budget: state.selectedCampaign.budget ?? null,
        });
        await loadCampaigns();
        renderDetail();
      } catch (error) {
        notifier.show({ message: mapApiError(error).message, variant: 'danger' });
      }
    });

    form.append(nameInput, objectiveInput, activeLabel, saveBtn, toggleBtn);
    detailBody.appendChild(form);
  };

  const renderPlacements = () => {
    placementsBody.innerHTML = '';

    if (!state.selectedCampaign) {
      placementsBody.textContent = 'Select a campaign to edit placements.';
      placementsBody.style.color = '#64748b';
      return;
    }

    const rows = document.createElement('div');
    rows.style.display = 'flex';
    rows.style.flexDirection = 'column';
    rows.style.gap = '6px';

    if (!state.placements.length) {
      const empty = document.createElement('div');
      empty.textContent = 'No placements configured.';
      empty.style.color = '#64748b';
      rows.appendChild(empty);
    }

    state.placements.forEach((placement, index) => {
      const row = document.createElement('div');
      row.style.display = 'grid';
      row.style.gridTemplateColumns = '120px 1fr 1fr 110px auto';
      row.style.gap = '6px';

      const slotInput = document.createElement('input');
      slotInput.placeholder = 'slotKey';
      slotInput.value = placement.slotKey || '';
      slotInput.addEventListener('input', () => { placement.slotKey = slotInput.value; });

      const headlineInput = document.createElement('input');
      headlineInput.placeholder = 'headline';
      headlineInput.value = placement.headline || '';
      headlineInput.addEventListener('input', () => { placement.headline = headlineInput.value; });

      const urlInput = document.createElement('input');
      urlInput.placeholder = 'destinationUrl';
      urlInput.value = placement.destinationUrl || '';
      urlInput.addEventListener('input', () => { placement.destinationUrl = urlInput.value; });

      const activeLabel = document.createElement('label');
      activeLabel.style.display = 'flex';
      activeLabel.style.alignItems = 'center';
      activeLabel.style.gap = '4px';
      const activeInput = document.createElement('input');
      activeInput.type = 'checkbox';
      activeInput.checked = placement.isActive !== false;
      activeInput.addEventListener('change', () => { placement.isActive = activeInput.checked; });
      activeLabel.append(activeInput, document.createTextNode('Active'));

      const removeBtn = button('Remove');
      removeBtn.addEventListener('click', () => {
        state.placements.splice(index, 1);
        renderPlacements();
      });

      [slotInput, headlineInput, urlInput].forEach((el) => {
        el.style.padding = '8px 10px';
        el.style.border = '1px solid #cbd5e1';
        el.style.borderRadius = '6px';
      });

      row.append(slotInput, headlineInput, urlInput, activeLabel, removeBtn);
      rows.appendChild(row);
    });

    const actions = document.createElement('div');
    actions.style.display = 'flex';
    actions.style.gap = '8px';

    const addBtn = button('Add placement');
    addBtn.addEventListener('click', () => {
      state.placements.push({ slotKey: '', headline: '', destinationUrl: '', order: state.placements.length + 1, isActive: true });
      renderPlacements();
    });

    const saveBtn = button('Save placements', true);
    saveBtn.addEventListener('click', async () => {
      try {
        const payload = {
          placements: state.placements.map((placement, index) => ({
            slotKey: placement.slotKey || '',
            pathPattern: placement.pathPattern || null,
            device: placement.device || 'all',
            headline: placement.headline || '',
            body: placement.body || null,
            imageUrl: placement.imageUrl || null,
            destinationUrl: placement.destinationUrl || '',
            ctaText: placement.ctaText || null,
            order: placement.order || index + 1,
            isActive: placement.isActive !== false,
          })),
        };

        state.selectedCampaign = await client.ads.upsertPlacements(state.selectedCampaignId, payload);
        state.placements = [...(state.selectedCampaign?.placements || [])];
        await loadCampaigns();
        await loadCampaignReport();
        renderPlacements();
      } catch (error) {
        notifier.show({ message: mapApiError(error).message, variant: 'danger' });
      }
    });

    actions.append(addBtn, saveBtn);
    placementsBody.append(rows, actions);
  };

  const renderReport = () => {
    reportBody.innerHTML = '';

    if (!state.selectedCampaignId) {
      reportBody.textContent = 'Select a campaign to view report.';
      reportBody.style.color = '#64748b';
      return;
    }

    if (!state.report) {
      reportBody.textContent = 'No report data.';
      reportBody.style.color = '#64748b';
      return;
    }

    const totals = state.report.totals || {};
    const totalsBlock = document.createElement('div');
    totalsBlock.style.display = 'grid';
    totalsBlock.style.gridTemplateColumns = 'repeat(3, minmax(120px, auto))';
    totalsBlock.style.gap = '10px';
    totalsBlock.innerHTML = `
      <div><strong>Impressions:</strong> ${totals.impressions || 0}</div>
      <div><strong>Clicks:</strong> ${totals.clicks || 0}</div>
      <div><strong>CTR:</strong> ${totals.ctr || 0}</div>
    `;

    const table = document.createElement('table');
    table.style.width = '100%';
    table.style.borderCollapse = 'collapse';

    const thead = document.createElement('thead');
    const headRow = document.createElement('tr');
    ['Placement', 'Impressions', 'Clicks', 'CTR'].forEach((label) => {
      const th = document.createElement('th');
      th.textContent = label;
      th.style.textAlign = 'left';
      th.style.padding = '6px 8px';
      th.style.borderBottom = '1px solid #e2e8f0';
      headRow.appendChild(th);
    });
    thead.appendChild(headRow);

    const tbody = document.createElement('tbody');
    const rows = state.report.byPlacement || [];

    if (!rows.length) {
      const tr = document.createElement('tr');
      const td = document.createElement('td');
      td.colSpan = 4;
      td.style.padding = '8px';
      td.style.color = '#64748b';
      td.textContent = 'No placement metrics.';
      tr.appendChild(td);
      tbody.appendChild(tr);
    } else {
      rows.forEach((row) => {
        const tr = document.createElement('tr');
        [row.slotKey || row.placementId || '—', row.impressions || 0, row.clicks || 0, row.ctr || 0].forEach((value) => {
          const td = document.createElement('td');
          td.style.padding = '6px 8px';
          td.style.borderBottom = '1px solid #f1f5f9';
          td.textContent = String(value);
          tr.appendChild(td);
        });
        tbody.appendChild(tr);
      });
    }

    table.append(thead, tbody);
    reportBody.append(totalsBlock, table);
  };

  const loadCampaigns = async () => {
    if (!state.siteId) {
      state.campaigns = [];
      renderCampaigns();
      return;
    }

    try {
      state.campaigns = await client.ads.listCampaigns(state.siteId);
      renderCampaigns();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  siteSelect.addEventListener('change', async () => {
    state.siteId = siteSelect.value;
    state.selectedCampaignId = '';
    state.selectedCampaign = null;
    state.placements = [];
    state.report = null;
    renderDetail();
    renderPlacements();
    renderReport();
    await loadCampaigns();
  });

  createForm.addEventListener('submit', async (event) => {
    event.preventDefault();

    if (!state.siteId) {
      notifier.show({ message: 'Select a site first.', variant: 'warning' });
      return;
    }

    if (!createNameInput.value.trim()) {
      notifier.show({ message: 'Name is required.', variant: 'warning' });
      return;
    }

    try {
      await client.ads.createCampaign({
        siteId: state.siteId,
        name: createNameInput.value.trim(),
        objective: createObjectiveInput.value.trim() || null,
        isActive: createActiveCheckbox.checked,
        placements: [],
      });

      createNameInput.value = '';
      createObjectiveInput.value = '';
      createActiveCheckbox.checked = false;
      await loadCampaigns();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  });

  const init = async () => {
    try {
      state.sites = await client.sites.list();
      renderSites();
      renderCampaigns();
      renderDetail();
      renderPlacements();
      renderReport();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  page.append(
    createCard({ title: 'Create campaign', body: createForm }),
    createCard({ title: 'Campaigns', body: campaignsBody }),
    createCard({ title: 'Campaign detail', body: detailBody }),
    createCard({ title: 'Placements', body: placementsBody }),
    createCard({ title: 'Report', body: reportBody }),
  );

  container.appendChild(page);
  init();
};
