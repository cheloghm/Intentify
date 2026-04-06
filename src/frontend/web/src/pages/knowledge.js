/**
 * knowledge.js — Intentify Knowledge Base
 * Revamped to match visitors.js design language exactly.
 * All original functionality preserved: URL/TEXT/PDF source creation,
 * quick facts, re-index stale, AI retrieval tester.
 */

import { createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

// ─── Helpers (identical to original logic) ────────────────────────────────────

const getSiteId = s => s?.siteId || s?.id || '';
const fmtDate   = v => { if (!v) return '—'; const d=new Date(v); return isNaN(d)?'—':d.toLocaleDateString('en-GB',{day:'numeric',month:'short',year:'numeric'}); };

const getFreshness = (source) => {
  const status = String(source?.status || '').toUpperCase();
  if (status === 'PROCESSING' || status === 'PENDING') return { label: '⏳ Indexing', pill: 'kb-pill-amber' };
  if (status === 'FAILED')                              return { label: '❌ Error',    pill: 'kb-pill-red'   };
  if (!source?.indexedAtUtc)                            return { label: '⚠ Stale',    pill: 'kb-pill-amber' };
  const idxAt = Date.parse(source.indexedAtUtc);
  const updAt = source?.updatedAtUtc ? Date.parse(source.updatedAtUtc) : NaN;
  if (isNaN(idxAt) || (!isNaN(updAt) && updAt > idxAt)) return { label: '⚠ Stale',   pill: 'kb-pill-amber' };
  return { label: '✅ Fresh', pill: 'kb-pill-green' };
};

const TYPE_ICON = { URL: '🔗', TEXT: '📝', PDF: '📄' };

// ─── el() helper ──────────────────────────────────────────────────────────────

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
  if (document.getElementById('_kb2_css')) return;
  const s = document.createElement('style');
  s.id = '_kb2_css';
  s.textContent = `
@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap');

.kb-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;gap:20px;width:100%;max-width:1100px;padding-bottom:60px}

/* Hero — same pattern as .v-hero */
.kb-hero{background:linear-gradient(135deg,#0f172a 0%,#1e293b 100%);border-radius:16px;padding:28px 36px;position:relative;overflow:hidden}
.kb-hero::before{content:'';position:absolute;top:-30px;right:-30px;width:180px;height:180px;background:radial-gradient(circle,rgba(16,185,129,.15) 0%,transparent 70%);pointer-events:none}
.kb-hero-title{font-size:24px;font-weight:700;color:#f8fafc;letter-spacing:-.02em;margin-bottom:6px}
.kb-hero-sub{font-size:13px;color:#94a3b8;margin-bottom:18px}
.kb-hero-stats{display:flex;gap:28px;flex-wrap:wrap}
.kb-stat{display:flex;flex-direction:column;gap:2px}
.kb-stat-val{font-family:'JetBrains Mono',monospace;font-size:22px;font-weight:700;color:#f1f5f9;letter-spacing:-.02em}
.kb-stat-lbl{font-size:10px;color:#64748b;text-transform:uppercase;letter-spacing:.07em}

/* Controls */
.kb-controls{display:flex;align-items:center;gap:10px;flex-wrap:wrap}
.kb-select{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;color:#1e293b;background:#fff;border:1px solid #e2e8f0;border-radius:8px;padding:7px 11px;outline:none;min-width:220px}
.kb-select:focus{border-color:#6366f1;box-shadow:0 0 0 3px rgba(99,102,241,.1)}

/* Buttons */
.kb-btn{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;font-weight:600;padding:7px 16px;border-radius:8px;border:none;cursor:pointer;transition:all .14s;display:inline-flex;align-items:center;gap:5px;white-space:nowrap}
.kb-btn-primary{background:#6366f1;color:#fff}
.kb-btn-primary:hover:not(:disabled){background:#4f46e5;transform:translateY(-1px);box-shadow:0 4px 12px rgba(99,102,241,.25)}
.kb-btn-primary:disabled{opacity:.5;cursor:not-allowed}
.kb-btn-outline{background:#fff;color:#64748b;border:1px solid #e2e8f0}
.kb-btn-outline:hover{background:#f8fafc;color:#1e293b}
.kb-btn-danger{background:#fee2e2;color:#dc2626;border:none}
.kb-btn-danger:hover{background:#fecaca}
.kb-btn-sm{padding:5px 12px;font-size:12px}

/* Layout */
.kb-grid{display:grid;grid-template-columns:1fr 340px;gap:16px;align-items:start}
@media(max-width:900px){.kb-grid{grid-template-columns:1fr}}

/* Panel — same pattern as .v-panel */
.kb-panel{background:#fff;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden}
.kb-panel-hd{display:flex;align-items:center;justify-content:space-between;padding:14px 20px;border-bottom:1px solid #f1f5f9}
.kb-panel-title{font-size:13px;font-weight:700;color:#0f172a;display:flex;align-items:center;gap:7px}
.kb-panel-meta{font-size:11px;color:#94a3b8}
.kb-panel-body{padding:18px 20px}

/* Source tabs — same as .v-tabs */
.kb-tabs{display:flex;gap:3px;background:#f1f5f9;border-radius:10px;padding:3px;margin-bottom:16px}
.kb-tab{flex:1;padding:7px 12px;border-radius:8px;border:none;font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:12px;font-weight:500;color:#64748b;cursor:pointer;transition:all .14s;text-align:center}
.kb-tab:hover{background:rgba(255,255,255,.7)}
.kb-tab.active{background:#fff;color:#6366f1;font-weight:700;box-shadow:0 1px 4px rgba(0,0,0,.08)}

/* Input */
.kb-input{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;color:#1e293b;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:8px 11px;outline:none;width:100%;box-sizing:border-box;transition:border .14s}
.kb-input:focus{border-color:#6366f1;background:#fff;box-shadow:0 0 0 3px rgba(99,102,241,.1)}
.kb-textarea{resize:vertical;min-height:100px;font-family:inherit;line-height:1.5}
.kb-field{display:flex;flex-direction:column;gap:5px;margin-bottom:10px}
.kb-field-lbl{font-size:10.5px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:#94a3b8}

/* File drop area */
.kb-drop{border:2px dashed #e2e8f0;border-radius:10px;padding:28px 16px;text-align:center;cursor:pointer;transition:all .14s;color:#94a3b8;font-size:13px;line-height:1.5}
.kb-drop:hover,.kb-drop.over{border-color:#6366f1;color:#6366f1;background:#eef2ff}

/* Source rows */
.kb-source{background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:12px 15px;display:flex;align-items:flex-start;gap:12px;transition:box-shadow .14s}
.kb-source:hover{box-shadow:0 3px 12px rgba(0,0,0,.06)}
.kb-source-icon{width:36px;height:36px;border-radius:8px;background:#eef2ff;display:flex;align-items:center;justify-content:center;font-size:16px;flex-shrink:0}
.kb-source-body{flex:1;min-width:0}
.kb-source-name{font-size:12.5px;font-weight:600;color:#1e293b;margin-bottom:2px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.kb-source-url{font-family:'JetBrains Mono',monospace;font-size:10.5px;color:#94a3b8;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;margin-bottom:5px}
.kb-source-meta{display:flex;gap:6px;align-items:center;flex-wrap:wrap}
.kb-source-acts{display:flex;gap:5px;flex-shrink:0}

/* Pills */
.kb-pill{display:inline-flex;align-items:center;padding:2px 8px;border-radius:999px;font-size:10px;font-weight:700}
.kb-pill-green{background:#d1fae5;color:#065f46}
.kb-pill-amber{background:#fef3c7;color:#92400e}
.kb-pill-red{background:#fee2e2;color:#dc2626}
.kb-pill-blue{background:#dbeafe;color:#1e40af}
.kb-pill-gray{background:#f1f5f9;color:#475569}

/* Quick facts */
.kb-fact-row{display:flex;align-items:flex-start;gap:8px;padding:9px 0;border-bottom:1px solid #f8fafc}
.kb-fact-row:last-child{border-bottom:none}
.kb-fact-text{font-size:12.5px;color:#334155;flex:1;line-height:1.5}

/* Retrieve results */
.kb-result{background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:12px 14px;margin-bottom:8px}
.kb-result-meta{font-family:'JetBrains Mono',monospace;font-size:10.5px;color:#94a3b8;margin-bottom:6px}
.kb-result-content{font-size:12.5px;color:#334155;line-height:1.6;white-space:pre-wrap}

/* Empty / skeleton */
.kb-empty{text-align:center;padding:36px 20px;display:flex;flex-direction:column;align-items:center;gap:8px}
.kb-empty-icon{font-size:36px;opacity:.3}
.kb-empty-title{font-size:13px;font-weight:600;color:#334155}
.kb-empty-desc{font-size:12px;color:#94a3b8;max-width:260px;line-height:1.6}
.kb-skel{background:linear-gradient(90deg,#f1f5f9 25%,#e2e8f0 50%,#f1f5f9 75%);background-size:200% 100%;animation:_sh 1.4s infinite;border-radius:8px;height:68px}
@keyframes _sh{0%{background-position:200% 0}100%{background-position:-200% 0}}
  `;
  document.head.appendChild(s);
};

