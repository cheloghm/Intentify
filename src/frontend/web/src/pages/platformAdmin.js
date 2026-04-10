/**
 * platformAdmin.js — Platform Admin tabbed dashboard
 * Tabs: 📊 Dashboard | 👥 Tenants | 💡 Feedback
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

const timeAgo = (v) => {
  if (!v) return '—';
  const d = new Date(v);
  if (isNaN(d)) return '—';
  const secs = Math.floor((Date.now() - d.getTime()) / 1000);
  if (secs < 60) return 'just now';
  if (secs < 3600) return `${Math.floor(secs / 60)}m ago`;
  if (secs < 86400) return `${Math.floor(secs / 3600)}h ago`;
  if (secs < 86400 * 7) return `${Math.floor(secs / 86400)}d ago`;
  return fmtDate(v);
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

/* Tabs */
.pa-tabs{display:flex;gap:3px;background:#1e293b;border-radius:10px;padding:3px;margin-bottom:20px;width:fit-content}
.pa-tab{font-family:'Plus Jakarta Sans',system-ui,sans-serif;padding:7px 18px;border-radius:8px;border:none;font-size:13px;font-weight:600;color:rgba(255,255,255,0.5);background:transparent;cursor:pointer;transition:all .14s}
.pa-tab.active{background:rgba(255,255,255,0.1);color:#fff}
.pa-tab-panel{display:none}
.pa-tab-panel.active{display:block}

/* Controls */
.pa-controls{display:flex;align-items:center;gap:8px;flex-wrap:wrap}
.pa-search{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;color:#1e293b;background:#fff;border:1px solid #e2e8f0;border-radius:8px;padding:8px 11px;outline:none;flex:1;max-width:360px;transition:border .14s}
.pa-search:focus{border-color:#6366f1;box-shadow:0 0 0 3px rgba(99,102,241,.1)}
.pa-btn{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;font-weight:600;padding:7px 16px;border-radius:8px;border:none;cursor:pointer;transition:all .14s;white-space:nowrap}
.pa-btn-primary{background:#6366f1;color:#fff}.pa-btn-primary:hover{background:#4f46e5}
.pa-btn-outline{background:#fff;color:#64748b;border:1px solid #e2e8f0}.pa-btn-outline:hover{background:#f8fafc;color:#1e293b}
.pa-btn-sm{padding:5px 12px;font-size:12px}

/* Dashboard KPI grid */
.pa-dash-grid{display:grid;grid-template-columns:repeat(5,1fr);gap:12px;margin-bottom:20px}
@media(max-width:900px){.pa-dash-grid{grid-template-columns:repeat(2,1fr)}}
.pa-dash-card{background:#fff;border:1px solid #e2e8f0;border-radius:12px;padding:16px 18px}
.pa-dash-val{font-family:'JetBrains Mono',monospace;font-size:26px;font-weight:700;color:#0f172a;letter-spacing:-.02em;line-height:1}
.pa-dash-lbl{font-size:11px;color:#94a3b8;text-transform:uppercase;letter-spacing:.07em;font-weight:700;margin-top:4px}
.pa-dash-delta{font-size:11px;color:#10b981;font-weight:600;margin-top:3px}

/* Chart row */
.pa-chart-row{display:grid;grid-template-columns:1fr 1fr;gap:16px;margin-bottom:20px}
@media(max-width:700px){.pa-chart-row{grid-template-columns:1fr}}
.pa-chart-card{background:#fff;border:1px solid #e2e8f0;border-radius:12px;padding:18px}
.pa-chart-title{font-size:12px;font-weight:700;color:#0f172a;margin-bottom:14px}

/* Health bar */
.pa-health-bar{height:8px;background:#e2e8f0;border-radius:999px;overflow:hidden;margin:10px 0}
.pa-health-fill{height:100%;border-radius:999px;background:#10b981;transition:width .5s ease}

/* Signup table */
.pa-signup-table{width:100%;border-collapse:collapse;font-size:12.5px}
.pa-signup-table th{padding:8px 12px;text-align:left;font-size:10.5px;font-weight:700;color:#94a3b8;text-transform:uppercase;letter-spacing:.06em;border-bottom:1px solid #f1f5f9}
.pa-signup-table td{padding:9px 12px;border-bottom:1px solid #f8fafc;color:#334155}
.pa-signup-table tbody tr{cursor:pointer}
.pa-signup-table tbody tr:hover{background:#fafbff}

/* Plan pill */
.pa-plan-pill{display:inline-flex;align-items:center;padding:2px 8px;border-radius:999px;font-size:10px;font-weight:700}
.pa-plan-starter{background:#f1f5f9;color:#64748b}
.pa-plan-growth{background:#eef2ff;color:#4338ca}
.pa-plan-agency{background:#0f172a;color:#93c5fd}

/* Metric grid (used in detail view) */
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

/* Table (tenants / feedback) */
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

const PLAN_BADGE_CLASS = (plan) => {
  const p = (plan || '').toLowerCase();
  if (p === 'agency') return 'pa-metric-badge pa-plan-agency';
  if (p === 'growth') return 'pa-metric-badge pa-plan-growth';
  return 'pa-metric-badge pa-plan-starter';
};

const PLAN_PILL_CLASS = (plan) => {
  const p = (plan || '').toLowerCase();
  if (p === 'agency') return 'pa-plan-pill pa-plan-agency';
  if (p === 'growth') return 'pa-plan-pill pa-plan-growth';
  return 'pa-plan-pill pa-plan-starter';
};

// ─── Platform Admin Overview ───────────────────────────────────────────────────

export const renderPlatformAdminView = (container, { apiClient, toast, query } = {}) => {
  injectStyles();
  const client   = apiClient || createApiClient();
  const notifier = toast     || createToastManager();

  const root = el('div', { class: 'pa-root' });
  container.appendChild(root);

  // ── Hero ───────────────────────────────────────────────────────────────────
  const hero = el('div', { class: 'pa-hero' });
  hero.appendChild(el('div', { class: 'pa-hero-title' }, '⚙️ Platform Admin'));
  hero.appendChild(el('div', { class: 'pa-hero-sub' }, 'Platform-level oversight across all tenants, sites, and activity'));
  root.appendChild(hero);

  // ── Tabs ───────────────────────────────────────────────────────────────────
  const TABS = ['dashboard', 'tenants', 'feedback'];
  const TAB_LABELS = { dashboard: '📊 Dashboard', tenants: '👥 Tenants', feedback: '💡 Feedback' };

  let activeTab = 'dashboard';
  const tabPanels = {};
  const tabBtns   = {};
  const loaded    = { dashboard: false, tenants: false, feedback: false };

  const tabBar = el('div', { class: 'pa-tabs' });
  TABS.forEach(id => {
    const btn = el('button', { class: 'pa-tab' + (id === activeTab ? ' active' : '') }, TAB_LABELS[id]);
    btn.addEventListener('click', () => switchTab(id));
    tabBtns[id] = btn;
    tabBar.appendChild(btn);
  });
  root.appendChild(tabBar);

  TABS.forEach(id => {
    const panel = el('div', { class: 'pa-tab-panel' + (id === activeTab ? ' active' : '') });
    tabPanels[id] = panel;
    root.appendChild(panel);
  });

  const switchTab = (id) => {
    activeTab = id;
    TABS.forEach(t => {
      tabBtns[t].className   = 'pa-tab'       + (t === id ? ' active' : '');
      tabPanels[t].className = 'pa-tab-panel' + (t === id ? ' active' : '');
    });
    if (!loaded[id]) {
      loaded[id] = true;
      if (id === 'dashboard') loadDashboard();
      if (id === 'tenants')   loadTenants();
      if (id === 'feedback')  loadFeedback();
    }
  };

  // ══════════════════════════════════════════════════════════════════════════
  // TAB 1: DASHBOARD
  // ══════════════════════════════════════════════════════════════════════════
  const dashPanel = tabPanels['dashboard'];
  dashPanel.style.display = 'flex';
  dashPanel.style.flexDirection = 'column';
  dashPanel.style.gap = '0';

  // Skeleton
  const kpiGrid = el('div', { class: 'pa-dash-grid' });
  Array(5).fill(null).forEach(() => kpiGrid.appendChild(el('div', { class: 'pa-skel', style: 'height:86px' })));
  dashPanel.appendChild(kpiGrid);

  const chartRow = el('div', { class: 'pa-chart-row' });
  dashPanel.appendChild(chartRow);

  const recentPanel = el('div', { class: 'pa-panel' });
  dashPanel.appendChild(recentPanel);

  let planChartInstance = null;

  const renderDashboard = (d) => {
    // KPI cards
    kpiGrid.replaceChildren();
    const kpis = [
      { lbl: 'Total Subscribers', val: fmtNum(d.totalTenants),      delta: `+${fmtNum(d.tenantsThisWeek)} this week` },
      { lbl: 'Total Sites',        val: fmtNum(d.totalSites),        delta: `${fmtNum(d.healthySites)} healthy` },
      { lbl: 'Visitors 30d',       val: fmtNum(d.totalVisitors),     delta: null },
      { lbl: 'Total Leads',        val: fmtNum(d.totalLeads),        delta: null },
      { lbl: 'Conversations',      val: fmtNum(d.totalConversations), delta: null },
    ];
    kpis.forEach(({ lbl, val, delta }) => {
      const card = el('div', { class: 'pa-dash-card' });
      card.appendChild(el('div', { class: 'pa-dash-val' }, val));
      card.appendChild(el('div', { class: 'pa-dash-lbl' }, lbl));
      if (delta) card.appendChild(el('div', { class: 'pa-dash-delta' }, delta));
      kpiGrid.appendChild(card);
    });

    // Charts
    chartRow.replaceChildren();

    // Plan distribution doughnut
    const planCard = el('div', { class: 'pa-chart-card' });
    planCard.appendChild(el('div', { class: 'pa-chart-title' }, 'Plan Distribution'));
    const planCanvas = el('canvas', { style: 'max-height:200px' });
    planCard.appendChild(planCanvas);
    chartRow.appendChild(planCard);

    if (planChartInstance) { planChartInstance.destroy(); planChartInstance = null; }
    const pb = d.planBreakdown || {};
    // eslint-disable-next-line no-undef
    if (typeof Chart !== 'undefined') {
      // eslint-disable-next-line no-undef
      planChartInstance = new Chart(planCanvas, {
        type: 'doughnut',
        data: {
          labels: ['Starter', 'Growth', 'Agency'],
          datasets: [{
            data: [pb.starter || 0, pb.growth || 0, pb.agency || 0],
            backgroundColor: ['#94a3b8', '#6366f1', '#0f172a'],
            borderWidth: 0,
          }],
        },
        options: {
          plugins: { legend: { position: 'bottom', labels: { font: { size: 11 } } } },
          cutout: '65%',
        },
      });
    }

    // Site health
    const healthCard = el('div', { class: 'pa-chart-card' });
    healthCard.appendChild(el('div', { class: 'pa-chart-title' }, 'Site Health'));
    const total   = d.totalSites   || 0;
    const healthy = d.healthySites || 0;
    const notYet  = total - healthy;
    const pct     = total > 0 ? Math.round((healthy / total) * 100) : 0;
    healthCard.appendChild(el('div', { style: 'font-family:JetBrains Mono,monospace;font-size:32px;font-weight:700;color:#0f172a;line-height:1' },
      `${fmtNum(healthy)} / ${fmtNum(total)}`));
    healthCard.appendChild(el('div', { style: 'font-size:12px;color:#64748b;margin-top:4px' }, 'sites healthy'));
    const bar  = el('div', { class: 'pa-health-bar' });
    const fill = el('div', { class: 'pa-health-fill', style: `width:${pct}%` });
    bar.appendChild(fill);
    healthCard.appendChild(bar);
    healthCard.appendChild(el('div', { style: 'font-size:11px;color:#64748b' }, `${fmtNum(healthy)} sites tracking live`));
    healthCard.appendChild(el('div', { style: 'font-size:11px;color:#94a3b8;margin-top:2px' }, `${fmtNum(notYet)} sites not yet installed`));
    healthCard.appendChild(el('div', { style: 'font-size:11px;color:#6366f1;font-weight:600;margin-top:10px' },
      `+${fmtNum(d.tenantsThisMonth || 0)} new tenants this month`));
    chartRow.appendChild(healthCard);

    // Recent signups
    recentPanel.replaceChildren();
    recentPanel.appendChild(el('div', { class: 'pa-panel-hd' }, el('div', { class: 'pa-panel-title' }, '🆕 Recent Signups')));
    const signups = Array.isArray(d.recentSignups) ? d.recentSignups : [];
    if (!signups.length) {
      recentPanel.appendChild(el('div', { style: 'padding:24px;text-align:center;color:#94a3b8;font-size:13px' }, 'No signups yet.'));
    } else {
      const wrap = el('div', { style: 'overflow-x:auto' });
      const tbl  = el('table', { class: 'pa-signup-table' });
      tbl.appendChild(el('thead', {}, el('tr', {},
        el('th', {}, 'Name'), el('th', {}, 'Plan'), el('th', {}, 'Signed up'),
      )));
      const tbody = el('tbody');
      signups.forEach(s => {
        const row = el('tr', { '@click': () => { window.location.hash = `#/platform-admin/tenant/${s.tenantId}`; } });
        row.append(
          el('td', {}, el('span', { style: 'font-weight:600;color:#1e293b' }, s.name || '—')),
          el('td', {}, el('span', { class: PLAN_PILL_CLASS(s.plan) }, s.plan || 'starter')),
          el('td', { style: 'color:#64748b;font-size:12px' }, timeAgo(s.createdAt)),
        );
        tbody.appendChild(row);
      });
      tbl.appendChild(tbody);
      wrap.appendChild(tbl);
      recentPanel.appendChild(wrap);
    }
  };

  const loadDashboard = async () => {
    try {
      const d = await client.platformAdmin.getDashboard();
      renderDashboard(d);
    } catch {
      kpiGrid.replaceChildren();
      kpiGrid.appendChild(el('div', { style: 'padding:16px;color:#94a3b8;font-size:13px;grid-column:1/-1' }, 'Dashboard data unavailable.'));
    }
  };

  // ══════════════════════════════════════════════════════════════════════════
  // TAB 2: TENANTS
  // ══════════════════════════════════════════════════════════════════════════
  const tenantsPanel = tabPanels['tenants'];

  const state = {
    loading: true,
    tenants: [],
    totalCount: 0,
    page: Number(query?.page) > 0 ? Number(query.page) : 1,
    pageSize: 25,
    search: typeof query?.search === 'string' ? query.search : '',
  };

  const controls = el('div', { class: 'pa-controls' });
  const searchInput = el('input', { class: 'pa-search', type: 'search', placeholder: '🔍 Search tenants by name or domain…', value: state.search });
  const searchBtn   = el('button', { class: 'pa-btn pa-btn-primary' }, 'Search');
  const clearBtn    = el('button', { class: 'pa-btn pa-btn-outline' }, 'Clear');
  controls.append(searchInput, searchBtn, clearBtn);
  tenantsPanel.appendChild(controls);

  const tablePanel    = el('div', { class: 'pa-panel' });
  const tablePanelHd  = el('div', { class: 'pa-panel-hd' });
  const tablePanelTitle = el('div', { class: 'pa-panel-title' }, '🏢 Tenants');
  const pageInfoEl    = el('div', { style: 'font-size:12px;color:#94a3b8;font-family:JetBrains Mono,monospace' });
  tablePanelHd.append(tablePanelTitle, pageInfoEl);
  tablePanel.appendChild(tablePanelHd);

  const tableWrap   = el('div', { style: 'overflow-x:auto' });
  tablePanel.appendChild(tableWrap);

  const paginationEl = el('div', { class: 'pa-pagination' });
  const prevBtn = el('button', { class: 'pa-btn pa-btn-outline pa-btn-sm' }, '← Previous');
  const nextBtn = el('button', { class: 'pa-btn pa-btn-outline pa-btn-sm' }, 'Next →');
  paginationEl.append(prevBtn, nextBtn);
  tablePanel.appendChild(paginationEl);
  tenantsPanel.appendChild(tablePanel);

  const renderTable = () => {
    tableWrap.replaceChildren();
    if (!state.tenants.length) {
      tableWrap.appendChild(el('div', { style: 'padding:32px;text-align:center;color:#94a3b8;font-size:13px' }, 'No tenants found.'));
      return;
    }
    const table = el('table', { class: 'pa-table' });
    table.appendChild(el('thead', {}, el('tr', {},
      ...['Tenant', 'Domain', 'Plan', 'Sites', 'Visitors', 'Tickets', 'Last Activity', 'Created', ''].map(h => el('th', {}, h))
    )));
    const tbody = el('tbody', {});
    state.tenants.forEach(item => {
      const tr = el('tr', {});
      tr.append(
        el('td', {}, el('div', { class: 'pa-td-name' }, item.tenantName || '—')),
        el('td', {}, el('div', { class: 'pa-td-mono' }, item.domain || '—')),
        el('td', {}, item.plan ? el('span', { class: PLAN_BADGE_CLASS(item.plan) }, item.plan) : '—'),
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
    pageInfoEl.textContent = `Showing ${start}–${end} of ${state.totalCount}`;
    prevBtn.disabled = state.page <= 1;
    nextBtn.disabled = end >= state.totalCount;
  };

  const syncHash = () => {
    const p = new URLSearchParams();
    p.set('page', String(state.page));
    if (state.search) p.set('search', state.search);
    window.location.hash = `#/platform-admin?${p.toString()}`;
  };

  const loadTenants = async () => {
    state.loading = true;
    try {
      const r = await client.platformAdmin.listTenants({ page: state.page, pageSize: state.pageSize, search: state.search || undefined });
      state.tenants    = Array.isArray(r.items) ? r.items : [];
      state.totalCount = Number(r.totalCount) || 0;
      renderTable();
    } catch (err) {
      notifier.show({ message: mapApiError(err).message, variant: 'danger' });
    } finally {
      state.loading = false;
    }
  };

  const doSearch = async () => { state.search = searchInput.value.trim(); state.page = 1; syncHash(); await loadTenants(); };
  searchBtn.addEventListener('click', doSearch);
  searchInput.addEventListener('keydown', e => { if (e.key === 'Enter') doSearch(); });
  clearBtn.addEventListener('click', async () => { searchInput.value = ''; state.search = ''; state.page = 1; syncHash(); await loadTenants(); });
  prevBtn.addEventListener('click', async () => { if (state.page <= 1) return; state.page--; syncHash(); await loadTenants(); });
  nextBtn.addEventListener('click', async () => {
    const max = Math.max(1, Math.ceil(state.totalCount / state.pageSize));
    if (state.page >= max) return;
    state.page++;
    syncHash();
    await loadTenants();
  });

  // ══════════════════════════════════════════════════════════════════════════
  // TAB 3: FEEDBACK
  // ══════════════════════════════════════════════════════════════════════════
  const feedbackPanel = tabPanels['feedback'];
  const fbPanel    = el('div', { class: 'pa-panel' });
  feedbackPanel.appendChild(fbPanel);
  const fbPanelHd = el('div', { class: 'pa-panel-hd' });
  fbPanelHd.appendChild(el('div', { class: 'pa-panel-title' }, '💡 User Feedback'));
  fbPanel.appendChild(fbPanelHd);

  const fbTableWrap = el('div', { style: 'overflow-x:auto' });
  fbPanel.appendChild(fbTableWrap);

  const STATUS_OPTIONS = ['pending', 'reviewing', 'planned', 'done', 'rejected'];

  const renderFeedbackTable = (items) => {
    fbTableWrap.replaceChildren();
    if (!items.length) {
      fbTableWrap.appendChild(el('div', { style: 'padding:32px;text-align:center;color:#94a3b8;font-size:13px' }, 'No feedback submitted yet.'));
      return;
    }
    const table = el('table', { class: 'pa-table' });
    table.appendChild(el('thead', {}, el('tr', {},
      el('th', {}, 'Type'), el('th', {}, 'Title'), el('th', {}, 'Description'),
      el('th', {}, 'Priority'), el('th', {}, 'Submitted'), el('th', {}, 'Status'),
    )));
    const tbody = el('tbody', {});
    items.forEach(item => {
      const statusSel = el('select', { style: 'font-size:11px;padding:3px 6px;border-radius:5px;border:1px solid #e2e8f0;font-family:inherit' });
      STATUS_OPTIONS.forEach(s => {
        const opt = el('option', { value: s }, s);
        if (s === item.status) opt.selected = true;
        statusSel.appendChild(opt);
      });
      statusSel.addEventListener('change', async () => {
        try {
          await client.feedback.updateStatus(item.id, statusSel.value);
          notifier.show({ message: 'Status updated.', variant: 'success' });
        } catch (err) {
          notifier.show({ message: mapApiError(err).message, variant: 'danger' });
        }
      });
      const row = el('tr', {},
        el('td', {}, item.type || ''),
        el('td', {}, item.title || ''),
        el('td', { style: 'max-width:240px;white-space:pre-wrap;word-break:break-word' }, item.description || ''),
        el('td', {}, item.priority || '—'),
        el('td', { style: 'white-space:nowrap' }, item.submittedAt ? new Date(item.submittedAt).toLocaleDateString() : ''),
        el('td', {}, statusSel),
      );
      tbody.appendChild(row);
    });
    table.appendChild(tbody);
    fbTableWrap.appendChild(table);
  };

  const loadFeedback = async () => {
    try {
      const items = await client.feedback.listAdmin();
      renderFeedbackTable(items);
    } catch {
      fbTableWrap.appendChild(el('div', { style: 'padding:20px;color:#94a3b8;font-size:13px' }, 'Could not load feedback.'));
    }
  };

  // ── Load default tab ───────────────────────────────────────────────────────
  loaded['dashboard'] = true;
  loadDashboard();
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

    const heroEl = el('div', { class: 'pa-hero' });
    heroEl.appendChild(el('div', { class: 'pa-hero-title' }, d.tenantName || 'Tenant Detail'));
    heroEl.appendChild(el('div', { class: 'pa-hero-sub' }, d.domain || ''));
    const hStats = el('div', { class: 'pa-hero-stats' });
    const mkHS = (lbl, val) => {
      const w = el('div', { class: 'pa-stat' });
      w.append(el('div', { class: 'pa-stat-val' }, fmtNum(val)), el('div', { class: 'pa-stat-lbl' }, lbl));
      hStats.appendChild(w);
    };
    mkHS('Sites', d.usage?.siteCount); mkHS('Visitors', d.usage?.visitorsCount);
    mkHS('Engage Sessions', d.usage?.engageSessionsCount); mkHS('Tickets', d.usage?.ticketsCount);
    heroEl.appendChild(hStats);
    content.appendChild(heroEl);

    content.appendChild(mkDetailSection('Tenant Metadata', [
      ['Tenant Name', d.tenantName], ['Domain', d.domain], ['Plan', d.plan],
      ['Industry', d.industry], ['Category', d.category],
      ['Created', fmtDate(d.createdAt)], ['Updated', fmtDate(d.updatedAt)],
      ['Last Activity', fmtDate(d.usage?.lastActivityAtUtc)],
    ]));

    const usagePanel = el('div', { class: 'pa-panel' });
    usagePanel.appendChild(el('div', { class: 'pa-panel-hd' }, el('div', { class: 'pa-panel-title' }, '📊 Usage Summary')));
    const u = d.usage || {};
    usagePanel.appendChild(el('div', { style: 'padding:14px' }, mkMetricGrid([
      ['Sites', u.siteCount], ['Visitors', u.visitorsCount], ['Engage Sessions', u.engageSessionsCount],
      ['Engage Messages', u.engageMessagesCount], ['Tickets', u.ticketsCount], ['Promos', u.promosCount],
      ['Promo Entries', u.promoEntriesCount], ['Intelligence Records', u.intelligenceRecordCount],
      ['Ads Campaigns', u.adsCampaignCount], ['Knowledge Sources', u.knowledgeSourcesCount],
      ['Knowledge Indexed', u.knowledgeIndexedCount], ['Knowledge Failed', u.knowledgeFailedCount],
    ])));
    content.appendChild(usagePanel);

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

    const act = d.recentActivity || {};
    content.appendChild(mkDetailSection('⏱ Recent Activity', [
      ['Sites', fmtDate(act.lastSiteActivityAtUtc)], ['Visitors', fmtDate(act.lastVisitorActivityAtUtc)],
      ['Engage Sessions', fmtDate(act.lastEngageSessionActivityAtUtc)], ['Tickets', fmtDate(act.lastTicketActivityAtUtc)],
      ['Promos', fmtDate(act.lastPromoActivityAtUtc)], ['Promo Entries', fmtDate(act.lastPromoEntryActivityAtUtc)],
      ['Intelligence', fmtDate(act.lastIntelligenceActivityAtUtc)], ['Ads', fmtDate(act.lastAdsActivityAtUtc)],
      ['Knowledge', fmtDate(act.lastKnowledgeActivityAtUtc)],
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
