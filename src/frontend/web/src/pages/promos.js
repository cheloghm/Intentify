/**
 * promos.js — Intentify Promos
 * Revamped: dark hero, panel cards, modal detail view.
 * All original: create promo, custom questions, flyer upload,
 * entries table, CSV export, flyer download.
 */

import { createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const getSiteId  = s => s?.siteId || s?.id || '';
const getPromoId = p => p?.id || p?.promoId || '';
const fmtDate    = v => { if (!v) return '—'; const d=new Date(v); return isNaN(d)?'—':d.toLocaleDateString('en-GB',{day:'numeric',month:'short',year:'numeric'}); };

const QUESTION_TYPES = ['text','email','phone','textarea','checkbox'];
const normType = v => { const n=String(v||'').trim().toLowerCase(); return QUESTION_TYPES.includes(n)?n:'text'; };

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
  if (document.getElementById('_pr_css')) return;
  const s = document.createElement('style');
  s.id = '_pr_css';
  s.textContent = `
@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap');
.pr-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;gap:20px;width:100%;max-width:1100px;padding-bottom:60px}
.pr-hero{background:linear-gradient(135deg,#0f172a 0%,#1e293b 100%);border-radius:16px;padding:28px 36px;position:relative;overflow:hidden}
.pr-hero::before{content:'';position:absolute;top:-30px;right:-30px;width:180px;height:180px;background:radial-gradient(circle,rgba(168,85,247,.15) 0%,transparent 70%);pointer-events:none}
.pr-hero-title{font-size:24px;font-weight:700;color:#f8fafc;letter-spacing:-.02em;margin-bottom:6px}
.pr-hero-sub{font-size:13px;color:#94a3b8;margin-bottom:18px}
.pr-hero-stats{display:flex;gap:28px;flex-wrap:wrap}
.pr-stat{display:flex;flex-direction:column;gap:2px}
.pr-stat-val{font-family:'JetBrains Mono',monospace;font-size:22px;font-weight:700;color:#f1f5f9}
.pr-stat-lbl{font-size:10px;color:#64748b;text-transform:uppercase;letter-spacing:.07em}
.pr-controls{display:flex;align-items:center;gap:10px;flex-wrap:wrap}
.pr-select{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;color:#1e293b;background:#fff;border:1px solid #e2e8f0;border-radius:8px;padding:7px 11px;outline:none;min-width:200px}
.pr-select:focus{border-color:#6366f1;box-shadow:0 0 0 3px rgba(99,102,241,.1)}
.pr-btn{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;font-weight:600;padding:7px 16px;border-radius:8px;border:none;cursor:pointer;transition:all .14s;display:inline-flex;align-items:center;gap:5px;white-space:nowrap}
.pr-btn-primary{background:#6366f1;color:#fff}.pr-btn-primary:hover:not(:disabled){background:#4f46e5;transform:translateY(-1px);box-shadow:0 4px 12px rgba(99,102,241,.25)}
.pr-btn-primary:disabled{opacity:.5;cursor:not-allowed}
.pr-btn-outline{background:#fff;color:#64748b;border:1px solid #e2e8f0}.pr-btn-outline:hover{background:#f8fafc;color:#1e293b}
.pr-btn-sm{padding:5px 12px;font-size:12px}
.pr-grid{display:grid;grid-template-columns:380px 1fr;gap:16px;align-items:start}
@media(max-width:900px){.pr-grid{grid-template-columns:1fr}}
.pr-panel{background:#fff;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden}
.pr-panel-hd{display:flex;align-items:center;justify-content:space-between;padding:14px 20px;border-bottom:1px solid #f1f5f9}
.pr-panel-title{font-size:13px;font-weight:700;color:#0f172a;display:flex;align-items:center;gap:7px}
.pr-panel-body{padding:18px 20px;display:flex;flex-direction:column;gap:12px}
.pr-field{display:flex;flex-direction:column;gap:5px}
.pr-field-lbl{font-size:10.5px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:#94a3b8}
.pr-input{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;color:#1e293b;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:8px 11px;outline:none;width:100%;box-sizing:border-box}
.pr-input:focus{border-color:#6366f1;background:#fff;box-shadow:0 0 0 3px rgba(99,102,241,.1)}
.pr-drop{border:2px dashed #e2e8f0;border-radius:10px;padding:18px;text-align:center;cursor:pointer;color:#94a3b8;font-size:13px;transition:all .14s}
.pr-drop:hover{border-color:#6366f1;color:#6366f1;background:#eef2ff}
.pr-q-row{display:grid;grid-template-columns:1fr 1fr 110px 80px auto;gap:7px;align-items:center;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:9px 11px}
/* Promo cards */
.pr-promo-card{background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:14px 16px;display:flex;align-items:flex-start;justify-content:space-between;gap:10px;cursor:pointer;transition:box-shadow .14s,border-color .14s}
.pr-promo-card:hover{box-shadow:0 3px 12px rgba(0,0,0,.07);border-color:#c7d2fe}
.pr-promo-card.active{border-color:#6366f1;background:#eef2ff}
.pr-promo-name{font-size:13px;font-weight:700;color:#1e293b;margin-bottom:2px}
.pr-promo-key{font-family:'JetBrains Mono',monospace;font-size:10.5px;color:#94a3b8}
.pr-pill{display:inline-flex;padding:2px 8px;border-radius:999px;font-size:10px;font-weight:700}
.pr-pill-green{background:#d1fae5;color:#065f46}
.pr-pill-gray{background:#f1f5f9;color:#475569}
/* Table */
.pr-table-wrap{border-radius:8px;overflow:hidden;border:1px solid #e2e8f0}
.pr-table{width:100%;border-collapse:collapse;font-size:12px}
.pr-table thead th{background:#f8fafc;padding:8px 12px;text-align:left;font-size:9.5px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:#94a3b8;border-bottom:1px solid #e2e8f0;white-space:nowrap}
.pr-table tbody td{padding:10px 12px;border-bottom:1px solid #f1f5f9;color:#334155;vertical-align:top}
.pr-table tbody tr:last-child td{border-bottom:none}
.pr-answers{font-family:'JetBrains Mono',monospace;font-size:10.5px;color:#64748b;max-width:200px;word-break:break-all}
/* Empty */
.pr-empty{text-align:center;padding:40px 20px;display:flex;flex-direction:column;align-items:center;gap:8px}
.pr-empty-icon{font-size:36px;opacity:.3}
.pr-empty-title{font-size:13px;font-weight:600;color:#334155}
.pr-empty-desc{font-size:12px;color:#94a3b8;max-width:260px;line-height:1.6}
/* Modal */
.pr-overlay{position:fixed;inset:0;background:rgba(15,23,42,.55);z-index:200;display:flex;align-items:center;justify-content:center;padding:20px;backdrop-filter:blur(3px)}
.pr-modal{background:#fff;border-radius:16px;width:100%;max-width:820px;max-height:90vh;overflow-y:auto;box-shadow:0 24px 64px rgba(0,0,0,.2)}
.pr-modal-hd{display:flex;align-items:center;justify-content:space-between;padding:18px 24px;border-bottom:1px solid #f1f5f9;position:sticky;top:0;background:#fff;z-index:1}
.pr-modal-title{font-size:15px;font-weight:700;color:#0f172a}
.pr-modal-body{padding:18px 24px;display:flex;flex-direction:column;gap:14px}
  `;
  document.head.appendChild(s);
};

