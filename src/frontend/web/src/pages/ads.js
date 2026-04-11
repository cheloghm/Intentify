/**
 * ads.js — Intentify Ads
 * Full campaign CRUD, placements management, and performance reports.
 */

import { createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const el = (tag, attrs = {}, ...kids) => {
  const e = document.createElement(tag);
  Object.entries(attrs).forEach(([k, v]) => {
    if (k === 'class')          e.className = v;
    else if (k === 'style')     typeof v === 'string' ? (e.style.cssText = v) : Object.assign(e.style, v);
    else if (k.startsWith('@')) e.addEventListener(k.slice(1), v);
    else                        e.setAttribute(k, v);
  });
  kids.flat(Infinity).forEach(c => c != null && e.append(typeof c === 'string' ? document.createTextNode(c) : c));
  return e;
};

const injectStyles = () => {
  if (document.getElementById('_ads_css')) return;
  const s = document.createElement('style');
  s.id = '_ads_css';
  s.textContent = `
@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&family=JetBrains+Mono:wght@400;500;700&display=swap');
.ad2-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;gap:20px;width:100%}
.ad2-hero{background:linear-gradient(135deg,#0f172a 0%,#1e293b 100%);border-radius:16px;padding:28px 32px;color:#fff}
.ad2-hero-title{font-size:22px;font-weight:800;letter-spacing:-.02em;margin-bottom:4px}
.ad2-hero-sub{font-size:13px;color:#94a3b8;margin-bottom:20px}
.ad2-stats{display:flex;gap:24px;flex-wrap:wrap}
.ad2-stat{display:flex;flex-direction:column;gap:2px}
.ad2-stat-val{font-family:'JetBrains Mono',monospace;font-size:22px;font-weight:700;color:#fff;line-height:1}
.ad2-stat-lbl{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.07em;color:#64748b}
.ad2-layout{display:grid;grid-template-columns:300px 1fr;gap:16px;align-items:start}
@media(max-width:800px){.ad2-layout{grid-template-columns:1fr}}
.ad2-panel{background:#fff;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden}
.ad2-panel-hd{display:flex;align-items:center;justify-content:space-between;padding:14px 18px;border-bottom:1px solid #f1f5f9}
.ad2-panel-title{font-size:13px;font-weight:700;color:#0f172a}
.ad2-panel-body{padding:16px 18px;display:flex;flex-direction:column;gap:14px}
.ad2-btn{display:inline-flex;align-items:center;gap:6px;padding:7px 14px;border-radius:8px;border:none;font-size:12.5px;font-weight:600;cursor:pointer;font-family:inherit;transition:all .14s}
.ad2-btn-primary{background:#6366f1;color:#fff}.ad2-btn-primary:hover:not(:disabled){background:#4f46e5}
.ad2-btn-primary:disabled{opacity:.5;cursor:not-allowed}
.ad2-btn-outline{background:#fff;color:#475569;border:1.5px solid #e2e8f0}.ad2-btn-outline:hover{background:#f8fafc}
.ad2-btn-success{background:#10b981;color:#fff}.ad2-btn-success:hover{background:#059669}
.ad2-btn-danger{background:#fff;color:#ef4444;border:1.5px solid #fecaca}.ad2-btn-danger:hover{background:#fef2f2}
.ad2-btn-sm{padding:4px 10px;font-size:11.5px}
.ad2-campaign-card{padding:12px 14px;border-bottom:1px solid #f8fafc;cursor:pointer;transition:background .12s}
.ad2-campaign-card:last-child{border-bottom:none}
.ad2-campaign-card:hover{background:#fafbff}
.ad2-campaign-card.active{background:#eef2ff;border-left:3px solid #6366f1}
.ad2-campaign-name{font-size:13px;font-weight:600;color:#0f172a;margin-bottom:2px}
.ad2-campaign-meta{font-size:11px;color:#94a3b8;display:flex;gap:8px;flex-wrap:wrap;align-items:center}
.ad2-pill{display:inline-flex;align-items:center;padding:2px 7px;border-radius:999px;font-size:10px;font-weight:700}
.ad2-pill-active{background:#dcfce7;color:#15803d}
.ad2-pill-inactive{background:#f1f5f9;color:#64748b}
.ad2-pill-draft{background:#fef9c3;color:#a16207}
.ad2-field{display:flex;flex-direction:column;gap:4px}
.ad2-field label{font-size:11px;font-weight:600;color:#64748b;text-transform:uppercase;letter-spacing:.04em}
.ad2-input{padding:8px 12px;border:1.5px solid #e2e8f0;border-radius:8px;font-size:13px;font-family:inherit;outline:none;transition:border-color .14s;width:100%;box-sizing:border-box}
.ad2-input:focus{border-color:#6366f1}
.ad2-select{padding:8px 12px;border:1.5px solid #e2e8f0;border-radius:8px;font-size:13px;font-family:inherit;outline:none;background:#fff;cursor:pointer;width:100%;box-sizing:border-box}
.ad2-select:focus{border-color:#6366f1}
.ad2-grid-2{display:grid;grid-template-columns:1fr 1fr;gap:10px}
@media(max-width:500px){.ad2-grid-2{grid-template-columns:1fr}}
.ad2-section-label{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.07em;color:#94a3b8;margin-bottom:8px}
.ad2-placement-card{border:1.5px solid #e2e8f0;border-radius:10px;padding:12px 14px;display:flex;flex-direction:column;gap:8px;background:#fafbff}
.ad2-placement-hd{display:flex;align-items:center;justify-content:space-between}
.ad2-placement-title{font-size:12px;font-weight:700;color:#0f172a}
.ad2-report-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:10px}
.ad2-report-card{background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:12px 14px;text-align:center}
.ad2-report-val{font-family:'JetBrains Mono',monospace;font-size:22px;font-weight:700;color:#0f172a;line-height:1}
.ad2-report-lbl{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.07em;color:#94a3b8;margin-top:3px}
.ad2-empty{padding:40px 24px;text-align:center;color:#94a3b8}
.ad2-empty-icon{font-size:28px;margin-bottom:8px}
.ad2-empty-title{font-size:13px;font-weight:600;color:#475569;margin-bottom:4px}
.ad2-empty-desc{font-size:12px;color:#94a3b8;line-height:1.6;max-width:260px;margin:0 auto}
.ad2-tab-bar{display:flex;gap:2px;background:#f1f5f9;border-radius:8px;padding:3px;margin-bottom:16px;width:fit-content}
.ad2-tab{padding:6px 14px;border-radius:6px;border:none;font-size:12px;font-weight:600;color:#64748b;background:transparent;cursor:pointer;font-family:inherit;transition:all .14s}
.ad2-tab.active{background:#fff;color:#0f172a;box-shadow:0 1px 3px rgba(0,0,0,.08)}
.ad2-sites-row{display:flex;align-items:center;gap:8px;margin-bottom:4px}
.ad2-hint{font-size:11px;color:#94a3b8;line-height:1.5}
  `;
  document.head.appendChild(s);
};

const fmtDate = d => d ? new Date(d).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' }) : '—';

const mkField = (labelText, inputEl) => {
  const f = el('div', { class: 'ad2-field' });
  f.appendChild(el('label', {}, labelText));
  f.appendChild(inputEl);
  return f;
};

export const renderAdsView = (container, { apiClient, toast } = {}) => {
  injectStyles();
  const client   = apiClient || createApiClient();
  const notifier = toast     || createToastManager();

  const state = {
    sites: [], siteId: '', campaigns: [],
    selectedCampaign: null, selectedTab: 'overview',
    placements: [], report: null, showCreateForm: false,
  };

  const root = el('div', { class: 'ad2-root' });
  container.appendChild(root);

  // ── Hero ──────────────────────────────────────────────────────────────────
  const hero = el('div', { class: 'ad2-hero' });
  hero.appendChild(el('div', { class: 'ad2-hero-title' }, '📢 Ads & Campaigns'));
  hero.appendChild(el('div', { class: 'ad2-hero-sub' }, 'Create and manage advertising campaigns. Track impressions, clicks, and conversions across your sites.'));
  const statsRow = el('div', { class: 'ad2-stats' });
  const mkStat = lbl => {
    const w = el('div', { class: 'ad2-stat' });
    const v = el('div', { class: 'ad2-stat-val' }, '—');
    w.append(v, el('div', { class: 'ad2-stat-lbl' }, lbl));
    statsRow.appendChild(w);
    return v;
  };
  const hTotal       = mkStat('Total Campaigns');
  const hActive      = mkStat('Active');
  const hImpressions = mkStat('Total Impressions');
  hero.appendChild(statsRow);
  root.appendChild(hero);

  // ── Site selector ─────────────────────────────────────────────────────────
  const sitesRow   = el('div', { class: 'ad2-sites-row' });
  const siteSelect = el('select', { class: 'ad2-select', style: 'max-width:280px' },
    el('option', { value: '' }, 'Loading sites…')
  );
  sitesRow.appendChild(siteSelect);
  root.appendChild(sitesRow);

  // ── Two-column layout ─────────────────────────────────────────────────────
  const layout  = el('div', { class: 'ad2-layout' });
  const leftCol  = el('div');
  const rightCol = el('div');
  layout.append(leftCol, rightCol);
  root.appendChild(layout);

  // ── Campaign list panel ───────────────────────────────────────────────────
  const listPanel = el('div', { class: 'ad2-panel' });
  const listHd    = el('div', { class: 'ad2-panel-hd' });
  listHd.appendChild(el('div', { class: 'ad2-panel-title' }, 'Campaigns'));
  const newBtn = el('button', { class: 'ad2-btn ad2-btn-primary ad2-btn-sm' }, '＋ New');
  newBtn.addEventListener('click', () => {
    state.showCreateForm = true;
    state.selectedCampaign = null;
    renderCampaignList();
    renderRight();
  });
  listHd.appendChild(newBtn);
  listPanel.appendChild(listHd);
  const listBody = el('div', { style: 'min-height:120px' });
  listPanel.appendChild(listBody);
  leftCol.appendChild(listPanel);

  // ── Right panel dispatcher ────────────────────────────────────────────────
  const renderRight = () => {
    rightCol.replaceChildren();
    if (state.showCreateForm || !state.selectedCampaign) {
      renderCreateForm();
    } else {
      renderDetailPanel();
    }
  };

  // ── Create campaign form ──────────────────────────────────────────────────
  const renderCreateForm = () => {
    const panel = el('div', { class: 'ad2-panel' });
    panel.appendChild(el('div', { class: 'ad2-panel-hd' },
      el('div', { class: 'ad2-panel-title' }, '＋ New Campaign')
    ));
    const body = el('div', { class: 'ad2-panel-body' });

    const nameInput   = el('input',  { class: 'ad2-input', placeholder: 'e.g. Summer 2026' });
    const objSelect   = el('select', { class: 'ad2-select' },
      el('option', { value: '' }, '— Select objective —'),
      el('option', { value: 'Brand Awareness' }, 'Brand Awareness'),
      el('option', { value: 'Lead Generation' }, 'Lead Generation'),
      el('option', { value: 'Traffic' }, 'Traffic'),
      el('option', { value: 'Retargeting' }, 'Retargeting'),
    );
    const budgetInput = el('input', { class: 'ad2-input', type: 'number', placeholder: '500', min: '0', step: '0.01' });

    const dateGrid   = el('div', { class: 'ad2-grid-2' });
    const startInput = el('input', { class: 'ad2-input', type: 'date' });
    const endInput   = el('input', { class: 'ad2-input', type: 'date' });
    dateGrid.append(mkField('Start Date', startInput), mkField('End Date', endInput));

    const actRow   = el('div', { style: 'display:flex;gap:8px;flex-wrap:wrap' });
    const createBtn = el('button', { class: 'ad2-btn ad2-btn-primary' }, '📢 Create Campaign');
    const cancelBtn = el('button', { class: 'ad2-btn ad2-btn-outline' }, 'Cancel');

    cancelBtn.addEventListener('click', () => {
      state.showCreateForm = false;
      renderRight();
    });

    createBtn.addEventListener('click', async () => {
      if (!state.siteId || !nameInput.value.trim()) {
        notifier.show({ message: 'Site and campaign name are required.', variant: 'warning' });
        return;
      }
      createBtn.disabled = true; createBtn.textContent = '⏳ Creating…';
      try {
        const created = await client.ads.createCampaign({
          siteId:      state.siteId,
          name:        nameInput.value.trim(),
          objective:   objSelect.value || null,
          budget:      budgetInput.value ? parseFloat(budgetInput.value) : null,
          startsAtUtc: startInput.value ? new Date(startInput.value).toISOString() : null,
          endsAtUtc:   endInput.value   ? new Date(endInput.value).toISOString()   : null,
          isActive:    false,
          placements:  [],
        });
        notifier.show({ message: 'Campaign created.', variant: 'success' });
        state.showCreateForm = false;
        await loadCampaigns();
        const newCampaign = state.campaigns.find(c => (c.id || c.campaignId) === (created?.id || created?.campaignId))
          || state.campaigns[0];
        if (newCampaign) await loadCampaignDetail(newCampaign);
        else renderRight();
      } catch (err) {
        notifier.show({ message: mapApiError(err).message, variant: 'danger' });
        createBtn.disabled = false; createBtn.textContent = '📢 Create Campaign';
      }
    });

    actRow.append(createBtn, cancelBtn);
    body.append(mkField('Campaign Name *', nameInput), mkField('Objective', objSelect), mkField('Budget (£)', budgetInput), dateGrid, actRow);
    panel.appendChild(body);
    rightCol.appendChild(panel);
  };

  // ── Detail panel (tabs) ───────────────────────────────────────────────────
  const renderDetailPanel = () => {
    const c = state.selectedCampaign;
    if (!c) return;

    const panel = el('div', { class: 'ad2-panel' });
    panel.appendChild(el('div', { class: 'ad2-panel-hd' },
      el('div', { class: 'ad2-panel-title' }, c.name || 'Campaign')
    ));
    const body = el('div', { class: 'ad2-panel-body' });

    // Tab bar
    const tabBar   = el('div', { class: 'ad2-tab-bar' });
    const tabDefs  = [['overview', 'Overview'], ['placements', 'Placements'], ['report', 'Report']];
    const tabBtns  = {};
    const tabPanes = {};

    tabDefs.forEach(([key, label]) => {
      const btn = el('button', { class: `ad2-tab${state.selectedTab === key ? ' active' : ''}` }, label);
      btn.addEventListener('click', () => {
        state.selectedTab = key;
        tabDefs.forEach(([k]) => {
          tabBtns[k].classList.toggle('active', k === key);
          tabPanes[k].style.display = k === key ? '' : 'none';
        });
      });
      tabBtns[key] = btn;
      tabBar.appendChild(btn);
    });
    body.appendChild(tabBar);

    // Build panes
    const overviewPane    = el('div', { style: state.selectedTab === 'overview'    ? '' : 'display:none' });
    const placementsPane  = el('div', { style: state.selectedTab === 'placements'  ? '' : 'display:none' });
    const reportPane      = el('div', { style: state.selectedTab === 'report'      ? '' : 'display:none' });
    tabPanes['overview']   = overviewPane;
    tabPanes['placements'] = placementsPane;
    tabPanes['report']     = reportPane;

    renderOverviewPane(overviewPane, c);
    renderPlacementsPane(placementsPane);
    renderReportPane(reportPane);

    body.append(overviewPane, placementsPane, reportPane);
    panel.appendChild(body);
    rightCol.appendChild(panel);
  };

  // ── Overview pane ─────────────────────────────────────────────────────────
  const renderOverviewPane = (pane, c) => {
    const nameInput   = el('input', { class: 'ad2-input', value: c.name || '' });
    const objSelect   = el('select', { class: 'ad2-select' },
      el('option', { value: '' }, '— Select objective —'),
      el('option', { value: 'Brand Awareness' }, 'Brand Awareness'),
      el('option', { value: 'Lead Generation' }, 'Lead Generation'),
      el('option', { value: 'Traffic' }, 'Traffic'),
      el('option', { value: 'Retargeting' }, 'Retargeting'),
    );
    objSelect.value = c.objective || '';

    const budgetInput = el('input', { class: 'ad2-input', type: 'number', value: c.budget != null ? String(c.budget) : '', min: '0', step: '0.01' });

    const dateGrid   = el('div', { class: 'ad2-grid-2' });
    const startInput = el('input', { class: 'ad2-input', type: 'date', value: c.startsAtUtc ? c.startsAtUtc.slice(0, 10) : '' });
    const endInput   = el('input', { class: 'ad2-input', type: 'date', value: c.endsAtUtc   ? c.endsAtUtc.slice(0, 10)   : '' });
    dateGrid.append(mkField('Start Date', startInput), mkField('End Date', endInput));

    // Status toggle
    const statusRow = el('div', { style: 'display:flex;align-items:center;gap:10px' });
    statusRow.appendChild(el('span', { style: 'font-size:12px;color:#64748b;font-weight:600' }, 'Status:'));
    const toggleBtn = el('button', {
      class: `ad2-btn ad2-btn-sm ${c.isActive ? 'ad2-btn-success' : 'ad2-btn-outline'}`,
    }, c.isActive ? '● Active' : '○ Inactive');
    toggleBtn.addEventListener('click', async () => {
      toggleBtn.disabled = true;
      try {
        const updated = c.isActive
          ? await client.ads.deactivateCampaign(c.id || c.campaignId)
          : await client.ads.activateCampaign(c.id || c.campaignId);
        state.selectedCampaign = updated;
        notifier.show({ message: `Campaign ${updated.isActive ? 'activated' : 'deactivated'}.`, variant: 'success' });
        await loadCampaigns();
        renderRight();
      } catch (err) {
        notifier.show({ message: mapApiError(err).message, variant: 'danger' });
        toggleBtn.disabled = false;
      }
    });
    statusRow.appendChild(toggleBtn);

    const actRow  = el('div', { style: 'display:flex;gap:8px;flex-wrap:wrap;margin-top:4px' });
    const saveBtn = el('button', { class: 'ad2-btn ad2-btn-primary ad2-btn-sm' }, '💾 Save Changes');
    saveBtn.addEventListener('click', async () => {
      saveBtn.disabled = true; saveBtn.textContent = '⏳…';
      try {
        state.selectedCampaign = await client.ads.updateCampaign(c.id || c.campaignId, {
          siteId:      c.siteId,
          name:        nameInput.value.trim(),
          objective:   objSelect.value || null,
          budget:      budgetInput.value ? parseFloat(budgetInput.value) : null,
          startsAtUtc: startInput.value ? new Date(startInput.value).toISOString() : null,
          endsAtUtc:   endInput.value   ? new Date(endInput.value).toISOString()   : null,
          isActive:    c.isActive,
        });
        notifier.show({ message: 'Campaign saved.', variant: 'success' });
        await loadCampaigns();
        renderRight();
      } catch (err) {
        notifier.show({ message: mapApiError(err).message, variant: 'danger' });
        saveBtn.disabled = false; saveBtn.textContent = '💾 Save Changes';
      }
    });
    actRow.appendChild(saveBtn);

    pane.append(
      mkField('Campaign Name', nameInput),
      mkField('Objective', objSelect),
      mkField('Budget (£)', budgetInput),
      dateGrid,
      statusRow,
      actRow,
    );
  };

  // ── Placements pane ───────────────────────────────────────────────────────
  const renderPlacementsPane = (pane) => {
    const listWrap = el('div', { style: 'display:flex;flex-direction:column;gap:10px' });

    const refreshList = () => {
      listWrap.replaceChildren();
      if (!state.placements.length) {
        listWrap.appendChild(el('div', { class: 'ad2-empty' },
          el('div', { class: 'ad2-empty-icon' }, '📍'),
          el('div', { class: 'ad2-empty-title' }, 'No placements yet'),
          el('div', { class: 'ad2-empty-desc' }, 'Placements define where your ads appear on your website.'),
        ));
        return;
      }
      state.placements.forEach((p, i) => {
        const card = el('div', { class: 'ad2-placement-card' });
        const hd   = el('div', { class: 'ad2-placement-hd' });
        hd.appendChild(el('span', { class: 'ad2-placement-title' }, p.slotKey || `Placement ${i + 1}`));

        const rmBtn = el('button', { class: 'ad2-btn ad2-btn-danger ad2-btn-sm' }, '✕ Remove');
        rmBtn.addEventListener('click', async () => {
          rmBtn.disabled = true;
          const updated = state.placements.filter((_, idx) => idx !== i);
          try {
            state.selectedCampaign = await client.ads.upsertPlacements(
              state.selectedCampaign.id || state.selectedCampaign.campaignId,
              { placements: updated.map((pl, idx) => toPlacementPayload(pl, idx)) }
            );
            state.placements = [...(state.selectedCampaign?.placements || [])];
            notifier.show({ message: 'Placement removed.', variant: 'success' });
            refreshList();
          } catch (err) {
            notifier.show({ message: mapApiError(err).message, variant: 'danger' });
            rmBtn.disabled = false;
          }
        });
        hd.appendChild(rmBtn);
        card.appendChild(hd);

        const meta = el('div', { class: 'ad2-campaign-meta' });
        if (p.pathPattern) meta.appendChild(el('span', {}, `Path: ${p.pathPattern}`));
        meta.appendChild(el('span', {}, `Device: ${p.device || 'all'}`));
        meta.appendChild(el('span', { class: `ad2-pill ${p.isActive !== false ? 'ad2-pill-active' : 'ad2-pill-inactive'}` },
          p.isActive !== false ? 'Active' : 'Inactive'));
        card.appendChild(meta);
        if (p.headline)    card.appendChild(el('div', { style: 'font-size:12px;color:#334155;font-weight:600' }, p.headline));
        if (p.ctaText)     card.appendChild(el('div', { style: 'font-size:11px;color:#6366f1' }, `CTA: ${p.ctaText}`));
        if (p.destinationUrl) card.appendChild(el('div', { style: 'font-size:11px;color:#94a3b8;overflow:hidden;text-overflow:ellipsis;white-space:nowrap' }, `→ ${p.destinationUrl}`));
        listWrap.appendChild(card);
      });
    };

    refreshList();
    pane.appendChild(listWrap);

    // Add placement form (hidden by default)
    const formWrap = el('div', { style: 'display:none;margin-top:12px' });
    const addBtn   = el('button', { class: 'ad2-btn ad2-btn-outline ad2-btn-sm', style: 'margin-top:10px' }, '＋ Add Placement');
    addBtn.addEventListener('click', () => { addBtn.style.display = 'none'; formWrap.style.display = ''; });

    const formPanel = el('div', { class: 'ad2-panel' });
    formPanel.appendChild(el('div', { class: 'ad2-panel-hd' }, el('div', { class: 'ad2-panel-title' }, 'New Placement')));
    const formBody = el('div', { class: 'ad2-panel-body' });

    const slotInput     = el('input',    { class: 'ad2-input', placeholder: 'e.g. header-banner' });
    const pathInput     = el('input',    { class: 'ad2-input', placeholder: 'e.g. /pricing or *' });
    const deviceSel     = el('select',   { class: 'ad2-select' },
      el('option', { value: 'all' },     'All Devices'),
      el('option', { value: 'desktop' }, 'Desktop'),
      el('option', { value: 'mobile' },  'Mobile'),
    );
    const headlineInput = el('input',    { class: 'ad2-input', placeholder: 'Ad headline' });
    const bodyTA        = el('textarea', { class: 'ad2-input', placeholder: 'Ad body text (optional)', rows: '2', style: 'resize:vertical' });
    const destInput     = el('input',    { class: 'ad2-input', placeholder: 'https://example.com/landing' });
    const ctaInput      = el('input',    { class: 'ad2-input', placeholder: 'e.g. Learn more' });

    const pActRow    = el('div', { style: 'display:flex;gap:8px;flex-wrap:wrap' });
    const savePlBtn  = el('button', { class: 'ad2-btn ad2-btn-primary ad2-btn-sm' }, '💾 Save Placement');
    const cancelPlBtn = el('button', { class: 'ad2-btn ad2-btn-outline ad2-btn-sm' }, 'Cancel');

    cancelPlBtn.addEventListener('click', () => { formWrap.style.display = 'none'; addBtn.style.display = ''; });

    savePlBtn.addEventListener('click', async () => {
      if (!slotInput.value.trim() || !headlineInput.value.trim() || !destInput.value.trim()) {
        notifier.show({ message: 'Slot key, headline, and destination URL are required.', variant: 'warning' });
        return;
      }
      savePlBtn.disabled = true; savePlBtn.textContent = '⏳…';
      const newPl = {
        slotKey: slotInput.value.trim(), pathPattern: pathInput.value.trim() || null,
        device: deviceSel.value, headline: headlineInput.value.trim(),
        body: bodyTA.value.trim() || null, imageUrl: null,
        destinationUrl: destInput.value.trim(), ctaText: ctaInput.value.trim() || null,
        order: state.placements.length + 1, isActive: true,
      };
      const allPlacements = [...state.placements, newPl];
      try {
        state.selectedCampaign = await client.ads.upsertPlacements(
          state.selectedCampaign.id || state.selectedCampaign.campaignId,
          { placements: allPlacements.map((pl, idx) => toPlacementPayload(pl, idx)) }
        );
        state.placements = [...(state.selectedCampaign?.placements || [])];
        notifier.show({ message: 'Placement saved.', variant: 'success' });
        slotInput.value = ''; pathInput.value = ''; headlineInput.value = '';
        bodyTA.value = ''; destInput.value = ''; ctaInput.value = '';
        deviceSel.value = 'all';
        formWrap.style.display = 'none';
        addBtn.style.display = '';
        refreshList();
      } catch (err) {
        notifier.show({ message: mapApiError(err).message, variant: 'danger' });
      } finally {
        savePlBtn.disabled = false; savePlBtn.textContent = '💾 Save Placement';
      }
    });

    pActRow.append(savePlBtn, cancelPlBtn);
    formBody.append(
      mkField('Slot Key *', slotInput),
      mkField('Path Pattern', pathInput),
      mkField('Device', deviceSel),
      mkField('Headline *', headlineInput),
      mkField('Body', bodyTA),
      mkField('Destination URL *', destInput),
      mkField('CTA Text', ctaInput),
      pActRow,
    );
    formPanel.appendChild(formBody);
    formWrap.appendChild(formPanel);
    pane.append(addBtn, formWrap);
  };

  // ── Report pane ───────────────────────────────────────────────────────────
  const renderReportPane = (pane) => {
    const now   = new Date();
    const dfrom = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
    const toISO = d => d.toISOString().slice(0, 10);

    const dateGrid  = el('div', { class: 'ad2-grid-2' });
    const fromInput = el('input', { class: 'ad2-input', type: 'date', value: toISO(dfrom) });
    const toInput   = el('input', { class: 'ad2-input', type: 'date', value: toISO(now) });
    dateGrid.append(mkField('From', fromInput), mkField('To', toInput));

    const loadBtn   = el('button', { class: 'ad2-btn ad2-btn-primary ad2-btn-sm', style: 'margin-top:4px' }, '📊 Load Report');
    const reportOut = el('div', { style: 'margin-top:14px' });

    const renderReportOutput = () => {
      reportOut.replaceChildren();
      const r = state.report;
      if (!r) {
        reportOut.appendChild(el('div', { class: 'ad2-empty' },
          el('div', { class: 'ad2-empty-icon' }, '📊'),
          el('div', { class: 'ad2-empty-title' }, 'No report data yet'),
          el('div', { class: 'ad2-empty-desc' }, 'Activate your campaign to start tracking impressions, clicks, and conversions.'),
        ));
        return;
      }
      const totals = r.totals || {};
      const grid   = el('div', { class: 'ad2-report-grid' });
      [
        ['Impressions', totals.impressions || 0],
        ['Clicks',      totals.clicks      || 0],
        ['CTR',         totals.ctr != null ? `${totals.ctr}%` : '0%'],
      ].forEach(([lbl, val]) => {
        grid.appendChild(el('div', { class: 'ad2-report-card' },
          el('div', { class: 'ad2-report-val' }, String(val)),
          el('div', { class: 'ad2-report-lbl' }, lbl),
        ));
      });
      reportOut.appendChild(grid);

      const rows = r.byPlacement || [];
      if (rows.length) {
        const tableWrap = el('div', { style: 'margin-top:14px;border-radius:8px;overflow:hidden;border:1px solid #e2e8f0' });
        const table  = el('table', { style: 'width:100%;border-collapse:collapse;font-size:12px' });
        const thStyle = 'background:#f8fafc;padding:8px 12px;text-align:left;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:#94a3b8;border-bottom:1px solid #e2e8f0';
        const tdStyle = 'padding:9px 12px;border-bottom:1px solid #f1f5f9;color:#334155';
        table.appendChild(el('thead', {}, el('tr', {},
          ...['Placement', 'Impressions', 'Clicks', 'CTR'].map(h => el('th', { style: thStyle }, h))
        )));
        const tbody = el('tbody');
        rows.forEach(row => {
          tbody.appendChild(el('tr', {},
            el('td', { style: tdStyle }, row.slotKey || row.placementId || '—'),
            el('td', { style: tdStyle }, String(row.impressions || 0)),
            el('td', { style: tdStyle }, String(row.clicks || 0)),
            el('td', { style: tdStyle }, row.ctr != null ? `${row.ctr}%` : '—'),
          ));
        });
        table.appendChild(tbody);
        tableWrap.appendChild(table);
        reportOut.appendChild(tableWrap);
      }
    };

    loadBtn.addEventListener('click', async () => {
      if (!state.selectedCampaign) return;
      loadBtn.disabled = true; loadBtn.textContent = '⏳ Loading…';
      try {
        const fromUtc = fromInput.value ? new Date(fromInput.value).toISOString() : undefined;
        const toUtc   = toInput.value   ? new Date(toInput.value).toISOString()   : undefined;
        state.report  = await client.ads.getReport(state.selectedCampaign.id || state.selectedCampaign.campaignId, fromUtc, toUtc);
        hImpressions.textContent = String(state.report?.totals?.impressions || 0);
        renderReportOutput();
      } catch (err) {
        notifier.show({ message: mapApiError(err).message, variant: 'danger' });
      } finally {
        loadBtn.disabled = false; loadBtn.textContent = '📊 Load Report';
      }
    });

    pane.append(dateGrid, loadBtn, reportOut);
    renderReportOutput();
  };

  // ── Campaign list renderer ────────────────────────────────────────────────
  const renderCampaignList = () => {
    listBody.replaceChildren();
    if (!state.campaigns.length) {
      listBody.appendChild(el('div', { class: 'ad2-empty' },
        el('div', { class: 'ad2-empty-icon' }, '📢'),
        el('div', { class: 'ad2-empty-title' }, 'No campaigns yet'),
        el('div', { class: 'ad2-empty-desc' }, 'Create your first campaign to start driving targeted traffic.'),
      ));
      return;
    }
    const selectedId = state.selectedCampaign?.id || state.selectedCampaign?.campaignId || '';
    state.campaigns.forEach(c => {
      const cid  = c.id || c.campaignId || '';
      const card = el('div', { class: `ad2-campaign-card${selectedId === cid ? ' active' : ''}` });

      const top = el('div', { style: 'display:flex;align-items:flex-start;justify-content:space-between;gap:8px;margin-bottom:4px' });
      top.append(
        el('div', { class: 'ad2-campaign-name' }, c.name || 'Unnamed'),
        el('span', { class: `ad2-pill ${c.isActive ? 'ad2-pill-active' : 'ad2-pill-inactive'}` }, c.isActive ? 'Active' : 'Inactive'),
      );
      card.appendChild(top);

      const meta = el('div', { class: 'ad2-campaign-meta' });
      if (c.objective) meta.appendChild(el('span', {}, c.objective));
      if (c.startsAtUtc || c.endsAtUtc) meta.appendChild(el('span', {}, `${fmtDate(c.startsAtUtc)} → ${fmtDate(c.endsAtUtc)}`));
      card.appendChild(meta);

      card.addEventListener('click', () => loadCampaignDetail(c));
      listBody.appendChild(card);
    });
  };

  // ── Load campaign detail ──────────────────────────────────────────────────
  const loadCampaignDetail = async (c) => {
    const cid = c.id || c.campaignId || '';
    try {
      state.selectedCampaign = await client.ads.getCampaign(cid);
      state.placements       = [...(state.selectedCampaign?.placements || [])];
      state.selectedTab      = 'overview';
      state.showCreateForm   = false;
      state.report           = null;
      renderCampaignList();
      renderRight();
    } catch (err) {
      notifier.show({ message: mapApiError(err).message, variant: 'danger' });
    }
  };

  // ── Load campaigns ────────────────────────────────────────────────────────
  const loadCampaigns = async () => {
    if (!state.siteId) { state.campaigns = []; renderCampaignList(); return; }
    try {
      state.campaigns        = await client.ads.listCampaigns(state.siteId);
      hTotal.textContent     = String(state.campaigns.length);
      hActive.textContent    = String(state.campaigns.filter(c => c.isActive).length);
      renderCampaignList();
    } catch (err) {
      notifier.show({ message: mapApiError(err).message, variant: 'danger' });
    }
  };

  // ── Placement payload helper ──────────────────────────────────────────────
  const toPlacementPayload = (pl, idx) => ({
    slotKey:        pl.slotKey        || '',
    pathPattern:    pl.pathPattern    || null,
    device:         pl.device         || 'all',
    headline:       pl.headline       || '',
    body:           pl.body           || null,
    imageUrl:       pl.imageUrl       || null,
    destinationUrl: pl.destinationUrl || '',
    ctaText:        pl.ctaText        || null,
    order:          pl.order          || idx + 1,
    isActive:       pl.isActive       !== false,
  });

  // ── Site change ───────────────────────────────────────────────────────────
  siteSelect.addEventListener('change', () => {
    state.siteId           = siteSelect.value;
    state.selectedCampaign = null;
    state.campaigns        = [];
    state.placements       = [];
    state.report           = null;
    state.showCreateForm   = false;
    loadCampaigns();
    renderRight();
  });

  // ── Init ──────────────────────────────────────────────────────────────────
  const init = async () => {
    try {
      state.sites = await client.sites.list();
      siteSelect.innerHTML = '';
      if (!state.sites.length) {
        siteSelect.appendChild(el('option', { value: '' }, 'No sites found'));
      } else {
        siteSelect.appendChild(el('option', { value: '' }, '— Select site —'));
        state.sites.forEach(s => {
          const sid = s.siteId || s.id || '';
          siteSelect.appendChild(el('option', { value: sid }, s.domain || sid));
        });
        state.siteId = state.sites[0]?.siteId || state.sites[0]?.id || '';
        siteSelect.value = state.siteId;
        await loadCampaigns();
      }
    } catch (err) {
      notifier.show({ message: mapApiError(err).message, variant: 'danger' });
    }
    renderRight();
  };

  init();
};
