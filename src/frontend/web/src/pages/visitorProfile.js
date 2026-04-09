/**
 * visitorProfile.js — Intentify Visitor Profile
 * Phase 3: Engage chat history, linked lead, tickets, full identity enrichment display
 */

import { createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

// ─── Utilities ────────────────────────────────────────────────────────────────

const el = (tag, attrs = {}, ...kids) => {
  const e = document.createElement(tag);
  Object.entries(attrs).forEach(([k, v]) => {
    if (k === 'class')         e.className = v;
    else if (k === 'style')    typeof v === 'string' ? (e.style.cssText = v) : Object.assign(e.style, v);
    else if (k.startsWith('@')) e.addEventListener(k.slice(1), v);
    else e.setAttribute(k, v);
  });
  kids.flat(Infinity).forEach(c => c != null && e.append(typeof c === 'string' ? document.createTextNode(c) : c));
  return e;
};

const fmtDate    = v => { if (!v) return '—'; const d = new Date(v); return isNaN(d) ? '—' : d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' }); };
const fmtTime    = v => { if (!v) return '—'; const d = new Date(v); return isNaN(d) ? '—' : d.toLocaleString('en-GB'); };
const fmtAgo     = v => {
  if (!v) return '—';
  const m = Math.floor((Date.now() - new Date(v)) / 60000);
  if (m < 1)  return 'just now';
  if (m < 60) return `${m}m ago`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h}h ago`;
  return `${Math.floor(h / 24)}d ago`;
};
const fmtDur  = s => { if (!s || s < 1) return '—'; if (s < 60) return `${s}s`; const m = Math.floor(s/60); return `${m}m ${s%60 > 0 ? s%60+'s':''}`.trim(); };
const normPath = url => { if (!url) return '—'; try { const u = new URL(url); return u.pathname + u.search || '/'; } catch { return url; } };
const normHost = url => { if (!url) return 'Direct'; try { return new URL(url).hostname; } catch { return url; } };
const shortId  = v => (!v || v.length < 8) ? (v || '—') : `${v.slice(0,8)}…`;

const PLATFORM_ICON = { mobile: '📱', desktop: '🖥️', tablet: '📋' };
const FLAGS = { GB:'🇬🇧', IE:'🇮🇪', US:'🇺🇸', DE:'🇩🇪', FR:'🇫🇷', AU:'🇦🇺', CA:'🇨🇦', NL:'🇳🇱' };
const flag = c => FLAGS[c?.toUpperCase()] || '🌍';

const TL_CFG = {
  pageview:    { icon: '👁️', cls: 'vp-tl-pv', label: 'Page View'         },
  page_view:   { icon: '👁️', cls: 'vp-tl-pv', label: 'Page View'         },
  engage:      { icon: '💬', cls: 'vp-tl-en', label: 'Chatted with Bot'  },
  lead_capture:{ icon: '⭐', cls: 'vp-tl-lc', label: 'Lead Captured'     },
  exit:        { icon: '🚪', cls: 'vp-tl-ot', label: 'Left Page'         },
  scroll:      { icon: '↕️', cls: 'vp-tl-ot', label: 'Scrolled'          },
};
const tlCfg = t => TL_CFG[t?.toLowerCase()] || { icon: '⚡', cls: 'vp-tl-ot', label: t || 'Event' };

// ─── Styles ───────────────────────────────────────────────────────────────────

const injectStyles = () => {
  if (document.getElementById('_vp3_css')) return;
  const s = document.createElement('style');
  s.id = '_vp3_css';
  s.textContent = `
@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap');
.vp-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;gap:16px;width:100%;max-width:1100px;padding-bottom:60px}
.vp-back{display:inline-flex;align-items:center;gap:6px;font-size:13px;font-weight:600;color:#6366f1;cursor:pointer;background:none;border:none;padding:0;font-family:inherit}
.vp-back:hover{text-decoration:underline}
/* Hero */
.vp-hero{background:#fff;border:1px solid #e2e8f0;border-radius:16px;padding:22px 26px}
.vp-hero-top{display:flex;align-items:flex-start;gap:16px;flex-wrap:wrap}
.vp-avatar{width:52px;height:52px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:20px;font-weight:700;flex-shrink:0}
.vp-avatar-id{background:linear-gradient(135deg,#6366f1,#8b5cf6);color:#fff}
.vp-avatar-anon{background:#f1f5f9;color:#94a3b8}
.vp-hero-body{flex:1;min-width:0}
.vp-hero-name{font-size:17px;font-weight:700;color:#0f172a;margin-bottom:2px}
.vp-hero-sub{font-size:11.5px;color:#94a3b8;font-family:'JetBrains Mono',monospace;margin-bottom:8px}
.vp-chips{display:flex;gap:6px;flex-wrap:wrap}
.vp-chip{display:inline-flex;align-items:center;gap:3px;padding:3px 8px;border-radius:999px;font-size:10.5px;font-weight:600;border:1px solid}
.vp-chip-green{background:#d1fae5;color:#065f46;border-color:#6ee7b7}
.vp-chip-blue{background:#dbeafe;color:#1e40af;border-color:#93c5fd}
.vp-chip-gray{background:#f1f5f9;color:#475569;border-color:#e2e8f0}
.vp-chip-amber{background:#fef3c7;color:#92400e;border-color:#fcd34d}
.vp-hero-stats{display:flex;gap:18px;flex-wrap:wrap;margin-top:14px;padding-top:12px;border-top:1px solid #f1f5f9}
.vp-stat-val{font-family:'JetBrains Mono',monospace;font-size:18px;font-weight:700;color:#0f172a;line-height:1}
.vp-stat-lbl{font-size:10px;color:#94a3b8;text-transform:uppercase;letter-spacing:.06em;margin-top:2px}
/* Layout */
.vp-grid{display:grid;grid-template-columns:1fr 320px;gap:14px;align-items:start}
@media(max-width:820px){.vp-grid{grid-template-columns:1fr}}
/* Panel */
.vp-panel{background:#fff;border:1px solid #e2e8f0;border-radius:12px;overflow:hidden}
.vp-panel-hd{display:flex;align-items:center;justify-content:space-between;padding:12px 16px;border-bottom:1px solid #f1f5f9}
.vp-panel-title{font-size:12.5px;font-weight:700;color:#0f172a;display:flex;align-items:center;gap:6px}
.vp-panel-meta{font-size:10.5px;color:#94a3b8}
.vp-panel-body{padding:14px 16px}
/* Identity rows */
.vp-id-row{display:flex;align-items:flex-start;justify-content:space-between;gap:8px;padding:7px 0;border-bottom:1px solid #f8fafc}
.vp-id-row:last-child{border-bottom:none}
.vp-id-lbl{font-size:10.5px;color:#94a3b8;text-transform:uppercase;letter-spacing:.05em;font-weight:600;flex-shrink:0;padding-top:1px}
.vp-id-val{font-size:12px;color:#1e293b;text-align:right;word-break:break-all;font-family:'JetBrains Mono',monospace}
.vp-conf-wrap{display:flex;align-items:center;gap:6px;width:100%}
.vp-conf-track{flex:1;height:4px;background:#e2e8f0;border-radius:999px}
.vp-conf-fill{height:100%;border-radius:999px;background:#6366f1;transition:width .5s cubic-bezier(.34,1.56,.64,1)}
/* Timeline */
.vp-timeline{display:flex;flex-direction:column;max-height:480px;overflow-y:auto}
.vp-tl-item{display:flex;gap:10px;padding:8px 0;position:relative}
.vp-tl-item::before{content:'';position:absolute;left:15px;top:28px;bottom:-8px;width:1px;background:#e2e8f0}
.vp-tl-item:last-child::before{display:none}
.vp-tl-dot{width:30px;height:30px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:13px;flex-shrink:0;z-index:1;border:2px solid}
.vp-tl-pv{background:#eef2ff;border-color:#c7d2fe}
.vp-tl-en{background:#d1fae5;border-color:#6ee7b7}
.vp-tl-lc{background:#fef3c7;border-color:#fcd34d}
.vp-tl-ot{background:#f1f5f9;border-color:#e2e8f0}
.vp-tl-body{flex:1;min-width:0;padding-top:2px}
.vp-tl-title{font-size:12px;font-weight:600;color:#1e293b}
.vp-tl-path{font-size:10.5px;color:#6366f1;font-family:'JetBrains Mono',monospace;word-break:break-all;margin-top:1px}
.vp-tl-ref{font-size:10px;color:#94a3b8;margin-top:1px}
.vp-tl-time{font-size:10px;color:#94a3b8;margin-top:2px}
/* Engage chat */
.vp-chat-session{background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;margin-bottom:10px;overflow:hidden}
.vp-chat-session-hd{display:flex;align-items:center;justify-content:space-between;padding:9px 12px;border-bottom:1px solid #e2e8f0;cursor:pointer;user-select:none}
.vp-chat-session-title{font-size:11.5px;font-weight:600;color:#1e293b;display:flex;align-items:center;gap:6px}
.vp-chat-session-meta{font-size:10px;color:#94a3b8;font-family:'JetBrains Mono',monospace}
.vp-chat-messages{padding:10px 12px;display:flex;flex-direction:column;gap:7px}
.vp-msg{max-width:85%;padding:8px 11px;border-radius:10px;font-size:12px;line-height:1.5}
.vp-msg-user{background:#6366f1;color:#fff;align-self:flex-end;border-radius:10px 10px 2px 10px}
.vp-msg-bot{background:#fff;color:#334155;border:1px solid #e2e8f0;align-self:flex-start;border-radius:10px 10px 10px 2px}
.vp-msg-time{font-size:9px;opacity:.6;margin-top:3px;text-align:right}
.vp-badge{display:inline-flex;align-items:center;gap:3px;padding:1px 6px;border-radius:999px;font-size:9.5px;font-weight:700}
.vp-badge-lead{background:#d1fae5;color:#065f46}
.vp-badge-ticket{background:#dbeafe;color:#1e40af}
/* Lead box */
.vp-lead-box{background:linear-gradient(135deg,#d1fae5,#a7f3d0);border:1px solid #6ee7b7;border-radius:10px;padding:12px 14px}
.vp-lead-hd{display:flex;align-items:center;gap:8px;margin-bottom:8px}
.vp-lead-icon{font-size:18px}
.vp-lead-name{font-size:13px;font-weight:700;color:#065f46}
.vp-lead-link{font-size:11px;font-weight:700;color:#065f46;cursor:pointer;text-decoration:underline;margin-left:auto}
.vp-lead-rows{display:flex;flex-direction:column;gap:3px}
.vp-lead-row{display:flex;gap:8px;font-size:11.5px}
.vp-lead-row-lbl{color:#6ee7b7;font-weight:600;min-width:48px}
.vp-lead-row-val{color:#065f46;font-family:'JetBrains Mono',monospace;word-break:break-all}
/* Ticket */
.vp-ticket{background:#fff;border:1px solid #e2e8f0;border-radius:9px;padding:11px 13px;margin-bottom:8px}
.vp-ticket-hd{display:flex;align-items:flex-start;justify-content:space-between;gap:6px;margin-bottom:5px}
.vp-ticket-subject{font-size:12.5px;font-weight:600;color:#1e293b}
.vp-ticket-status{display:inline-flex;padding:2px 8px;border-radius:999px;font-size:9.5px;font-weight:700;text-transform:uppercase}
.vp-ticket-open{background:#fef3c7;color:#92400e}
.vp-ticket-closed{background:#d1fae5;color:#065f46}
.vp-ticket-meta{font-size:10.5px;color:#94a3b8;font-family:'JetBrains Mono',monospace}
/* Session cards */
.vp-session{background:#f8fafc;border:1px solid #e2e8f0;border-radius:9px;padding:10px 12px;margin-bottom:8px}
.vp-session-hd{display:flex;align-items:flex-start;justify-content:space-between;gap:6px;margin-bottom:5px}
.vp-session-title{font-size:11.5px;font-weight:600;color:#1e293b}
.vp-session-time{font-size:10px;color:#94a3b8;font-family:'JetBrains Mono',monospace}
.vp-session-stats{display:flex;gap:10px;flex-wrap:wrap}
.vp-session-stat{font-size:11px;color:#64748b}
/* Intelligence */
.vp-intel{background:linear-gradient(135deg,#0f172a,#1e1b4b);border-radius:12px;padding:14px 16px}
.vp-intel-lbl{font-size:10px;font-weight:700;letter-spacing:.1em;text-transform:uppercase;color:#818cf8;margin-bottom:7px;display:flex;align-items:center;gap:4px}
.vp-intel-text{font-size:12.5px;line-height:1.65;color:#e2e8f0}
/* Return visit banner */
.vp-return-banner{background:#eef2ff;border:1px solid #c7d2fe;border-radius:8px;padding:8px 14px;font-size:12.5px;color:#4338ca;display:flex;align-items:center;gap:8px}
/* Empty/loading */
.vp-empty{text-align:center;padding:30px 16px;display:flex;flex-direction:column;align-items:center;gap:7px}
.vp-empty-icon{font-size:30px;opacity:.3}
.vp-empty-title{font-size:13px;font-weight:600;color:#334155}
.vp-empty-desc{font-size:11.5px;color:#94a3b8;max-width:240px;line-height:1.6}
  `;
  document.head.appendChild(s);
};

// ─── Panel helper ─────────────────────────────────────────────────────────────
const mkPanel = (title, meta = '') => {
  const panel = el('div', { class: 'vp-panel' });
  const hd    = el('div', { class: 'vp-panel-hd' });
  hd.appendChild(el('div', { class: 'vp-panel-title' }, title));
  if (meta) hd.appendChild(el('div', { class: 'vp-panel-meta' }, meta));
  panel.appendChild(hd);
  const body = el('div', { class: 'vp-panel-body' });
  panel.appendChild(body);
  return { panel, body, hd };
};

// ─── Main export ──────────────────────────────────────────────────────────────

export const renderVisitorProfileView = async (container, { apiClient, toast, visitorId, siteId } = {}) => {
  injectStyles();
  const client   = apiClient || createApiClient();
  const notifier = toast     || createToastManager();

  if (!visitorId) {
    const hash  = window.location.hash;
    const match = hash.match(/#\/visitors\/([^?]+)/);
    visitorId   = match?.[1];
    const sp    = new URLSearchParams(hash.split('?')[1] || '');
    siteId      = siteId || sp.get('siteId') || '';
  }

  const root = el('div', { class: 'vp-root' });
  container.appendChild(root);

  root.appendChild(el('button', { class: 'vp-back', '@click': () => { window.location.hash = '#/visitors'; } }, '← Back to Visitors'));

  if (!visitorId) {
    root.appendChild(el('div', { class: 'vp-empty' }, el('div',{class:'vp-empty-icon'},'🔍'), el('div',{class:'vp-empty-title'},'No visitor selected')));
    return;
  }

  const loadingEl = el('div', { style: 'padding:40px;text-align:center;color:#94a3b8;font-size:13px' }, '⏳ Loading visitor profile…');
  root.appendChild(loadingEl);

  let visitor = null, timeline = [], conversations = [], linkedLead = null, tickets = [], intelligence = null;

  try {
    [visitor, timeline] = await Promise.all([
      client.visitors.detail(visitorId, siteId),
      client.visitors.timeline(visitorId, 200, siteId),
    ]);
  } catch (err) {
    loadingEl.remove();
    root.appendChild(el('div', { class: 'vp-empty' }, el('div',{class:'vp-empty-icon'},'😕'), el('div',{class:'vp-empty-title'},'Could not load visitor'), el('div',{class:'vp-empty-desc'}, mapApiError(err).message)));
    return;
  }

  loadingEl.remove();
  if (!visitor) { root.appendChild(el('div', { class: 'vp-empty' }, el('div',{class:'vp-empty-icon'},'🔍'), el('div',{class:'vp-empty-title'},'Visitor not found'))); return; }

  // Load secondary data in parallel — non-blocking
  const collectorSessionId = visitor.sessions?.[0]?.sessionId || visitor.recentSessions?.[0]?.sessionId || '';

  // Collect all session IDs — tickets created before the VisitorId fix only have EngageSessionId set
  const allSessionIds = [
    ...(visitor.sessions || []).map(s => s.sessionId),
    ...(visitor.recentSessions || []).map(s => s.sessionId),
  ].filter((v, i, a) => v && a.indexOf(v) === i);

  await Promise.allSettled([
    siteId ? client.engage.listConversations(siteId, collectorSessionId).then(r => { conversations = Array.isArray(r) ? r : []; }) : Promise.resolve(),
    siteId ? client.leads.getByVisitor(siteId, visitorId).then(r => { linkedLead = r; }).catch(() => {}) : Promise.resolve(),
    // Query by visitorId AND by each engageSessionId to catch all tickets regardless of how they were indexed
    siteId ? (async () => {
      const seen = new Set();
      const merge = arr => (Array.isArray(arr) ? arr : []).filter(t => {
        const id = t.id || t.ticketId || JSON.stringify(t);
        if (seen.has(id)) return false;
        seen.add(id); return true;
      });
      const results = await Promise.allSettled([
        client.tickets.listTickets({ siteId, visitorId, page: 1, pageSize: 50 }),
        ...allSessionIds.map(sid => client.tickets.listTickets({ siteId, engageSessionId: sid, page: 1, pageSize: 20 })),
      ]);
      tickets = results.flatMap(r => r.status === 'fulfilled' ? merge(r.value) : []);
    })() : Promise.resolve(),
    siteId ? client.intelligence.siteSummary({ siteId, timeWindow: '7d' }).then(r => { intelligence = r; }).catch(() => {}) : Promise.resolve(),
  ]);

  // ── Hero ───────────────────────────────────────────────────────────────────
  const isId      = visitor.identification?.isIdentified;
  const dispName  = visitor.displayName || visitor.primaryEmail || 'Anonymous Visitor';
  const initials  = isId ? (dispName[0]?.toUpperCase() || '?') : (PLATFORM_ICON[visitor.platform] || '👤');

  const hero = el('div', { class: 'vp-hero' });
  const heroTop = el('div', { class: 'vp-hero-top' });
  heroTop.appendChild(el('div', { class: `vp-avatar ${isId ? 'vp-avatar-id' : 'vp-avatar-anon'}` }, initials));

  const heroBody = el('div', { class: 'vp-hero-body' });
  heroBody.appendChild(el('div', { class: 'vp-hero-name' }, dispName));
  heroBody.appendChild(el('div', { class: 'vp-hero-sub' }, `ID: ${shortId(visitorId)}`));

  const chips = el('div', { class: 'vp-chips' });
  if (isId)             chips.appendChild(el('span', { class: 'vp-chip vp-chip-green' }, '✓ Identified'));
  if (linkedLead)       chips.appendChild(el('span', { class: 'vp-chip vp-chip-amber' }, '⭐ Lead'));
  if (tickets.length)   chips.appendChild(el('span', { class: 'vp-chip vp-chip-blue'  }, `🎫 ${tickets.length} ticket${tickets.length > 1 ? 's' : ''}`));
  if (visitor.country)  chips.appendChild(el('span', { class: 'vp-chip vp-chip-blue'  }, flag(visitor.country), ' ', visitor.country));
  if (visitor.platform) chips.appendChild(el('span', { class: 'vp-chip vp-chip-gray'  }, PLATFORM_ICON[visitor.platform] || '💻', ' ', visitor.platform));
  heroBody.appendChild(chips);

  const heroStats = el('div', { class: 'vp-hero-stats' });
  const mkStat = (val, lbl) => { const w = el('div',{}); w.append(el('div',{class:'vp-stat-val'},String(val)), el('div',{class:'vp-stat-lbl'},lbl)); heroStats.appendChild(w); };
  mkStat(visitor.visitCount || 0, 'Visits');
  mkStat(visitor.totalPagesVisited || 0, 'Pages');
  mkStat(conversations.length, 'Conversations');
  mkStat(fmtAgo(visitor.lastSeenAtUtc), 'Last seen');

  // Intent score — colored ring stat
  const intentScore = visitor.intentScore || 0;
  const intentColor = intentScore >= 80 ? '#10b981' : intentScore >= 60 ? '#3b82f6' : intentScore >= 40 ? '#f59e0b' : '#94a3b8';
  const intentLabel = intentScore >= 80 ? 'Intent · 🔥 Hot' : intentScore >= 60 ? 'Intent · ♨ Warm' : intentScore >= 40 ? 'Intent · ~ Cool' : 'Intent Score';
  const intentW = el('div', { style: 'display:flex;flex-direction:column;gap:2px' });
  intentW.appendChild(el('div', { class: 'vp-stat-val', style: `color:${intentColor}` }, String(intentScore)));
  intentW.appendChild(el('div', { class: 'vp-stat-lbl' }, intentLabel));
  heroStats.appendChild(intentW);

  heroBody.appendChild(heroStats);

  heroTop.appendChild(heroBody);
  hero.appendChild(heroTop);
  root.appendChild(hero);

  // ── Return visit banner ────────────────────────────────────────────────────
  const minsAgo = (Date.now() - new Date(visitor.lastSeenAtUtc)) / 60000;
  const totalVisits = visitor.visitCount || visitor.recentSessions?.length || 0;
  if (totalVisits > 1 && minsAgo > 60) {
    const knownName = visitor.displayName || visitor.primaryEmail;
    const label = knownName
      ? `↩ ${knownName} is back — last seen ${fmtAgo(visitor.lastSeenAtUtc)}`
      : `↩ Return visitor — last seen ${fmtAgo(visitor.lastSeenAtUtc)}`;
    root.appendChild(el('div', { class: 'vp-return-banner' },
      el('span', {}, label),
      el('span', { style: 'margin-left:auto;font-weight:700;white-space:nowrap' }, `${totalVisits} visits total`)
    ));
  }

  // ── Lead banner ────────────────────────────────────────────────────────────
  if (linkedLead) {
    const leadBox = el('div', { class: 'vp-lead-box' });
    const leadHd  = el('div', { class: 'vp-lead-hd' });
    leadHd.appendChild(el('div', { class: 'vp-lead-icon' }, '⭐'));
    leadHd.appendChild(el('div', { class: 'vp-lead-name' }, linkedLead.displayName || linkedLead.primaryEmail || 'Lead'));
    leadHd.appendChild(el('span', { class: 'vp-lead-link', '@click': () => { window.location.hash = '#/leads'; } }, 'View Leads →'));
    leadBox.appendChild(leadHd);

    const rows = el('div', { class: 'vp-lead-rows' });
    const addRow = (lbl, val) => {
      if (!val) return;
      const r = el('div', { class: 'vp-lead-row' });
      r.append(el('span', { class: 'vp-lead-row-lbl' }, lbl), el('span', { class: 'vp-lead-row-val' }, val));
      rows.appendChild(r);
    };
    addRow('Email',    linkedLead.primaryEmail);
    addRow('Phone',    linkedLead.phone);
    addRow('Intent',   linkedLead.opportunityLabel);
    addRow('Score',    linkedLead.intentScore?.toString());
    leadBox.appendChild(rows);
    root.appendChild(leadBox);
  }

  // ── Grid ───────────────────────────────────────────────────────────────────
  const grid = el('div', { class: 'vp-grid' });
  root.appendChild(grid);

  // ── LEFT column ────────────────────────────────────────────────────────────
  const left = el('div', { style: 'display:flex;flex-direction:column;gap:14px' });

  // Intelligence overlay
  if (intelligence?.summary) {
    const intel = el('div', { class: 'vp-intel' });
    intel.appendChild(el('div', { class: 'vp-intel-lbl' }, '✦ What this audience is searching for'));
    intel.appendChild(el('div', { class: 'vp-intel-text' }, intelligence.summary));
    left.appendChild(intel);
  }

  // Engage chat history
  const { panel: chatPanel, body: chatBody } = mkPanel('💬 Chat History', `${conversations.length} session${conversations.length !== 1 ? 's' : ''}`);
  if (!conversations.length) {
    chatBody.appendChild(el('div', { class: 'vp-empty' }, el('div',{class:'vp-empty-icon'},'💬'), el('div',{class:'vp-empty-title'},'No chat sessions'), el('div',{class:'vp-empty-desc'},'This visitor has not interacted with the chat widget.')));
  } else {
    conversations.forEach(conv => {
      const sessionBox = el('div', { class: 'vp-chat-session' });
      const hd = el('div', { class: 'vp-chat-session-hd' });
      const titleEl = el('div', { class: 'vp-chat-session-title' }, '💬 Session');
      if (conv.hasLead)   titleEl.appendChild(el('span', { class: 'vp-badge vp-badge-lead' }, '⭐ Lead'));
      if (conv.hasTicket) titleEl.appendChild(el('span', { class: 'vp-badge vp-badge-ticket' }, '🎫 Ticket'));
      hd.append(titleEl, el('div', { class: 'vp-chat-session-meta' }, fmtDate(conv.createdAtUtc)));
      sessionBox.appendChild(hd);

      // Load messages lazily when visible
      const messagesEl = el('div', { class: 'vp-chat-messages', style: 'display:none' });
      sessionBox.appendChild(messagesEl);
      let loaded = false;
      hd.addEventListener('click', async () => {
        const open = messagesEl.style.display !== 'none';
        messagesEl.style.display = open ? 'none' : 'flex';
        if (!open && !loaded) {
          loaded = true;
          messagesEl.appendChild(el('div', { style: 'font-size:11px;color:#94a3b8;padding:4px' }, '⏳ Loading…'));
          try {
            const msgs = await client.engage.getConversationMessages(conv.sessionId, siteId);
            messagesEl.replaceChildren();
            if (!msgs?.length) { messagesEl.appendChild(el('div',{style:'font-size:11px;color:#94a3b8'},'No messages')); return; }
            msgs.forEach(m => {
              const isUser = m.role === 'user';
              const wrap   = el('div', { style: 'display:flex;flex-direction:column;align-items:' + (isUser ? 'flex-end' : 'flex-start') });
              const bubble = el('div', { class: `vp-msg ${isUser ? 'vp-msg-user' : 'vp-msg-bot'}` }, m.content || '');
              bubble.appendChild(el('div', { class: 'vp-msg-time' }, fmtTime(m.createdAtUtc)));
              wrap.appendChild(bubble);
              messagesEl.appendChild(wrap);
            });
          } catch { messagesEl.replaceChildren(el('div',{style:'font-size:11px;color:#ef4444'},'Failed to load messages')); }
        }
      });
      chatBody.appendChild(sessionBox);
    });
  }
  left.appendChild(chatPanel);

  // Session timeline
  const { panel: tlPanel, body: tlBody } = mkPanel('📍 Page Timeline', `${timeline.length} events`);
  const tlList = el('div', { class: 'vp-timeline' });
  if (!timeline.length) {
    tlList.appendChild(el('div', { class: 'vp-empty' }, el('div',{class:'vp-empty-icon'},'📭'), el('div',{class:'vp-empty-title'},'No events yet')));
  } else {
    timeline.forEach(item => {
      const cfg  = tlCfg(item.type);
      const row  = el('div', { class: 'vp-tl-item' });
      row.appendChild(el('div', { class: `vp-tl-dot ${cfg.cls}` }, cfg.icon));
      const body = el('div', { class: 'vp-tl-body' });
      body.appendChild(el('div', { class: 'vp-tl-title' }, cfg.label));
      if (item.url)      body.appendChild(el('div', { class: 'vp-tl-path' }, normPath(item.url)));
      if (item.referrer) body.appendChild(el('div', { class: 'vp-tl-ref' }, '↗ ', normHost(item.referrer)));
      body.appendChild(el('div', { class: 'vp-tl-time' }, fmtTime(item.occurredAtUtc)));
      row.appendChild(body);
      tlList.appendChild(row);
    });
  }
  tlBody.appendChild(tlList);
  left.appendChild(tlPanel);

  // Recent sessions
  if (visitor.recentSessions?.length) {
    const { panel: sessPanel, body: sessBody } = mkPanel('🔄 Sessions', `${visitor.visitCount} total`);
    visitor.recentSessions.forEach((sess, i) => {
      const card = el('div', { class: 'vp-session' });
      const hd   = el('div', { class: 'vp-session-hd' });
      hd.append(el('div',{class:'vp-session-title'},`Session ${i+1}`), el('div',{class:'vp-session-time'},`${fmtDate(sess.firstSeenAtUtc)}`));
      card.appendChild(hd);
      const stats = el('div', { class: 'vp-session-stats' });
      stats.append(
        el('div',{class:'vp-session-stat'},'📄 ', `${sess.pagesVisited} pages`),
        el('div',{class:'vp-session-stat'},'⏱ ', fmtDur(sess.timeOnSiteSeconds)),
        el('div',{class:'vp-session-stat'},'⚡ ', `Score: ${sess.engagementScore}`)
      );
      card.appendChild(stats);
      sessBody.appendChild(card);
    });
    left.appendChild(sessPanel);
  }

  grid.appendChild(left);

  // ── RIGHT column ───────────────────────────────────────────────────────────
  const right = el('div', { style: 'display:flex;flex-direction:column;gap:14px' });

  // Identity
  const { panel: idPanel, body: idBody } = mkPanel('🪪 Identity');
  const idRows = [
    ['Email',    visitor.primaryEmail],
    ['Name',     visitor.displayName],
    ['Phone',    visitor.phone],
    ['Country',  visitor.country ? `${flag(visitor.country)} ${visitor.country}` : null],
    ['Platform', visitor.platform ? `${PLATFORM_ICON[visitor.platform] || '💻'} ${visitor.platform}` : null],
    ['Language', visitor.language],
  ];
  idRows.forEach(([lbl, val]) => {
    if (!val) return;
    const row = el('div', { class: 'vp-id-row' });
    row.append(el('div',{class:'vp-id-lbl'},lbl), el('div',{class:'vp-id-val'},val));
    idBody.appendChild(row);
  });

  if (visitor.identification) {
    const conf = Math.round((Number(visitor.identification.confidence) || 0) * 100);
    const confRow = el('div', { class: 'vp-id-row', style: 'flex-direction:column;gap:5px;align-items:flex-start' });
    confRow.appendChild(el('div',{class:'vp-id-lbl'},'Identity Confidence'));
    const cw = el('div', { class: 'vp-conf-wrap' });
    const track = el('div',{class:'vp-conf-track'}); const fill = el('div',{class:'vp-conf-fill',style:'width:0%'});
    track.appendChild(fill); setTimeout(() => { fill.style.width = `${conf}%`; }, 100);
    cw.append(track, el('span',{style:'font-size:11px;color:#94a3b8;font-family:JetBrains Mono,monospace'},`${conf}%`));
    confRow.appendChild(cw);
    idBody.appendChild(confRow);
  }
  right.appendChild(idPanel);

  // Tickets
  const { panel: tickPanel, body: tickBody } = mkPanel('🎫 Support Tickets', `${tickets.length}`);
  if (!tickets.length) {
    tickBody.appendChild(el('div',{class:'vp-empty'}, el('div',{class:'vp-empty-icon'},'🎫'), el('div',{class:'vp-empty-title'},'No tickets'), el('div',{class:'vp-empty-desc'},'Tickets created during chats appear here.')));
  } else {
    tickets.forEach(t => {
      const card = el('div', { class: 'vp-ticket' });
      const hd   = el('div', { class: 'vp-ticket-hd' });
      hd.appendChild(el('div',{class:'vp-ticket-subject'},t.subject || 'Untitled ticket'));
      const statusLbl = (t.status || 'open').toLowerCase();
      const statusCls = statusLbl === 'closed' || statusLbl === 'resolved' ? 'vp-ticket-closed' : 'vp-ticket-open';
      hd.appendChild(el('span',{class:`vp-ticket-status ${statusCls}`},t.status || 'Open'));
      card.appendChild(hd);
      card.appendChild(el('div',{class:'vp-ticket-meta'}, fmtDate(t.createdAtUtc)));
      tickBody.appendChild(card);
    });
  }
  right.appendChild(tickPanel);

  // Activity stats
  const { panel: statsPanel, body: statsBody } = mkPanel('📊 Activity');
  [
    ['First Visit',  fmtDate(visitor.firstSeenAtUtc)],
    ['Last Seen',    fmtAgo(visitor.lastSeenAtUtc)],
    ['Total Visits', String(visitor.visitCount || 0)],
    ['Pages Viewed', String(visitor.totalPagesVisited || 0)],
    ['Chats',        String(conversations.length)],
    ['Tickets',      String(tickets.length)],
  ].forEach(([lbl, val]) => {
    const row = el('div', { class: 'vp-id-row' });
    row.append(el('div',{class:'vp-id-lbl'},lbl), el('div',{class:'vp-id-val'},val));
    statsBody.appendChild(row);
  });
  right.appendChild(statsPanel);

  grid.appendChild(right);
};
