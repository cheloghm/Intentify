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
/* Drag-drop zone */
.kb-dz{border:2px dashed #c7d2fe;border-radius:12px;padding:48px 24px;text-align:center;transition:all .15s;background:#fafbff;cursor:default}
.kb-dz.kb-dz-over{border-color:#6366f1;background:#eef2ff}
/* Source grid */
.kb-source-grid{display:grid;grid-template-columns:1fr 1fr;gap:12px}
@media(max-width:700px){.kb-source-grid{grid-template-columns:1fr}}
/* Progress bar */
.kb-prog-wrap{height:4px;background:#e2e8f0;border-radius:999px;overflow:hidden;margin-top:8px}
.kb-prog-fill{height:100%;border-radius:999px}
.kb-prog-animate{background:#6366f1;animation:kb-fill 8s ease-out forwards}
@keyframes kb-fill{from{width:0}to{width:85%}}
.kb-prog-done{background:#10b981;width:100%}
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
    sites: [], siteId: '',
    sources: [], loadingSources: false, retrieveResults: [],
  };

  let pollTimer = null;
  const isInProgress = s => { const st = String(s?.status||'').toUpperCase(); return st==='PROCESSING'||st==='PENDING'||st==='QUEUED'||st==='INDEXING'; };
  const stopPolling = () => { if (pollTimer) { clearInterval(pollTimer); pollTimer = null; } };
  const schedulePolling = () => {
    stopPolling();
    if (!state.siteId || !state.sources.some(isInProgress)) return;
    pollTimer = setInterval(async () => {
      if (!state.siteId) { stopPolling(); return; }
      try { state.sources = await client.knowledge.listSources(state.siteId); renderSources(); } catch {}
      if (!state.sources.some(isInProgress)) stopPolling();
    }, 5000);
  };

  const root = el('div', { class: 'kb-root' });
  container.appendChild(root);

  // ── Hero ───────────────────────────────────────────────────────────────────
  const hero = el('div', { class: 'kb-hero' });
  hero.appendChild(el('div', { class: 'kb-hero-title' }, '📚 Knowledge Workspace'));
  hero.appendChild(el('div', { class: 'kb-hero-sub' }, 'Everything your AI assistant knows about your business'));
  const heroStats = el('div', { class: 'kb-hero-stats' });
  const mkStat = lbl => { const w=el('div',{class:'kb-stat'}); const v=el('div',{class:'kb-stat-val'},'—'); w.append(v,el('div',{class:'kb-stat-lbl'},lbl)); heroStats.appendChild(w); return v; };
  const hSources = mkStat('Sources');
  const hFresh   = mkStat('Indexed');
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

  // ── Drag-drop zone ─────────────────────────────────────────────────────────
  const CLOUD_SVG = `<svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="#6366f1" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12"/></svg>`;

  const dzFileInput = el('input',{type:'file',style:'display:none'}); dzFileInput.accept='application/pdf';
  addBody.appendChild(dzFileInput);
  const dropZone = el('div',{class:'kb-dz'});
  addBody.appendChild(dropZone);

  dropZone.addEventListener('dragover', e => { e.preventDefault(); dropZone.classList.add('kb-dz-over'); });
  dropZone.addEventListener('dragleave', e => { if (!dropZone.contains(e.relatedTarget)) dropZone.classList.remove('kb-dz-over'); });
  dropZone.addEventListener('drop', e => {
    e.preventDefault(); dropZone.classList.remove('kb-dz-over');
    const f = e.dataTransfer.files[0];
    if (f && f.type === 'application/pdf') handlePdfDz(f);
    else if (f) notifier.show({message:'Please drop a PDF file.',variant:'warning'});
  });
  dzFileInput.addEventListener('change', () => { if (dzFileInput.files[0]) handlePdfDz(dzFileInput.files[0]); dzFileInput.value=''; });

  const showDzEmpty = () => {
    dropZone.innerHTML = '';
    const iconWrap = el('div',{style:'margin-bottom:10px;cursor:pointer;display:inline-block'});
    iconWrap.innerHTML = CLOUD_SVG;
    iconWrap.addEventListener('click', () => dzFileInput.click());
    const hint = el('div',{style:'font-size:14px;color:#475569;margin-bottom:4px;font-weight:500'},'Drop a PDF here, or');
    const sub  = el('div',{style:'font-size:12px;color:#94a3b8;margin-bottom:18px'},'Click the cloud icon to browse files');
    const btnRow = el('div',{style:'display:flex;gap:10px;justify-content:center;flex-wrap:wrap'});
    const urlBtn = el('button',{class:'kb-btn kb-btn-outline'},'🔗 Paste a URL');
    const txtBtn = el('button',{class:'kb-btn kb-btn-outline'},'📝 Enter text');
    urlBtn.addEventListener('click', e => { e.stopPropagation(); showDzUrl(); });
    txtBtn.addEventListener('click', e => { e.stopPropagation(); showDzText(); });
    btnRow.append(urlBtn, txtBtn);
    dropZone.append(iconWrap, hint, sub, btnRow);
  };

  const showDzUrl = () => {
    dropZone.innerHTML = '';
    const lbl = el('div',{style:'font-size:11px;font-weight:700;color:#475569;text-transform:uppercase;letter-spacing:.05em;margin-bottom:8px'},'Paste a URL');
    const input = el('input',{class:'kb-input',type:'url',placeholder:'https://example.com/docs',style:'margin-bottom:10px'});
    const btnRow = el('div',{style:'display:flex;gap:8px;justify-content:flex-end'});
    const cancelBtn = el('button',{class:'kb-btn kb-btn-outline kb-btn-sm'},'Cancel');
    const addBtn    = el('button',{class:'kb-btn kb-btn-primary kb-btn-sm'},'Add URL →');
    cancelBtn.addEventListener('click', showDzEmpty);
    const doAddUrl = async () => {
      const url = input.value.trim();
      if (!url) { notifier.show({message:'URL is required.',variant:'warning'}); return; }
      if (!state.siteId) { notifier.show({message:'Select a site first.',variant:'warning'}); return; }
      addBtn.disabled=true; addBtn.textContent='⏳ Adding…';
      try {
        await client.knowledge.createSource({siteId:state.siteId,type:'URL',name:url,url});
        notifier.show({message:'URL source added. Indexing starts shortly.',variant:'success'});
        showDzEmpty(); await loadSources();
      } catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); addBtn.disabled=false; addBtn.textContent='Add URL →'; }
    };
    addBtn.addEventListener('click', doAddUrl);
    input.addEventListener('keydown', e => { if (e.key==='Enter') doAddUrl(); });
    btnRow.append(cancelBtn, addBtn);
    dropZone.append(lbl, input, btnRow);
    requestAnimationFrame(() => input.focus());
  };

  const showDzText = () => {
    dropZone.innerHTML = '';
    const lbl = el('div',{style:'font-size:11px;font-weight:700;color:#475569;text-transform:uppercase;letter-spacing:.05em;margin-bottom:8px'},'Enter text');
    const nameInput = el('input',{class:'kb-input',placeholder:'Source name (e.g. Company FAQ)',style:'margin-bottom:8px'});
    const textarea  = el('textarea',{class:'kb-input kb-textarea',placeholder:'Paste or type your content…',style:'min-height:120px;margin-bottom:10px'});
    const btnRow = el('div',{style:'display:flex;gap:8px;justify-content:flex-end'});
    const cancelBtn = el('button',{class:'kb-btn kb-btn-outline kb-btn-sm'},'Cancel');
    const addBtn    = el('button',{class:'kb-btn kb-btn-primary kb-btn-sm'},'Add text →');
    cancelBtn.addEventListener('click', showDzEmpty);
    addBtn.addEventListener('click', async () => {
      const text = textarea.value.trim();
      if (!text) { notifier.show({message:'Content is required.',variant:'warning'}); return; }
      if (!state.siteId) { notifier.show({message:'Select a site first.',variant:'warning'}); return; }
      addBtn.disabled=true; addBtn.textContent='⏳ Adding…';
      try {
        await client.knowledge.createSource({siteId:state.siteId,type:'TEXT',name:nameInput.value.trim()||'Text source',text});
        notifier.show({message:'Text source added.',variant:'success'});
        showDzEmpty(); await loadSources();
      } catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); addBtn.disabled=false; addBtn.textContent='Add text →'; }
    });
    btnRow.append(cancelBtn, addBtn);
    dropZone.append(lbl, nameInput, textarea, btnRow);
    requestAnimationFrame(() => textarea.focus());
  };

  const handlePdfDz = async (file) => {
    if (!state.siteId) { notifier.show({message:'Select a site first.',variant:'warning'}); return; }
    dropZone.innerHTML = '';
    const progWrap  = el('div',{style:'width:100%;max-width:320px;margin:0 auto'});
    const progLabel = el('div',{style:'font-size:13px;color:#475569;margin-bottom:10px;font-weight:500'},`📄 Uploading ${file.name}…`);
    const progTrack = el('div',{style:'height:6px;background:#e2e8f0;border-radius:999px;overflow:hidden'});
    const progFill  = el('div',{style:'height:100%;background:#6366f1;border-radius:999px;width:0;transition:width .3s ease'});
    progTrack.appendChild(progFill); progWrap.append(progLabel, progTrack);
    dropZone.appendChild(progWrap);
    let pct = 0;
    const ticker = setInterval(() => {
      pct = Math.min(pct + (85/80), 85);
      progFill.style.width = pct.toFixed(1) + '%';
      if (pct >= 85) clearInterval(ticker);
    }, 100);
    try {
      const created = await client.knowledge.createSource({siteId:state.siteId,type:'PDF',name:file.name});
      await client.knowledge.uploadPdf(created.sourceId, file);
      clearInterval(ticker);
      progFill.style.width='100%'; progFill.style.background='#10b981';
      setTimeout(async () => {
        notifier.show({message:'PDF uploaded. Indexing starts shortly.',variant:'success'});
        showDzEmpty(); await loadSources();
      }, 600);
    } catch(err) { clearInterval(ticker); notifier.show({message:mapApiError(err).message,variant:'danger'}); showDzEmpty(); }
  };

  showDzEmpty();

  // (legacy tab vars removed — using drag-drop zone above)


  // ─── LEFT: Sources list panel ───────────────────────────────────────────
  const { panel: srcPanel, body: srcBody, hd: srcHd } = mkPanel('📚 Sources', '');
  leftCol.appendChild(srcPanel);
  const srcMeta = el('div', { class: 'kb-panel-meta' });
  srcHd.appendChild(srcMeta);

  const srcList = el('div', { class: 'kb-source-grid' });
  srcBody.appendChild(srcList);

  const loadSources = async () => {
    if (!state.siteId) { state.sources=[]; renderSources(); return; }
    state.loadingSources = true; renderSources();
    try {
      state.sources = await client.knowledge.listSources(state.siteId);
    } catch (err) { notifier.show({message:mapApiError(err).message,variant:'danger'}); }
    finally { state.loadingSources = false; renderSources(); schedulePolling(); }
  };

  const renderSources = () => {
    srcList.replaceChildren();
    const indexed = state.sources.filter(s => String(s?.status||'').toUpperCase() === 'INDEXED').length;
    const totalChunks = state.sources.reduce((sum, s) => sum + (s.chunkCount || 0), 0);
    hSources.textContent = String(state.sources.length);
    hFresh.textContent   = String(indexed);
    srcMeta.textContent  = `${state.sources.length} source${state.sources.length!==1?'s':''} · ${totalChunks} chunks indexed`;

    if (state.loadingSources) {
      srcList.append(el('div',{class:'kb-skel'}), el('div',{class:'kb-skel'}));
      return;
    }
    if (!state.sources.length) {
      srcList.appendChild(el('div',{class:'kb-empty'},el('div',{class:'kb-empty-icon'},'📚'),el('div',{class:'kb-empty-title'},state.siteId?'No sources yet':'Select a site'),el('div',{class:'kb-empty-desc'},state.siteId?'Add a URL, upload a PDF, or paste text above.':'Choose a site to see its knowledge sources.')));
      return;
    }
    state.sources.forEach(source => {
      const statusRaw = String(source?.status||'').toUpperCase();
      const isFailed   = statusRaw === 'FAILED';
      const isIndexed  = statusRaw === 'INDEXED';
      const isIndexing = statusRaw === 'INDEXING' || statusRaw === 'PROCESSING';
      const isQueued   = statusRaw === 'QUEUED' || statusRaw === 'PENDING';

      const row = el('div',{class:'kb-source'});
      const icon = el('div',{class:'kb-source-icon'},TYPE_ICON[source.type?.toUpperCase()]||'📄');
      const body2 = el('div',{class:'kb-source-body'});
      const rawName = source.name||source.url||source.sourceId||'—';
      body2.appendChild(el('div',{class:'kb-source-name',title:rawName},rawName.length>70?rawName.slice(0,67)+'…':rawName));
      if (source.url) body2.appendChild(el('div',{class:'kb-source-url',title:source.url},source.url));

      const meta = el('div',{class:'kb-source-meta'});
      if (isIndexed) {
        meta.appendChild(el('span',{class:'kb-pill',style:'background:#d1fae5;color:#065f46'},'Indexed ✓'));
        if (source.chunkCount) meta.appendChild(el('span',{style:'font-size:10px;color:#94a3b8'},`${source.chunkCount} chunks`));
        if (source.indexedAtUtc) meta.appendChild(el('span',{style:'font-size:10px;color:#94a3b8'},fmtDate(source.indexedAtUtc)));
      } else if (isIndexing) {
        meta.appendChild(el('span',{class:'kb-pill',style:'background:#fef3c7;color:#92400e'},'⏳ Indexing…'));
      } else if (isQueued) {
        meta.appendChild(el('span',{class:'kb-pill',style:'background:#f1f5f9;color:#64748b'},'Queued'));
      } else if (isFailed) {
        meta.appendChild(el('span',{class:'kb-pill',style:'background:#fee2e2;color:#991b1b'},'Failed'));
      } else {
        const { label, pill } = getFreshness(source);
        meta.appendChild(el('span',{class:`kb-pill ${pill}`},label));
        if (source.chunkCount) meta.appendChild(el('span',{style:'font-size:10px;color:#94a3b8'},`${source.chunkCount} chunks`));
        if (source.indexedAtUtc) meta.appendChild(el('span',{style:'font-size:10px;color:#94a3b8'},fmtDate(source.indexedAtUtc)));
      }
      if (source.type) meta.appendChild(el('span',{class:'kb-pill kb-pill-blue'},source.type.toUpperCase()));
      body2.appendChild(meta);

      if (isFailed && source.failureReason) {
        body2.appendChild(el('div',{style:'font-size:11px;color:#ef4444;margin-top:4px;line-height:1.4'},source.failureReason));
      }

      // Progress bar
      if (isIndexed) {
        const pw = el('div',{class:'kb-prog-wrap'});
        pw.appendChild(el('div',{class:'kb-prog-fill kb-prog-done'}));
        body2.appendChild(pw);
      } else if (isIndexing || isQueued) {
        const pw = el('div',{class:'kb-prog-wrap'});
        pw.appendChild(el('div',{class:'kb-prog-fill kb-prog-animate'}));
        body2.appendChild(pw);
      }

      const acts = el('div',{class:'kb-source-acts'});
      const idxLabel = isFailed ? '↩ Retry' : isIndexed ? '🔄 Re-index' : '▶ Index';
      const idxBtn = el('button',{class:`kb-btn kb-btn-sm ${isFailed?'kb-btn-danger':'kb-btn-outline'}`,title: isFailed?'Retry indexing':'Index this source'},idxLabel);
      idxBtn.addEventListener('click', async () => {
        idxBtn.disabled=true; idxBtn.textContent='⏳';
        try {
          await client.knowledge.indexSource(source.sourceId);
          notifier.show({message:'Indexing started.',variant:'success'});
          await loadSources();
        } catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); }
        finally { idxBtn.disabled=false; idxBtn.textContent=idxLabel; }
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
