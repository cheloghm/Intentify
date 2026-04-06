/**
 * dashboard.js — Intentify Dashboard  (Phase 6 final)
 * Dark hero matching visitors.js exactly.
 * Health check widget, live visitor count, metrics, charts, quick links.
 */

import { createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const getSiteId = s => s?.siteId || s?.id || '';
const fmtDate   = v => { if (!v) return '—'; const d=new Date(v); return isNaN(d)?'—':d.toLocaleDateString('en-GB',{day:'numeric',month:'short',year:'numeric'}); };
const fmtAgo    = v => { if (!v) return 'Never'; const m=Math.floor((Date.now()-new Date(v))/60000); if(m<1) return 'just now'; if(m<60) return `${m}m ago`; const h=Math.floor(m/60); if(h<24) return `${h}h ago`; return `${Math.floor(h/24)}d ago`; };

const SITE_KEY  = 'intentify.selectedSiteId';
const loadSiteId = () => { try { return localStorage.getItem(SITE_KEY)||''; } catch { return ''; } };
const saveSiteId = id => { try { id?localStorage.setItem(SITE_KEY,id):localStorage.removeItem(SITE_KEY); } catch {} };

// ─── el() ─────────────────────────────────────────────────────────────────────

const el = (tag, attrs={}, ...kids) => {
  const e = document.createElement(tag);
  Object.entries(attrs).forEach(([k,v])=>{
    if (k==='class') e.className=v;
    else if (k==='style') typeof v==='string'?(e.style.cssText=v):Object.assign(e.style,v);
    else if (k.startsWith('@')) e.addEventListener(k.slice(1),v);
    else e.setAttribute(k,v);
  });
  kids.flat(Infinity).forEach(c=>c!=null&&e.append(typeof c==='string'?document.createTextNode(c):c));
  return e;
};

// ─── Styles ───────────────────────────────────────────────────────────────────

const injectStyles = () => {
  if (document.getElementById('_db_css')) return;
  const s = document.createElement('style');
  s.id = '_db_css';
  s.textContent = `
@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap');

.db-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;gap:20px;width:100%;max-width:1280px;padding-bottom:60px}

/* Hero — identical to .v-hero in visitors.js */
.db-hero{background:linear-gradient(135deg,#0f172a 0%,#1e293b 100%);border-radius:16px;padding:28px 36px;position:relative;overflow:hidden}
.db-hero::before{content:'';position:absolute;top:-30px;right:-30px;width:220px;height:220px;background:radial-gradient(circle,rgba(99,102,241,.16) 0%,transparent 70%);pointer-events:none}
.db-hero-title{font-size:24px;font-weight:700;color:#f8fafc;letter-spacing:-.02em;margin-bottom:6px}
.db-hero-sub{font-size:13px;color:#94a3b8;margin-bottom:18px}
.db-hero-stats{display:flex;gap:28px;flex-wrap:wrap}
.db-stat{display:flex;flex-direction:column;gap:2px}
.db-stat-val{font-family:'JetBrains Mono',monospace;font-size:22px;font-weight:700;color:#f1f5f9;letter-spacing:-.02em}
.db-stat-lbl{font-size:10px;color:#64748b;text-transform:uppercase;letter-spacing:.07em}

/* Live dot — identical to visitors.js .v-live-dot */
.db-live-dot{display:inline-block;width:9px;height:9px;background:#10b981;border-radius:50%;box-shadow:0 0 0 0 rgba(16,185,129,.4);animation:_dlp 2s infinite;margin-right:6px;vertical-align:middle}
@keyframes _dlp{0%{box-shadow:0 0 0 0 rgba(16,185,129,.4)}70%{box-shadow:0 0 0 7px rgba(16,185,129,0)}100%{box-shadow:0 0 0 0 rgba(16,185,129,0)}}

/* Controls */
.db-controls{display:flex;align-items:center;gap:10px;flex-wrap:wrap}
.db-select{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;color:#1e293b;background:#fff;border:1px solid #e2e8f0;border-radius:8px;padding:7px 11px;outline:none;min-width:220px}
.db-select:focus{border-color:#6366f1;box-shadow:0 0 0 3px rgba(99,102,241,.1)}
.db-btn{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;font-weight:600;padding:7px 16px;border-radius:8px;border:none;cursor:pointer;transition:all .14s;display:inline-flex;align-items:center;gap:5px}
.db-btn-outline{background:#fff;color:#64748b;border:1px solid #e2e8f0}
.db-btn-outline:hover{background:#f8fafc;color:#1e293b}

/* Metrics grid — identical to visitors.js .v-metrics */
.db-metrics{display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:12px}
.db-metric{background:#fff;border:1px solid #e2e8f0;border-radius:12px;padding:16px 18px;position:relative;overflow:hidden;transition:box-shadow .16s,transform .16s}
.db-metric:hover{box-shadow:0 4px 16px rgba(0,0,0,.07);transform:translateY(-1px)}
.db-metric::before{content:'';position:absolute;top:0;left:0;right:0;height:3px;background:var(--a,#6366f1);border-radius:12px 12px 0 0}
.db-metric-icon{width:32px;height:32px;border-radius:8px;background:var(--al,#eef2ff);display:flex;align-items:center;justify-content:center;font-size:15px;margin-bottom:10px}
.db-metric-val{font-family:'JetBrains Mono',monospace;font-size:26px;font-weight:700;color:#0f172a;line-height:1;letter-spacing:-.02em}
.db-metric-lbl{font-size:10px;color:#94a3b8;text-transform:uppercase;letter-spacing:.06em;font-weight:700;margin-top:5px}

/* Panel — identical to visitors.js .v-panel */
.db-panel{background:#fff;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden}
.db-panel-hd{display:flex;align-items:center;justify-content:space-between;padding:14px 20px;border-bottom:1px solid #f1f5f9}
.db-panel-title{font-size:13px;font-weight:700;color:#0f172a;display:flex;align-items:center;gap:7px}
.db-panel-meta{font-size:11px;color:#94a3b8;font-family:'JetBrains Mono',monospace}
.db-panel-body{padding:16px 20px}

/* Health widget */
.db-health-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:10px}
.db-health-item{display:flex;align-items:center;gap:10px;padding:12px 14px;border-radius:10px;border:1px solid #e2e8f0;background:#f8fafc}
.db-health-dot{width:10px;height:10px;border-radius:50%;flex-shrink:0}
.db-health-ok{background:#10b981;box-shadow:0 0 0 3px rgba(16,185,129,.2)}
.db-health-warn{background:#f59e0b;box-shadow:0 0 0 3px rgba(245,158,11,.2)}
.db-health-err{background:#ef4444;box-shadow:0 0 0 3px rgba(239,68,68,.2)}
.db-health-unknown{background:#94a3b8}
.db-health-label{font-size:12.5px;font-weight:600;color:#1e293b}
.db-health-sub{font-size:10.5px;color:#94a3b8;margin-top:1px;font-family:'JetBrains Mono',monospace}

/* Grid layouts */
.db-grid2{display:grid;grid-template-columns:1fr 1fr;gap:14px}
.db-grid3{display:grid;grid-template-columns:repeat(3,1fr);gap:12px}
@media(max-width:700px){.db-grid2,.db-grid3{grid-template-columns:1fr}}

/* Table */
.db-table-wrap{border-radius:10px;overflow:hidden;border:1px solid #e2e8f0}
.db-table{width:100%;border-collapse:collapse;font-size:12.5px}
.db-table thead th{background:#f8fafc;padding:8px 14px;text-align:left;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.07em;color:#94a3b8;border-bottom:1px solid #e2e8f0;white-space:nowrap}
.db-table tbody td{padding:11px 14px;border-bottom:1px solid #f1f5f9;color:#334155;vertical-align:middle}
.db-table tbody tr:last-child td{border-bottom:none}
.db-table tbody tr:hover{background:#fafbff}

/* Pill */
.db-pill{display:inline-flex;padding:2px 8px;border-radius:999px;font-size:10px;font-weight:700}
.db-pill-amber{background:#fef3c7;color:#92400e}
.db-pill-blue{background:#dbeafe;color:#1e40af}
.db-pill-green{background:#d1fae5;color:#065f46}
.db-pill-red{background:#fee2e2;color:#dc2626}

/* Quick links */
.db-quick-link{display:block;background:#fff;border:1px solid #e2e8f0;border-radius:12px;padding:16px 18px;text-decoration:none;transition:box-shadow .16s,transform .16s}
.db-quick-link:hover{box-shadow:0 4px 16px rgba(0,0,0,.08);transform:translateY(-2px)}
.db-quick-icon{font-size:22px;margin-bottom:8px}
.db-quick-title{font-size:13px;font-weight:700;color:#0f172a;margin-bottom:3px}
.db-quick-desc{font-size:12px;color:#94a3b8;line-height:1.5}

/* Canvas chart wrapper */
.db-chart-wrap{position:relative;width:100%;height:200px}

/* Empty */
.db-empty{text-align:center;padding:32px;color:#94a3b8;font-size:12.5px}

/* Skeleton */
.db-skel{background:linear-gradient(90deg,#f1f5f9 25%,#e2e8f0 50%,#f1f5f9 75%);background-size:200% 100%;animation:_sh 1.4s infinite;border-radius:8px}
@keyframes _sh{0%{background-position:200% 0}100%{background-position:-200% 0}}
  `;
  document.head.appendChild(s);
};

// ─── Pure-canvas bar chart ────────────────────────────────────────────────────

const drawBarChart = (canvas, labels, data) => {
  const ctx=canvas.getContext('2d'), W=canvas.width, H=canvas.height;
  const max=Math.max(...data,1), pad={t:10,r:10,b:28,l:32};
  const cW=W-pad.l-pad.r, cH=H-pad.t-pad.b;
  ctx.clearRect(0,0,W,H);
  ctx.strokeStyle='#f1f5f9'; ctx.lineWidth=1;
  for(let i=0;i<=4;i++){const y=pad.t+cH-(i/4)*cH; ctx.beginPath();ctx.moveTo(pad.l,y);ctx.lineTo(pad.l+cW,y);ctx.stroke(); ctx.fillStyle='#94a3b8';ctx.font='9px JetBrains Mono,monospace';ctx.textAlign='right'; ctx.fillText(Math.round((i/4)*max),pad.l-4,y+3);}
  const barW=Math.max(4,(cW/labels.length)*0.65), step=cW/labels.length;
  data.forEach((v,i)=>{const bH=(v/max)*cH,x=pad.l+i*step+(step-barW)/2,y=pad.t+cH-bH,r=Math.min(4,barW/2);ctx.fillStyle='rgba(99,102,241,0.75)';ctx.beginPath();ctx.moveTo(x+r,y);ctx.lineTo(x+barW-r,y);ctx.arcTo(x+barW,y,x+barW,y+r,r);ctx.lineTo(x+barW,y+bH);ctx.lineTo(x,y+bH);ctx.arcTo(x,y,x+r,y,r);ctx.closePath();ctx.fill();});
  ctx.fillStyle='#94a3b8';ctx.font='9px Plus Jakarta Sans,system-ui';ctx.textAlign='center';
  const every=Math.ceil(labels.length/7);
  labels.forEach((lbl,i)=>{if(i%every===0||i===labels.length-1) ctx.fillText(lbl,pad.l+i*step+step/2,pad.t+cH+16);});
};

// ─── Panel factory ────────────────────────────────────────────────────────────

const mkPanel = (title, meta='') => {
  const panel=el('div',{class:'db-panel'}); const hd=el('div',{class:'db-panel-hd'});
  hd.appendChild(el('div',{class:'db-panel-title'},title));
  if(meta) hd.appendChild(el('div',{class:'db-panel-meta'},meta));
  panel.appendChild(hd);
  const body=el('div',{class:'db-panel-body'}); panel.appendChild(body);
  return {panel,body};
};

// ─── Main export ──────────────────────────────────────────────────────────────

export const renderDashboardView = async (container, { apiClient, toast } = {}) => {
  injectStyles();
  const client   = apiClient || createApiClient();
  const notifier = toast     || createToastManager();
  const state    = { sites:[], siteId:loadSiteId(), liveCount:0, liveTimer:null };

  const root = el('div',{class:'db-root'});
  container.appendChild(root);

  // ── Hero ───────────────────────────────────────────────────────────────────
  const hero = el('div',{class:'db-hero'});
  hero.appendChild(el('div',{class:'db-hero-title'},'📊 Dashboard'));
  hero.appendChild(el('div',{class:'db-hero-sub'},new Date().toLocaleDateString('en-GB',{weekday:'long',year:'numeric',month:'long',day:'numeric'})));

  const heroStats = el('div',{class:'db-hero-stats'});
  const mkStat = (lbl) => { const w=el('div',{class:'db-stat'}); const v=el('div',{class:'db-stat-val'},'—'); w.append(v,el('div',{class:'db-stat-lbl'},lbl)); heroStats.appendChild(w); return v; };
  const hLive    = mkStat('Live Now');
  const hToday   = mkStat("Today's Visitors");
  const hLeads   = mkStat('Active Leads');
  const hTickets = mkStat('Open Tickets');
  hero.appendChild(heroStats);
  root.appendChild(hero);

  // ── Controls ───────────────────────────────────────────────────────────────
  const controls = el('div',{class:'db-controls'});
  const siteSelect = el('select',{class:'db-select'},el('option',{value:''},'Loading sites…'));
  const refreshBtn = el('button',{class:'db-btn db-btn-outline'},'↻ Refresh');
  controls.append(siteSelect, refreshBtn);
  root.appendChild(controls);

  // ── Metric cards ───────────────────────────────────────────────────────────
  const metricsGrid = el('div',{class:'db-metrics'});
  const mkMetric = ({icon,lbl,a,al}) => {
    const card=el('div',{class:'db-metric',style:`--a:${a};--al:${al}`});
    const ico=el('div',{class:'db-metric-icon'},icon);
    const val=el('div',{class:'db-metric-val'},'—');
    const label=el('div',{class:'db-metric-lbl'},lbl);
    card.append(ico,val,label); metricsGrid.appendChild(card); return val;
  };
  const mVisitors  = mkMetric({icon:'👥',lbl:'Visitors (30d)',     a:'#6366f1',al:'#eef2ff'});
  const mLeads2    = mkMetric({icon:'⭐',lbl:'Captured Leads',     a:'#f59e0b',al:'#fef3c7'});
  const mTickets2  = mkMetric({icon:'🎫',lbl:'Open Tickets',       a:'#ef4444',al:'#fee2e2'});
  const mKnowledge = mkMetric({icon:'📚',lbl:'Knowledge Sources',  a:'#10b981',al:'#d1fae5'});
  root.appendChild(metricsGrid);

  // ── Health check widget ────────────────────────────────────────────────────
  const {panel:healthPanel, body:healthBody} = mkPanel('🔍 Site Health Check');
  const healthGrid = el('div',{class:'db-health-grid'});

  const mkHealthItem = (label, sub, status='unknown') => {
    const item=el('div',{class:'db-health-item'});
    const cls = status==='ok'?'db-health-ok':status==='warn'?'db-health-warn':status==='err'?'db-health-err':'db-health-unknown';
    const dot=el('div',{class:`db-health-dot ${cls}`});
    const info=el('div',{}); info.appendChild(el('div',{class:'db-health-label'},label)); info.appendChild(el('div',{class:'db-health-sub'},sub));
    item.append(dot,info); healthGrid.appendChild(item); return {item,dot,info};
  };

  const hTracker = mkHealthItem('Tracker Script','Checking…','unknown');
  const hEngage  = mkHealthItem('Engage Widget', 'Checking…','unknown');
  const hEvents  = mkHealthItem('Events Received','—','unknown');
  const hLastEvt = mkHealthItem('Last Event','—','unknown');
  healthBody.appendChild(healthGrid);
  root.appendChild(healthPanel);

  const updateHealth = (status) => {
    const upd = (item, label, sub, st) => {
      const cls = st==='ok'?'db-health-ok':st==='warn'?'db-health-warn':st==='err'?'db-health-err':'db-health-unknown';
      item.dot.className = `db-health-dot ${cls}`;
      item.info.children[0].textContent = label;
      item.info.children[1].textContent = sub;
    };
    if (!status) {
      upd(hTracker,'Tracker Script','Not detected','warn');
      upd(hEngage, 'Engage Widget', 'Unknown','unknown');
      upd(hEvents, 'Events Received','No events yet','warn');
      upd(hLastEvt,'Last Event','—','unknown');
      return;
    }
    upd(hTracker,'Tracker Script', status.isInstalled?'Active ✓':'Not detected yet', status.isInstalled?'ok':'warn');
    upd(hEngage, 'Engage Widget',  status.isConfigured?'Configured ✓':'Not configured', status.isConfigured?'ok':'warn');
    upd(hEvents, 'Events Received', status.firstEventReceivedAtUtc?'Events flowing ✓':'No events yet', status.firstEventReceivedAtUtc?'ok':'warn');
    upd(hLastEvt,'Last Event', status.firstEventReceivedAtUtc?fmtAgo(status.firstEventReceivedAtUtc):'—', status.firstEventReceivedAtUtc?'ok':'unknown');
  };

  // ── Charts row ─────────────────────────────────────────────────────────────
  const chartsRow = el('div',{class:'db-grid2'});
  const {panel:convPanel, body:convBody} = mkPanel('💬 Conversations — Last 7 Days');
  const convWrap=el('div',{class:'db-chart-wrap'}); const convCanvas=el('canvas',{style:'width:100%;height:200px'});
  convWrap.appendChild(convCanvas); convBody.appendChild(convWrap);
  const {panel:pipePanel, body:pipeBody} = mkPanel('🎯 Lead Pipeline');
  chartsRow.append(convPanel, pipePanel);
  root.appendChild(chartsRow);

  // ── Recent leads ───────────────────────────────────────────────────────────
  const {panel:leadsPanel, body:leadsBody} = mkPanel('⭐ Recent Leads');
  const tableWrap = el('div',{class:'db-table-wrap'});
  const table = el('table',{class:'db-table'});
  table.appendChild(el('thead',{},el('tr',{}, ...['Name','Email','Opportunity','Captured'].map(c=>el('th',{},c)))));
  const tbody=el('tbody',{}); tbody.appendChild(el('tr',{},el('td',{colspan:'4',class:'db-empty'},'⏳ Loading…')));
  table.appendChild(tbody); tableWrap.appendChild(table); leadsBody.appendChild(tableWrap);
  root.appendChild(leadsPanel);

  // ── Quick links ────────────────────────────────────────────────────────────
  const quickGrid = el('div',{class:'db-grid3'});
  [{icon:'📚',title:'Knowledge Base',   desc:'Upload docs & URLs for your AI bot', href:'#/knowledge'},
   {icon:'💬',title:'Conversations',    desc:'Review Engage chat sessions',         href:'#/engage'},
   {icon:'⭐',title:'Leads',            desc:'Browse captured leads',               href:'#/leads'},
  ].forEach(({icon,title,desc,href})=>{
    const a=el('a',{class:'db-quick-link',href});
    a.append(el('div',{class:'db-quick-icon'},icon),el('div',{class:'db-quick-title'},title),el('div',{class:'db-quick-desc'},desc));
    quickGrid.appendChild(a);
  });
  root.appendChild(quickGrid);

  // ── Live visitor poll ──────────────────────────────────────────────────────
  const pollLive = async () => {
    if (!state.siteId) return;
    try {
      const res = await client.visitors.onlineNow(state.siteId, 5, 1);
      state.liveCount = res?.count ?? 0;
      hLive.replaceChildren(el('span',{class:'db-live-dot'}),document.createTextNode(String(state.liveCount)));
    } catch {}
  };

  // ── Main data load ─────────────────────────────────────────────────────────
  const loadAll = async () => {
    if (!state.siteId) return;

    const [visitRes, leadsRes, ticketsRes, kbRes, engRes, healthRes] = await Promise.allSettled([
      client.visitors.visitCounts(state.siteId),
      client.leads.list(state.siteId, 1, 100),
      client.tickets.listTickets({siteId:state.siteId, page:1, pageSize:100}),
      client.knowledge.listSources(state.siteId),
      client.engage.getOpportunityAnalytics(state.siteId),
      client.request(`/sites/${state.siteId}/installation-status`),
    ]);

    const visitData  = visitRes.status  ==='fulfilled'?visitRes.value:null;
    const leadsData  = leadsRes.status  ==='fulfilled'&&Array.isArray(leadsRes.value)?leadsRes.value:[];
    const ticketData = ticketsRes.status==='fulfilled'&&Array.isArray(ticketsRes.value)?ticketsRes.value:[];
    const kbData     = kbRes.status     ==='fulfilled'&&Array.isArray(kbRes.value)?kbRes.value:[];
    const engData    = engRes.status    ==='fulfilled'?engRes.value:null;
    const healthData = healthRes.status ==='fulfilled'?healthRes.value:null;

    const openTickets = ticketData.filter(t=>{ const s=(t.status||'').toLowerCase().replace(/[^a-z]/g,''); return s==='open'||s==='inprogress'; }).length;

    // Metric cards
    mVisitors.textContent  = String(visitData?.last30 ?? '—');
    mLeads2.textContent    = String(leadsData.length);
    mTickets2.textContent  = String(openTickets);
    mKnowledge.textContent = String(kbData.length);

    // Hero live stats (today approximation from 30d data)
    hToday.textContent   = String(visitData?.today ?? visitData?.last7 ?? '—');
    hLeads.textContent   = String(leadsData.length);
    hTickets.textContent = String(openTickets);

    // Health check
    updateHealth(healthData);

    // Recent leads table
    tbody.replaceChildren();
    if (!leadsData.length) {
      tbody.appendChild(el('tr',{},el('td',{colspan:'4',class:'db-empty'},'No leads yet')));
    } else {
      const PIPE_LABELS = {evaluating:'🔍 Evaluating',deciding:'⚖️ Deciding',won:'🏆 Won',lost:'❌ Lost'};
      const pipePill = opp => { const k=(opp||'').toLowerCase().replace(/[^a-z]/g,''); const cls=k.includes('won')?'db-pill-green':k.includes('lost')?'db-pill-red':k.includes('decid')?'db-pill-blue':'db-pill-amber'; return el('span',{class:`db-pill ${cls}`},opp||'—'); };
      leadsData.slice(0,8).forEach(lead=>{
        const tr=el('tr',{});
        tr.append(
          el('td',{style:'font-weight:600;color:#1e293b'},lead.displayName||lead.fullName||lead.name||'Anonymous'),
          el('td',{style:'font-family:JetBrains Mono,monospace;font-size:11.5px;color:#64748b'},lead.primaryEmail||lead.email||'—'),
          el('td',{},pipePill(lead.opportunityLabel||lead.opportunity)),
          el('td',{style:'font-size:11.5px;color:#94a3b8'},fmtDate(lead.createdAtUtc||lead.createdAt))
        );
        tbody.appendChild(tr);
      });
    }

    // Conversations bar chart (last 7 days)
    const dailyPoints = Array.isArray(engData?.opportunitiesOverTime)?engData.opportunitiesOverTime:[];
    const todayMid = new Date(); todayMid.setHours(0,0,0,0);
    const last7 = Array.from({length:7},(_,i)=>{ const d=new Date(todayMid); d.setDate(d.getDate()-(6-i)); return d; });
    const convLabels = last7.map(d=>d.toLocaleDateString('en-GB',{day:'numeric',month:'short'}));
    const convData   = last7.map(d=>{ const iso=d.toISOString().slice(0,10); const m=dailyPoints.find(p=>p.dateUtc&&String(p.dateUtc).slice(0,10)===iso); return m?m.count:0; });

    // Lead pipeline breakdown
    const pipeline={};
    leadsData.forEach(l=>{const k=(l.opportunityLabel||l.opportunity||'Unknown'); pipeline[k]=(pipeline[k]||0)+1;});
    const pipeEntries=Object.entries(pipeline);
    const PIPE_COLORS={evaluating:'#f59e0b',deciding:'#3b82f6',won:'#10b981',lost:'#ef4444',unknown:'#94a3b8'};

    requestAnimationFrame(()=>{
      const cW=convWrap.getBoundingClientRect().width||500;
      convCanvas.width=cW; convCanvas.height=200;
      drawBarChart(convCanvas,convLabels,convData);

      pipeBody.replaceChildren();
      if (!pipeEntries.length) {
        pipeBody.appendChild(el('div',{class:'db-empty'},'No leads yet'));
      } else {
        const total=leadsData.length;
        const pipeList=el('div',{style:'display:flex;flex-direction:column;gap:8px'});
        pipeEntries.sort((a,b)=>b[1]-a[1]).forEach(([lbl,count])=>{
          const pct=total>0?Math.round(count/total*100):0;
          const key=lbl.toLowerCase().replace(/[^a-z]/g,'');
          const color=PIPE_COLORS[key]||'#94a3b8';
          const row=el('div',{style:'display:flex;align-items:center;gap:10px'});
          const labelEl=el('div',{style:'font-size:12.5px;font-weight:500;color:#334155;min-width:100px'},lbl);
          const barWrap=el('div',{style:'flex:1;height:6px;background:#e2e8f0;border-radius:999px;overflow:hidden'});
          const bar=el('div',{style:`height:100%;border-radius:999px;background:${color};width:0;transition:width .5s cubic-bezier(.34,1.56,.64,1)`});
          barWrap.appendChild(bar); setTimeout(()=>bar.style.width=`${pct}%`,80);
          const countEl=el('div',{style:'font-family:JetBrains Mono,monospace;font-size:11px;color:#94a3b8;min-width:32px;text-align:right'},`${count}`);
          row.append(labelEl,barWrap,countEl);
          pipeList.appendChild(row);
        });
        pipeBody.appendChild(pipeList);
      }
    });

    await pollLive();
  };

  // ── Wire ───────────────────────────────────────────────────────────────────
  siteSelect.addEventListener('change',()=>{ state.siteId=siteSelect.value; saveSiteId(state.siteId); loadAll(); });
  refreshBtn.addEventListener('click',loadAll);

  // ── Init ───────────────────────────────────────────────────────────────────
  try {
    const sites = await client.sites.list();
    state.sites = Array.isArray(sites)?sites:[];
    siteSelect.innerHTML='';
    if (!state.sites.length) { siteSelect.appendChild(el('option',{value:''},'No sites')); updateHealth(null); return; }
    state.sites.forEach(s=>{const id=getSiteId(s);const opt=el('option',{value:id},s.domain||id);if(id===state.siteId)opt.selected=true;siteSelect.appendChild(opt);});
    if (!state.siteId||!state.sites.find(s=>getSiteId(s)===state.siteId)) state.siteId=getSiteId(state.sites[0]);
    siteSelect.value=state.siteId;
    await loadAll();
    state.liveTimer = setInterval(pollLive, 15000);
  } catch(err) {
    const uiErr=mapApiError(err); if(uiErr.status!==401) notifier.show({message:uiErr.message,variant:'warning'});
  }
};
