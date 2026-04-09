/**
 * leads.js — Intentify Leads Page
 * Phase 4: Pipeline kanban, lead detail panel with visitor journey,
 *          intelligence overlay, CSV export, stage progression
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

const fmtDate  = v => { if (!v) return '—'; const d = new Date(v); return isNaN(d) ? '—' : d.toLocaleDateString('en-GB', { day:'numeric', month:'short', year:'numeric' }); };
const fmtAgo   = v => { if (!v) return '—'; const m = Math.floor((Date.now()-new Date(v))/60000); if (m<1) return 'just now'; if (m<60) return `${m}m ago`; const h=Math.floor(m/60); if (h<24) return `${h}h ago`; return `${Math.floor(h/24)}d ago`; };
const normPath = url => { if (!url) return '—'; try { const u=new URL(url); return u.pathname+u.search||'/'; } catch { return url; } };
const getSiteId = s => s?.siteId || s?.id || '';
const SITE_KEY  = 'intentify.selectedSiteId';
const loadSiteId = () => { try { return localStorage.getItem(SITE_KEY)||''; } catch { return ''; } };
const saveSiteId = id => { try { id ? localStorage.setItem(SITE_KEY,id) : localStorage.removeItem(SITE_KEY); } catch {} };

const STAGES = [
  { key: 'new',        label: '🆕 New',        color: '#6366f1', light: '#eef2ff', border: '#c7d2fe' },
  { key: 'evaluating', label: '🔍 Evaluating', color: '#f59e0b', light: '#fef3c7', border: '#fcd34d' },
  { key: 'deciding',   label: '⚖️ Deciding',  color: '#3b82f6', light: '#dbeafe', border: '#93c5fd' },
  { key: 'won',        label: '🏆 Won',        color: '#10b981', light: '#d1fae5', border: '#6ee7b7' },
  { key: 'lost',       label: '❌ Lost',       color: '#ef4444', light: '#fee2e2', border: '#fca5a5' },
];
const stageCfg = key => STAGES.find(s => (key||'').toLowerCase().includes(s.key.replace('🆕 ','').replace('🔍 ','').replace('⚖️ ','').replace('🏆 ','').replace('❌ ','').toLowerCase())) || STAGES[0];
// Simpler key match:
const getStage = opp => {
  if (!opp) return STAGES[0];
  const l = opp.toLowerCase();
  if (l.includes('won') || l.includes('closed')) return STAGES[3];
  if (l.includes('lost')) return STAGES[4];
  if (l.includes('decid')) return STAGES[2];
  if (l.includes('evaluat')) return STAGES[1];
  return STAGES[0];
};

// ─── Styles ───────────────────────────────────────────────────────────────────

const injectStyles = () => {
  if (document.getElementById('_leads_css')) return;
  const s = document.createElement('style');
  s.id = '_leads_css';
  s.textContent = `
@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap');
.l-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;gap:20px;width:100%;max-width:1280px;padding-bottom:60px}
/* Hero */
.l-hero{background:linear-gradient(135deg,#0f172a,#1e293b);border-radius:16px;padding:26px 34px;position:relative;overflow:hidden}
.l-hero::before{content:'';position:absolute;top:-20px;right:-20px;width:160px;height:160px;background:radial-gradient(circle,rgba(99,102,241,.2) 0%,transparent 70%);pointer-events:none}
.l-hero-title{font-size:22px;font-weight:700;color:#f8fafc;letter-spacing:-.02em;margin-bottom:4px}
.l-hero-sub{font-size:12.5px;color:#64748b;margin-bottom:18px}
.l-hero-stats{display:flex;gap:28px;flex-wrap:wrap}
.l-stat-val{font-family:'JetBrains Mono',monospace;font-size:24px;font-weight:700;color:#f1f5f9;line-height:1}
.l-stat-lbl{font-size:10px;color:#475569;text-transform:uppercase;letter-spacing:.07em;margin-top:3px}
/* Controls */
.l-controls{display:flex;align-items:center;gap:10px;flex-wrap:wrap}
.l-select{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;color:#1e293b;background:#fff;border:1px solid #e2e8f0;border-radius:8px;padding:7px 11px;outline:none}
.l-select:focus{border-color:#6366f1;box-shadow:0 0 0 3px rgba(99,102,241,.1)}
.l-input{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;color:#1e293b;background:#fff;border:1px solid #e2e8f0;border-radius:8px;padding:7px 11px;outline:none;min-width:180px}
.l-input:focus{border-color:#6366f1;box-shadow:0 0 0 3px rgba(99,102,241,.1)}
.l-btn{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;font-weight:600;padding:7px 16px;border-radius:8px;border:none;cursor:pointer;transition:all .14s;display:inline-flex;align-items:center;gap:5px;white-space:nowrap}
.l-btn-primary{background:#6366f1;color:#fff}
.l-btn-primary:hover{background:#4f46e5;transform:translateY(-1px);box-shadow:0 4px 12px rgba(99,102,241,.25)}
.l-btn-outline{background:#fff;color:#64748b;border:1px solid #e2e8f0}
.l-btn-outline:hover{background:#f8fafc;color:#1e293b}
.l-btn-sm{padding:5px 12px;font-size:11.5px}
.l-view-toggle{display:flex;background:#f1f5f9;border-radius:8px;padding:3px;gap:2px}
.l-view-btn{padding:5px 12px;border-radius:6px;border:none;font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:12px;font-weight:500;color:#64748b;cursor:pointer;transition:all .12s}
.l-view-btn.active{background:#fff;color:#6366f1;font-weight:700;box-shadow:0 1px 3px rgba(0,0,0,.08)}
/* Pipeline */
.l-pipeline{display:grid;grid-template-columns:repeat(5,1fr);gap:12px;align-items:start}
@media(max-width:900px){.l-pipeline{grid-template-columns:repeat(2,1fr)}}
.l-col{background:#f8fafc;border:1px solid #e2e8f0;border-radius:12px;overflow:hidden}
.l-col-hd{padding:10px 14px;display:flex;align-items:center;justify-content:space-between;border-bottom:1px solid #e2e8f0}
.l-col-label{font-size:12px;font-weight:700;color:#1e293b}
.l-col-count{font-family:'JetBrains Mono',monospace;font-size:10.5px;font-weight:700;padding:2px 7px;border-radius:999px}
.l-col-body{padding:10px;display:flex;flex-direction:column;gap:8px;min-height:120px}
/* Lead cards */
.l-card{background:#fff;border:1px solid #e2e8f0;border-radius:10px;padding:12px 14px;cursor:pointer;transition:box-shadow .15s,transform .15s;position:relative}
.l-card:hover{box-shadow:0 4px 14px rgba(0,0,0,.08);transform:translateY(-1px)}
.l-card.active{border-color:#6366f1;box-shadow:0 0 0 2px rgba(99,102,241,.15)}
.l-card-accent{position:absolute;top:0;left:0;bottom:0;width:3px;border-radius:10px 0 0 10px}
.l-card-name{font-size:12.5px;font-weight:700;color:#1e293b;margin-bottom:2px;padding-left:8px}
.l-card-email{font-size:10.5px;color:#94a3b8;font-family:'JetBrains Mono',monospace;padding-left:8px;margin-bottom:6px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.l-card-meta{display:flex;gap:6px;flex-wrap:wrap;padding-left:8px}
.l-pill{display:inline-flex;align-items:center;gap:3px;padding:2px 7px;border-radius:999px;font-size:9.5px;font-weight:700;border:1px solid}
/* Table */
.l-table-wrap{background:#fff;border:1px solid #e2e8f0;border-radius:12px;overflow:hidden}
.l-table{width:100%;border-collapse:collapse;font-size:12.5px}
.l-table thead th{background:#f8fafc;padding:9px 16px;text-align:left;font-size:9.5px;font-weight:700;text-transform:uppercase;letter-spacing:.07em;color:#94a3b8;border-bottom:1px solid #e2e8f0;white-space:nowrap}
.l-table tbody td{padding:12px 16px;border-bottom:1px solid #f1f5f9;color:#334155;vertical-align:middle}
.l-table tbody tr:last-child td{border-bottom:none}
.l-table tbody tr:hover{background:#fafbff;cursor:pointer}
.l-table tbody tr.active{background:#eef2ff}
/* Detail panel */
.l-layout{display:grid;grid-template-columns:1fr 380px;gap:16px;align-items:start}
@media(max-width:1000px){.l-layout{grid-template-columns:1fr}}
.l-detail{background:#fff;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden}
.l-detail-hd{display:flex;align-items:flex-start;gap:14px;padding:18px 20px;border-bottom:1px solid #f1f5f9}
.l-detail-avatar{width:46px;height:46px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:18px;font-weight:700;flex-shrink:0;background:linear-gradient(135deg,#6366f1,#8b5cf6);color:#fff}
.l-detail-name{font-size:16px;font-weight:700;color:#0f172a;margin-bottom:2px}
.l-detail-email{font-size:11.5px;color:#94a3b8;font-family:'JetBrains Mono',monospace;margin-bottom:8px}
.l-detail-chips{display:flex;gap:6px;flex-wrap:wrap}
.l-detail-body{padding:16px 20px;display:flex;flex-direction:column;gap:14px}
/* Rows */
.l-row{display:flex;align-items:flex-start;justify-content:space-between;gap:8px;padding:7px 0;border-bottom:1px solid #f8fafc}
.l-row:last-child{border-bottom:none}
.l-row-lbl{font-size:10.5px;color:#94a3b8;text-transform:uppercase;letter-spacing:.05em;font-weight:600;flex-shrink:0;padding-top:1px}
.l-row-val{font-size:12px;color:#1e293b;text-align:right;font-family:'JetBrains Mono',monospace;word-break:break-all}
/* Stage selector */
.l-stages{display:flex;gap:6px;flex-wrap:wrap;padding:12px 20px;border-bottom:1px solid #f1f5f9}
.l-stage-btn{font-size:11px;font-weight:700;padding:4px 10px;border-radius:999px;border:1px solid;cursor:pointer;transition:all .12s}
/* Journey timeline */
.l-journey{max-height:260px;overflow-y:auto;display:flex;flex-direction:column}
.l-journey-item{display:flex;gap:10px;padding:7px 0;position:relative}
.l-journey-item::before{content:'';position:absolute;left:14px;top:26px;bottom:-7px;width:1px;background:#e2e8f0}
.l-journey-item:last-child::before{display:none}
.l-journey-dot{width:28px;height:28px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:12px;flex-shrink:0;z-index:1;border:2px solid #e2e8f0;background:#f1f5f9}
.l-journey-body{flex:1;min-width:0;padding-top:2px}
.l-journey-title{font-size:11.5px;font-weight:600;color:#1e293b}
.l-journey-sub{font-size:10.5px;color:#6366f1;font-family:'JetBrains Mono',monospace;word-break:break-all;margin-top:1px}
.l-journey-time{font-size:9.5px;color:#94a3b8;margin-top:1px}
/* Intel box */
.l-intel{background:linear-gradient(135deg,#0f172a,#1e1b4b);border-radius:10px;padding:14px 16px}
.l-intel-lbl{font-size:10px;font-weight:700;letter-spacing:.1em;text-transform:uppercase;color:#818cf8;margin-bottom:7px}
.l-intel-text{font-size:12px;line-height:1.65;color:#e2e8f0}
/* Empty */
.l-empty{text-align:center;padding:40px 20px;display:flex;flex-direction:column;align-items:center;gap:8px}
.l-empty-icon{font-size:36px;opacity:.3}
.l-empty-title{font-size:14px;font-weight:600;color:#334155}
.l-empty-desc{font-size:12px;color:#94a3b8;max-width:260px;line-height:1.6}
/* Panel section */
.l-section-title{font-size:11.5px;font-weight:700;color:#0f172a;margin-bottom:10px;display:flex;align-items:center;gap:6px}
/* Drag-and-drop */
.l-col-drag-over{border:2px dashed #6366f1!important;background:#eef2ff!important}
.l-col-drag-over .l-col-body{background:#eef2ff}
.l-card[draggable=true]{cursor:grab}
.l-card[draggable=true]:active{cursor:grabbing}
/* Kanban card enhancements */
.l-card-handle{color:#cbd5e1;font-size:15px;line-height:1.2;user-select:none;flex-shrink:0;margin-top:1px}
.l-card-preview{font-size:11px;color:#64748b;line-height:1.5;margin:3px 0 5px;overflow:hidden;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical}
/* Summary bar */
.l-summary-bar{font-size:12px;color:#64748b;padding:2px 0 10px;display:flex;gap:12px;align-items:center}
.l-summary-bar strong{color:#1e293b}
  `;
  document.head.appendChild(s);
};

// ─── Main export ──────────────────────────────────────────────────────────────

export const renderLeadsView = (container, { apiClient, toast } = {}) => {
  injectStyles();
  const client   = apiClient || createApiClient();
  const notifier = toast     || createToastManager();

  const state = {
    sites: [], siteId: loadSiteId(), leads: [], filtered: [],
    selected: null, view: 'pipeline', search: '', page: 1,
    visitor: null, timeline: [], intelligence: null,
  };

  const root = el('div', { class: 'l-root' });
  container.appendChild(root);

  // ── Hero ───────────────────────────────────────────────────────────────────
  const hero = el('div', { class: 'l-hero' });
  hero.appendChild(el('div', { class: 'l-hero-title' }, '⭐ Lead Pipeline'));
  hero.appendChild(el('div', { class: 'l-hero-sub' }, 'All leads captured from visitor conversations, with journey context'));
  const heroStats = el('div', { class: 'l-hero-stats' });
  const mkStat = lbl => { const w=el('div',{}); const v=el('div',{class:'l-stat-val'},'—'); w.append(v,el('div',{class:'l-stat-lbl'},lbl)); heroStats.appendChild(w); return v; };
  const hTotal = mkStat('Total Leads');
  const hNew   = mkStat('New This Week');
  const hWon   = mkStat('Won');
  hero.appendChild(heroStats);
  root.appendChild(hero);

  // ── Controls ───────────────────────────────────────────────────────────────
  const controls = el('div', { class: 'l-controls' });
  const siteSelect = el('select', { class: 'l-select' }, el('option',{value:''},'Loading sites…'));
  const searchInput = el('input', { class: 'l-input', placeholder: '🔍 Search name or email…' });
  const exportBtn   = el('button', { class: 'l-btn l-btn-outline l-btn-sm' }, '⬇ Export CSV');
  const viewToggle  = el('div', { class: 'l-view-toggle' });
  const pipelineBtn = el('button', { class: 'l-view-btn active' }, '⊞ Pipeline');
  const tableBtn    = el('button', { class: 'l-view-btn' }, '≡ Table');
  viewToggle.append(pipelineBtn, tableBtn);
  controls.append(siteSelect, searchInput, exportBtn, viewToggle);
  root.appendChild(controls);

  // ── Main layout ────────────────────────────────────────────────────────────
  const layout = el('div', { class: 'l-layout' });
  const mainCol = el('div', {});
  const detailCol = el('div', {});
  layout.append(mainCol, detailCol);
  root.appendChild(layout);

  // ── Detail panel ───────────────────────────────────────────────────────────
  const renderDetail = async (lead) => {
    detailCol.replaceChildren();
    if (!lead) return;

    const panel = el('div', { class: 'l-detail' });
    const stage = getStage(lead.opportunityLabel);

    // Header
    const hd = el('div', { class: 'l-detail-hd' });
    const initials = (lead.displayName || lead.primaryEmail || '?')[0].toUpperCase();
    hd.appendChild(el('div', { class: 'l-detail-avatar' }, initials));
    const hdbody = el('div', { style: 'flex:1;min-width:0' });
    hdbody.appendChild(el('div', { class: 'l-detail-name' }, lead.displayName || 'Anonymous'));
    hdbody.appendChild(el('div', { class: 'l-detail-email' }, lead.primaryEmail || '—'));
    const chips = el('div', { class: 'l-detail-chips' });
    chips.appendChild(el('span', { class: 'l-pill', style: `background:${stage.light};color:${stage.color};border-color:${stage.border}` }, stage.label));
    if (lead.intentScore) chips.appendChild(el('span', { class: 'l-pill', style: 'background:#f1f5f9;color:#475569;border-color:#e2e8f0' }, `Score: ${lead.intentScore}`));
    hdbody.appendChild(chips);
    hd.appendChild(hdbody);
    panel.appendChild(hd);

    // Stage progression buttons
    const stageBtns = el('div', { class: 'l-stages' });
    STAGES.forEach(s => {
      const btn = el('button', { class: 'l-stage-btn', style: `background:${s===stage?s.light:'#f8fafc'};color:${s===stage?s.color:'#64748b'};border-color:${s===stage?s.border:'#e2e8f0'}` }, s.label);
      btn.addEventListener('click', async () => {
        try {
          await client.leads.tagStage(lead.id || lead.leadId, s.key);
          lead.opportunityLabel = s.key;
          notifier.show({ message: `Moved to ${s.label}`, variant: 'success' });
          renderDetail(lead);
          renderMainView();
        } catch (err) { notifier.show({ message: mapApiError(err).message, variant: 'danger' }); }
      });
      stageBtns.appendChild(btn);
    });
    panel.appendChild(stageBtns);

    // Body
    const body = el('div', { class: 'l-detail-body' });

    // Contact details
    const detailRows = el('div', {});
    const addRow = (lbl, val) => {
      if (!val) return;
      const r = el('div', { class: 'l-row' });
      r.append(el('div',{class:'l-row-lbl'},lbl), el('div',{class:'l-row-val'},val));
      detailRows.appendChild(r);
    };
    addRow('Email',   lead.primaryEmail);
    addRow('Phone',   lead.phone);
    addRow('Contact', lead.preferredContactMethod);
    addRow('Intent',  lead.opportunityLabel);
    addRow('Score',   lead.intentScore?.toString());
    addRow('Captured',fmtDate(lead.createdAtUtc));
    addRow('Updated', fmtAgo(lead.updatedAtUtc));
    body.appendChild(detailRows);

    // Conversation summary
    if (lead.conversationSummary) {
      const summaryBox = el('div', {});
      summaryBox.appendChild(el('div', { class: 'l-section-title' }, '💬 Conversation Summary'));
      summaryBox.appendChild(el('div', { style: 'font-size:12px;color:#475569;line-height:1.65;background:#f8fafc;border-radius:8px;padding:10px 12px' }, lead.conversationSummary));
      body.appendChild(summaryBox);
    }
    if (lead.suggestedFollowUp) {
      body.appendChild(el('div', { style: 'background:#fef3c7;border:1px solid #fcd34d;border-radius:8px;padding:10px 12px;font-size:12px;color:#92400e;line-height:1.6' }, '💡 ', lead.suggestedFollowUp));
    }

    // AI follow-up email generator
    {
      const aiBox = el('div', {});
      aiBox.appendChild(el('div', { class: 'l-section-title' }, '✉ AI Follow-up Email'));

      const genBtn = el('button', { class: 'l-btn l-btn-outline l-btn-sm' }, '✉ Generate follow-up email');
      const statusEl = el('div', { style: 'font-size:11.5px;color:#94a3b8;margin-top:6px;display:none' }, '⏳ Generating personalised email…');
      const emailWrap = el('div', { style: 'display:none;margin-top:10px' });
      const emailTA = el('textarea', { style: 'width:100%;min-height:200px;font-size:12.5px;font-family:inherit;color:#1e293b;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:10px 12px;resize:vertical;outline:none;box-sizing:border-box;line-height:1.65' });
      const btnRow = el('div', { style: 'display:flex;gap:8px;margin-top:8px' });
      const copyBtn = el('button', { class: 'l-btn l-btn-outline l-btn-sm' }, '📋 Copy email');
      const closeBtn = el('button', { class: 'l-btn l-btn-outline l-btn-sm' }, 'Close');

      copyBtn.addEventListener('click', () => {
        navigator.clipboard.writeText(emailTA.value).then(() => {
          copyBtn.textContent = '✓ Copied!';
          setTimeout(() => { copyBtn.textContent = '📋 Copy email'; }, 2000);
        });
      });
      closeBtn.addEventListener('click', () => {
        emailWrap.style.display = 'none';
        genBtn.style.display = '';
      });

      btnRow.append(copyBtn, closeBtn);
      emailWrap.append(emailTA, btnRow);

      genBtn.addEventListener('click', async () => {
        genBtn.disabled = true;
        statusEl.style.display = '';
        emailWrap.style.display = 'none';
        try {
          const result = await client.engage.generateFollowUp(
            lead.leadId || lead.id,
            lead.conversationSummary,
            lead.displayName,
            lead.primaryEmail,
            state.siteId
          );
          emailTA.value = result?.emailBody || '';
          emailWrap.style.display = '';
          genBtn.style.display = 'none';
        } catch (err) {
          notifier.show({ message: mapApiError(err).message, variant: 'danger' });
        } finally {
          genBtn.disabled = false;
          statusEl.style.display = 'none';
        }
      });

      aiBox.append(genBtn, statusEl, emailWrap);
      body.appendChild(aiBox);
    }

    // Visitor journey — load async
    if (lead.linkedVisitorId) {
      const journeyBox = el('div', {});
      journeyBox.appendChild(el('div', { class: 'l-section-title' }, '📍 Pre-Conversion Journey'));
      const journeyList = el('div', { class: 'l-journey', style: 'font-size:12px;color:#94a3b8' }, '⏳ Loading…');
      journeyBox.appendChild(journeyList);
      body.appendChild(journeyBox);

      client.visitors.timeline(lead.linkedVisitorId, 50, state.siteId).then(items => {
        journeyList.replaceChildren();
        if (!items?.length) { journeyList.appendChild(el('div',{style:'color:#94a3b8;font-size:11.5px'},'No page history')); return; }
        items.forEach(item => {
          const row = el('div', { class: 'l-journey-item' });
          const icon = item.type === 'pageview' || item.type === 'page_view' ? '👁️' : item.type === 'lead_capture' ? '⭐' : '⚡';
          row.appendChild(el('div', { class: 'l-journey-dot' }, icon));
          const body2 = el('div', { class: 'l-journey-body' });
          body2.appendChild(el('div', { class: 'l-journey-title' }, item.type === 'lead_capture' ? 'Lead captured' : 'Page view'));
          if (item.url) body2.appendChild(el('div', { class: 'l-journey-sub' }, normPath(item.url)));
          body2.appendChild(el('div', { class: 'l-journey-time' }, fmtDate(item.occurredAtUtc)));
          row.appendChild(body2);
          journeyList.appendChild(row);
        });
      }).catch(() => { journeyList.replaceChildren(el('div',{style:'color:#94a3b8;font-size:11.5px'},'Could not load journey')); });
    }

    // Intelligence overlay — load async
    if (state.siteId) {
      client.intelligence.siteSummary({ siteId: state.siteId, timeWindow: '7d' }).then(intel => {
        if (!intel?.summary) return;
        const intelBox = el('div', {});
        intelBox.appendChild(el('div', { class: 'l-section-title' }, '📡 What their audience is searching for'));
        const box = el('div', { class: 'l-intel' });
        box.appendChild(el('div', { class: 'l-intel-lbl' }, '✦ Market Intelligence'));
        box.appendChild(el('div', { class: 'l-intel-text' }, intel.summary));
        intelBox.appendChild(box);
        body.appendChild(intelBox);
      }).catch(() => {});
    }

    panel.appendChild(body);
    detailCol.appendChild(panel);
  };

  // ── Pipeline view ──────────────────────────────────────────────────────────
  const renderPipeline = () => {
    mainCol.replaceChildren();

    // ── Summary bar ──
    const total      = state.filtered.length;
    const inPipeline = state.filtered.filter(l => { const k = getStage(l.opportunityLabel).key; return k !== 'won' && k !== 'lost'; }).length;
    const wonCount   = state.filtered.filter(l => getStage(l.opportunityLabel).key === 'won').length;
    mainCol.appendChild(el('div', { class: 'l-summary-bar' },
      el('strong', {}, String(total)), ` total lead${total !== 1 ? 's' : ''}`,
      el('span', { style: 'color:#e2e8f0' }, '·'),
      el('strong', {}, String(inPipeline)), ' in pipeline',
      el('span', { style: 'color:#e2e8f0' }, '·'),
      el('strong', {}, String(wonCount)), ' won'
    ));

    const pipeline = el('div', { class: 'l-pipeline' });

    STAGES.forEach(stage => {
      const stageLeads = state.filtered.filter(l => getStage(l.opportunityLabel).key === stage.key);
      const col  = el('div', { class: 'l-col' });
      const hd   = el('div', { class: 'l-col-hd' });
      hd.appendChild(el('div', { class: 'l-col-label' }, stage.label));
      hd.appendChild(el('span', { class: 'l-col-count', style: `background:${stage.light};color:${stage.color}` }, String(stageLeads.length)));
      col.appendChild(hd);

      const body = el('div', { class: 'l-col-body' });

      // ── Drag-and-drop: column as drop target ──
      body.addEventListener('dragover', e => { e.preventDefault(); col.classList.add('l-col-drag-over'); });
      col.addEventListener('dragleave', e => { if (!col.contains(e.relatedTarget)) col.classList.remove('l-col-drag-over'); });
      col.addEventListener('drop', async e => {
        e.preventDefault();
        col.classList.remove('l-col-drag-over');
        const leadId = e.dataTransfer.getData('text/plain');
        if (!leadId) return;
        const lead = state.leads.find(l => String(l.leadId || l.id) === leadId);
        if (!lead || getStage(lead.opportunityLabel).key === stage.key) return;
        // Optimistic update
        lead.opportunityLabel = stage.key;
        state.selected = lead;
        renderPipeline();
        renderDetail(lead);
        // Sync to API
        try { await client.leads.tagStage(leadId, stage.key); }
        catch (err) { notifier.show({ message: mapApiError(err).message, variant: 'danger' }); await loadLeads(); }
      });

      if (!stageLeads.length) {
        body.appendChild(el('div', { style: 'color:#94a3b8;font-size:11px;text-align:center;padding:16px 0' }, 'No leads'));
      } else {
        stageLeads.forEach(lead => {
          const leadId = String(lead.leadId || lead.id);
          const card   = el('div', { class: `l-card${state.selected?.id === lead.id ? ' active' : ''}` });
          card.setAttribute('draggable', 'true');
          card.addEventListener('dragstart', e => {
            e.dataTransfer.setData('text/plain', leadId);
            e.dataTransfer.effectAllowed = 'move';
            setTimeout(() => { card.style.opacity = '0.45'; }, 0);
          });
          card.addEventListener('dragend', () => { card.style.opacity = ''; });

          card.appendChild(el('div', { class: 'l-card-accent', style: `background:${stage.color}` }));
          const row  = el('div', { style: 'display:flex;align-items:flex-start;gap:6px;padding-left:8px' });
          const info = el('div', { style: 'flex:1;min-width:0' });

          info.appendChild(el('div', { class: 'l-card-name', style: 'padding-left:0' }, lead.displayName || 'Anonymous'));
          if (lead.conversationSummary) {
            const txt = lead.conversationSummary.length > 60 ? lead.conversationSummary.slice(0,60)+'…' : lead.conversationSummary;
            info.appendChild(el('div', { class: 'l-card-preview' }, txt));
          }
          const meta = el('div', { class: 'l-card-meta', style: 'padding-left:0' });
          meta.appendChild(el('span', { class: 'l-pill', style: 'background:#f8fafc;color:#94a3b8;border-color:#e2e8f0' }, fmtAgo(lead.updatedAtUtc || lead.createdAtUtc)));
          info.appendChild(meta);

          row.append(el('span', { class: 'l-card-handle' }, '⠿'), info);
          card.appendChild(row);
          card.addEventListener('click', () => { state.selected = lead; renderPipeline(); renderDetail(lead); });
          body.appendChild(card);
        });
      }
      col.appendChild(body);
      pipeline.appendChild(col);
    });

    mainCol.appendChild(pipeline);
  };

  // ── Table view ─────────────────────────────────────────────────────────────
  const renderTable = () => {
    mainCol.replaceChildren();
    if (!state.filtered.length) {
      mainCol.appendChild(el('div', { class: 'l-empty' }, el('div',{class:'l-empty-icon'},'⭐'), el('div',{class:'l-empty-title'},'No leads yet'), el('div',{class:'l-empty-desc'},'Leads are captured when visitors chat and share contact details.')));
      return;
    }
    const tableWrap = el('div', { class: 'l-table-wrap' });
    const table = el('table', { class: 'l-table' });
    table.appendChild(el('thead',{},el('tr',{}, ...['Name','Email','Stage','Score','Intent','Captured'].map(c=>el('th',{},c)))));
    const tbody = el('tbody',{});
    state.filtered.forEach(lead => {
      const tr = el('tr', { class: state.selected?.id === lead.id ? 'active' : '' });
      const stage = getStage(lead.opportunityLabel);
      tr.append(
        el('td',{style:'font-weight:600;color:#1e293b'},lead.displayName||'Anonymous'),
        el('td',{style:'font-family:JetBrains Mono,monospace;font-size:11px;color:#64748b'},lead.primaryEmail||'—'),
        el('td',{},el('span',{class:'l-pill',style:`background:${stage.light};color:${stage.color};border-color:${stage.border}`},stage.label)),
        el('td',{style:'font-family:JetBrains Mono,monospace'},lead.intentScore ? String(lead.intentScore) : '—'),
        el('td',{style:'font-size:11.5px;color:#475569;max-width:180px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap'},lead.opportunityLabel||'—'),
        el('td',{style:'font-size:11.5px;color:#94a3b8'},fmtDate(lead.createdAtUtc))
      );
      tr.addEventListener('click', () => { state.selected = lead; renderTable(); renderDetail(lead); });
      tbody.appendChild(tr);
    });
    table.appendChild(tbody);
    tableWrap.appendChild(table);
    mainCol.appendChild(tableWrap);
  };

  const renderMainView = () => state.view === 'pipeline' ? renderPipeline() : renderTable();

  const applyFilter = () => {
    const q = state.search.toLowerCase();
    state.filtered = q
      ? state.leads.filter(l => (l.displayName||'').toLowerCase().includes(q) || (l.primaryEmail||'').toLowerCase().includes(q))
      : [...state.leads];
    renderMainView();
    // Update hero stats
    hTotal.textContent = String(state.leads.length);
    const weekAgo = new Date(); weekAgo.setDate(weekAgo.getDate()-7);
    hNew.textContent = String(state.leads.filter(l => new Date(l.createdAtUtc) >= weekAgo).length);
    hWon.textContent = String(state.leads.filter(l => getStage(l.opportunityLabel).key === 'won').length);
  };

  // ── CSV export ─────────────────────────────────────────────────────────────
  const exportCsv = () => {
    const rows = [['Name','Email','Phone','Stage','Score','Intent','Summary','Captured']];
    state.leads.forEach(l => rows.push([
      l.displayName||'', l.primaryEmail||'', l.phone||'',
      getStage(l.opportunityLabel).label, l.intentScore||'',
      l.opportunityLabel||'', (l.conversationSummary||'').replace(/,/g,'；'),
      fmtDate(l.createdAtUtc)
    ]));
    const csv = rows.map(r => r.map(c => `"${c}"`).join(',')).join('\n');
    const a = document.createElement('a');
    a.href = 'data:text/csv;charset=utf-8,'+encodeURIComponent(csv);
    a.download = `intentify-leads-${new Date().toISOString().slice(0,10)}.csv`;
    a.click();
  };

  // ── API ────────────────────────────────────────────────────────────────────
  const loadLeads = async () => {
    if (!state.siteId) return;
    try {
      const data = await client.leads.list(state.siteId, 1, 200);
      state.leads = Array.isArray(data) ? data : [];
      applyFilter();
    } catch (err) {
      notifier.show({ message: mapApiError(err).message, variant: 'danger' });
    }
  };

  const syncSites = sites => {
    siteSelect.innerHTML = '';
    if (!sites.length) { siteSelect.appendChild(el('option',{value:''},'No sites')); return; }
    sites.forEach(s => {
      const id = getSiteId(s); const opt = el('option',{value:id},s.domain||id);
      if (id === state.siteId) opt.selected = true;
      siteSelect.appendChild(opt);
    });
    if (!state.siteId || !sites.find(s => getSiteId(s) === state.siteId))
      state.siteId = getSiteId(sites[0]);
    siteSelect.value = state.siteId;
  };

  // ── Wire events (after all functions declared) ─────────────────────────────
  siteSelect.addEventListener('change', () => { state.siteId = siteSelect.value; saveSiteId(state.siteId); loadLeads(); });
  searchInput.addEventListener('input', () => { state.search = searchInput.value; applyFilter(); });
  exportBtn.addEventListener('click', exportCsv);
  pipelineBtn.addEventListener('click', () => { state.view='pipeline'; pipelineBtn.classList.add('active'); tableBtn.classList.remove('active'); renderMainView(); });
  tableBtn.addEventListener('click', () => { state.view='table'; tableBtn.classList.add('active'); pipelineBtn.classList.remove('active'); renderMainView(); });

  // ── Init ───────────────────────────────────────────────────────────────────
  const init = async () => {
    try {
      const sites = await client.sites.list();
      state.sites = Array.isArray(sites) ? sites : [];
      syncSites(state.sites);
      await loadLeads();
    } catch (err) { notifier.show({ message: 'Could not load sites.', variant: 'danger' }); }
  };

  init();
};
