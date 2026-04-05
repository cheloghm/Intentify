/**
 * multiSiteAnalytics.js — Phase 7.3  Multi-Site Analytics
 * Cross-site dashboard for agencies managing multiple clients.
 * Shows all sites in a comparison grid: visitors, leads, tickets,
 * health status, and a unified sparkline for each.
 */

import { createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const getSiteId = s => s?.siteId || s?.id || '';
const fmtNum   = n => n == null ? '—' : Number(n).toLocaleString();
const fmtChg   = pct => {
  if (pct == null) return '';
  const sign = pct >= 0 ? '+' : '';
  return `${sign}${pct.toFixed(1)}%`;
};

// ─── el() ─────────────────────────────────────────────────────────────────────

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

// ─── Sparkline (pure canvas) ──────────────────────────────────────────────────

const drawSparkline = (canvas, points, color = '#6366f1') => {
  if (!points.length) return;
  const ctx = canvas.getContext('2d');
  const W = canvas.width, H = canvas.height;
  const max = Math.max(...points, 1);
  ctx.clearRect(0, 0, W, H);
  ctx.beginPath();
  points.forEach((v, i) => {
    const x = (i / (points.length - 1)) * W;
    const y = H - (v / max) * H * 0.85 - H * 0.07;
    i === 0 ? ctx.moveTo(x, y) : ctx.lineTo(x, y);
  });
  ctx.strokeStyle = color;
  ctx.lineWidth = 2;
  ctx.lineJoin = 'round';
  ctx.stroke();

  // Fill
  ctx.lineTo(W, H); ctx.lineTo(0, H); ctx.closePath();
  ctx.fillStyle = color + '22';
  ctx.fill();
};

// ─── Styles ───────────────────────────────────────────────────────────────────

const injectStyles = () => {
  if (document.getElementById('_msa_css')) return;
  const s = document.createElement('style');
  s.id = '_msa_css';
  s.textContent = `
@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap');
.msa-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;gap:20px;width:100%;max-width:1280px;padding-bottom:60px}
.msa-hero{background:linear-gradient(135deg,#0f172a 0%,#1e293b 100%);border-radius:16px;padding:28px 36px;position:relative;overflow:hidden}
.msa-hero::before{content:'';position:absolute;top:-30px;right:-30px;width:200px;height:200px;background:radial-gradient(circle,rgba(16,185,129,.15) 0%,transparent 70%);pointer-events:none}
.msa-hero-title{font-size:24px;font-weight:700;color:#f8fafc;letter-spacing:-.02em;margin-bottom:6px}
.msa-hero-sub{font-size:13px;color:#94a3b8;margin-bottom:18px}
.msa-hero-stats{display:flex;gap:28px;flex-wrap:wrap}
.msa-stat{display:flex;flex-direction:column;gap:2px}
.msa-stat-val{font-family:'JetBrains Mono',monospace;font-size:22px;font-weight:700;color:#f1f5f9;letter-spacing:-.02em}
.msa-stat-lbl{font-size:10px;color:#64748b;text-transform:uppercase;letter-spacing:.07em}
/* Controls */
.msa-controls{display:flex;align-items:center;gap:10px;flex-wrap:wrap}
.msa-btn{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;font-weight:600;padding:7px 16px;border-radius:8px;border:none;cursor:pointer;transition:all .14s;display:inline-flex;align-items:center;gap:5px}
.msa-btn-outline{background:#fff;color:#64748b;border:1px solid #e2e8f0}.msa-btn-outline:hover{background:#f8fafc;color:#1e293b}
.msa-btn-primary{background:#6366f1;color:#fff}.msa-btn-primary:hover{background:#4f46e5}
/* Sort select */
.msa-select{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;color:#1e293b;background:#fff;border:1px solid #e2e8f0;border-radius:8px;padding:7px 11px;outline:none}
/* Grid */
.msa-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(320px,1fr));gap:14px}
/* Site card */
.msa-card{background:#fff;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden;transition:box-shadow .16s,transform .16s}
.msa-card:hover{box-shadow:0 4px 20px rgba(0,0,0,.08);transform:translateY(-1px)}
.msa-card-hd{display:flex;align-items:flex-start;justify-content:space-between;padding:16px 18px 10px;border-bottom:1px solid #f8fafc}
.msa-card-domain{font-size:14px;font-weight:700;color:#1e293b;margin-bottom:2px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:200px}
.msa-card-name{font-size:11.5px;color:#94a3b8}
.msa-health-dot{width:9px;height:9px;border-radius:50%;flex-shrink:0;margin-top:3px}
.msa-dot-ok{background:#10b981;box-shadow:0 0 0 3px rgba(16,185,129,.2)}
.msa-dot-warn{background:#f59e0b;box-shadow:0 0 0 3px rgba(245,158,11,.2)}
.msa-dot-err{background:#ef4444;box-shadow:0 0 0 3px rgba(239,68,68,.2)}
/* Metrics row */
.msa-metrics{display:grid;grid-template-columns:repeat(4,1fr);gap:0;padding:0}
.msa-metric{padding:12px 14px;border-right:1px solid #f1f5f9;display:flex;flex-direction:column;gap:2px}
.msa-metric:last-child{border-right:none}
.msa-metric-val{font-family:'JetBrains Mono',monospace;font-size:17px;font-weight:700;color:#0f172a}
.msa-metric-lbl{font-size:9.5px;color:#94a3b8;text-transform:uppercase;letter-spacing:.06em;font-weight:700}
.msa-metric-chg{font-size:10px;margin-top:1px;font-weight:600}
.msa-chg-pos{color:#10b981}.msa-chg-neg{color:#ef4444}.msa-chg-neu{color:#94a3b8}
/* Sparkline */
.msa-spark{padding:10px 14px 14px;border-top:1px solid #f8fafc}
.msa-spark canvas{width:100%;height:50px;display:block}
/* Footer */
.msa-card-ft{display:flex;align-items:center;justify-content:space-between;padding:10px 14px;border-top:1px solid #f1f5f9;background:#fafbff}
.msa-ft-label{font-size:11px;color:#94a3b8;font-family:'JetBrains Mono',monospace}
.msa-view-btn{font-size:11.5px;font-weight:600;color:#6366f1;text-decoration:none;padding:4px 10px;border:1px solid #c7d2fe;border-radius:6px;background:#eef2ff;transition:background .12s}
.msa-view-btn:hover{background:#e0e7ff}
/* Skeleton */
.msa-skel{background:linear-gradient(90deg,#f1f5f9 25%,#e2e8f0 50%,#f1f5f9 75%);background-size:200% 100%;animation:_sh 1.4s infinite;border-radius:14px;height:200px}
@keyframes _sh{0%{background-position:200% 0}100%{background-position:-200% 0}}
/* Empty */
.msa-empty{grid-column:1/-1;text-align:center;padding:52px 20px;color:#94a3b8;display:flex;flex-direction:column;align-items:center;gap:8px}
.msa-empty-icon{font-size:42px;opacity:.3}
/* Summary table */
.msa-panel{background:#fff;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden}
.msa-panel-hd{display:flex;align-items:center;justify-content:space-between;padding:14px 20px;border-bottom:1px solid #f1f5f9}
.msa-panel-title{font-size:13px;font-weight:700;color:#0f172a}
.msa-table{width:100%;border-collapse:collapse;font-size:12.5px}
.msa-table thead th{background:#f8fafc;padding:9px 16px;text-align:left;font-size:9.5px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:#94a3b8;border-bottom:1px solid #e2e8f0;white-space:nowrap}
.msa-table tbody td{padding:12px 16px;border-bottom:1px solid #f1f5f9;color:#334155;vertical-align:middle}
.msa-table tbody tr:last-child td{border-bottom:none}
.msa-table tbody tr:hover{background:#fafbff;cursor:pointer}
.msa-rank{font-family:'JetBrains Mono',monospace;font-size:11px;color:#94a3b8;font-weight:600}
  `;
  document.head.appendChild(s);
};

// ─── Main export ──────────────────────────────────────────────────────────────

export const renderMultiSiteAnalyticsView = async (container, { apiClient, toast } = {}) => {
  injectStyles();
  const client   = apiClient || createApiClient();
  const notifier = toast     || createToastManager();

  const root = el('div', { class: 'msa-root' });
  container.appendChild(root);

  // ── Hero ───────────────────────────────────────────────────────────────────
  const hero = el('div', { class: 'msa-hero' });
  hero.appendChild(el('div', { class: 'msa-hero-title' }, '🏢 Multi-Site Analytics'));
  hero.appendChild(el('div', { class: 'msa-hero-sub' }, 'Cross-site performance overview across all your managed properties'));
  const heroStats = el('div', { class: 'msa-hero-stats' });
  const mkS = lbl => { const w = el('div', { class: 'msa-stat' }); const v = el('div', { class: 'msa-stat-val' }, '—'); w.append(v, el('div', { class: 'msa-stat-lbl' }, lbl)); heroStats.appendChild(w); return v; };
  const hSites     = mkS('Sites');
  const hVisitors  = mkS('Total Visitors (7d)');
  const hLeads     = mkS('Total Leads');
  const hHealthy   = mkS('Sites Healthy');
  hero.appendChild(heroStats);
  root.appendChild(hero);

  // ── Controls ───────────────────────────────────────────────────────────────
  const controls = el('div', { class: 'msa-controls' });
  const sortSelect = el('select', { class: 'msa-select' });
  [['visitors', 'Sort: Visitors (7d)'], ['leads', 'Sort: Leads'], ['tickets', 'Sort: Open Tickets'], ['domain', 'Sort: Domain A–Z']].forEach(([v, l]) =>
    sortSelect.appendChild(el('option', { value: v }, l)));
  const refreshBtn = el('button', { class: 'msa-btn msa-btn-outline' }, '↻ Refresh');
  controls.append(sortSelect, refreshBtn);
  root.appendChild(controls);

  // ── Cards grid ─────────────────────────────────────────────────────────────
  const grid = el('div', { class: 'msa-grid' });
  root.appendChild(grid);

  // ── Summary table ──────────────────────────────────────────────────────────
  const tablePanel = el('div', { class: 'msa-panel' });
  const tablePanelHd = el('div', { class: 'msa-panel-hd' });
  tablePanelHd.appendChild(el('div', { class: 'msa-panel-title' }, '📋 Side-by-Side Comparison'));
  tablePanel.appendChild(tablePanelHd);
  const tableWrap = el('div', { style: 'overflow-x:auto' });
  tablePanel.appendChild(tableWrap);
  root.appendChild(tablePanel);

  // ── Data state ─────────────────────────────────────────────────────────────
  let siteData = []; // { site, analytics, leads, tickets, health }

  const sortAndRender = () => {
    const sort = sortSelect.value;
    const sorted = [...siteData].sort((a, b) => {
      if (sort === 'visitors') return (b.analytics?.weekVisitors ?? 0) - (a.analytics?.weekVisitors ?? 0);
      if (sort === 'leads')    return (b.leads ?? 0) - (a.leads ?? 0);
      if (sort === 'tickets')  return (b.tickets ?? 0) - (a.tickets ?? 0);
      return (a.site.domain ?? '').localeCompare(b.site.domain ?? '');
    });
    renderGrid(sorted);
    renderTable(sorted);
  };

  const renderGrid = (data) => {
    grid.replaceChildren();
    if (!data.length) {
      grid.appendChild(el('div', { class: 'msa-empty' },
        el('div', { class: 'msa-empty-icon' }, '🏢'),
        el('div', { style: 'font-size:14px;font-weight:600;color:#334155' }, 'No sites yet'),
        el('div', { style: 'font-size:12px;max-width:280px;line-height:1.6' }, 'Add a site in the Sites page to start tracking analytics across your properties.')
      ));
      return;
    }

    data.forEach(({ site, analytics, leads, tickets, health }) => {
      const siteId = getSiteId(site);
      const isHealthy = health?.isInstalled && health?.firstEventReceivedAtUtc;

      const card = el('div', { class: 'msa-card' });

      // Header
      const hd = el('div', { class: 'msa-card-hd' });
      const hdLeft = el('div', { style: 'flex:1;min-width:0' });
      hdLeft.append(
        el('div', { class: 'msa-card-domain' }, site.domain || site.name || 'Unknown'),
        el('div', { class: 'msa-card-name' }, site.name || site.description || ''),
      );
      const hdot = el('div', { class: `msa-health-dot ${isHealthy ? 'msa-dot-ok' : health?.isConfigured ? 'msa-dot-warn' : 'msa-dot-err'}`, title: isHealthy ? 'Tracking active' : 'Not yet tracking' });
      hd.append(hdLeft, hdot);
      card.appendChild(hd);

      // Metrics
      const metrics = el('div', { class: 'msa-metrics' });
      const mkM = (val, lbl, chg) => {
        const m = el('div', { class: 'msa-metric' });
        m.appendChild(el('div', { class: 'msa-metric-val' }, fmtNum(val)));
        m.appendChild(el('div', { class: 'msa-metric-lbl' }, lbl));
        if (chg != null) {
          const cls = chg > 0 ? 'msa-chg-pos' : chg < 0 ? 'msa-chg-neg' : 'msa-chg-neu';
          m.appendChild(el('div', { class: `msa-metric-chg ${cls}` }, fmtChg(chg)));
        }
        return m;
      };
      metrics.append(
        mkM(analytics?.weekVisitors, 'Visitors 7d', analytics?.weekChangePercent),
        mkM(analytics?.onlineNow, 'Live Now', null),
        mkM(leads, 'Leads', null),
        mkM(tickets, 'Open Tickets', null),
      );
      card.appendChild(metrics);

      // Sparkline (last 14 days visitors)
      if (analytics?.last14Days?.length) {
        const sparkWrap = el('div', { class: 'msa-spark' });
        const canvas = el('canvas', { style: 'width:100%;height:50px' });
        sparkWrap.appendChild(canvas);
        card.appendChild(sparkWrap);
        requestAnimationFrame(() => {
          const rect = canvas.getBoundingClientRect();
          canvas.width  = rect.width  || 280;
          canvas.height = 50;
          drawSparkline(canvas, analytics.last14Days.map(d => d.visitors), isHealthy ? '#6366f1' : '#94a3b8');
        });
      }

      // Footer
      const ft = el('div', { class: 'msa-card-ft' });
      ft.appendChild(el('div', { class: 'msa-ft-label' }, siteId.slice(0, 8) + '…'));
      const link = el('a', { class: 'msa-view-btn', href: `#/dashboard?siteId=${siteId}` }, 'View Dashboard →');
      ft.appendChild(link);
      card.appendChild(ft);

      grid.appendChild(card);
    });
  };

  const renderTable = (data) => {
    tableWrap.replaceChildren();
    const table = el('table', { class: 'msa-table' });
    table.appendChild(el('thead', {}, el('tr', {},
      ...['#', 'Site', 'Visitors (7d)', 'Δ Week', 'Live Now', 'Leads', 'Open Tickets', 'Status'].map(c => el('th', {}, c))
    )));
    const tbody = el('tbody', {});
    if (!data.length) {
      const tr = el('tr', {}); const td = el('td', { colspan: '8', style: 'text-align:center;padding:24px;color:#94a3b8' }, 'No sites'); tr.appendChild(td); tbody.appendChild(tr);
    } else {
      data.forEach(({ site, analytics, leads, tickets, health }, i) => {
        const siteId = getSiteId(site);
        const isHealthy = health?.isInstalled && health?.firstEventReceivedAtUtc;
        const tr = el('tr', { '@click': () => { window.location.hash = `#/dashboard?siteId=${siteId}`; } });
        const chgVal = analytics?.weekChangePercent;
        const chgCls = chgVal == null ? '' : chgVal >= 0 ? 'msa-chg-pos' : 'msa-chg-neg';
        tr.append(
          el('td', {}, el('span', { class: 'msa-rank' }, String(i + 1))),
          el('td', {}, el('div', { style: 'font-weight:600;color:#1e293b' }, site.domain || site.name || '—'), el('div', { style: 'font-size:11px;color:#94a3b8' }, site.name || '')),
          el('td', { style: 'font-family:JetBrains Mono,monospace' }, fmtNum(analytics?.weekVisitors)),
          el('td', {}, el('span', { class: chgCls, style: 'font-size:12px;font-weight:600' }, fmtChg(chgVal))),
          el('td', { style: 'font-family:JetBrains Mono,monospace' }, fmtNum(analytics?.onlineNow)),
          el('td', { style: 'font-family:JetBrains Mono,monospace' }, fmtNum(leads)),
          el('td', { style: 'font-family:JetBrains Mono,monospace' }, fmtNum(tickets)),
          el('td', {}, el('span', {
            style: `display:inline-flex;align-items:center;gap:5px;font-size:11px;font-weight:700;padding:2px 8px;border-radius:999px;background:${isHealthy ? '#d1fae5' : '#fef3c7'};color:${isHealthy ? '#065f46' : '#92400e'}`
          }, isHealthy ? '✓ Active' : '⚠ Setup')),
        );
        tbody.appendChild(tr);
      });
    }
    table.appendChild(tbody);
    tableWrap.appendChild(table);
  };

  // ── Load all site data in parallel ─────────────────────────────────────────
  const loadAll = async () => {
    grid.replaceChildren(
      ...Array(3).fill(null).map(() => el('div', { class: 'msa-skel' }))
    );
    tableWrap.replaceChildren();

    try {
      const sites = await client.sites.list();
      if (!sites?.length) { siteData = []; hSites.textContent = '0'; sortAndRender(); return; }

      hSites.textContent = String(sites.length);

      // Load per-site data in parallel
      const results = await Promise.allSettled(sites.map(async site => {
        const siteId = getSiteId(site);
        const [analytics, leadsRes, ticketsRes, healthRes] = await Promise.allSettled([
          client.visitors.dashboardAnalytics(siteId),
          client.leads.list(siteId, 1, 200),
          client.tickets.listTickets({ siteId, page: 1, pageSize: 200 }),
          client.request(`/sites/${siteId}/installation-status`),
        ]);

        const a   = analytics.status === 'fulfilled' ? analytics.value : null;
        const l   = leadsRes.status === 'fulfilled' && Array.isArray(leadsRes.value) ? leadsRes.value.length : 0;
        const t   = ticketsRes.status === 'fulfilled' && Array.isArray(ticketsRes.value)
          ? ticketsRes.value.filter(tk => { const s = (tk.status || '').toLowerCase().replace(/[^a-z]/g, ''); return s === 'open' || s === 'inprogress'; }).length : 0;
        const h   = healthRes.status === 'fulfilled' ? healthRes.value : null;

        return { site, analytics: a, leads: l, tickets: t, health: h };
      }));

      siteData = results.map(r => r.status === 'fulfilled' ? r.value : null).filter(Boolean);

      // Update hero stats
      const totalVisitors = siteData.reduce((sum, d) => sum + (d.analytics?.weekVisitors ?? 0), 0);
      const totalLeads    = siteData.reduce((sum, d) => sum + (d.leads ?? 0), 0);
      const healthy       = siteData.filter(d => d.health?.isInstalled && d.health?.firstEventReceivedAtUtc).length;
      hVisitors.textContent = fmtNum(totalVisitors);
      hLeads.textContent    = fmtNum(totalLeads);
      hHealthy.textContent  = `${healthy}/${sites.length}`;

      sortAndRender();
    } catch (err) {
      notifier.show({ message: mapApiError(err).message, variant: 'danger' });
      grid.replaceChildren();
    }
  };

  sortSelect.addEventListener('change', sortAndRender);
  refreshBtn.addEventListener('click', loadAll);

  await loadAll();
};