// ─── Main export ──────────────────────────────────────────────────────────────

export const renderPromosView = (container, { apiClient, toast } = {}) => {
  injectStyles();
  const client   = apiClient || createApiClient();
  const notifier = toast     || createToastManager();
  const state    = { sites:[], siteId:'', promos:[], selectedId:'', selectedPromo:null, entries:[], questions:[] };

  const root = el('div',{class:'pr-root'});
  container.appendChild(root);

  // ── Hero ───────────────────────────────────────────────────────────────────
  const hero = el('div',{class:'pr-hero'});
  hero.appendChild(el('div',{class:'pr-hero-title'},'🎁 Promos'));
  hero.appendChild(el('div',{class:'pr-hero-sub'},'Create lead-capture widgets with custom questions and flyer attachments'));
  const heroStats = el('div',{class:'pr-hero-stats'});
  const mkS = lbl => { const w=el('div',{class:'pr-stat'}); const v=el('div',{class:'pr-stat-val'},'—'); w.append(v,el('div',{class:'pr-stat-lbl'},lbl)); heroStats.appendChild(w); return v; };
  const hPromos = mkS('Promos'); const hEntries = mkS('Total Entries');
  hero.appendChild(heroStats);
  root.appendChild(hero);

  // ── Controls ───────────────────────────────────────────────────────────────
  const controls = el('div',{class:'pr-controls'});
  const siteSelect = el('select',{class:'pr-select'},el('option',{value:''},'Loading…'));
  controls.appendChild(siteSelect);
  root.appendChild(controls);

  // ── Grid ───────────────────────────────────────────────────────────────────
  const grid = el('div',{class:'pr-grid'});
  const leftCol  = el('div',{style:'display:flex;flex-direction:column;gap:16px'});
  const rightCol = el('div',{style:'display:flex;flex-direction:column;gap:16px'});
  grid.append(leftCol, rightCol);
  root.appendChild(grid);

  // ── Create panel ───────────────────────────────────────────────────────────
  const { panel: createPanel, body: createBody } = mkPanel('➕ Create Promo');
  leftCol.appendChild(createPanel);

  const nameField = el('div',{class:'pr-field'}, el('div',{class:'pr-field-lbl'},'Promo Name'));
  const nameInput = el('input',{class:'pr-input',placeholder:'Summer Sale 2025'});
  nameField.appendChild(nameInput);

  const descField = el('div',{class:'pr-field'}, el('div',{class:'pr-field-lbl'},'Description (optional)'));
  const descInput = el('input',{class:'pr-input',placeholder:'Short description…'});
  descField.appendChild(descInput);

  const fileInput = el('input',{type:'file',style:'display:none'}); fileInput.accept='image/*,.pdf';
  const flyerDrop = el('div',{class:'pr-drop'},'🖼 Click to attach a flyer (image or PDF)');
  flyerDrop.addEventListener('click', ()=>fileInput.click());
  fileInput.addEventListener('change', ()=>{ flyerDrop.textContent = fileInput.files[0]?.name || '🖼 Click to attach a flyer'; });

  // Custom questions
  const qSectionHd = el('div',{style:'font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:#94a3b8;margin-top:4px'},'Custom Questions');
  const qList = el('div',{style:'display:flex;flex-direction:column;gap:7px'});
  const addQBtn = el('button',{class:'pr-btn pr-btn-outline pr-btn-sm'},'+ Add Question');

  const renderQuestions = () => {
    qList.replaceChildren();
    if (!state.questions.length) { qList.appendChild(el('div',{style:'font-size:12px;color:#94a3b8'},'No questions yet.')); return; }
    state.questions.forEach((q, i) => {
      const row = el('div',{class:'pr-q-row'});
      const keyIn = el('input',{class:'pr-input',style:'font-size:12px',placeholder:'key',value:q.key});
      keyIn.addEventListener('input',()=>{ q.key=keyIn.value; });
      const lblIn = el('input',{class:'pr-input',style:'font-size:12px',placeholder:'Label',value:q.label});
      lblIn.addEventListener('input',()=>{ q.label=lblIn.value; });
      const typeSel = el('select',{class:'pr-input',style:'font-size:12px'});
      QUESTION_TYPES.forEach(t=>{ const o=el('option',{value:t},t); if(t===normType(q.type)) o.selected=true; typeSel.appendChild(o); });
      typeSel.addEventListener('change',()=>{ q.type=typeSel.value; });
      const reqLbl = el('label',{style:'display:flex;align-items:center;gap:4px;font-size:12px;white-space:nowrap'});
      const reqCb = el('input',{type:'checkbox'}); reqCb.checked=q.required;
      reqCb.addEventListener('change',()=>{ q.required=reqCb.checked; });
      reqLbl.append(reqCb,'Req');
      const rmBtn = el('button',{class:'pr-btn pr-btn-outline pr-btn-sm'},'✕');
      rmBtn.addEventListener('click',()=>{ state.questions.splice(i,1); renderQuestions(); });
      row.append(keyIn,lblIn,typeSel,reqLbl,rmBtn);
      qList.appendChild(row);
    });
  };

  addQBtn.addEventListener('click', ()=>{ state.questions.push({key:'',label:'',type:'text',required:false}); renderQuestions(); });

  const createBtn = el('button',{class:'pr-btn pr-btn-primary',style:'width:100%;justify-content:center'},'🎁 Create Promo');
  createBtn.addEventListener('click', async () => {
    if (!siteSelect.value||!nameInput.value.trim()) { notifier.show({message:'Site and name are required.',variant:'warning'}); return; }
    createBtn.disabled=true; createBtn.textContent='⏳ Creating…';
    try {
      const fd = new FormData();
      fd.append('siteId', siteSelect.value);
      fd.append('name', nameInput.value.trim());
      fd.append('description', descInput.value.trim());
      fd.append('isActive','true');
      fd.append('questions', JSON.stringify(state.questions.map((q,i)=>({ key:q.key.trim(), label:q.label.trim()||q.key.trim(), type:normType(q.type), required:Boolean(q.required), order:i })).filter(q=>q.key)));
      if (fileInput.files?.[0]) fd.append('flyer', fileInput.files[0]);
      await client.promos.create(fd);
      nameInput.value=''; descInput.value=''; fileInput.value=''; flyerDrop.textContent='🖼 Click to attach a flyer';
      state.questions=[]; renderQuestions();
      notifier.show({message:'Promo created.',variant:'success'});
      await loadPromos();
    } catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); }
    finally { createBtn.disabled=false; createBtn.textContent='🎁 Create Promo'; }
  });

  createBody.append(nameField, descField, fileInput, flyerDrop, qSectionHd, qList, addQBtn, createBtn);

  // ── Promos list panel ──────────────────────────────────────────────────────
  const { panel: listPanel, body: listBody } = mkPanel('📋 Your Promos');
  leftCol.appendChild(listPanel);
  const promoList = el('div',{style:'display:flex;flex-direction:column;gap:8px'});
  listBody.appendChild(promoList);

  // ── Detail panel (right) ──────────────────────────────────────────────────
  const { panel: detailPanel, body: detailBody } = mkPanel('📊 Promo Details');
  detailPanel.style.display = 'none';
  rightCol.appendChild(detailPanel);

  // ── Modal for entries ──────────────────────────────────────────────────────
  let modalOverlay = null;
  const openEntriesModal = (promo, entries) => {
    if (modalOverlay) modalOverlay.remove();
    modalOverlay = el('div',{class:'pr-overlay'});
    const modal = el('div',{class:'pr-modal'});
    const mhd   = el('div',{class:'pr-modal-hd'});
    mhd.append(el('div',{class:'pr-modal-title'},`📋 Entries — ${promo.name||'Promo'}`), el('button',{class:'pr-btn pr-btn-outline pr-btn-sm','@click':()=>modalOverlay.remove()},'✕'));
    const mbody = el('div',{class:'pr-modal-body'});

    const exportBtn = el('button',{class:'pr-btn pr-btn-outline pr-btn-sm',style:'align-self:flex-start'},'⬇ Export CSV');
    exportBtn.addEventListener('click', async ()=>{
      try { const blob=await client.promos.downloadCsv(state.selectedId); const url=URL.createObjectURL(blob); const a=document.createElement('a'); a.href=url; a.download=`promo-${state.selectedId}.csv`; a.click(); URL.revokeObjectURL(url); }
      catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); }
    });
    mbody.appendChild(exportBtn);

    if (!entries.length) {
      mbody.appendChild(el('div',{class:'pr-empty'},el('div',{class:'pr-empty-icon'},'📭'),el('div',{class:'pr-empty-title'},'No entries yet')));
    } else {
      const tableWrap = el('div',{class:'pr-table-wrap'});
      const table = el('table',{class:'pr-table'});
      table.appendChild(el('thead',{},el('tr',{}, ...['Email','Name','Answers','Created'].map(c=>el('th',{},c)))));
      const tbody = el('tbody',{});
      entries.forEach(entry=>{
        const answers = Object.entries(entry.answers||{}).map(([k,v])=>`${k}: ${v}`).join('; ');
        const tr = el('tr',{});
        tr.append(
          el('td',{},entry.email||'—'),
          el('td',{},entry.name||'—'),
          el('td',{},el('div',{class:'pr-answers'},answers||'—')),
          el('td',{style:'white-space:nowrap;font-size:11px;color:#94a3b8'},fmtDate(entry.createdAtUtc))
        );
        tbody.appendChild(tr);
      });
      table.appendChild(tbody);
      tableWrap.appendChild(table);
      mbody.appendChild(tableWrap);
    }

    modal.append(mhd, mbody);
    modalOverlay.appendChild(modal);
    modalOverlay.addEventListener('click', e=>{ if(e.target===modalOverlay) modalOverlay.remove(); });
    document.body.appendChild(modalOverlay);
  };

  // ── Render promo detail ────────────────────────────────────────────────────
  const renderDetail = async (promo) => {
    state.selectedId = getPromoId(promo);
    detailPanel.style.display='';
    detailBody.replaceChildren(el('div',{style:'color:#94a3b8;font-size:12.5px;padding:4px 0'},'⏳ Loading…'));
    try {
      const detail = await client.promos.getDetail(state.selectedId);
      state.selectedPromo = detail.promo||detail;
      state.entries = detail.entries||[];
      const p = state.selectedPromo;
      detailBody.replaceChildren();

      // Info rows
      [['Name',p.name||'—'],['Description',p.description||'—'],['Public Key',p.publicKey||'—'],['Entries',String(state.entries.length)]].forEach(([lbl,val])=>{
        const row = el('div',{style:'display:flex;gap:8px;align-items:baseline;padding:5px 0;border-bottom:1px solid #f8fafc'});
        row.append(el('div',{style:'font-size:10.5px;font-weight:600;text-transform:uppercase;letter-spacing:.05em;color:#94a3b8;min-width:100px'},lbl),el('div',{style:'font-size:12.5px;color:#334155;font-family:JetBrains Mono,monospace'},val));
        detailBody.appendChild(row);
      });

      // Flyer
      if (p.flyerFileName) {
        const flyerRow = el('div',{style:'display:flex;gap:8px;align-items:center;margin-top:4px'});
        flyerRow.append(el('div',{style:'font-size:12px;color:#64748b'},`📎 ${p.flyerFileName}`));
        const dlBtn = el('button',{class:'pr-btn pr-btn-outline pr-btn-sm'},'⬇ Download');
        dlBtn.addEventListener('click', async ()=>{
          try { const blob=await client.promos.downloadFlyer(state.selectedId); const url=URL.createObjectURL(blob); const a=document.createElement('a'); a.href=url; a.download=p.flyerFileName; a.click(); URL.revokeObjectURL(url); }
          catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); }
        });
        flyerRow.appendChild(dlBtn);
        detailBody.appendChild(flyerRow);
      }

      // View entries button
      const viewEntriesBtn = el('button',{class:'pr-btn pr-btn-primary',style:'margin-top:8px'},'📋 View Entries & Export CSV');
      viewEntriesBtn.addEventListener('click',()=>openEntriesModal(p, state.entries));
      detailBody.appendChild(viewEntriesBtn);

      hEntries.textContent = String(state.entries.length);
    } catch(err){ detailBody.replaceChildren(el('div',{style:'color:#dc2626;font-size:12.5px'},mapApiError(err).message)); }
  };

  // ── Load promos ────────────────────────────────────────────────────────────
  const loadPromos = async () => {
    promoList.replaceChildren(el('div',{style:'color:#94a3b8;font-size:12.5px;padding:4px 0'},'⏳ Loading…'));
    try {
      state.promos = await client.promos.list(state.siteId||undefined);
      hPromos.textContent = String(state.promos.length);
      promoList.replaceChildren();
      if (!state.promos.length) { promoList.appendChild(el('div',{class:'pr-empty'},el('div',{class:'pr-empty-icon'},'🎁'),el('div',{class:'pr-empty-title'},'No promos yet'),el('div',{class:'pr-empty-desc'},'Create a promo to start capturing leads via the widget.'))); return; }
      state.promos.forEach(promo=>{
        const card = el('div',{class:`pr-promo-card${state.selectedId===getPromoId(promo)?' active':''}`});
        card.appendChild(el('div',{style:'flex:1;min-width:0'},
          el('div',{class:'pr-promo-name'},promo.name||'Unnamed'),
          el('div',{class:'pr-promo-key'},promo.publicKey||getPromoId(promo))
        ));
        card.appendChild(el('span',{class:'pr-pill pr-pill-green'},'Active'));
        card.addEventListener('click',()=>renderDetail(promo));
        promoList.appendChild(card);
      });
    } catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); }
  };

  function mkPanel(title) {
    const panel = el('div',{class:'pr-panel'});
    const hd    = el('div',{class:'pr-panel-hd'}); hd.appendChild(el('div',{class:'pr-panel-title'},title)); panel.appendChild(hd);
    const body  = el('div',{class:'pr-panel-body'}); panel.appendChild(body);
    return { panel, body };
  }

  // ── Wire ───────────────────────────────────────────────────────────────────
  siteSelect.addEventListener('change',()=>{ state.siteId=siteSelect.value; loadPromos(); });

  // ── Init ───────────────────────────────────────────────────────────────────
  const init = async () => {
    try {
      state.sites = await client.sites.list();
      siteSelect.innerHTML='';
      siteSelect.appendChild(el('option',{value:''},'All sites'));
      state.sites.forEach(s=>{ const id=getSiteId(s); siteSelect.appendChild(el('option',{value:id},s.domain||id)); });
      if (state.sites.length) { state.siteId=getSiteId(state.sites[0]); siteSelect.value=state.siteId; }
      renderQuestions();
      await loadPromos();
    } catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); }
  };

  init();
};
