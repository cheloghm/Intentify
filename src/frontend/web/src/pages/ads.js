/**
 * ads.js — Intentify Ads
 * Revamped: dark hero, panel layout.
 * All original: create campaign, view/edit campaign, placements CRUD,
 * activate/deactivate, performance report.
 */

import { createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const getSiteId    = s => s?.siteId || s?.id || '';
const getCampaignId = c => c?.id || c?.campaignId || '';

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

const injectStyles = () => {
  if (document.getElementById('_ads_css')) return;
  const s = document.createElement('style');
  s.id = '_ads_css';
  s.textContent = `
@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap');
.ad-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;gap:20px;width:100%;max-width:1100px;padding-bottom:60px}
.ad-hero{background:linear-gradient(135deg,#0f172a 0%,#1e293b 100%);border-radius:16px;padding:28px 36px;position:relative;overflow:hidden}
.ad-hero::before{content:'';position:absolute;top:-30px;right:-30px;width:180px;height:180px;background:radial-gradient(circle,rgba(59,130,246,.15) 0%,transparent 70%);pointer-events:none}
.ad-hero-title{font-size:24px;font-weight:700;color:#f8fafc;letter-spacing:-.02em;margin-bottom:6px}
.ad-hero-sub{font-size:13px;color:#94a3b8;margin-bottom:18px}
.ad-hero-stats{display:flex;gap:28px;flex-wrap:wrap}
.ad-stat{display:flex;flex-direction:column;gap:2px}
.ad-stat-val{font-family:'JetBrains Mono',monospace;font-size:22px;font-weight:700;color:#f1f5f9}
.ad-stat-lbl{font-size:10px;color:#64748b;text-transform:uppercase;letter-spacing:.07em}
.ad-controls{display:flex;align-items:center;gap:10px;flex-wrap:wrap}
.ad-select{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;color:#1e293b;background:#fff;border:1px solid #e2e8f0;border-radius:8px;padding:7px 11px;outline:none;min-width:200px}
.ad-select:focus{border-color:#6366f1;box-shadow:0 0 0 3px rgba(99,102,241,.1)}
.ad-btn{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;font-weight:600;padding:7px 16px;border-radius:8px;border:none;cursor:pointer;transition:all .14s;display:inline-flex;align-items:center;gap:5px;white-space:nowrap}
.ad-btn-primary{background:#6366f1;color:#fff}.ad-btn-primary:hover:not(:disabled){background:#4f46e5;transform:translateY(-1px);box-shadow:0 4px 12px rgba(99,102,241,.25)}
.ad-btn-primary:disabled{opacity:.5;cursor:not-allowed}
.ad-btn-outline{background:#fff;color:#64748b;border:1px solid #e2e8f0}.ad-btn-outline:hover{background:#f8fafc;color:#1e293b}
.ad-btn-green{background:#d1fae5;color:#065f46;border:none}.ad-btn-green:hover{background:#a7f3d0}
.ad-btn-sm{padding:5px 12px;font-size:12px}
.ad-layout{display:grid;grid-template-columns:320px 1fr;gap:16px;align-items:start}
@media(max-width:900px){.ad-layout{grid-template-columns:1fr}}
.ad-panel{background:#fff;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden}
.ad-panel-hd{display:flex;align-items:center;justify-content:space-between;padding:14px 20px;border-bottom:1px solid #f1f5f9}
.ad-panel-title{font-size:13px;font-weight:700;color:#0f172a;display:flex;align-items:center;gap:7px}
.ad-panel-body{padding:18px 20px;display:flex;flex-direction:column;gap:12px}
.ad-field{display:flex;flex-direction:column;gap:5px}
.ad-field-lbl{font-size:10.5px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:#94a3b8}
.ad-input{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;color:#1e293b;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:8px 11px;outline:none;width:100%;box-sizing:border-box}
.ad-input:focus{border-color:#6366f1;background:#fff;box-shadow:0 0 0 3px rgba(99,102,241,.1)}
.ad-toggle-lbl{display:flex;align-items:center;gap:8px;font-size:13px;color:#334155;cursor:pointer}
.ad-campaign-card{background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:12px 15px;display:flex;align-items:flex-start;justify-content:space-between;gap:10px;cursor:pointer;transition:all .14s}
.ad-campaign-card:hover{border-color:#c7d2fe;box-shadow:0 3px 12px rgba(0,0,0,.06)}
.ad-campaign-card.active{border-color:#6366f1;background:#eef2ff}
.ad-campaign-name{font-size:13px;font-weight:700;color:#1e293b;margin-bottom:2px}
.ad-campaign-obj{font-size:11.5px;color:#64748b}
.ad-pill{display:inline-flex;padding:2px 8px;border-radius:999px;font-size:10px;font-weight:700}
.ad-pill-green{background:#d1fae5;color:#065f46}
.ad-pill-gray{background:#f1f5f9;color:#475569}
.ad-pill-blue{background:#dbeafe;color:#1e40af}
.ad-placement-row{display:grid;grid-template-columns:100px 1fr 1fr 70px auto;gap:8px;align-items:center;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:9px 12px;margin-bottom:8px}
.ad-section-hd{font-size:12px;font-weight:700;color:#0f172a;padding-bottom:8px;border-bottom:1px solid #f1f5f9;margin-top:4px}
.ad-report-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin-bottom:14px}
.ad-report-card{background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:14px 16px;text-align:center}
.ad-report-val{font-family:'JetBrains Mono',monospace;font-size:22px;font-weight:700;color:#0f172a;margin-bottom:4px}
.ad-report-lbl{font-size:10px;color:#94a3b8;text-transform:uppercase;letter-spacing:.06em;font-weight:700}
.ad-table-wrap{border-radius:8px;overflow:hidden;border:1px solid #e2e8f0}
.ad-table{width:100%;border-collapse:collapse;font-size:12.5px}
.ad-table thead th{background:#f8fafc;padding:8px 14px;text-align:left;font-size:9.5px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:#94a3b8;border-bottom:1px solid #e2e8f0}
.ad-table tbody td{padding:10px 14px;border-bottom:1px solid #f1f5f9;color:#334155}
.ad-table tbody tr:last-child td{border-bottom:none}
.ad-empty{text-align:center;padding:32px 16px;color:#94a3b8;font-size:12.5px}
  `;
  document.head.appendChild(s);
};

export const renderAdsView = (container, { apiClient, toast } = {}) => {
  injectStyles();
  const client   = apiClient || createApiClient();
  const notifier = toast     || createToastManager();
  const state    = { sites:[], campaigns:[], siteId:'', selectedId:'', selectedCampaign:null, report:null, placements:[], comingSoon:false };

  const root = el('div',{class:'ad-root'});
  container.appendChild(root);

  // ── Hero ───────────────────────────────────────────────────────────────────
  const hero = el('div',{class:'ad-hero'});
  const heroTop = el('div',{style:'display:flex;align-items:center;gap:10px;margin-bottom:4px'});
  heroTop.appendChild(el('div',{class:'ad-hero-title',style:'margin-bottom:0'},'📢 Ads'));
  heroTop.appendChild(el('span',{style:'display:inline-flex;align-items:center;gap:5px;background:rgba(99,102,241,.15);border:1px solid rgba(99,102,241,.3);border-radius:99px;padding:3px 10px;font-size:10px;font-weight:700;color:#a5b4fc;letter-spacing:.06em;text-transform:uppercase'},'✦ Coming Soon'));
  hero.appendChild(heroTop);
  hero.appendChild(el('div',{class:'ad-hero-sub'},'Manage advertising campaigns, placements, and view performance reports'));
  const heroStats = el('div',{class:'ad-hero-stats'});
  const mkS = lbl => { const w=el('div',{class:'ad-stat'}); const v=el('div',{class:'ad-stat-val'},'—'); w.append(v,el('div',{class:'ad-stat-lbl'},lbl)); heroStats.appendChild(w); return v; };
  const hCampaigns=mkS('Campaigns'); const hActive=mkS('Active'); const hImpressions=mkS('Impressions');
  hero.appendChild(heroStats);
  root.appendChild(hero);

  // ── Controls ───────────────────────────────────────────────────────────────
  const controls = el('div',{class:'ad-controls'});
  const siteSelect = el('select',{class:'ad-select'},el('option',{value:''},'Loading…'));
  controls.appendChild(siteSelect);
  root.appendChild(controls);

  // ── Layout ─────────────────────────────────────────────────────────────────
  const layout = el('div',{class:'ad-layout'});
  const leftCol  = el('div',{style:'display:flex;flex-direction:column;gap:16px'});
  const rightCol = el('div',{style:'display:flex;flex-direction:column;gap:16px'});
  layout.append(leftCol, rightCol);
  root.appendChild(layout);

  function mkPanel(title) {
    const panel=el('div',{class:'ad-panel'}); const hd=el('div',{class:'ad-panel-hd'}); hd.appendChild(el('div',{class:'ad-panel-title'},title)); panel.appendChild(hd);
    const body=el('div',{class:'ad-panel-body'}); panel.appendChild(body);
    return { panel, body };
  }

  // ── Create campaign panel ──────────────────────────────────────────────────
  const { panel: createPanel, body: createBody } = mkPanel('➕ New Campaign');
  leftCol.appendChild(createPanel);

  const cnameField = el('div',{class:'ad-field'}, el('div',{class:'ad-field-lbl'},'Campaign Name'));
  const cnameInput = el('input',{class:'ad-input',placeholder:'Summer Campaign'});
  cnameField.appendChild(cnameInput);
  const cobjField = el('div',{class:'ad-field'}, el('div',{class:'ad-field-lbl'},'Objective (optional)'));
  const cobjInput = el('input',{class:'ad-input',placeholder:'e.g. Brand awareness'});
  cobjField.appendChild(cobjInput);
  const cActiveLbl = el('label',{class:'ad-toggle-lbl'});
  const cActiveCb  = el('input',{type:'checkbox'}); cActiveCb.checked=false;
  cActiveLbl.append(cActiveCb,'Start as Active');
  const createBtn = el('button',{class:'ad-btn ad-btn-primary',style:'width:100%;justify-content:center'},'📢 Create Campaign');
  createBtn.addEventListener('click', async ()=>{
    if (!state.siteId||!cnameInput.value.trim()) { notifier.show({message:'Site and name are required.',variant:'warning'}); return; }
    createBtn.disabled=true; createBtn.textContent='⏳ Creating…';
    try {
      await client.ads.createCampaign({ siteId:state.siteId, name:cnameInput.value.trim(), objective:cobjInput.value.trim()||null, isActive:cActiveCb.checked, placements:[] });
      cnameInput.value=''; cobjInput.value=''; cActiveCb.checked=false;
      notifier.show({message:'Campaign created.',variant:'success'});
      await loadCampaigns();
    } catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); }
    finally { createBtn.disabled=false; createBtn.textContent='📢 Create Campaign'; }
  });
  createBody.append(cnameField, cobjField, cActiveLbl, createBtn);

  // ── Campaigns list panel ───────────────────────────────────────────────────
  const { panel: listPanel, body: listBody } = mkPanel('📋 Campaigns');
  leftCol.appendChild(listPanel);
  const campaignList = el('div',{style:'display:flex;flex-direction:column;gap:8px'});
  listBody.appendChild(campaignList);

  // ── Detail panels (right) ──────────────────────────────────────────────────
  const { panel: detailPanel, body: detailBody } = mkPanel('✏️ Campaign Details');
  detailPanel.style.display='none';
  rightCol.appendChild(detailPanel);

  const { panel: placementsPanel, body: placementsBody } = mkPanel('📍 Placements');
  placementsPanel.style.display='none';
  rightCol.appendChild(placementsPanel);

  const { panel: reportPanel, body: reportBody } = mkPanel('📊 Performance Report');
  reportPanel.style.display='none';
  rightCol.appendChild(reportPanel);

  // ── Render campaign detail ─────────────────────────────────────────────────
  const renderDetail = () => {
    const c = state.selectedCampaign;
    if (!c) { detailPanel.style.display='none'; return; }
    detailPanel.style.display='';
    detailBody.replaceChildren();

    const nameField = el('div',{class:'ad-field'}, el('div',{class:'ad-field-lbl'},'Name'));
    const nameInput = el('input',{class:'ad-input',value:c.name||''});
    nameField.appendChild(nameInput);
    const objField  = el('div',{class:'ad-field'}, el('div',{class:'ad-field-lbl'},'Objective'));
    const objInput  = el('input',{class:'ad-input',placeholder:'e.g. Lead gen',value:c.objective||''});
    objField.appendChild(objInput);
    const activeLbl = el('label',{class:'ad-toggle-lbl'});
    const activeCb  = el('input',{type:'checkbox'}); activeCb.checked=Boolean(c.isActive);
    activeLbl.append(activeCb,'Is Active');

    const actRow = el('div',{style:'display:flex;gap:8px;flex-wrap:wrap;margin-top:4px'});
    const saveBtn = el('button',{class:'ad-btn ad-btn-primary ad-btn-sm'},'💾 Save');
    saveBtn.addEventListener('click', async ()=>{
      saveBtn.disabled=true; saveBtn.textContent='⏳…';
      try {
        state.selectedCampaign = await client.ads.updateCampaign(state.selectedId,{siteId:c.siteId,name:nameInput.value.trim(),objective:objInput.value.trim()||null,isActive:activeCb.checked,startsAtUtc:c.startsAtUtc||null,endsAtUtc:c.endsAtUtc||null,budget:c.budget??null});
        notifier.show({message:'Campaign saved.',variant:'success'});
        await loadCampaigns(); renderDetail();
      } catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); }
      finally { saveBtn.disabled=false; saveBtn.textContent='💾 Save'; }
    });
    const toggleBtn = el('button',{class:`ad-btn ${c.isActive?'ad-btn-outline':'ad-btn-green'} ad-btn-sm`}, c.isActive?'⏸ Deactivate':'▶ Activate');
    toggleBtn.addEventListener('click', async ()=>{
      toggleBtn.disabled=true;
      try {
        state.selectedCampaign = c.isActive ? await client.ads.deactivateCampaign(state.selectedId) : await client.ads.activateCampaign(state.selectedId);
        notifier.show({message:`Campaign ${state.selectedCampaign.isActive?'activated':'deactivated'}.`,variant:'success'});
        await loadCampaigns(); renderDetail();
      } catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); }
      finally { toggleBtn.disabled=false; }
    });
    actRow.append(saveBtn, toggleBtn);
    detailBody.append(nameField, objField, activeLbl, actRow);
  };

  // ── Render placements ──────────────────────────────────────────────────────
  const renderPlacements = () => {
    if (!state.selectedCampaign) { placementsPanel.style.display='none'; return; }
    placementsPanel.style.display='';
    placementsBody.replaceChildren();
    placementsBody.appendChild(el('div',{class:'ad-section-hd'},'Placements'));

    const rowsWrap = el('div',{});
    if (!state.placements.length) rowsWrap.appendChild(el('div',{class:'ad-empty'},'No placements configured.'));
    state.placements.forEach((p,i)=>{
      const row = el('div',{class:'ad-placement-row'});
      const slot = el('input',{class:'ad-input',style:'font-size:12px',placeholder:'slotKey',value:p.slotKey||''}); slot.addEventListener('input',()=>p.slotKey=slot.value);
      const hl   = el('input',{class:'ad-input',style:'font-size:12px',placeholder:'Headline',value:p.headline||''}); hl.addEventListener('input',()=>p.headline=hl.value);
      const url  = el('input',{class:'ad-input',style:'font-size:12px',placeholder:'Destination URL',value:p.destinationUrl||''}); url.addEventListener('input',()=>p.destinationUrl=url.value);
      const aLbl = el('label',{style:'display:flex;align-items:center;gap:4px;font-size:12px;white-space:nowrap'});
      const aCb  = el('input',{type:'checkbox'}); aCb.checked=p.isActive!==false; aCb.addEventListener('change',()=>p.isActive=aCb.checked);
      aLbl.append(aCb,'Active');
      const rmBtn = el('button',{class:'ad-btn ad-btn-outline ad-btn-sm'},'✕');
      rmBtn.addEventListener('click',()=>{ state.placements.splice(i,1); renderPlacements(); });
      row.append(slot,hl,url,aLbl,rmBtn);
      rowsWrap.appendChild(row);
    });
    placementsBody.appendChild(rowsWrap);

    const actRow = el('div',{style:'display:flex;gap:8px;flex-wrap:wrap;margin-top:4px'});
    const addBtn = el('button',{class:'ad-btn ad-btn-outline ad-btn-sm'},'+ Add Placement');
    addBtn.addEventListener('click',()=>{ state.placements.push({slotKey:'',headline:'',destinationUrl:'',order:state.placements.length+1,isActive:true}); renderPlacements(); });
    const saveBtn = el('button',{class:'ad-btn ad-btn-primary ad-btn-sm'},'💾 Save Placements');
    saveBtn.addEventListener('click', async ()=>{
      saveBtn.disabled=true; saveBtn.textContent='⏳…';
      try {
        const payload = { placements: state.placements.map((p,i)=>({ slotKey:p.slotKey||'', pathPattern:p.pathPattern||null, device:p.device||'all', headline:p.headline||'', body:p.body||null, imageUrl:p.imageUrl||null, destinationUrl:p.destinationUrl||'', ctaText:p.ctaText||null, order:p.order||i+1, isActive:p.isActive!==false })) };
        state.selectedCampaign = await client.ads.upsertPlacements(state.selectedId, payload);
        state.placements = [...(state.selectedCampaign?.placements||[])];
        notifier.show({message:'Placements saved.',variant:'success'});
        await loadCampaigns(); await loadReport(); renderPlacements();
      } catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); }
      finally { saveBtn.disabled=false; saveBtn.textContent='💾 Save Placements'; }
    });
    actRow.append(addBtn, saveBtn);
    placementsBody.appendChild(actRow);
  };

  // ── Render report ──────────────────────────────────────────────────────────
  const renderReport = () => {
    if (!state.selectedId) { reportPanel.style.display='none'; return; }
    reportPanel.style.display='';
    reportBody.replaceChildren();
    const r = state.report;
    if (!r) { reportBody.appendChild(el('div',{class:'ad-empty'},'No report data yet.')); return; }
    const totals = r.totals||{};
    const grid = el('div',{class:'ad-report-grid'});
    [['Impressions',totals.impressions||0],['Clicks',totals.clicks||0],['CTR',totals.ctr||0]].forEach(([lbl,val])=>{
      const card=el('div',{class:'ad-report-card'}); card.append(el('div',{class:'ad-report-val'},String(val)),el('div',{class:'ad-report-lbl'},lbl)); grid.appendChild(card);
    });
    reportBody.appendChild(grid);
    const rows = r.byPlacement||[];
    if (!rows.length) { reportBody.appendChild(el('div',{class:'ad-empty'},'No placement data.')); return; }
    const tableWrap = el('div',{class:'ad-table-wrap'});
    const table = el('table',{class:'ad-table'});
    table.appendChild(el('thead',{},el('tr',{}, ...['Placement','Impressions','Clicks','CTR'].map(c=>el('th',{},c)))));
    const tbody = el('tbody',{});
    rows.forEach(row=>{
      const tr=el('tr',{}); [row.slotKey||row.placementId||'—',row.impressions||0,row.clicks||0,row.ctr||0].forEach(v=>{ tr.appendChild(el('td',{},String(v))); }); tbody.appendChild(tr);
    });
    table.appendChild(tbody); tableWrap.appendChild(table); reportBody.appendChild(tableWrap);
  };

  const loadReport = async () => {
    if (!state.selectedId) { state.report=null; renderReport(); return; }
    try { state.report=await client.ads.getReport(state.selectedId); hImpressions.textContent=String(state.report?.totals?.impressions||0); renderReport(); }
    catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); }
  };

  // ── Load campaigns ─────────────────────────────────────────────────────────
  const loadCampaigns = async () => {
    if (!state.siteId) { state.campaigns=[]; renderCampaignList(); return; }
    try {
      state.campaigns   = await client.ads.listCampaigns(state.siteId);
      state.comingSoon  = false;
      hCampaigns.textContent = String(state.campaigns.length);
      hActive.textContent    = String(state.campaigns.filter(c=>c.isActive).length);
      renderCampaignList();
    } catch {
      state.campaigns  = [];
      state.comingSoon = true;
      renderCampaignList();
    }
  };

  const renderCampaignList = () => {
    campaignList.replaceChildren();
    if (state.comingSoon) {
      const empty = el('div', { class: 'ad-empty', style: 'padding:48px 24px;text-align:center' });
      empty.innerHTML = `
        <div style="font-size:36px;margin-bottom:12px">📢</div>
        <div style="font-size:16px;font-weight:700;color:#0f172a;margin-bottom:8px">Ads — Coming Soon</div>
        <div style="font-size:13px;color:#64748b;max-width:340px;margin:0 auto;line-height:1.7">
          Run targeted ad campaigns directly from Hven, powered by visitor intent data.
          Create campaigns that reach the right companies at exactly the right moment.
        </div>
        <div style="margin-top:20px;padding:12px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;display:inline-block;font-size:11px;color:#6366f1;font-weight:700">✦ Planned for Q3 2026</div>
      `;
      campaignList.appendChild(empty);
      return;
    }
    if (!state.campaigns.length) {
      const empty = el('div',{class:'ad-empty'});
      empty.innerHTML = '<div style="font-size:28px;margin-bottom:8px">📢</div><div style="font-weight:600;color:#475569;margin-bottom:4px">No campaigns yet</div><div>Create your first ad campaign to start driving targeted traffic.</div>';
      campaignList.appendChild(empty);
      return;
    }
    state.campaigns.forEach(c=>{
      const cid=getCampaignId(c);
      const card=el('div',{class:`ad-campaign-card${state.selectedId===cid?' active':''}`});
      card.append(
        el('div',{style:'flex:1;min-width:0'},el('div',{class:'ad-campaign-name'},c.name||'Unnamed'),el('div',{class:'ad-campaign-obj'},c.objective||'')),
        el('span',{class:`ad-pill ${c.isActive?'ad-pill-green':'ad-pill-gray'}`},c.isActive?'Active':'Paused')
      );
      card.addEventListener('click', async ()=>{
        state.selectedId=cid;
        try {
          state.selectedCampaign=await client.ads.getCampaign(cid);
          state.placements=[...(state.selectedCampaign?.placements||[])];
          renderCampaignList(); renderDetail(); renderPlacements(); await loadReport();
        } catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); }
      });
      campaignList.appendChild(card);
    });
  };

  // ── Wire + Init ────────────────────────────────────────────────────────────
  siteSelect.addEventListener('change',()=>{ state.siteId=siteSelect.value; state.selectedId=''; state.selectedCampaign=null; state.placements=[]; state.report=null; detailPanel.style.display='none'; placementsPanel.style.display='none'; reportPanel.style.display='none'; loadCampaigns(); });

  const init = async () => {
    try {
      state.sites=await client.sites.list();
      siteSelect.innerHTML='';
      siteSelect.appendChild(el('option',{value:''},'Select site'));
      state.sites.forEach(s=>{ const id=getSiteId(s); siteSelect.appendChild(el('option',{value:id},s.domain||id)); });
      if (state.sites.length) { state.siteId=getSiteId(state.sites[0]); siteSelect.value=state.siteId; await loadCampaigns(); }
    } catch(err){ notifier.show({message:mapApiError(err).message,variant:'danger'}); }
  };
  init();
};
