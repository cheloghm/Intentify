/**
 * platformAdmin.js — Platform Admin (revamped Phase 7 design)
 * Dark hero + panel design language matching visitors.js / sites.js / leads.js.
 * All original functionality preserved: summary cards, tenant list, search,
 * pagination, tenant detail view.
 */

import { createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

// ─── Helpers ──────────────────────────────────────────────────────────────────

const fmtDate = (v) => {
  if (!v) return '—';
  const d = new Date(v);
  return isNaN(d) ? '—' : d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
};

const fmtNum = (v) => {
  const n = Number(v);
  return isNaN(n) ? '0' : n.toLocaleString();
};

const el = (tag, attrs = {}, ...kids) => {
  const e = document.createElement(tag);
  Object.entries(attrs).forEach(([k, v]) => {
    if (k === 'class')          e.className = v;
    else if (k === 'style')     typeof v === 'string' ? (e.style.cssText = v) : Object.assign(e.style, v);
    else if (k.startsWith('@')) e.addEventListener(k.slice(1), v);
    else e.setAttribute(k, v);
  });
  kids.flat(Infinity).forEach(c => c != null && e.append(typeof c === 'string' ? document.createTextNode(c) : c));
  return e;
};

// ─── Styles ───────────────────────────────────────────────────────────────────

const injectStyles = () => {
  if (document.getElementById('_pa_css')) return;
  const s = document.createElement('style');
  s.id = '_pa_css';
  s.textContent = `
@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap');

.pa-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;gap:20px;width:100%;max-width:1200px;padding-bottom:60px}

/* Hero */
.pa-hero{background:linear-gradient(135deg,#0f172a 0%,#1e293b 100%);border-radius:16px;padding:28px 36px;position:relative;overflow:hidden}
.pa-hero::before{content:'';position:absolute;top:-40px;right:-40px;width:220px;height:220px;background:radial-gradient(circle,rgba(99,102,241,.18) 0%,transparent 70%);pointer-events:none}
.pa-hero::after{content:'';position:absolute;bottom:-20px;left:20%;width:160px;height:160px;background:radial-gradient(circle,rgba(16,185,129,.1) 0%,transparent 70%);pointer-events:none}
.pa-hero-title{font-size:24px;font-weight:700;color:#f8fafc;letter-spacing:-.02em;margin-bottom:4px}
.pa-hero-sub{font-size:13px;color:#64748b;margin-bottom:20px}
.pa-hero-stats{display:flex;gap:28px;flex-wrap:wrap}
.pa-stat{display:flex;flex-direction:column;gap:2px}
.pa-stat-val{font-family:'JetBrains Mono',monospace;font-size:22px;font-weight:700;color:#f1f5f9;letter-spacing:-.02em}
.pa-stat-lbl{font-size:10px;color:#64748b;text-transform:uppercase;letter-spacing:.07em}

/* Controls */
.pa-controls{display:flex;align-items:center;gap:8px;flex-wrap:wrap}
.pa-search{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;color:#1e293b;background:#fff;border:1px solid #e2e8f0;border-radius:8px;padding:8px 11px;outline:none;flex:1;max-width:360px;transition:border .14s}
.pa-search:focus{border-color:#6366f1;box-shadow:0 0 0 3px rgba(99,102,241,.1)}
.pa-btn{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;font-weight:600;padding:7px 16px;border-radius:8px;border:none;cursor:pointer;transition:all .14s;white-space:nowrap}
.pa-btn-primary{background:#6366f1;color:#fff}.pa-btn-primary:hover{background:#4f46e5}
.pa-btn-outline{background:#fff;color:#64748b;border:1px solid #e2e8f0}.pa-btn-outline:hover{background:#f8fafc;color:#1e293b}
.pa-btn-danger{background:#fee2e2;color:#dc2626;border:none}.pa-btn-danger:hover{background:#fecaca}
.pa-btn-sm{padding:5px 12px;font-size:12px}

/* Metric grid */
.pa-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(160px,1fr));gap:10px}
.pa-metric{background:#fff;border:1px solid #e2e8f0;border-radius:12px;padding:16px;transition:box-shadow .14s}
.pa-metric:hover{box-shadow:0 4px 14px rgba(0,0,0,.07)}
.pa-metric-val{font-family:'JetBrains Mono',monospace;font-size:22px;font-weight:700;color:#0f172a;margin-bottom:4px}
.pa-metric-lbl{font-size:10px;color:#94a3b8;text-transform:uppercase;letter-spacing:.06em;font-weight:700}
.pa-metric-badge{display:inline-flex;align-items:center;gap:4px;padding:3px 8px;border-radius:999px;font-size:10px;font-weight:700}
.pa-badge-ok{background:#d1fae5;color:#065f46}
.pa-badge-warn{background:#fef3c7;color:#92400e}
.pa-badge-err{background:#fee2e2;color:#dc2626}

/* Panel */
.pa-panel{background:#fff;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden}
.pa-panel-hd{display:flex;align-items:center;justify-content:space-between;padding:14px 20px;border-bottom:1px solid #f1f5f9}
.pa-panel-title{font-size:13px;font-weight:700;color:#0f172a;letter-spacing:-.01em}

/* Table */
.pa-table{width:100%;border-collapse:collapse;font-size:12.5px}
.pa-table thead th{background:#f8fafc;padding:10px 16px;text-align:left;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:#94a3b8;border-bottom:1px solid #e2e8f0;white-space:nowrap}
.pa-table tbody td{padding:13px 16px;border-bottom:1px solid #f1f5f9;color:#334155;vertical-align:middle}
.pa-table tbody tr:last-child td{border-bottom:none}
.pa-table tbody tr:hover{background:#fafbff;cursor:pointer}
.pa-td-name{font-weight:600;color:#1e293b}
.pa-td-mono{font-family:'JetBrains Mono',monospace;font-size:11.5px;color:#64748b}
.pa-link{color:#6366f1;text-decoration:none;font-weight:600;font-size:12px;padding:4px 10px;border:1px solid #c7d2fe;border-radius:6px;background:#eef2ff;transition:background .12s}
.pa-link:hover{background:#e0e7ff}

/* Pagination */
.pa-pagination{display:flex;align-items:center;justify-content:space-between;padding:12px 20px;border-top:1px solid #f1f5f9;font-size:12px;color:#64748b}

/* Skeleton */
.pa-skel{background:linear-gradient(90deg,#f1f5f9 25%,#e2e8f0 50%,#f1f5f9 75%);background-size:200% 100%;animation:_pash 1.4s infinite;border-radius:12px;height:100px}
@keyframes _pash{0%{background-position:200% 0}100%{background-position:-200% 0}}

/* Detail page */
.pa-detail-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(150px,1fr));gap:10px}
.pa-detail-table{width:100%;border-collapse:collapse;font-size:13px}
.pa-detail-table td{padding:10px 16px;border-bottom:1px solid #f1f5f9;vertical-align:top}
.pa-detail-table td:first-child{font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:#94a3b8;width:180px;white-space:nowrap}
.pa-detail-table td:last-child{color:#1e293b}
.pa-detail-table tr:last-child td{border-bottom:none}
.pa-back{display:inline-flex;align-items:center;gap:6px;color:#6366f1;text-decoration:none;font-size:13px;font-weight:600;padding:6px 12px;border:1px solid #c7d2fe;border-radius:8px;background:#eef2ff;transition:background .12s}
.pa-back:hover{background:#e0e7ff}
  `;
  document.head.appendChild(s);
};

// ─── Platform Admin Overview ───────────────────────────────────────────────────

export const renderPlatformAdminView = (container, { apiClient, toast, query } = {}) => {
  injectStyles();
  const client   = apiClient || createApiClient();
  const notifier = toast     || createToastManager();

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

  const root = el('div', { class: 'pa-root' });
  container.appendChild(root);

  // ── Hero ───────────────────────────────────────────────────────────────────
  const hero = el('div', { class: 'pa-hero' });
  hero.appendChild(el('div', { class: 'pa-hero-title' }, '⚙️ Platform Admin'));
  hero.appendChild(el('div', { class: 'pa-hero-sub' }, 'Platform-level oversight across all tenants and modules'));

  const heroStats = el('div', { class: 'pa-hero-stats' });
  const mkStat = (lbl) => {
    const w = el('div', { class: 'pa-stat' });
    const v = el('div', { class: 'pa-stat-val' }, '—');
    w.append(v, el('div', { class: 'pa-stat-lbl' }, lbl));
    heroStats.appendChild(w);
    return v;
  };
  const hTenants  = mkStat('Tenants');
  const hSites    = mkStat('Sites');
  const hVisitors = mkStat('Visitors');
  const hSessions = mkStat('Engage Sessions');
  hero.appendChild(heroStats);
  root.appendChild(hero);

  // ── Controls ───────────────────────────────────────────────────────────────
  const controls = el('div', { class: 'pa-controls' });
  const searchInput = el('input', { class: 'pa-search', type: 'search', placeholder: '🔍 Search tenants by name or domain…', value: state.search });
  const searchBtn = el('button', { class: 'pa-btn pa-btn-primary' }, 'Search');
  const clearBtn  = el('button', { class: 'pa-btn pa-btn-outline' }, 'Clear');
  controls.append(searchInput, searchBtn, clearBtn);
  root.appendChild(controls);

  // ── Metric grid ────────────────────────────────────────────────────────────
  const metricsGrid = el('div', { class: 'pa-grid' });
  root.appendChild(metricsGrid);

  const renderMetrics = () => {
    metricsGrid.replaceChildren();
    if (!state.summary) {
      Array(8).fill(null).forEach(() => metricsGrid.appendChild(el('div', { class: 'pa-skel', style: 'height:80px' })));
      return;
    }
    const s = state.summary;
    const entries = [
      ['Tenants',               fmtNum(s.totalTenants)],
      ['Sites',                 fmtNum(s.totalSites)],
      ['Visitors',              fmtNum(s.totalVisitors)],
      ['Engage Sessions',       fmtNum(s.totalEngageSessions)],
      ['Engage Messages',       fmtNum(s.totalEngageMessages)],
      ['Tickets',               fmtNum(s.totalTickets)],
      ['Promos',                fmtNum(s.totalPromos)],
      ['Promo Entries',         fmtNum(s.totalPromoEntries)],
      ['Intelligence Records',  fmtNum(s.totalIntelligenceTrendRecords)],
      ['Knowledge Sources',     fmtNum(s.totalKnowledgeSources)],
    ];
    entries.forEach(([lbl, val]) => {
      const m = el('div', { class: 'pa-metric' });
      m.appendChild(el('div', { class: 'pa-metric-val' }, val));
      m.appendChild(el('div', { class: 'pa-metric-lbl' }, lbl));
      metricsGrid.appendChild(m);
    });

    // Health card
    const health = el('div', { class: 'pa-metric' });
    const status = (s.healthStatus || 'unknown').toLowerCase();
    const badgeCls = status === 'ok' ? 'pa-badge-ok' : status === 'warning' ? 'pa-badge-warn' : 'pa-badge-err';
    const icon = status === 'ok' ? '✓' : '⚠';
    health.appendChild(el('div', { class: `pa-metric-badge ${badgeCls}`, style: 'margin-bottom:8px;font-size:12px' }, `${icon} ${s.healthStatus || 'Unknown'}`));
    health.appendChild(el('div', { class: 'pa-metric-lbl' }, 'Health Status'));
    if (state.operational) {
      const o = state.operational;
      health.appendChild(el('div', { style: 'font-size:11px;color:#94a3b8;margin-top:6px;line-height:1.5' },
        `Indexed: ${fmtNum(o.indexedKnowledgeSources)} · Failed: ${fmtNum(o.failedKnowledgeSources)}`
      ));
    }
    metricsGrid.appendChild(health);
  };

  // ── Tenant table panel ─────────────────────────────────────────────────────
  const tablePanel = el('div', { class: 'pa-panel' });
  const tablePanelHd = el('div', { class: 'pa-panel-hd' });
  const tablePanelTitle = el('div', { class: 'pa-panel-title' }, '🏢 Tenants');
  const pageInfoEl = el('div', { style: 'font-size:12px;color:#94a3b8;font-family:JetBrains Mono,monospace' });
  tablePanelHd.append(tablePanelTitle, pageInfoEl);
  tablePanel.appendChild(tablePanelHd);

  const tableWrap = el('div', { style: 'overflow-x:auto' });
  tablePanel.appendChild(tableWrap);

  const paginationEl = el('div', { class: 'pa-pagination' });
  const prevBtn = el('button', { class: 'pa-btn pa-btn-outline pa-btn-sm' }, '← Previous');
  const nextBtn = el('button', { class: 'pa-btn pa-btn-outline pa-btn-sm' }, 'Next →');
  paginationEl.append(prevBtn, pageInfoEl.cloneNode(true), nextBtn);
  tablePanel.appendChild(paginationEl);
  root.appendChild(tablePanel);

  const renderTable = () => {
    tableWrap.replaceChildren();
    if (!state.tenants.length) {
      tableWrap.appendChild(el('div', { style: 'padding:32px;text-align:center;color:#94a3b8;font-size:13px' }, 'No tenants found.'));
      return;
    }
    const table = el('table', { class: 'pa-table' });
    table.appendChild(el('thead', {}, el('tr', {},
      ...['Tenant', 'Domain', 'Plan', 'Sites', 'Visitors', 'Tickets', 'Last Activity', 'Created', ''].map(h =>
        el('th', {}, h))
    )));
    const tbody = el('tbody', {});
    state.tenants.forEach(item => {
      const tr = el('tr', {});
      tr.append(
        el('td', {}, el('div', { class: 'pa-td-name' }, item.tenantName || '—')),
        el('td', {}, el('div', { class: 'pa-td-mono' }, item.domain || '—')),
        el('td', {}, item.plan ? el('span', { class: 'pa-metric-badge', style: 'background:#eef2ff;color:#6366f1' }, item.plan) : '—'),
        el('td', { style: 'font-family:JetBrains Mono,monospace;font-size:12px' }, fmtNum(item.usage?.siteCount)),
        el('td', { style: 'font-family:JetBrains Mono,monospace;font-size:12px' }, fmtNum(item.usage?.visitorsCount)),
        el('td', { style: 'font-family:JetBrains Mono,monospace;font-size:12px' }, fmtNum(item.usage?.ticketsCount)),
        el('td', { style: 'font-size:12px;color:#64748b' }, fmtDate(item.usage?.lastActivityAtUtc)),
        el('td', { style: 'font-size:12px;color:#64748b' }, fmtDate(item.createdAt)),
        el('td', {}, el('a', { class: 'pa-link', href: `#/platform-admin/tenant/${item.tenantId}` }, 'View →')),
      );
      tbody.appendChild(tr);
    });
    table.appendChild(tbody);
    tableWrap.appendChild(table);

    const start = state.totalCount === 0 ? 0 : (state.page - 1) * state.pageSize + 1;
    const end   = Math.min(state.page * state.pageSize, state.totalCount);
    const info  = `Showing ${start}–${end} of ${state.totalCount}`;
    pageInfoEl.textContent  = info;
    prevBtn.disabled = state.page <= 1;
    nextBtn.disabled = end >= state.totalCount;
  };

  const syncHash = () => {
    const p = new URLSearchParams();
    p.set('page', String(state.page));
    if (state.search) p.set('search', state.search);
    window.location.hash = `#/platform-admin?${p.toString()}`;
  };

  const load = async () => {
    state.loading = true;
    renderMetrics();
    try {
      const [summary, operational, tenantResponse] = await Promise.all([
        client.platformAdmin.getSummary(),
        client.platformAdmin.getOperationalSummary(),
        client.platformAdmin.listTenants({ page: state.page, pageSize: state.pageSize, search: state.search || undefined }),
      ]);
      state.summary     = summary;
      state.operational = operational;
      state.tenants     = Array.isArray(tenantResponse.items) ? tenantResponse.items : [];
      state.totalCount  = Number(tenantResponse.totalCount) || 0;

      // Update hero stats
      hTenants.textContent  = fmtNum(summary.totalTenants);
      hSites.textContent    = fmtNum(summary.totalSites);
      hVisitors.textContent = fmtNum(summary.totalVisitors);
      hSessions.textContent = fmtNum(summary.totalEngageSessions);

      renderMetrics();
      renderTable();
    } catch (err) {
      notifier.show({ message: mapApiError(err).message, variant: 'danger' });
    } finally {
      state.loading = false;
    }
  };

  const doSearch = async () => {
    state.search = searchInput.value.trim();
    state.page   = 1;
    syncHash();
    await load();
  };

  searchBtn.addEventListener('click', doSearch);
  searchInput.addEventListener('keydown', e => { if (e.key === 'Enter') doSearch(); });
  clearBtn.addEventListener('click', async () => {
    searchInput.value = '';
    state.search = '';
    state.page   = 1;
    syncHash();
    await load();
  });
  prevBtn.addEventListener('click', async () => {
    if (state.page <= 1) return;
    state.page--;
    syncHash();
    await load();
  });
  nextBtn.addEventListener('click', async () => {
    const max = Math.max(1, Math.ceil(state.totalCount / state.pageSize));
    if (state.page >= max) return;
    state.page++;
    syncHash();
    await load();
  });

  load();
};

// ─── Tenant Detail ─────────────────────────────────────────────────────────────

export const renderPlatformAdminTenantDetailView = (container, { apiClient, toast, params } = {}) => {
  injectStyles();
  const client   = apiClient || createApiClient();
  const notifier = toast     || createToastManager();
  const tenantId = params?.tenantId;

  const root = el('div', { class: 'pa-root' });
  container.appendChild(root);

  const backLink = el('a', { class: 'pa-back', href: '#/platform-admin' }, '← Back to Platform Overview');
  root.appendChild(backLink);

  const loadingEl = el('div', { style: 'color:#94a3b8;font-size:13px;padding:20px 0' }, '⏳ Loading tenant…');
  root.appendChild(loadingEl);

  const content = el('div', { style: 'display:flex;flex-direction:column;gap:14px' });
  root.appendChild(content);

  const mkDetailSection = (title, rows) => {
    const panel = el('div', { class: 'pa-panel' });
    panel.appendChild(el('div', { class: 'pa-panel-hd' }, el('div', { class: 'pa-panel-title' }, title)));
    const table = el('table', { class: 'pa-detail-table' });
    const tbody = el('tbody', {});
    rows.forEach(([field, value]) => {
      tbody.appendChild(el('tr', {}, el('td', {}, field), el('td', {}, value || '—')));
    });
    table.appendChild(tbody);
    panel.appendChild(table);
    return panel;
  };

  const mkMetricGrid = (entries) => {
    const grid = el('div', { class: 'pa-detail-grid' });
    entries.forEach(([lbl, val]) => {
      const m = el('div', { class: 'pa-metric' });
      m.appendChild(el('div', { class: 'pa-metric-val' }, fmtNum(val)));
      m.appendChild(el('div', { class: 'pa-metric-lbl' }, lbl));
      grid.appendChild(m);
    });
    return grid;
  };

  const renderDetail = (d) => {
    content.replaceChildren();

    // Hero
    const heroEl = el('div', { class: 'pa-hero' });
    heroEl.appendChild(el('div', { class: 'pa-hero-title' }, d.tenantName || 'Tenant Detail'));
    heroEl.appendChild(el('div', { class: 'pa-hero-sub' }, d.domain || ''));
    const hStats = el('div', { class: 'pa-hero-stats' });
    const mkHS = (lbl, val) => {
      const w = el('div', { class: 'pa-stat' });
      w.append(el('div', { class: 'pa-stat-val' }, fmtNum(val)), el('div', { class: 'pa-stat-lbl' }, lbl));
      hStats.appendChild(w);
    };
    mkHS('Sites',           d.usage?.siteCount);
    mkHS('Visitors',        d.usage?.visitorsCount);
    mkHS('Engage Sessions', d.usage?.engageSessionsCount);
    mkHS('Tickets',         d.usage?.ticketsCount);
    heroEl.appendChild(hStats);
    content.appendChild(heroEl);

    // Metadata
    content.appendChild(mkDetailSection('Tenant Metadata', [
      ['Tenant Name',   d.tenantName],
      ['Domain',        d.domain],
      ['Plan',          d.plan],
      ['Industry',      d.industry],
      ['Category',      d.category],
      ['Created',       fmtDate(d.createdAt)],
      ['Updated',       fmtDate(d.updatedAt)],
      ['Last Activity', fmtDate(d.usage?.lastActivityAtUtc)],
    ]));

    // Usage grid panel
    const usagePanel = el('div', { class: 'pa-panel' });
    usagePanel.appendChild(el('div', { class: 'pa-panel-hd' }, el('div', { class: 'pa-panel-title' }, '📊 Usage Summary')));
    const u = d.usage || {};
    usagePanel.appendChild(el('div', { style: 'padding:14px' }, mkMetricGrid([
      ['Sites',                u.siteCount],
      ['Visitors',             u.visitorsCount],
      ['Engage Sessions',      u.engageSessionsCount],
      ['Engage Messages',      u.engageMessagesCount],
      ['Tickets',              u.ticketsCount],
      ['Promos',               u.promosCount],
      ['Promo Entries',        u.promoEntriesCount],
      ['Intelligence Records', u.intelligenceRecordCount],
      ['Ads Campaigns',        u.adsCampaignCount],
      ['Knowledge Sources',    u.knowledgeSourcesCount],
      ['Knowledge Indexed',    u.knowledgeIndexedCount],
      ['Knowledge Failed',     u.knowledgeFailedCount],
    ])));
    content.appendChild(usagePanel);

    // Sites table
    const sites = Array.isArray(d.sites) ? d.sites : [];
    const sitesPanel = el('div', { class: 'pa-panel' });
    sitesPanel.appendChild(el('div', { class: 'pa-panel-hd' }, el('div', { class: 'pa-panel-title' }, `🌐 Sites (${sites.length})`)));
    if (!sites.length) {
      sitesPanel.appendChild(el('div', { style: 'padding:20px;color:#94a3b8;font-size:13px' }, 'No sites found.'));
    } else {
      const tbl = el('table', { class: 'pa-table' });
      tbl.appendChild(el('thead', {}, el('tr', {},
        ...['Domain', 'Site ID', 'Created', 'Updated', 'First Event'].map(h => el('th', {}, h))
      )));
      const tb = el('tbody');
      sites.forEach(site => {
        tb.appendChild(el('tr', {},
          el('td', {}, el('span', { style: 'font-weight:600;color:#1e293b' }, site.domain || '—')),
          el('td', {}, el('span', { class: 'pa-td-mono' }, site.siteId || '—')),
          el('td', { style: 'font-size:12px;color:#64748b' }, fmtDate(site.createdAtUtc)),
          el('td', { style: 'font-size:12px;color:#64748b' }, fmtDate(site.updatedAtUtc)),
          el('td', { style: 'font-size:12px;color:#64748b' }, fmtDate(site.firstEventReceivedAtUtc)),
        ));
      });
      tbl.appendChild(tb);
      sitesPanel.appendChild(el('div', { style: 'overflow-x:auto' }, tbl));
    }
    content.appendChild(sitesPanel);

    // Recent activity
    const act = d.recentActivity || {};
    content.appendChild(mkDetailSection('⏱ Recent Activity', [
      ['Sites',           fmtDate(act.lastSiteActivityAtUtc)],
      ['Visitors',        fmtDate(act.lastVisitorActivityAtUtc)],
      ['Engage Sessions', fmtDate(act.lastEngageSessionActivityAtUtc)],
      ['Tickets',         fmtDate(act.lastTicketActivityAtUtc)],
      ['Promos',          fmtDate(act.lastPromoActivityAtUtc)],
      ['Promo Entries',   fmtDate(act.lastPromoEntryActivityAtUtc)],
      ['Intelligence',    fmtDate(act.lastIntelligenceActivityAtUtc)],
      ['Ads',             fmtDate(act.lastAdsActivityAtUtc)],
      ['Knowledge',       fmtDate(act.lastKnowledgeActivityAtUtc)],
    ]));
  };

  const load = async () => {
    if (!tenantId) { loadingEl.textContent = 'Tenant ID is missing.'; return; }
    try {
      const detail = await client.platformAdmin.getTenantDetail(tenantId);
      renderDetail(detail);
    } catch (err) {
      const uiErr = mapApiError(err);
      notifier.show({ message: uiErr.message, variant: 'danger' });
      content.appendChild(el('div', { style: 'color:#dc2626;font-size:13px' }, uiErr.status === 404 ? 'Tenant not found.' : uiErr.message));
    } finally {
      loadingEl.style.display = 'none';
    }
  };

  load();
};
