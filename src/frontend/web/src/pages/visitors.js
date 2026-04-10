/**
 * visitors.js — Intentify Visitors Page
 * Phase 2: Live visitors, session timeline, page analytics, country breakdown
 */

import { createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

// ─── Utilities ────────────────────────────────────────────────────────────────

const el = (tag, attrs = {}, ...kids) => {
  const e = document.createElement(tag);
  Object.entries(attrs).forEach(([k, v]) => {
    if (k === 'class')       e.className = v;
    else if (k === 'style')  typeof v === 'string' ? (e.style.cssText = v) : Object.assign(e.style, v);
    else if (k.startsWith('@')) e.addEventListener(k.slice(1), v);
    else e.setAttribute(k, v);
  });
  kids.flat(Infinity).forEach(c => c != null && e.append(typeof c === 'string' ? document.createTextNode(c) : c));
  return e;
};

const fmtDate = v => { if (!v) return '—'; const d = new Date(v); return isNaN(d) ? '—' : d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' }); };
const fmtTime = v => { if (!v) return '—'; const d = new Date(v); return isNaN(d) ? '—' : d.toLocaleString('en-GB'); };
const fmtTimeAgo = v => {
  if (!v) return '—';
  const m = Math.floor((Date.now() - new Date(v)) / 60000);
  if (m < 1)  return 'just now';
  if (m < 60) return `${m}m ago`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h}h ago`;
  return `${Math.floor(h / 24)}d ago`;
};
const fmtDuration = secs => {
  if (!secs || secs < 1) return '—';
  if (secs < 60) return `${secs}s`;
  const m = Math.floor(secs / 60), s = secs % 60;
  return s > 0 ? `${m}m ${s}s` : `${m}m`;
};
const shortId = v => (!v || v.length < 8) ? (v || '—') : `${v.slice(0, 8)}…`;
const normPath = url => {
  if (!url) return '—';
  try { const u = new URL(url); return u.pathname + u.search || '/'; } catch { return url; }
};
const getSiteId = s => s?.siteId || s?.id || '';
const SITE_KEY = 'intentify.selectedSiteId';
const loadSiteId = () => { try { return localStorage.getItem(SITE_KEY) || ''; } catch { return ''; } };
const saveSiteId = id => { try { id ? localStorage.setItem(SITE_KEY, id) : localStorage.removeItem(SITE_KEY); } catch {} };

const PLATFORM_ICON = { mobile: '📱', desktop: '🖥️', tablet: '📋' };
const FLAG_MAP = { 'GB': '🇬🇧', 'IE': '🇮🇪', 'US': '🇺🇸', 'DE': '🇩🇪', 'FR': '🇫🇷', 'AU': '🇦🇺', 'CA': '🇨🇦', 'NL': '🇳🇱', 'SE': '🇸🇪', 'NO': '🇳🇴', 'DK': '🇩🇰', 'IT': '🇮🇹', 'ES': '🇪🇸', 'PL': '🇵🇱', 'IN': '🇮🇳', 'SG': '🇸🇬', 'BR': '🇧🇷', 'Unknown': '🌍' };
const flag = country => FLAG_MAP[country?.toUpperCase()] || '🌍';

const PAGE_SIZE = 20;
const LIVE_POLL_MS = 15000;

// ─── Styles ───────────────────────────────────────────────────────────────────

const injectStyles = () => {
  if (document.getElementById('_vis_css')) return;
  const s = document.createElement('style');
  s.id = '_vis_css';
  s.textContent = `
@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap');
.v-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;gap:20px;width:100%;max-width:1280px;padding-bottom:60px}

/* Header */
.v-hero{background:linear-gradient(135deg,#0f172a 0%,#1e293b 100%);border-radius:16px;padding:28px 36px;position:relative;overflow:hidden}
.v-hero::before{content:'';position:absolute;top:-30px;right:-30px;width:180px;height:180px;background:radial-gradient(circle,rgba(99,102,241,.18) 0%,transparent 70%);pointer-events:none}
.v-hero-title{font-size:24px;font-weight:700;color:#f8fafc;letter-spacing:-.02em;margin-bottom:6px}
.v-hero-sub{font-size:13px;color:#94a3b8;margin-bottom:18px}
.v-hero-stats{display:flex;gap:28px;flex-wrap:wrap}
.v-stat{display:flex;flex-direction:column;gap:2px}
.v-stat-val{font-family:'JetBrains Mono',monospace;font-size:22px;font-weight:700;color:#f1f5f9;letter-spacing:-.02em}
.v-stat-lbl{font-size:10px;color:#64748b;text-transform:uppercase;letter-spacing:.07em}

/* Controls */
.v-controls{display:flex;align-items:center;gap:10px;flex-wrap:wrap}
.v-select{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;color:#1e293b;background:#fff;border:1px solid #e2e8f0;border-radius:8px;padding:7px 11px;outline:none;min-width:200px}
.v-select:focus{border-color:#6366f1;box-shadow:0 0 0 3px rgba(99,102,241,.1)}
.v-btn{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;font-weight:600;padding:7px 16px;border-radius:8px;border:none;cursor:pointer;transition:all .14s;display:inline-flex;align-items:center;gap:5px;white-space:nowrap}
.v-btn-primary{background:#6366f1;color:#fff}
.v-btn-primary:hover:not(:disabled){background:#4f46e5;transform:translateY(-1px);box-shadow:0 4px 12px rgba(99,102,241,.25)}
.v-btn-primary:disabled{opacity:.5;cursor:not-allowed}
.v-btn-outline{background:#fff;color:#64748b;border:1px solid #e2e8f0}
.v-btn-outline:hover{background:#f8fafc;color:#1e293b}
.v-btn-sm{padding:5px 12px;font-size:12px}

/* Panel */
.v-panel{background:#fff;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden}
.v-panel-hd{display:flex;align-items:center;justify-content:space-between;padding:14px 20px;border-bottom:1px solid #f1f5f9}
.v-panel-title{font-size:13px;font-weight:700;color:#0f172a;display:flex;align-items:center;gap:7px}
.v-panel-body{padding:18px 20px}

/* Live Now */
.v-live-header{display:flex;align-items:center;gap:8px;margin-bottom:14px}
.v-live-dot{width:9px;height:9px;background:#10b981;border-radius:50%;box-shadow:0 0 0 0 rgba(16,185,129,.4);animation:_lp 2s infinite;flex-shrink:0}
@keyframes _lp{0%{box-shadow:0 0 0 0 rgba(16,185,129,.4)}70%{box-shadow:0 0 0 7px rgba(16,185,129,0)}100%{box-shadow:0 0 0 0 rgba(16,185,129,0)}}
.v-live-count{font-family:'JetBrains Mono',monospace;font-size:22px;font-weight:700;color:#0f172a}
.v-live-label{font-size:12px;color:#94a3b8}
.v-live-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:10px}
.v-live-card{background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:12px 14px;transition:box-shadow .15s,transform .15s;cursor:pointer}
.v-live-card:hover{box-shadow:0 4px 14px rgba(0,0,0,.07);transform:translateY(-1px)}
.v-live-card-top{display:flex;align-items:flex-start;justify-content:space-between;gap:8px;margin-bottom:6px}
.v-live-path{font-size:12px;font-weight:600;color:#1e293b;font-family:'JetBrains Mono',monospace;word-break:break-all;line-height:1.3}
.v-live-badge{display:inline-flex;align-items:center;gap:3px;background:#d1fae5;color:#065f46;font-size:9px;font-weight:700;padding:2px 6px;border-radius:999px;flex-shrink:0;white-space:nowrap}
.v-live-meta{display:flex;align-items:center;gap:8px;flex-wrap:wrap}
.v-live-chip{font-size:10px;color:#64748b;display:flex;align-items:center;gap:3px}

/* Tabs */
.v-tabs{display:flex;gap:3px;background:#f1f5f9;border-radius:10px;padding:3px}
.v-tab{flex:1;padding:7px 12px;border-radius:8px;border:none;font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:12px;font-weight:500;color:#64748b;cursor:pointer;transition:all .14s;white-space:nowrap}
.v-tab:hover{background:rgba(255,255,255,.7)}
.v-tab.active{background:#fff;color:#6366f1;font-weight:700;box-shadow:0 1px 4px rgba(0,0,0,.08)}

/* Table */
.v-table-wrap{border-radius:10px;overflow:hidden;border:1px solid #e2e8f0}
.v-table{width:100%;border-collapse:collapse;font-size:12.5px}
.v-table thead th{background:#f8fafc;padding:8px 14px;text-align:left;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.07em;color:#94a3b8;border-bottom:1px solid #e2e8f0;white-space:nowrap}
.v-table tbody td{padding:11px 14px;border-bottom:1px solid #f1f5f9;color:#334155;vertical-align:middle}
.v-table tbody tr:last-child td{border-bottom:none}
.v-table tbody tr:hover{background:#fafbff;cursor:pointer}
.v-visitor-name{font-weight:600;color:#1e293b;display:flex;align-items:center;gap:6px}
.v-visitor-sub{font-size:10.5px;color:#94a3b8;margin-top:1px;font-family:'JetBrains Mono',monospace}

/* Engagement bar */
.v-eng{display:flex;align-items:center;gap:6px}
.v-eng-track{width:60px;height:4px;background:#e2e8f0;border-radius:999px;overflow:hidden}
.v-eng-fill{height:100%;border-radius:999px;background:#6366f1;transition:width .4s}

/* Pill / badge */
.v-pill{display:inline-flex;align-items:center;gap:3px;padding:2px 7px;border-radius:999px;font-size:10px;font-weight:700}
.v-pill-green{background:#d1fae5;color:#065f46}
.v-pill-blue{background:#dbeafe;color:#1e40af}
.v-pill-gray{background:#f1f5f9;color:#475569}
.v-pill-amber{background:#fef3c7;color:#92400e}

/* Timeline */
.v-timeline{display:flex;flex-direction:column;gap:0}
.v-tl-item{display:flex;gap:14px;padding:10px 0;position:relative}
.v-tl-item::before{content:'';position:absolute;left:18px;top:28px;bottom:-10px;width:1px;background:#e2e8f0}
.v-tl-item:last-child::before{display:none}
.v-tl-dot{width:37px;height:37px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:15px;flex-shrink:0;background:#f1f5f9;border:2px solid #e2e8f0;z-index:1}
.v-tl-dot-pv{background:#eef2ff;border-color:#c7d2fe}
.v-tl-dot-en{background:#d1fae5;border-color:#6ee7b7}
.v-tl-dot-lc{background:#fef3c7;border-color:#fcd34d}
.v-tl-dot-ot{background:#f1f5f9;border-color:#e2e8f0}
.v-tl-body{flex:1;min-width:0;padding-top:4px}
.v-tl-title{font-size:12.5px;font-weight:600;color:#1e293b}
.v-tl-sub{font-size:11px;color:#94a3b8;font-family:'JetBrains Mono',monospace;word-break:break-all;margin-top:2px}
.v-tl-time{font-size:10px;color:#94a3b8;margin-top:3px}

/* Page analytics */
.v-pages{display:flex;flex-direction:column;gap:8px}
.v-page-item{display:flex;align-items:center;gap:10px}
.v-page-label{font-size:12px;font-family:'JetBrains Mono',monospace;color:#1e293b;flex:1;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.v-page-bar-wrap{width:120px;height:5px;background:#e2e8f0;border-radius:999px;overflow:hidden;flex-shrink:0}
.v-page-bar{height:100%;background:#6366f1;border-radius:999px;transition:width .5s cubic-bezier(.34,1.56,.64,1)}
.v-page-count{font-size:11px;color:#64748b;min-width:40px;text-align:right;font-family:'JetBrains Mono',monospace}

/* Country */
.v-countries{display:flex;flex-direction:column;gap:9px}
.v-country-item{display:flex;align-items:center;gap:10px}
.v-country-flag{font-size:18px;width:28px;text-align:center;flex-shrink:0}
.v-country-name{font-size:12.5px;color:#1e293b;flex:1;font-weight:500}
.v-country-bar-wrap{width:100px;height:5px;background:#e2e8f0;border-radius:999px;overflow:hidden}
.v-country-bar{height:100%;background:#10b981;border-radius:999px;transition:width .5s cubic-bezier(.34,1.56,.64,1)}
.v-country-pct{font-size:11px;color:#94a3b8;min-width:36px;text-align:right;font-family:'JetBrains Mono',monospace}

/* Empty / loading */
.v-empty{text-align:center;padding:44px 20px;display:flex;flex-direction:column;align-items:center;gap:8px}
.v-empty-icon{font-size:38px;opacity:.3}
.v-empty-title{font-size:14px;font-weight:600;color:#334155}
.v-empty-desc{font-size:12px;color:#94a3b8;max-width:280px;line-height:1.6}
.v-skel{background:linear-gradient(90deg,#f1f5f9 25%,#e2e8f0 50%,#f1f5f9 75%);background-size:200% 100%;animation:_sh 1.4s infinite;border-radius:5px}
@keyframes _sh{0%{background-position:200% 0}100%{background-position:-200% 0}}

/* Pagination */
.v-pagination{display:flex;align-items:center;justify-content:space-between;padding:12px 20px;border-top:1px solid #f1f5f9}
.v-page-info{font-size:12px;color:#94a3b8}
.v-page-btns{display:flex;gap:6px}

/* Metric grid */
.v-metrics{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:12px}
.v-metric{background:#fff;border:1px solid #e2e8f0;border-radius:12px;padding:14px 16px;position:relative;overflow:hidden}
.v-metric::before{content:'';position:absolute;top:0;left:0;right:0;height:3px;background:var(--a,#6366f1);border-radius:12px 12px 0 0}
.v-metric-icon{width:32px;height:32px;border-radius:8px;background:var(--al,#eef2ff);display:flex;align-items:center;justify-content:center;font-size:15px;margin-bottom:10px}
.v-metric-val{font-family:'JetBrains Mono',monospace;font-size:22px;font-weight:700;color:#0f172a;line-height:1}
.v-metric-lbl{font-size:10px;color:#94a3b8;text-transform:uppercase;letter-spacing:.06em;font-weight:700;margin-top:4px}
  `;
  document.head.appendChild(s);
};

// ─── Main export ──────────────────────────────────────────────────────────────

export const renderVisitorsView = async (container, { apiClient, toast, query } = {}) => {
  injectStyles();
  const client   = apiClient || createApiClient();
  const notifier = toast     || createToastManager();

  const state = {
    sites: [], siteId: query?.siteId || loadSiteId(),
    visitors: [], counts: null, onlineNow: [], pageAnalytics: [],
    countries: [], currentPage: 1, loading: false, liveTimer: null,
    activeTab: 'visitors',
  };

  // ── Root ───────────────────────────────────────────────────────────────────
  const root = el('div', { class: 'v-root' });
  container.appendChild(root);

  // ── Hero ───────────────────────────────────────────────────────────────────
  const hero = el('div', { class: 'v-hero' });
  hero.appendChild(el('div', { class: 'v-hero-title' }, 'Visitor Intelligence'));
  hero.appendChild(el('div', { class: 'v-hero-sub' }, 'See who is on your site right now, what they did, and who they are.'));
  const heroStats = el('div', { class: 'v-hero-stats' });
  const mkStat = (lbl) => { const w = el('div',{class:'v-stat'}); const v = el('div',{class:'v-stat-val'},'—'); w.append(v,el('div',{class:'v-stat-lbl'},lbl)); heroStats.appendChild(w); return v; };
  const hLive  = mkStat('Live Now');
  const h7     = mkStat('This Week');
  const h30    = mkStat('This Month');
  const hTotal = mkStat('Total Visitors');
  hero.appendChild(heroStats);
  root.appendChild(hero);

  // ── Controls ───────────────────────────────────────────────────────────────
  const controls = el('div', { class: 'v-controls' });
  const siteSelect = el('select', { class: 'v-select' }, el('option', { value: '' }, 'Loading sites…'));
  const refreshBtn = el('button', { class: 'v-btn v-btn-outline' }, '↻ Refresh');
  controls.append(siteSelect, refreshBtn);
  root.appendChild(controls);

  // ── Live Now panel ─────────────────────────────────────────────────────────
  const { panel: livePanel, body: liveBody } = mkPanel('🟢 Live Now', '');
  root.appendChild(livePanel);

  // ── Metrics ────────────────────────────────────────────────────────────────
  const metricsRow = el('div', { class: 'v-metrics' });
  const mkM = ({icon,lbl,a,al}) => { const c=el('div',{class:'v-metric',style:`--a:${a};--al:${al}`}); const ico=el('div',{class:'v-metric-icon'},icon); const vEl=el('div',{class:'v-metric-val'},'—'); c.append(ico,vEl,el('div',{class:'v-metric-lbl'},lbl)); metricsRow.appendChild(c); return vEl; };
  const mPage7  = mkM({icon:'📈',lbl:'Page Views (7d)',   a:'#6366f1',al:'#eef2ff'});
  const mSess7  = mkM({icon:'👥',lbl:'Sessions (7d)',     a:'#10b981',al:'#d1fae5'});
  const mSess30 = mkM({icon:'📅',lbl:'Sessions (30d)',    a:'#f59e0b',al:'#fef3c7'});
  const mCountry= mkM({icon:'🌍',lbl:'Top Country',       a:'#3b82f6',al:'#dbeafe'});
  root.appendChild(metricsRow);

  // ── Main panel with tabs ───────────────────────────────────────────────────
  const { panel: mainPanel, body: mainBody } = mkPanel('', '');
  // override header with tabs
  mainPanel.querySelector('.v-panel-hd').style.display = 'none';

  const tabBar = el('div', { class: 'v-tabs', style: 'margin:14px 20px 0' });
  const TABS = [
    { key: 'visitors',  label: '👥 Visitors'    },
    { key: 'pages',     label: '📄 Top Pages'   },
    { key: 'countries', label: '🌍 By Country'  },
  ];
  const tabEls = {};
  TABS.forEach(({ key, label }) => {
    const btn = el('button', { class: 'v-tab' + (key === state.activeTab ? ' active' : '') }, label);
    btn.addEventListener('click', () => switchTab(key));
    tabEls[key] = btn; tabBar.appendChild(btn);
  });
  mainPanel.insertBefore(tabBar, mainBody);

  // Visitors tab
  const visitorsBody = el('div', {});
  const tableWrap = el('div', { class: 'v-table-wrap', style: 'margin:14px 20px' });
  const paginationEl = el('div', { class: 'v-pagination' });
  visitorsBody.append(tableWrap, paginationEl);

  // Pages tab
  const pagesBody = el('div', { style: 'display:none;padding:18px 20px' });

  // Countries tab
  const countriesBody = el('div', { style: 'display:none;padding:18px 20px' });

  mainBody.append(visitorsBody, pagesBody, countriesBody);
  mainBody.style.padding = '0';
  root.appendChild(mainPanel);

  const switchTab = key => {
    state.activeTab = key;
    Object.entries(tabEls).forEach(([k,b]) => b.classList.toggle('active', k === key));
    visitorsBody.style.display  = key === 'visitors'  ? '' : 'none';
    pagesBody.style.display     = key === 'pages'     ? 'block' : 'none';
    countriesBody.style.display = key === 'countries' ? 'block' : 'none';
  };

  // ── Panel helper ───────────────────────────────────────────────────────────
  function mkPanel(title, subtitle) {
    const panel = el('div', { class: 'v-panel' });
    const hd = el('div', { class: 'v-panel-hd' });
    const left = el('div', {});
    left.appendChild(el('div', { class: 'v-panel-title' }, title));
    if (subtitle) left.appendChild(el('div', { style: 'font-size:11px;color:#94a3b8;margin-top:1px' }, subtitle));
    hd.appendChild(left);
    panel.appendChild(hd);
    const body = el('div', { class: 'v-panel-body' });
    panel.appendChild(body);
    return { panel, body, hd };
  }

  // ── Render live now ────────────────────────────────────────────────────────
  const renderLiveNow = (visitors) => {
    liveBody.replaceChildren();
    const header = el('div', { class: 'v-live-header' });
    header.append(
      el('div', { class: 'v-live-dot' }),
      el('span', { class: 'v-live-count' }, String(visitors.length)),
      el('span', { class: 'v-live-label' }, `visitor${visitors.length !== 1 ? 's' : ''} online right now`)
    );
    liveBody.appendChild(header);
    hLive.textContent = String(visitors.length);

    if (!visitors.length) {
      liveBody.appendChild(el('div', { class: 'v-empty' },
        el('div', { class: 'v-empty-icon' }, '👁️'),
        el('div', { class: 'v-empty-title' }, 'No one online right now'),
        el('div', { class: 'v-empty-desc' }, 'Visitors will appear here as soon as someone lands on your site.')));
      return;
    }

    const grid = el('div', { class: 'v-live-grid' });
    visitors.forEach(v => {
      const card = el('div', { class: 'v-live-card', '@click': () => openProfile(v.visitorId) });
      const top  = el('div', { class: 'v-live-card-top' });
      top.appendChild(el('div', { class: 'v-live-path' }, normPath(v.lastPath)));
      top.appendChild(el('div', { class: 'v-live-badge' }, '● LIVE'));
      card.appendChild(top);

      const meta = el('div', { class: 'v-live-meta' });
      if (v.country) meta.appendChild(el('span', { class: 'v-live-chip' }, flag(v.country), ' ', v.country));
      if (v.platform) meta.appendChild(el('span', { class: 'v-live-chip' }, PLATFORM_ICON[v.platform] || '💻', ' ', v.platform));
      if (v.primaryEmail) meta.appendChild(el('span', { class: 'v-live-chip' }, '✉️', ' ', v.primaryEmail));
      else meta.appendChild(el('span', { class: 'v-live-chip' }, '🕐', ' ', fmtTimeAgo(v.lastSeenAtUtc)));
      card.appendChild(meta);
      grid.appendChild(card);
    });
    liveBody.appendChild(grid);
  };

  // ── Render visitors table ──────────────────────────────────────────────────
  const renderVisitorsTable = (visitors) => {
    tableWrap.replaceChildren();
    if (!visitors.length) {
      tableWrap.appendChild(el('div', { class: 'v-empty' },
        el('div', { class: 'v-empty-icon' }, '👥'),
        el('div', { class: 'v-empty-title' }, 'No visitors yet'),
        el('div', { class: 'v-empty-desc' }, 'Install the tracker snippet on your site to start collecting visitor data.')));
      return;
    }

    const table = el('table', { class: 'v-table' });
    const thead = el('thead', {}, el('tr', {},
      ...['Visitor', 'Last Seen', 'Sessions', 'Pages', 'Last Page', 'Engagement', 'Intent', 'Location'].map(c => el('th', {}, c))
    ));
    table.appendChild(thead);

    const tbody = el('tbody', {});
    visitors.forEach(v => {
      const tr = el('tr', { '@click': () => openProfile(v.visitorId.toString?.() || v.visitorId) });

      // Visitor cell
      const visCell = el('td', {});
      const name = v.primaryEmail || (v.platform ? `${PLATFORM_ICON[v.platform] || '💻'} ${v.platform} visitor` : 'Anonymous visitor');
      const nameRow = el('div', { style: 'display:flex;align-items:center;gap:6px;flex-wrap:wrap' },
        el('div', { class: 'v-visitor-name' }, name),
        ...(v.sessionsCount > 1 ? [el('span', { style: 'background:#f1f5f9;color:#64748b;font-size:10px;padding:2px 6px;border-radius:999px;font-weight:600' }, '↩ Return')] : [])
      );
      visCell.appendChild(nameRow);
      visCell.appendChild(el('div', { class: 'v-visitor-sub' }, shortId(v.visitorId?.toString?.() || v.visitorId)));
      if (v.companyName) {
        visCell.appendChild(el('div', { style: 'font-size:11px;color:#64748b;margin-top:2px' }, '🏢 ', v.companyName));
      }
      tr.appendChild(visCell);

      tr.appendChild(el('td', {}, fmtTimeAgo(v.lastSeenAtUtc)));
      tr.appendChild(el('td', {}, String(v.sessionsCount || 0)));
      tr.appendChild(el('td', {}, String(v.totalPagesVisited || 0)));
      tr.appendChild(el('td', { style: 'max-width:200px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;font-size:11px;font-family:JetBrains Mono,monospace;color:#475569' }, normPath(v.lastPath)));

      // Engagement bar
      const engCell = el('td', {});
      const score = Math.min(100, v.lastSessionEngagementScore || 0);
      const engWrap = el('div', { class: 'v-eng' });
      const track = el('div', { class: 'v-eng-track' });
      const fill  = el('div', { class: 'v-eng-fill', style: 'width:0%' });
      track.appendChild(fill);
      engWrap.append(track, el('span', { style: 'font-size:10px;color:#94a3b8;font-family:JetBrains Mono,monospace' }, String(score)));
      setTimeout(() => { fill.style.width = `${score}%`; }, 80);
      engCell.appendChild(engWrap);
      tr.appendChild(engCell);

      // Intent score badge
      const intentScore = v.intentScore || 0;
      const intentCell = el('td', {});
      if (intentScore >= 80) {
        intentCell.appendChild(el('span', { class: 'v-pill v-pill-green' }, '🔥 Hot'));
      } else if (intentScore >= 60) {
        intentCell.appendChild(el('span', { class: 'v-pill v-pill-blue' }, '♨ Warm'));
      } else if (intentScore >= 40) {
        intentCell.appendChild(el('span', { class: 'v-pill v-pill-amber' }, '~ Cool'));
      } else if (intentScore > 0) {
        intentCell.appendChild(el('span', { class: 'v-pill v-pill-gray' }, String(intentScore)));
      } else {
        intentCell.appendChild(el('span', { style: 'color:#cbd5e1;font-size:11px' }, '—'));
      }
      tr.appendChild(intentCell);

      // Location
      const country = v.country || '';
      tr.appendChild(el('td', {}, country ? `${flag(country)} ${country}` : '—'));
      tbody.appendChild(tr);
    });

    table.appendChild(tbody);
    tableWrap.appendChild(table);
  };

  // ── Render pages ───────────────────────────────────────────────────────────
  const renderPages = (pages) => {
    pagesBody.replaceChildren();
    if (!pages.length) {
      pagesBody.appendChild(el('div', { class: 'v-empty' }, el('div',{class:'v-empty-icon'},'📄'), el('div',{class:'v-empty-title'},'No page data yet')));
      return;
    }
    const max = pages[0]?.pageViews || 1;
    pagesBody.appendChild(el('div', { class: 'v-pages' },
      ...pages.map(p => {
        const row = el('div', { class: 'v-page-item' });
        row.appendChild(el('div', { class: 'v-page-label', title: p.pageUrl }, p.pageUrl));
        const barWrap = el('div', { class: 'v-page-bar-wrap' });
        const bar = el('div', { class: 'v-page-bar', style: 'width:0%' });
        barWrap.appendChild(bar);
        setTimeout(() => { bar.style.width = `${Math.round(p.pageViews / max * 100)}%`; }, 80);
        row.appendChild(barWrap);
        row.appendChild(el('div', { class: 'v-page-count' }, String(p.pageViews)));
        return row;
      })
    ));
  };

  // ── Render countries ───────────────────────────────────────────────────────
  const renderCountries = (countries) => {
    countriesBody.replaceChildren();
    if (!countries.length) {
      countriesBody.appendChild(el('div', { class: 'v-empty' }, el('div',{class:'v-empty-icon'},'🌍'), el('div',{class:'v-empty-title'},'No geographic data yet')));
      return;
    }
    const top = countries[0];
    if (top) mCountry.textContent = `${flag(top.country)} ${top.country}`;

    countriesBody.appendChild(el('div', { class: 'v-countries' },
      ...countries.map(c => {
        const row = el('div', { class: 'v-country-item' });
        row.appendChild(el('div', { class: 'v-country-flag' }, flag(c.country)));
        row.appendChild(el('div', { class: 'v-country-name' }, c.country));
        const barWrap = el('div', { class: 'v-country-bar-wrap' });
        const bar = el('div', { class: 'v-country-bar', style: 'width:0%' });
        barWrap.appendChild(bar);
        setTimeout(() => { bar.style.width = `${c.percentage}%`; }, 80);
        row.appendChild(barWrap);
        row.appendChild(el('div', { class: 'v-country-pct' }, `${c.percentage}%`));
        row.appendChild(el('div', { style: 'font-size:10.5px;color:#94a3b8;min-width:30px;text-align:right;font-family:JetBrains Mono,monospace' }, String(c.visitorCount)));
        return row;
      })
    ));
  };

  // ── Pagination ─────────────────────────────────────────────────────────────
  const renderPagination = (visitors) => {
    paginationEl.replaceChildren();
    const start = (state.currentPage - 1) * PAGE_SIZE + 1;
    const end   = Math.min(state.currentPage * PAGE_SIZE, start + visitors.length - 1);
    paginationEl.appendChild(el('span', { class: 'v-page-info' }, `Showing ${visitors.length ? start : 0}–${end}`));
    const btns = el('div', { class: 'v-page-btns' });
    const prev = el('button', { class: 'v-btn v-btn-outline v-btn-sm' }, '← Prev');
    const next = el('button', { class: 'v-btn v-btn-outline v-btn-sm' }, 'Next →');
    prev.disabled = state.currentPage <= 1;
    next.disabled = visitors.length < PAGE_SIZE;
    prev.addEventListener('click', () => { state.currentPage--; loadVisitors(); });
    next.addEventListener('click', () => { state.currentPage++; loadVisitors(); });
    btns.append(prev, next);
    paginationEl.appendChild(btns);
  };

  // ── Open profile ───────────────────────────────────────────────────────────
  const openProfile = (visitorId) => {
    if (!visitorId) return;
    window.location.hash = `#/visitors/${visitorId}?siteId=${state.siteId}`;
  };

  // ── API calls ──────────────────────────────────────────────────────────────
  const loadLiveNow = async () => {
    if (!state.siteId) return;
    try {
      const res = await client.visitors.onlineNow(state.siteId, 5, 50);
      state.onlineNow = res?.visitors || [];
      renderLiveNow(state.onlineNow);
    } catch {}
  };

  const loadVisitors = async () => {
    if (!state.siteId) { tableWrap.innerHTML = ''; return; }
    tableWrap.innerHTML = '<div class="v-empty"><div class="v-skel" style="width:100%;height:14px;margin-bottom:8px"></div><div class="v-skel" style="width:80%;height:14px"></div></div>';
    try {
      const res = await client.visitors.list(state.siteId, state.currentPage, PAGE_SIZE);
      state.visitors = Array.isArray(res) ? res : (res?.items || []);
      renderVisitorsTable(state.visitors);
      renderPagination(state.visitors);
    } catch (err) {
      tableWrap.innerHTML = '';
      notifier.show({ message: mapApiError(err).message, variant: 'danger' });
    }
  };

  const loadCounts = async () => {
    if (!state.siteId) return;
    try {
      const c = await client.visitors.visitCounts(state.siteId);
      state.counts = c;
      h7.textContent  = String(c.last7  || 0);
      h30.textContent = String(c.last30 || 0);
      mSess7.textContent  = String(c.last7  || 0);
      mSess30.textContent = String(c.last30 || 0);
    } catch {}
  };

  const loadPageAnalytics = async () => {
    if (!state.siteId) return;
    try {
      const res = await client.visitors.pageAnalytics(state.siteId, 7, 15);
      state.pageAnalytics = res?.pages || [];
      const total = state.pageAnalytics.reduce((s, p) => s + (p.pageViews || 0), 0);
      mPage7.textContent = String(total);
      renderPages(state.pageAnalytics);
    } catch {}
  };

  const loadCountries = async () => {
    if (!state.siteId) return;
    try {
      const res = await client.visitors.countryBreakdown(state.siteId, 7, 20);
      state.countries = res?.countries || [];
      renderCountries(state.countries);
    } catch {}
  };

  const loadAll = async () => {
    if (!state.siteId) return;
    await Promise.all([loadLiveNow(), loadVisitors(), loadCounts(), loadPageAnalytics(), loadCountries()]);
  };

  // ── Live polling ───────────────────────────────────────────────────────────
  const startLivePolling = () => {
    if (state.liveTimer) clearInterval(state.liveTimer);
    state.liveTimer = setInterval(loadLiveNow, LIVE_POLL_MS);
  };

  // ── Sync site selector ─────────────────────────────────────────────────────
  const syncSites = (sites) => {
    siteSelect.innerHTML = '';
    if (!sites.length) { siteSelect.appendChild(el('option', { value: '' }, 'No sites')); return; }
    sites.forEach(s => {
      const id  = getSiteId(s);
      const opt = el('option', { value: id }, s.domain || id);
      if (id === state.siteId) opt.selected = true;
      siteSelect.appendChild(opt);
    });
    if (!state.siteId || !sites.find(s => getSiteId(s) === state.siteId)) {
      state.siteId = getSiteId(sites[0]);
      siteSelect.value = state.siteId;
    }
  };

  // ── Wire event listeners (after all function declarations) ────────────────
  siteSelect.addEventListener('change', () => {
    state.siteId = siteSelect.value; saveSiteId(state.siteId); state.currentPage = 1; loadAll();
  });
  refreshBtn.addEventListener('click', loadAll);

  // ── Init ───────────────────────────────────────────────────────────────────
  const init = async () => {
    try {
      const sites = await client.sites.list();
      state.sites = Array.isArray(sites) ? sites : [];
      syncSites(state.sites);
      hTotal.textContent = String(state.sites.length > 0 ? '…' : '0');
      await loadAll();
      startLivePolling();
    } catch (err) {
      notifier.show({ message: 'Could not load sites.', variant: 'danger' });
    }
  };

  init();
};
