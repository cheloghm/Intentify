/**
 * tickets.js — Intentify Tickets
 * Revamped: dark hero, panel layout, slide-in detail view.
 * All original: status transition, 7-section description, contact fields,
 * pagination, status filter, site filter.
 */

import { createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

// ─── Helpers (original logic preserved) ──────────────────────────────────────

const getSiteId  = s => s?.siteId || s?.id || '';
const getTicketId = t => {
  const raw = t?.id || t?.ticketId;
  if (typeof raw === 'string') return raw;
  if (raw && typeof raw === 'object') return raw.value || raw.id || raw._id || '';
  return typeof t === 'string' ? t : '';
};
const fmtDate = v => { if (!v) return '—'; const d=new Date(v); return isNaN(d)?'—':d.toLocaleDateString('en-GB',{day:'numeric',month:'short',year:'numeric'}); };

const normStatus = s => String(s||'').toLowerCase().replace(/[^a-z]/g,'');
const statusLabel = s => ({ inprogress:'In Progress', open:'Open', closed:'Closed', resolved:'Resolved' }[normStatus(s)] || s || '—');
const statusPill  = s => {
  const n = normStatus(s);
  if (n==='open')                    return 'tk-pill-amber';
  if (n==='inprogress')              return 'tk-pill-blue';
  if (n==='closed'||n==='resolved')  return 'tk-pill-green';
  return 'tk-pill-gray';
};

const SECTION_LABELS = ['Visitor Overview','What They Need','Key Details',
  'What They Have Not Considered','Their Concerns','Recommended Next Step','Conversation Tone'];
const parseDescription = desc => {
  if (!desc) return [];
  return desc.split(/\n?\d+\.\s+/).filter(Boolean)
    .map((content, i) => ({ label: SECTION_LABELS[i] || `Section ${i+1}`, content: content.trim() }));
};

const PAGE_SIZE = 15;

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

// ─── Styles ───────────────────────────────────────────────────────────────────

const injectStyles = () => {
  if (document.getElementById('_tk_css')) return;
  const s = document.createElement('style');
  s.id = '_tk_css';
  s.textContent = `
@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap');
.tk-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;gap:20px;width:100%;max-width:1100px;padding-bottom:60px}
/* Hero */
.tk-hero{background:linear-gradient(135deg,#0f172a 0%,#1e293b 100%);border-radius:16px;padding:28px 36px;position:relative;overflow:hidden}
.tk-hero::before{content:'';position:absolute;top:-30px;right:-30px;width:180px;height:180px;background:radial-gradient(circle,rgba(239,68,68,.15) 0%,transparent 70%);pointer-events:none}
.tk-hero-title{font-size:24px;font-weight:700;color:#f8fafc;letter-spacing:-.02em;margin-bottom:6px}
.tk-hero-sub{font-size:13px;color:#94a3b8;margin-bottom:18px}
.tk-hero-stats{display:flex;gap:28px;flex-wrap:wrap}
.tk-stat{display:flex;flex-direction:column;gap:2px}
.tk-stat-val{font-family:'JetBrains Mono',monospace;font-size:22px;font-weight:700;color:#f1f5f9;letter-spacing:-.02em}
.tk-stat-lbl{font-size:10px;color:#64748b;text-transform:uppercase;letter-spacing:.07em}
/* Controls */
.tk-controls{display:flex;align-items:center;gap:10px;flex-wrap:wrap}
.tk-select{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;color:#1e293b;background:#fff;border:1px solid #e2e8f0;border-radius:8px;padding:7px 11px;outline:none}
.tk-select:focus{border-color:#6366f1;box-shadow:0 0 0 3px rgba(99,102,241,.1)}
/* Btn */
.tk-btn{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;font-weight:600;padding:7px 16px;border-radius:8px;border:none;cursor:pointer;transition:all .14s;display:inline-flex;align-items:center;gap:5px;white-space:nowrap}
.tk-btn-primary{background:#6366f1;color:#fff}.tk-btn-primary:hover:not(:disabled){background:#4f46e5;transform:translateY(-1px)}
.tk-btn-outline{background:#fff;color:#64748b;border:1px solid #e2e8f0}.tk-btn-outline:hover{background:#f8fafc;color:#1e293b}
.tk-btn-danger{background:#fee2e2;color:#dc2626;border:none}.tk-btn-danger:hover{background:#fecaca}
.tk-btn-sm{padding:5px 12px;font-size:12px}
/* Layout */
.tk-layout{display:grid;grid-template-columns:1fr 420px;gap:14px;align-items:start}
@media(max-width:900px){.tk-layout{grid-template-columns:1fr}}
/* Panel */
.tk-panel{background:#fff;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden}
.tk-panel-hd{display:flex;align-items:center;justify-content:space-between;padding:14px 20px;border-bottom:1px solid #f1f5f9}
.tk-panel-title{font-size:13px;font-weight:700;color:#0f172a;display:flex;align-items:center;gap:7px}
.tk-panel-meta{font-size:11px;color:#94a3b8;font-family:'JetBrains Mono',monospace}
.tk-panel-body{padding:0}
/* Table */
.tk-table-wrap{overflow:hidden}
.tk-table{width:100%;border-collapse:collapse;font-size:12.5px}
.tk-table thead th{background:#f8fafc;padding:9px 16px;text-align:left;font-size:9.5px;font-weight:700;text-transform:uppercase;letter-spacing:.07em;color:#94a3b8;border-bottom:1px solid #e2e8f0;white-space:nowrap}
.tk-table tbody td{padding:12px 16px;border-bottom:1px solid #f1f5f9;color:#334155;vertical-align:middle}
.tk-table tbody tr:last-child td{border-bottom:none}
.tk-table tbody tr:hover{background:#fafbff;cursor:pointer}
.tk-table tbody tr.tk-active{background:#eef2ff}
/* Pills */
.tk-pill{display:inline-flex;align-items:center;padding:2px 8px;border-radius:999px;font-size:10px;font-weight:700}
.tk-pill-amber{background:#fef3c7;color:#92400e}
.tk-pill-blue{background:#dbeafe;color:#1e40af}
.tk-pill-green{background:#d1fae5;color:#065f46}
.tk-pill-gray{background:#f1f5f9;color:#475569}
.tk-pill-red{background:#fee2e2;color:#dc2626}
/* Pagination */
.tk-pages{display:flex;align-items:center;justify-content:space-between;padding:12px 20px;border-top:1px solid #f1f5f9;font-size:12px;color:#94a3b8}
.tk-page-btns{display:flex;gap:5px}
/* Detail panel */
.tk-detail{background:#fff;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden}
.tk-detail-hd{display:flex;align-items:flex-start;justify-content:space-between;padding:16px 20px;border-bottom:1px solid #f1f5f9;gap:10px}
.tk-detail-subject{font-size:15px;font-weight:700;color:#0f172a;margin-bottom:4px}
.tk-detail-body{padding:16px 20px;display:flex;flex-direction:column;gap:14px;max-height:calc(100vh - 300px);overflow-y:auto}
.tk-section-lbl{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.07em;color:#6366f1;margin-bottom:5px}
.tk-section-content{font-size:13px;color:#334155;line-height:1.65;white-space:pre-wrap}
.tk-divider{height:1px;background:#f1f5f9;margin:4px 0}
.tk-contact-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:12px}
.tk-contact-lbl{font-size:10px;color:#94a3b8;text-transform:uppercase;letter-spacing:.05em;margin-bottom:2px;font-weight:600}
.tk-contact-val{font-size:12.5px;color:#334155}
.tk-followup{background:#eef2ff;border-left:3px solid #6366f1;border-radius:0 8px 8px 0;padding:12px 16px;font-size:13px;color:#334155;line-height:1.6}
.tk-status-actions{display:flex;gap:8px;flex-wrap:wrap;padding-top:4px}
/* Empty */
.tk-empty{text-align:center;padding:52px 20px;display:flex;flex-direction:column;align-items:center;gap:8px}
.tk-empty-icon{font-size:38px;opacity:.3}
.tk-empty-title{font-size:14px;font-weight:600;color:#334155}
.tk-empty-desc{font-size:12px;color:#94a3b8;max-width:280px;line-height:1.6}
.tk-skel{background:linear-gradient(90deg,#f1f5f9 25%,#e2e8f0 50%,#f1f5f9 75%);background-size:200% 100%;animation:_sh 1.4s infinite;border-radius:8px;height:48px;margin:12px 20px}
@keyframes _sh{0%{background-position:200% 0}100%{background-position:-200% 0}}
  `;
  document.head.appendChild(s);
};

// ─── Main export ──────────────────────────────────────────────────────────────

export const renderTicketsView = async (container, { apiClient, toast } = {}) => {
  injectStyles();
  const client   = apiClient || createApiClient();
  const notifier = toast     || createToastManager();
  const state    = { sites:[], siteId:'', tickets:[], filtered:[], selected:null, statusFilter:'', page:1 };

  const root = el('div', { class: 'tk-root' });
  container.appendChild(root);

  // ── Hero ───────────────────────────────────────────────────────────────────
  const hero = el('div', { class: 'tk-hero' });
  hero.appendChild(el('div',{class:'tk-hero-title'},'🎫 Tickets'));
  hero.appendChild(el('div',{class:'tk-hero-sub'},'Support escalations from Engage conversations'));
  const heroStats = el('div',{class:'tk-hero-stats'});
  const mkStat = lbl => { const w=el('div',{class:'tk-stat'}); const v=el('div',{class:'tk-stat-val'},'—'); w.append(v,el('div',{class:'tk-stat-lbl'},lbl)); heroStats.appendChild(w); return v; };
  const hTotal = mkStat('Total'); const hOpen = mkStat('Open'); const hResolved = mkStat('Resolved');
  hero.appendChild(heroStats);
  root.appendChild(hero);

  // ── Controls ───────────────────────────────────────────────────────────────
  const controls = el('div',{class:'tk-controls'});
  const siteSelect   = el('select',{class:'tk-select'},el('option',{value:''},'Loading…'));
  const statusSelect = el('select',{class:'tk-select'});
  [['','All Statuses'],['Open','Open'],['InProgress','In Progress'],['Resolved','Resolved'],['Closed','Closed']]
    .forEach(([v,l]) => statusSelect.appendChild(el('option',{value:v},l)));
  controls.append(siteSelect, statusSelect);
  root.appendChild(controls);

  // ── Layout ─────────────────────────────────────────────────────────────────
  const layout = el('div',{class:'tk-layout'});
  const leftCol  = el('div',{});
  const rightCol = el('div',{});
  layout.append(leftCol, rightCol);
  root.appendChild(layout);

  // ── Tickets panel ──────────────────────────────────────────────────────────
  const tickPanel = el('div',{class:'tk-panel'});
  const tickHd = el('div',{class:'tk-panel-hd'});
  const tickTitle = el('div',{class:'tk-panel-title'},'All Tickets');
  const tickMeta  = el('div',{class:'tk-panel-meta'},'');
  tickHd.append(tickTitle, tickMeta);
  const tickBody = el('div',{class:'tk-panel-body'});
  const tableWrap = el('div',{class:'tk-table-wrap'});
  const pagesEl   = el('div',{class:'tk-pages'});
  const pagesInfo = el('div',{});
  const pageBtns  = el('div',{class:'tk-page-btns'});
  pagesEl.append(pagesInfo, pageBtns);
  tickBody.append(tableWrap, pagesEl);
  tickPanel.append(tickHd, tickBody);
  leftCol.appendChild(tickPanel);

  // ── Detail panel (right) ──────────────────────────────────────────────────
  const detailPanel = el('div',{class:'tk-detail',style:'display:none'});
  rightCol.appendChild(detailPanel);

  // ── Render detail ──────────────────────────────────────────────────────────
  const renderDetail = async (ticket) => {
    state.selected = ticket;
    detailPanel.style.display = '';
    detailPanel.replaceChildren();

    if (!ticket) { detailPanel.style.display='none'; return; }

    // Load full ticket
    const ticketId = getTicketId(ticket);
    detailPanel.appendChild(el('div',{style:'padding:20px;color:#94a3b8;font-size:12.5px'},'⏳ Loading…'));
    let full = ticket;
    try { full = await client.tickets.getTicket(ticketId); } catch {}
    detailPanel.replaceChildren();

    const dhd = el('div',{class:'tk-detail-hd'});
    const dleft = el('div',{style:'flex:1;min-width:0'});
    dleft.appendChild(el('div',{class:'tk-detail-subject'},full.subject||'(no subject)'));
    dleft.appendChild(el('span',{class:`tk-pill ${statusPill(full.status)}`},statusLabel(full.status)));
    const closeBtn = el('button',{class:'tk-btn tk-btn-outline tk-btn-sm','@click':()=>{ state.selected=null; detailPanel.style.display='none'; renderTable(); }},'✕');
    dhd.append(dleft, closeBtn);
    detailPanel.appendChild(dhd);

    const dbody = el('div',{class:'tk-detail-body'});

    // 7-section description
    const sections = parseDescription(full.description);
    if (sections.length) {
      sections.forEach((sec, i) => {
        if (i>0) dbody.appendChild(el('div',{class:'tk-divider'}));
        dbody.appendChild(el('div',{class:'tk-section-lbl'},sec.label));
        dbody.appendChild(el('div',{class:'tk-section-content'},sec.content));
      });
    } else if (full.description) {
      dbody.appendChild(el('div',{class:'tk-section-content'},full.description));
    }

    // Contact details
    if (full.contactName || full.preferredContactMethod || full.preferredContactDetail) {
      dbody.appendChild(el('div',{class:'tk-divider'}));
      const cGrid = el('div',{class:'tk-contact-grid'});
      const mkC = (lbl,val) => { const w=el('div',{}); w.append(el('div',{class:'tk-contact-lbl'},lbl),el('div',{class:'tk-contact-val'},val||'—')); cGrid.appendChild(w); };
      mkC('Contact Name',full.contactName);
      mkC('Preferred Contact',full.preferredContactMethod);
      mkC('Detail',full.preferredContactDetail);
      dbody.appendChild(cGrid);
    }

    // Follow-up / summary
    const followText = full.suggestedFollowUp || full.conversationSummary;
    if (followText) {
      dbody.appendChild(el('div',{class:'tk-divider'}));
      dbody.appendChild(el('div',{style:'font-size:12px;font-weight:700;color:#1e293b;margin-bottom:6px'},full.suggestedFollowUp?'💡 Suggested Follow-Up':'📝 Conversation Summary'));
      dbody.appendChild(el('div',{class:'tk-followup'},followText));
    }

    // Status transitions
    const s = normStatus(full.status);
    const transitions = s==='open'?[{label:'Start Progress',to:'InProgress',cls:'tk-btn-primary'}]
      :s==='inprogress'?[{label:'Mark Resolved',to:'Resolved',cls:'tk-btn-primary'}]
      :s==='resolved'?[{label:'Mark Closed',to:'Closed',cls:'tk-btn-primary'},{label:'Reopen',to:'Open',cls:'tk-btn-outline'}]
      :s==='closed'?[{label:'Reopen',to:'Open',cls:'tk-btn-outline'}]:[];

    if (transitions.length) {
      dbody.appendChild(el('div',{class:'tk-divider'}));
      const actRow = el('div',{class:'tk-status-actions'});
      transitions.forEach(({label,to,cls}) => {
        const btn = el('button',{class:`tk-btn ${cls} tk-btn-sm`},label);
        btn.addEventListener('click', async () => {
          btn.disabled=true; btn.textContent='⏳…';
          try {
            await client.tickets.transitionTicketStatus(ticketId, to);
            const idx = state.tickets.findIndex(t=>getTicketId(t)===ticketId);
            if (idx!==-1) state.tickets[idx]={...state.tickets[idx],status:to};
            applyFilter(); updateStats();
            notifier.show({message:`Ticket marked as ${statusLabel(to)}.`,variant:'success'});
            renderDetail({...full,status:to});
          } catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); btn.disabled=false; btn.textContent=label; }
        });
        actRow.appendChild(btn);
      });
      dbody.appendChild(actRow);
    }

    detailPanel.appendChild(dbody);
  };

  // ── Render table ───────────────────────────────────────────────────────────
  const renderTable = () => {
    tableWrap.replaceChildren();
    pageBtns.replaceChildren();
    pagesInfo.textContent = '';

    if (!state.filtered.length) {
      tableWrap.appendChild(el('div',{class:'tk-empty'},el('div',{class:'tk-empty-icon'},'🎫'),el('div',{class:'tk-empty-title'},state.siteId?'No tickets found':'Select a site'),el('div',{class:'tk-empty-desc'},state.siteId?'Tickets appear when Engage conversations are escalated.':'Choose a site from the dropdown above.')));
      return;
    }

    const total   = state.filtered.length;
    const pages   = Math.max(1, Math.ceil(total/PAGE_SIZE));
    const pg      = Math.min(state.page, pages);
    const start   = (pg-1)*PAGE_SIZE;
    const slice   = state.filtered.slice(start, start+PAGE_SIZE);

    const table = el('table',{class:'tk-table'});
    table.appendChild(el('thead',{},el('tr',{}, ...['Subject','Contact','Status','Opportunity','Created'].map(c=>el('th',{},c)))));
    const tbody = el('tbody',{});
    slice.forEach(ticket => {
      const tid = getTicketId(ticket);
      const tr  = el('tr',{class:state.selected&&getTicketId(state.selected)===tid?'tk-active':''});
      tr.append(
        el('td',{style:'font-weight:600;color:#1e293b;max-width:200px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap'},ticket.subject||(ticket.description||'').slice(0,60)||'—'),
        el('td',{style:'font-size:12px;color:#64748b'},ticket.contactName||ticket.preferredContactDetail||'—'),
        el('td',{},el('span',{class:`tk-pill ${statusPill(ticket.status)}`},statusLabel(ticket.status))),
        el('td',{},ticket.opportunityLabel?el('span',{class:'tk-pill tk-pill-blue'},ticket.opportunityLabel):document.createTextNode('—')),
        el('td',{style:'font-size:11.5px;color:#94a3b8'},fmtDate(ticket.createdAtUtc))
      );
      tr.addEventListener('click', () => renderDetail(ticket));
      tbody.appendChild(tr);
    });
    table.appendChild(tbody);
    tableWrap.appendChild(table);

    tickMeta.textContent = `${total} ticket${total!==1?'s':''}`;
    pagesInfo.textContent = `${start+1}–${Math.min(start+PAGE_SIZE,total)} of ${total}`;

    if (pages>1) {
      const prev = el('button',{class:'tk-btn tk-btn-outline tk-btn-sm'},'←');
      prev.disabled = pg<=1;
      prev.addEventListener('click', ()=>{ state.page=pg-1; renderTable(); });
      const next = el('button',{class:'tk-btn tk-btn-outline tk-btn-sm'},'→');
      next.disabled = pg>=pages;
      next.addEventListener('click', ()=>{ state.page=pg+1; renderTable(); });
      pageBtns.append(prev, el('span',{style:'font-size:12px;color:#94a3b8;padding:0 6px'},`${pg}/${pages}`), next);
    }
  };

  const applyFilter = () => {
    state.filtered = state.tickets.filter(t => {
      if (!state.statusFilter) return true;
      return normStatus(t.status) === normStatus(state.statusFilter);
    });
    state.page = 1;
    renderTable();
  };

  const updateStats = () => {
    hTotal.textContent    = String(state.tickets.length);
    hOpen.textContent     = String(state.tickets.filter(t=>normStatus(t.status)==='open').length);
    hResolved.textContent = String(state.tickets.filter(t=>{ const n=normStatus(t.status); return n==='resolved'||n==='closed'; }).length);
  };

  // ── Load ───────────────────────────────────────────────────────────────────
  const loadTickets = async () => {
    if (!state.siteId) { state.tickets=[]; applyFilter(); updateStats(); return; }
    tableWrap.replaceChildren(el('div',{class:'tk-skel'}),el('div',{class:'tk-skel'}),el('div',{class:'tk-skel'}));
    try {
      const res = await client.tickets.listTickets({ siteId:state.siteId, page:1, pageSize:200 });
      state.tickets = Array.isArray(res) ? res : [];
    } catch(err) { state.tickets=[]; notifier.show({message:mapApiError(err).message,variant:'danger'}); }
    updateStats(); applyFilter();
  };

  // ── Wire ───────────────────────────────────────────────────────────────────
  siteSelect.addEventListener('change',   () => { state.siteId=siteSelect.value; loadTickets(); });
  statusSelect.addEventListener('change', () => { state.statusFilter=statusSelect.value; applyFilter(); });

  // ── Init ───────────────────────────────────────────────────────────────────
  try {
    const sites = await client.sites.list();
    state.sites = Array.isArray(sites)?sites:[];
    siteSelect.innerHTML='';
    siteSelect.appendChild(el('option',{value:''},'All sites'));
    state.sites.forEach(s=>{ const id=getSiteId(s); siteSelect.appendChild(el('option',{value:id},s.domain||id)); });
    if (state.sites.length) { state.siteId=getSiteId(state.sites[0]); siteSelect.value=state.siteId; }
    await loadTickets();
  } catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); }
};