// ─── Panel factory ────────────────────────────────────────────────────────────

const mkPanel = (title, meta = '') => {
  const panel = el('div', { class: 'kb-panel' });
  const hd    = el('div', { class: 'kb-panel-hd' });
  hd.appendChild(el('div', { class: 'kb-panel-title' }, title));
  if (meta) hd.appendChild(el('div', { class: 'kb-panel-meta' }, meta));
  panel.appendChild(hd);
  const body = el('div', { class: 'kb-panel-body' });
  panel.appendChild(body);
  return { panel, body, hd };
};

// ─── Main export ──────────────────────────────────────────────────────────────

export const renderKnowledgeView = (container, { apiClient, toast } = {}) => {
  injectStyles();
  const client   = apiClient || createApiClient();
  const notifier = toast     || createToastManager();

  const state = {
    sites: [], siteId: '', activeTab: 'URL',
    sources: [], loadingSources: false, retrieveResults: [],
  };

  const root = el('div', { class: 'kb-root' });
  container.appendChild(root);

  // ── Hero ───────────────────────────────────────────────────────────────────
  const hero = el('div', { class: 'kb-hero' });
  hero.appendChild(el('div', { class: 'kb-hero-title' }, '📚 Knowledge Base'));
  hero.appendChild(el('div', { class: 'kb-hero-sub' }, 'Teach your AI assistant — index URLs, upload PDFs, add quick facts, and test retrieval'));
  const heroStats = el('div', { class: 'kb-hero-stats' });
  const mkStat = lbl => { const w=el('div',{class:'kb-stat'}); const v=el('div',{class:'kb-stat-val'},'—'); w.append(v,el('div',{class:'kb-stat-lbl'},lbl)); heroStats.appendChild(w); return v; };
  const hSources = mkStat('Sources');
  const hFresh   = mkStat('Fresh');
  const hFacts   = mkStat('Quick Facts');
  hero.appendChild(heroStats);
  root.appendChild(hero);

  // ── Controls ───────────────────────────────────────────────────────────────
  const controls = el('div', { class: 'kb-controls' });
  const siteSelect  = el('select', { class: 'kb-select' }, el('option',{value:''},'Loading sites…'));
  const reindexBtn  = el('button', { class: 'kb-btn kb-btn-outline' }, '🔄 Reindex Stale');
  const refreshBtn  = el('button', { class: 'kb-btn kb-btn-outline' }, '↻ Refresh');
  controls.append(siteSelect, reindexBtn, refreshBtn);
  root.appendChild(controls);

  // ── Layout ─────────────────────────────────────────────────────────────────
  const grid = el('div', { class: 'kb-grid' });
  const leftCol  = el('div', { style: 'display:flex;flex-direction:column;gap:16px' });
  const rightCol = el('div', { style: 'display:flex;flex-direction:column;gap:16px' });
  grid.append(leftCol, rightCol);
  root.appendChild(grid);

  // ─── LEFT: Add source panel ─────────────────────────────────────────────
  const { panel: addPanel, body: addBody } = mkPanel('➕ Add Knowledge Source');
  leftCol.appendChild(addPanel);

  // Source type tabs
  const tabBar = el('div', { class: 'kb-tabs' });
  const TAB_DEFS = [{ key:'URL', label:'🔗 URL' }, { key:'TEXT', label:'📝 Text' }, { key:'PDF', label:'📄 PDF' }];
  const tabEls = {}, tabPanels = {};
  TAB_DEFS.forEach(({ key, label }) => {
    const btn = el('button', { class: `kb-tab${key===state.activeTab?' active':''}` }, label);
    btn.addEventListener('click', () => {
      state.activeTab = key;
      TAB_DEFS.forEach(t => { tabEls[t.key].classList.toggle('active', t.key===key); tabPanels[t.key].style.display = t.key===key?'block':'none'; });
    });
    tabEls[key] = btn; tabBar.appendChild(btn);
  });
  addBody.appendChild(tabBar);

  // ── URL tab ────────────────────────────────────────────────────────────────
  const urlPanel = el('div', {});
  const urlNameField = el('div',{class:'kb-field'}, el('div',{class:'kb-field-lbl'},'Source Name (optional)'));
  const urlNameInput = el('input',{class:'kb-input',placeholder:'My documentation'});
  urlNameField.appendChild(urlNameInput);
  const urlField = el('div',{class:'kb-field'}, el('div',{class:'kb-field-lbl'},'URL'));
  const urlInput = el('input',{class:'kb-input',type:'url',placeholder:'https://example.com/docs'});
  urlField.appendChild(urlInput);
  const addUrlBtn = el('button',{class:'kb-btn kb-btn-primary',style:'width:100%;justify-content:center'},'🔗 Add URL Source');
  addUrlBtn.addEventListener('click', async () => {
    const url = urlInput.value.trim();
    if (!url) { notifier.show({message:'URL is required.',variant:'warning'}); return; }
    if (!state.siteId) { notifier.show({message:'Select a site first.',variant:'warning'}); return; }
    addUrlBtn.disabled=true; addUrlBtn.textContent='⏳ Adding…';
    try {
      await client.knowledge.createSource({ siteId:state.siteId, type:'URL', name:urlNameInput.value.trim()||url, url });
      urlInput.value=''; urlNameInput.value='';
      notifier.show({message:'URL source added. Indexing starts shortly.',variant:'success'});
      await loadSources();
    } catch (err) { notifier.show({message:mapApiError(err).message,variant:'danger'}); }
    finally { addUrlBtn.disabled=false; addUrlBtn.textContent='🔗 Add URL Source'; }
  });
  urlPanel.append(urlNameField, urlField, addUrlBtn);
  tabPanels.URL = urlPanel;

  // ── TEXT tab ───────────────────────────────────────────────────────────────
  const textPanel = el('div',{style:'display:none'});
  const txNameField = el('div',{class:'kb-field'}, el('div',{class:'kb-field-lbl'},'Source Name'));
  const txNameInput = el('input',{class:'kb-input',placeholder:'e.g. Company FAQ'});
  txNameField.appendChild(txNameInput);
  const txField = el('div',{class:'kb-field'}, el('div',{class:'kb-field-lbl'},'Content'));
  const txArea  = el('textarea',{class:'kb-input kb-textarea',placeholder:'Paste raw text to index…'});
  txField.appendChild(txArea);
  const addTxtBtn = el('button',{class:'kb-btn kb-btn-primary',style:'width:100%;justify-content:center'},'📝 Add Text Source');
  addTxtBtn.addEventListener('click', async () => {
    const text = txArea.value.trim();
    if (!text) { notifier.show({message:'Content is required.',variant:'warning'}); return; }
    if (!state.siteId) { notifier.show({message:'Select a site first.',variant:'warning'}); return; }
    addTxtBtn.disabled=true; addTxtBtn.textContent='⏳ Adding…';
    try {
      await client.knowledge.createSource({ siteId:state.siteId, type:'TEXT', name:txNameInput.value.trim()||'Text source', text });
      txNameInput.value=''; txArea.value='';
      notifier.show({message:'Text source added.',variant:'success'});
      await loadSources();
    } catch (err) { notifier.show({message:mapApiError(err).message,variant:'danger'}); }
    finally { addTxtBtn.disabled=false; addTxtBtn.textContent='📝 Add Text Source'; }
  });
  textPanel.append(txNameField, txField, addTxtBtn);
  tabPanels.TEXT = textPanel;

  // ── PDF tab ────────────────────────────────────────────────────────────────
  const pdfPanel = el('div',{style:'display:none'});
  const pdfNameField = el('div',{class:'kb-field'}, el('div',{class:'kb-field-lbl'},'Source Name (optional)'));
  const pdfNameInput = el('input',{class:'kb-input',placeholder:'e.g. Product Manual'});
  pdfNameField.appendChild(pdfNameInput);
  const fileInput = el('input',{type:'file',style:'display:none'}); fileInput.accept='application/pdf';
  const dropArea = el('div',{class:'kb-drop'},'📄 Click or drag a PDF here to upload');
  dropArea.addEventListener('click', () => fileInput.click());
  dropArea.addEventListener('dragover', e => { e.preventDefault(); dropArea.classList.add('over'); });
  dropArea.addEventListener('dragleave', () => dropArea.classList.remove('over'));
  dropArea.addEventListener('drop', e => { e.preventDefault(); dropArea.classList.remove('over'); const f=e.dataTransfer.files[0]; if (f) handlePdf(f); });
  fileInput.addEventListener('change', () => { if (fileInput.files[0]) handlePdf(fileInput.files[0]); });

  const handlePdf = async (file) => {
    if (!state.siteId) { notifier.show({message:'Select a site first.',variant:'warning'}); return; }
    dropArea.textContent='⏳ Uploading…'; dropArea.style.pointerEvents='none';
    try {
      const created = await client.knowledge.createSource({ siteId:state.siteId, type:'PDF', name:pdfNameInput.value.trim()||file.name });
      await client.knowledge.uploadPdf(created.sourceId, file);
      pdfNameInput.value=''; fileInput.value='';
      notifier.show({message:'PDF uploaded. Indexing starts shortly.',variant:'success'});
      await loadSources();
    } catch (err) { notifier.show({message:mapApiError(err).message,variant:'danger'}); }
    finally { dropArea.textContent='📄 Click or drag a PDF here to upload'; dropArea.style.pointerEvents=''; }
  };

  pdfPanel.append(pdfNameField, fileInput, dropArea);
  tabPanels.PDF = pdfPanel;

  Object.values(tabPanels).forEach(p => addBody.appendChild(p));

  // ─── LEFT: Sources list panel ───────────────────────────────────────────
  const { panel: srcPanel, body: srcBody, hd: srcHd } = mkPanel('📚 Sources', '');
  leftCol.appendChild(srcPanel);
  const srcMeta = el('div', { class: 'kb-panel-meta' });
  srcHd.appendChild(srcMeta);

  const srcList = el('div', { style: 'display:flex;flex-direction:column;gap:8px' });
  srcBody.appendChild(srcList);

  const loadSources = async () => {
    if (!state.siteId) { state.sources=[]; renderSources(); return; }
    state.loadingSources = true; renderSources();
    try {
      state.sources = await client.knowledge.listSources(state.siteId);
    } catch (err) { notifier.show({message:mapApiError(err).message,variant:'danger'}); }
    finally { state.loadingSources = false; renderSources(); }
  };

  const renderSources = () => {
    srcList.replaceChildren();
    const fresh = state.sources.filter(s=>getFreshness(s).label.includes('Fresh')).length;
    hSources.textContent = String(state.sources.length);
    hFresh.textContent   = String(fresh);
    srcMeta.textContent  = `${state.sources.length} source${state.sources.length!==1?'s':''}`;

    if (state.loadingSources) {
      srcList.append(el('div',{class:'kb-skel'}), el('div',{class:'kb-skel'}));
      return;
    }
    if (!state.sources.length) {
      srcList.appendChild(el('div',{class:'kb-empty'},el('div',{class:'kb-empty-icon'},'📚'),el('div',{class:'kb-empty-title'},state.siteId?'No sources yet':'Select a site'),el('div',{class:'kb-empty-desc'},state.siteId?'Add a URL, upload a PDF, or paste text above.':'Choose a site to see its knowledge sources.')));
      return;
    }
    state.sources.forEach(source => {
      const { label, pill } = getFreshness(source);
      const row = el('div',{class:'kb-source'});
      const icon = el('div',{class:'kb-source-icon'},TYPE_ICON[source.type?.toUpperCase()]||'📄');
      const body2 = el('div',{class:'kb-source-body'});
      const rawName = source.name||source.url||source.sourceId||'—';
      body2.appendChild(el('div',{class:'kb-source-name',title:rawName},rawName.length>70?rawName.slice(0,67)+'…':rawName));
      if (source.url) body2.appendChild(el('div',{class:'kb-source-url',title:source.url},source.url));
      const meta = el('div',{class:'kb-source-meta'});
      meta.appendChild(el('span',{class:`kb-pill ${pill}`},label));
      if (source.type) meta.appendChild(el('span',{class:'kb-pill kb-pill-blue'},source.type.toUpperCase()));
      if (source.indexedAtUtc) meta.appendChild(el('span',{style:'font-size:10px;color:#94a3b8'},fmtDate(source.indexedAtUtc)));
      if (source.chunkCount)   meta.appendChild(el('span',{style:'font-size:10px;color:#94a3b8'},`${source.chunkCount} chunks`));
      body2.appendChild(meta);

      const acts = el('div',{class:'kb-source-acts'});
      const idxBtn = el('button',{class:'kb-btn kb-btn-outline kb-btn-sm',title:'Re-index this source'},'🔄');
      idxBtn.addEventListener('click', async () => {
        idxBtn.disabled=true; idxBtn.textContent='⏳';
        try {
          const resp = await client.knowledge.indexSource(source.sourceId);
          const ns = String(resp.status||'').toUpperCase();
          if (ns==='FAILED') notifier.show({message:resp.failureReason?`Index failed: ${resp.failureReason}`:'Index failed.',variant:'danger'});
          else notifier.show({message:`Reindex started${resp.chunkCount!=null?` (${resp.chunkCount} chunks)`:'.'}.`,variant:'success'});
          await loadSources();
        } catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); }
        finally { idxBtn.disabled=false; idxBtn.textContent='🔄'; }
      });
      const delBtn = el('button',{class:'kb-btn kb-btn-danger kb-btn-sm'},'✕');
      delBtn.addEventListener('click', async () => {
        delBtn.disabled=true;
        try { await client.knowledge.deleteSource(source.sourceId); notifier.show({message:'Source deleted.',variant:'success'}); await loadSources(); }
        catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); delBtn.disabled=false; }
      });
      acts.append(idxBtn, delBtn);
      row.append(icon, body2, acts);
      srcList.appendChild(row);
    });
  };

  // ─── RIGHT: Quick Facts ─────────────────────────────────────────────────
  const { panel: factPanel, body: factBody } = mkPanel('⚡ Quick Facts');
  rightCol.appendChild(factPanel);
  factBody.appendChild(el('div',{style:'font-size:12px;color:#94a3b8;margin-bottom:12px;line-height:1.6'},'Short facts your AI always knows — business hours, pricing, contact details.'));

  const factList = el('div',{});
  factBody.appendChild(factList);

  const factInput = el('input',{class:'kb-input',placeholder:"e.g. We're open Mon–Fri 9am–5pm",style:'margin-top:10px'});
  const addFactBtn = el('button',{class:'kb-btn kb-btn-primary kb-btn-sm',style:'margin-top:8px;width:100%;justify-content:center'},'+ Add Fact');
  factBody.append(factInput, addFactBtn);

  const loadFacts = async () => {
    if (!state.siteId) { factList.replaceChildren(); hFacts.textContent='—'; return; }
    try {
      const facts = await client.knowledge.listQuickFacts(state.siteId);
      const list  = Array.isArray(facts) ? facts : [];
      hFacts.textContent = String(list.length);
      factList.replaceChildren();
      if (!list.length) { factList.appendChild(el('div',{style:'font-size:12px;color:#94a3b8;padding:4px 0'},'No quick facts yet.')); return; }
      list.forEach(item => {
        const row = el('div',{class:'kb-fact-row'});
        row.appendChild(el('div',{class:'kb-fact-text'},item.fact||item.text||item.content||String(item)));
        const delBtn = el('button',{class:'kb-btn kb-btn-danger kb-btn-sm'},'×');
        delBtn.addEventListener('click', async () => {
          delBtn.disabled=true;
          try { await client.knowledge.deleteQuickFact(state.siteId, item.id||item.factId||item._id); await loadFacts(); }
          catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); delBtn.disabled=false; }
        });
        row.appendChild(delBtn);
        factList.appendChild(row);
      });
    } catch { factList.replaceChildren(el('div',{style:'font-size:12px;color:#94a3b8'},'Could not load facts.')); }
  };

  addFactBtn.addEventListener('click', async () => {
    if (!state.siteId) { notifier.show({message:'Select a site first.',variant:'warning'}); return; }
    const fact = factInput.value.trim();
    if (!fact) { notifier.show({message:'Enter a fact to add.',variant:'warning'}); return; }
    addFactBtn.disabled=true; addFactBtn.textContent='⏳…';
    try { await client.knowledge.addQuickFact(state.siteId,fact); factInput.value=''; notifier.show({message:'Fact added.',variant:'success'}); await loadFacts(); }
    catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); }
    finally { addFactBtn.disabled=false; addFactBtn.textContent='+ Add Fact'; }
  });
  factInput.addEventListener('keydown', e => { if (e.key==='Enter') { e.preventDefault(); addFactBtn.click(); } });

  // ─── RIGHT: AI Retrieval tester ─────────────────────────────────────────
  const { panel: testPanel, body: testBody } = mkPanel('🤖 Test Retrieval');
  rightCol.appendChild(testPanel);
  testBody.appendChild(el('div',{style:'font-size:12px;color:#94a3b8;margin-bottom:12px;line-height:1.6'},"Ask a question to see what your AI bot would retrieve from indexed sources."));

  const testInput = el('input',{class:'kb-input',placeholder:'e.g. What are your opening hours?'});
  const testBtn   = el('button',{class:'kb-btn kb-btn-outline',style:'margin-top:8px;width:100%;justify-content:center'},'🔍 Retrieve');
  const testResults = el('div',{style:'margin-top:12px'});
  testBody.append(testInput, testBtn, testResults);

  const runTest = async () => {
    if (!state.siteId) { notifier.show({message:'Select a site first.',variant:'warning'}); return; }
    const query = testInput.value.trim();
    if (!query) { notifier.show({message:'Enter a query.',variant:'warning'}); return; }
    testBtn.disabled=true; testBtn.textContent='⏳ Retrieving…';
    testResults.replaceChildren(el('div',{style:'color:#94a3b8;font-size:12.5px;padding:8px 0'},'Searching knowledge base…'));
    try {
      const results = await client.knowledge.retrieve({ siteId:state.siteId, query, top:5 });
      testResults.replaceChildren();
      const list = Array.isArray(results) ? results : [];
      if (!list.length) { testResults.appendChild(el('div',{style:'color:#94a3b8;font-size:12.5px;padding:8px 0'},'No results found.')); return; }
      list.forEach(r => {
        const card = el('div',{class:'kb-result'});
        card.appendChild(el('div',{class:'kb-result-meta'},`Source ${r.sourceId||'—'} · Chunk ${r.chunkIndex??'—'} · Score ${Number(r.score||r.relevanceScore||0).toFixed(3)}`));
        card.appendChild(el('div',{class:'kb-result-content'},r.content||r.chunk||r.text||''));
        testResults.appendChild(card);
      });
    } catch(err){ testResults.replaceChildren(el('div',{style:'color:#dc2626;font-size:12.5px'},mapApiError(err).message)); }
    finally { testBtn.disabled=false; testBtn.textContent='🔍 Retrieve'; }
  };

  testBtn.addEventListener('click', runTest);
  testInput.addEventListener('keydown', e => { if (e.key==='Enter') runTest(); });

  // ─── Wire controls ─────────────────────────────────────────────────────────
  siteSelect.addEventListener('change', async () => { state.siteId=siteSelect.value; await Promise.all([loadSources(),loadFacts()]); });

  refreshBtn.addEventListener('click', loadSources);

  reindexBtn.addEventListener('click', async () => {
    if (!state.siteId) { notifier.show({message:'Select a site first.',variant:'warning'}); return; }
    const stale = state.sources.filter(s => !getFreshness(s).label.includes('Fresh') && String(s.status||'').toUpperCase()!=='PROCESSING');
    if (!stale.length) { notifier.show({message:'All sources are fresh.',variant:'success'}); return; }
    reindexBtn.disabled=true; reindexBtn.textContent='⏳ Reindexing…';
    let ok=0, fail=0;
    try {
      for (const src of stale) {
        try { const r=await client.knowledge.indexSource(src.sourceId); String(r.status||'').toUpperCase()==='FAILED'?fail++:ok++; }
        catch { fail++; }
      }
      await loadSources();
      notifier.show({message:`Reindex complete — ${ok} succeeded, ${fail} failed.`, variant:fail>0?'warning':'success'});
    } finally { reindexBtn.disabled=false; reindexBtn.textContent='🔄 Reindex Stale'; }
  });

  // ─── Init ─────────────────────────────────────────────────────────────────
  const init = async () => {
    try {
      state.sites = await client.sites.list();
      siteSelect.innerHTML = '';
      if (!state.sites.length) { siteSelect.appendChild(el('option',{value:''},'No sites available')); renderSources(); return; }
      const placeholder = el('option',{value:''},'Select a site'); siteSelect.appendChild(placeholder);
      state.sites.forEach(s => { const id=getSiteId(s); const opt=el('option',{value:id},s.domain||id); siteSelect.appendChild(opt); });
      if (state.sites[0]) { state.siteId=getSiteId(state.sites[0]); siteSelect.value=state.siteId; }
      await Promise.all([loadSources(), loadFacts()]);
    } catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); }
  };

  renderSources();
  void init();
};
