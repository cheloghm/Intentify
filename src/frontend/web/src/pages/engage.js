/**
 * engage.js — Intentify Engage Page
 * Phase 5: Conversation inbox, widget customisation with live preview,
 *          auto-trigger rule builder, bot configuration wizard
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

const fmtDate  = v => { if (!v) return '—'; const d = new Date(v); return isNaN(d) ? '—' : d.toLocaleDateString('en-GB', {day:'numeric',month:'short',year:'numeric'}); };
const fmtTime  = v => { if (!v) return '—'; const d = new Date(v); return isNaN(d) ? '—' : d.toLocaleString('en-GB'); };
const fmtAgo   = v => { if (!v) return '—'; const m = Math.floor((Date.now()-new Date(v))/60000); if (m<1) return 'just now'; if (m<60) return `${m}m ago`; const h=Math.floor(m/60); if (h<24) return `${h}h ago`; return `${Math.floor(h/24)}d ago`; };
const getSiteId = s => s?.siteId || s?.id || '';
const SITE_KEY  = 'intentify.selectedSiteId';
const loadSiteId = () => { try { return localStorage.getItem(SITE_KEY)||''; } catch { return ''; } };
const saveSiteId = id => { try { id ? localStorage.setItem(SITE_KEY,id) : localStorage.removeItem(SITE_KEY); } catch {} };

const TRIGGER_TYPES = [
  { value: 'time_on_page',   label: '⏱ Time on page',   unit: 'seconds', placeholder: '30' },
  { value: 'scroll_percent', label: '↕ Scroll %',        unit: '%',       placeholder: '70' },
  { value: 'exit_intent',    label: '🚪 Exit intent',    unit: '',        placeholder: '' },
  { value: 'url_match',      label: '🔗 URL contains',  unit: 'text',    placeholder: '/pricing' },
];

// ─── Styles ───────────────────────────────────────────────────────────────────

const injectStyles = () => {
  if (document.getElementById('_eng_css')) return;
  const s = document.createElement('style');
  s.id = '_eng_css';
  s.textContent = `
@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap');
.e-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;gap:20px;width:100%;max-width:1280px;padding-bottom:60px}
/* Hero */
.e-hero{background:linear-gradient(135deg,#0f172a,#1e293b);border-radius:16px;padding:26px 34px;position:relative;overflow:hidden}
.e-hero::before{content:'';position:absolute;top:-20px;right:-20px;width:160px;height:160px;background:radial-gradient(circle,rgba(16,185,129,.18) 0%,transparent 70%);pointer-events:none}
.e-hero-title{font-size:22px;font-weight:700;color:#f8fafc;letter-spacing:-.02em;margin-bottom:4px}
.e-hero-sub{font-size:12.5px;color:#64748b;margin-bottom:18px}
.e-hero-stats{display:flex;gap:28px;flex-wrap:wrap}
.e-stat-val{font-family:'JetBrains Mono',monospace;font-size:24px;font-weight:700;color:#f1f5f9;line-height:1}
.e-stat-lbl{font-size:10px;color:#475569;text-transform:uppercase;letter-spacing:.07em;margin-top:3px}
/* Controls */
.e-controls{display:flex;align-items:center;gap:10px;flex-wrap:wrap}
.e-select{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;color:#1e293b;background:#fff;border:1px solid #e2e8f0;border-radius:8px;padding:7px 11px;outline:none}
.e-select:focus{border-color:#6366f1;box-shadow:0 0 0 3px rgba(99,102,241,.1)}
/* Tabs */
.e-tabs{display:flex;gap:3px;background:#f1f5f9;border-radius:10px;padding:3px}
.e-tab{flex:1;padding:7px 14px;border-radius:8px;border:none;font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:12px;font-weight:500;color:#64748b;cursor:pointer;transition:all .14s;white-space:nowrap}
.e-tab:hover{background:rgba(255,255,255,.7)}
.e-tab.active{background:#fff;color:#6366f1;font-weight:700;box-shadow:0 1px 4px rgba(0,0,0,.08)}
/* Panel */
.e-panel{background:#fff;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden}
.e-panel-hd{display:flex;align-items:center;justify-content:space-between;padding:14px 20px;border-bottom:1px solid #f1f5f9}
.e-panel-title{font-size:13px;font-weight:700;color:#0f172a;display:flex;align-items:center;gap:7px}
.e-panel-meta{font-size:11px;color:#94a3b8;font-family:'JetBrains Mono',monospace}
.e-panel-body{padding:18px 20px}
/* Inbox */
.e-inbox{display:grid;grid-template-columns:280px 1fr;gap:0;height:560px}
@media(max-width:800px){.e-inbox{grid-template-columns:1fr;height:auto}}
.e-inbox-list{border-right:1px solid #f1f5f9;overflow-y:auto;display:flex;flex-direction:column}
.e-conv-item{padding:12px 16px;border-bottom:1px solid #f8fafc;cursor:pointer;transition:background .12s;position:relative}
.e-conv-item:hover{background:#f8fafc}
.e-conv-item.active{background:#eef2ff}
.e-conv-item-top{display:flex;align-items:flex-start;justify-content:space-between;gap:6px;margin-bottom:4px}
.e-conv-name{font-size:12.5px;font-weight:600;color:#1e293b}
.e-conv-time{font-size:10px;color:#94a3b8;flex-shrink:0}
.e-conv-preview{font-size:11.5px;color:#64748b;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.e-conv-badges{display:flex;gap:4px;margin-top:5px;flex-wrap:wrap}
.e-badge{display:inline-flex;align-items:center;gap:2px;padding:1px 6px;border-radius:999px;font-size:9.5px;font-weight:700}
.e-badge-lead{background:#d1fae5;color:#065f46}
.e-badge-ticket{background:#dbeafe;color:#1e40af}
.e-badge-new{background:#fef3c7;color:#92400e}
.e-unread-dot{position:absolute;top:14px;right:14px;width:8px;height:8px;background:#6366f1;border-radius:50%}
/* Thread */
.e-thread{display:flex;flex-direction:column;overflow:hidden}
.e-thread-hd{padding:14px 20px;border-bottom:1px solid #f1f5f9;background:#f8fafc;display:flex;align-items:center;justify-content:space-between}
.e-thread-title{font-size:13px;font-weight:700;color:#0f172a}
.e-thread-meta{font-size:11px;color:#94a3b8}
.e-messages{flex:1;overflow-y:auto;padding:16px;display:flex;flex-direction:column;gap:8px;min-height:300px}
.e-msg{max-width:80%;padding:9px 12px;border-radius:10px;font-size:12.5px;line-height:1.5}
.e-msg-user{background:#6366f1;color:#fff;align-self:flex-end;border-radius:10px 10px 2px 10px}
.e-msg-bot{background:#f1f5f9;color:#334155;align-self:flex-start;border-radius:10px 10px 10px 2px}
.e-msg-time{font-size:9.5px;opacity:.6;margin-top:3px;text-align:right}
/* Settings form */
.e-form-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(200px,1fr));gap:14px}
.e-field{display:flex;flex-direction:column;gap:5px}
.e-field-label{font-size:10.5px;font-weight:700;letter-spacing:.05em;text-transform:uppercase;color:#94a3b8}
.e-field-hint{font-size:10px;color:#94a3b8;margin-top:2px}
.e-input{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;color:#1e293b;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:8px 11px;outline:none;width:100%;transition:border .14s}
.e-input:focus{border-color:#6366f1;background:#fff;box-shadow:0 0 0 3px rgba(99,102,241,.1)}
.e-textarea{resize:vertical;min-height:72px;font-family:inherit}
.e-btn{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;font-weight:600;padding:8px 18px;border-radius:8px;border:none;cursor:pointer;transition:all .14s;display:inline-flex;align-items:center;gap:5px}
.e-btn-primary{background:#6366f1;color:#fff}
.e-btn-primary:hover:not(:disabled){background:#4f46e5;transform:translateY(-1px);box-shadow:0 4px 12px rgba(99,102,241,.25)}
.e-btn-primary:disabled{opacity:.5;cursor:not-allowed}
.e-btn-outline{background:#fff;color:#64748b;border:1px solid #e2e8f0}
.e-btn-outline:hover{background:#f8fafc;color:#1e293b}
.e-btn-sm{padding:5px 12px;font-size:11.5px}
.e-btn-danger{background:#fee2e2;color:#dc2626;border:none}
.e-btn-danger:hover{background:#fecaca}
/* Widget preview */
.e-preview-wrap{position:relative;height:340px;background:linear-gradient(135deg,#e2e8f0,#cbd5e1);border-radius:12px;overflow:hidden;margin-top:16px}
.e-preview-site{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;color:#94a3b8;font-size:13px}
.e-widget-preview{position:absolute;bottom:20px;right:20px;display:flex;flex-direction:column;align-items:flex-end;gap:8px}
.e-widget-preview.left{right:auto;left:20px;align-items:flex-start}
.e-widget-bubble{background:#fff;border:1px solid #e2e8f0;border-radius:12px;padding:10px 14px;font-size:12px;color:#1e293b;box-shadow:0 4px 14px rgba(0,0,0,.12);max-width:220px;line-height:1.5;display:none}
.e-widget-bubble.show{display:block}
.e-widget-launcher{width:52px;height:52px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:22px;box-shadow:0 4px 14px rgba(0,0,0,.2);cursor:pointer;transition:transform .2s}
.e-widget-launcher:hover{transform:scale(1.08)}
/* Auto-triggers */
.e-trigger-list{display:flex;flex-direction:column;gap:10px}
.e-trigger-item{display:grid;grid-template-columns:160px 100px 1fr auto auto;gap:10px;align-items:center;background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:12px 14px}
@media(max-width:700px){.e-trigger-item{grid-template-columns:1fr 1fr;grid-template-rows:auto auto auto}}
/* Empty */
.e-empty{text-align:center;padding:48px 20px;display:flex;flex-direction:column;align-items:center;gap:8px}
.e-empty-icon{font-size:36px;opacity:.3}
.e-empty-title{font-size:14px;font-weight:600;color:#334155}
.e-empty-desc{font-size:12px;color:#94a3b8;max-width:260px;line-height:1.6}
/* Toggle */
.e-toggle{position:relative;width:36px;height:19px;flex-shrink:0}
.e-toggle input{opacity:0;width:0;height:0;position:absolute}
.e-toggle-slider{position:absolute;inset:0;background:#e2e8f0;border-radius:999px;cursor:pointer;transition:.18s}
.e-toggle-slider::before{content:'';position:absolute;left:3px;top:3px;width:13px;height:13px;background:#fff;border-radius:50%;transition:.18s;box-shadow:0 1px 3px rgba(0,0,0,.2)}
.e-toggle input:checked+.e-toggle-slider{background:#6366f1}
.e-toggle input:checked+.e-toggle-slider::before{transform:translateX(17px)}
  `;
  document.head.appendChild(s);
};

// ─── Main export ──────────────────────────────────────────────────────────────

export const renderEngageView = async (container, { apiClient, toast } = {}) => {
  injectStyles();
  const client   = apiClient || createApiClient();
  const notifier = toast     || createToastManager();

  const state = {
    sites: [], siteId: loadSiteId(),
    conversations: [], selectedConv: null, messages: [],
    bot: null, saving: false, activeTab: 'inbox',
    triggers: [], // parsed from bot.autoTriggerRulesJson
  };

  const root = el('div', { class: 'e-root' });
  container.appendChild(root);

  // ── Hero ───────────────────────────────────────────────────────────────────
  const hero = el('div', { class: 'e-hero' });
  hero.appendChild(el('div', { class: 'e-hero-title' }, '💬 Engage'));
  hero.appendChild(el('div', { class: 'e-hero-sub' }, 'Chat inbox, widget customisation, and auto-trigger rules'));
  const heroStats = el('div', { class: 'e-hero-stats' });
  const mkStat = lbl => { const w=el('div',{}); const v=el('div',{class:'e-stat-val'},'—'); w.append(v,el('div',{class:'e-stat-lbl'},lbl)); heroStats.appendChild(w); return v; };
  const hConvs = mkStat('Conversations');
  const hLeads = mkStat('With Leads');
  const hTickets = mkStat('With Tickets');
  hero.appendChild(heroStats);
  root.appendChild(hero);

  // ── Controls ───────────────────────────────────────────────────────────────
  const controls = el('div', { class: 'e-controls' });
  const siteSelect = el('select', { class: 'e-select' }, el('option',{value:''},'Loading…'));
  const refreshBtn = el('button', { class: 'e-btn e-btn-outline e-btn-sm' }, '↻ Refresh');
  controls.append(siteSelect, refreshBtn);
  root.appendChild(controls);

  // ── Tabs ───────────────────────────────────────────────────────────────────
  const tabBar = el('div', { class: 'e-tabs' });
  const TABS = [
    { key: 'inbox',   label: '📬 Inbox'      },
    { key: 'widget',  label: '🎨 Widget'      },
    { key: 'triggers',label: '⚡ Auto-Triggers'},
    { key: 'bot',     label: '🤖 Bot Config'  },
    { key: 'survey',  label: '📋 Survey'      },
  ];
  const tabEls = {}, panelEls = {};
  TABS.forEach(({ key, label }) => {
    const btn = el('button', { class: `e-tab${key===state.activeTab?' active':''}` }, label);
    btn.addEventListener('click', () => switchTab(key));
    tabEls[key] = btn; tabBar.appendChild(btn);
  });
  TABS.forEach(({ key }) => {
    const p = el('div', { style: key===state.activeTab?'':'display:none' });
    panelEls[key] = p; root.appendChild(p);
  });
  root.insertBefore(tabBar, panelEls.inbox);
  const switchTab = key => {
    state.activeTab = key;
    Object.entries(tabEls).forEach(([k,b]) => b.classList.toggle('active', k===key));
    Object.entries(panelEls).forEach(([k,p]) => { p.style.display = k===key?'':'none'; });
  };

  // ══════════════════════════════════════════════════════════════════════════
  // TAB: INBOX
  // ══════════════════════════════════════════════════════════════════════════

  const { panel: inboxPanel } = mkPanel('📬 Conversation Inbox', '', panelEls.inbox);
  const inbox = el('div', { class: 'e-inbox' });
  inboxPanel.appendChild(inbox);

  const convList = el('div', { class: 'e-inbox-list' });
  const threadArea = el('div', { class: 'e-thread', style: 'flex:1;border-left:1px solid #f1f5f9' });
  inbox.append(convList, threadArea);

  const renderThread = async (conv) => {
    state.selectedConv = conv;
    threadArea.replaceChildren();
    if (!conv) { threadArea.appendChild(el('div',{class:'e-empty'},el('div',{class:'e-empty-icon'},'💬'),el('div',{class:'e-empty-title'},'Select a conversation'))); return; }

    const hd = el('div', { class: 'e-thread-hd' });
    hd.appendChild(el('div', { class: 'e-thread-title' }, `Session ${(conv.sessionId||'').slice(0,8)}…`));
    const metaWrap = el('div', { style: 'display:flex;align-items:center;gap:8px' });
    if (conv.hasLead)   metaWrap.appendChild(el('span',{class:'e-badge e-badge-lead'},'⭐ Lead'));
    if (conv.hasTicket) metaWrap.appendChild(el('span',{class:'e-badge e-badge-ticket'},'🎫 Ticket'));
    metaWrap.appendChild(el('div',{class:'e-thread-meta'},fmtDate(conv.createdAtUtc)));
    hd.appendChild(metaWrap);
    threadArea.appendChild(hd);

    const msgArea = el('div', { class: 'e-messages' });
    msgArea.appendChild(el('div',{style:'color:#94a3b8;font-size:11.5px;text-align:center;padding:8px'},'⏳ Loading messages…'));
    threadArea.appendChild(msgArea);

    try {
      const msgs = await client.engage.getConversationMessages(conv.sessionId, state.siteId);
      msgArea.replaceChildren();
      if (!msgs?.length) { msgArea.appendChild(el('div',{style:'color:#94a3b8;font-size:11.5px;text-align:center'},'No messages')); return; }
      msgs.forEach(m => {
        const isUser = m.role === 'user';
        const wrap = el('div', { style: `display:flex;flex-direction:column;align-items:${isUser?'flex-end':'flex-start'}` });
        const bubble = el('div', { class: `e-msg ${isUser?'e-msg-user':'e-msg-bot'}` }, m.content||'');
        bubble.appendChild(el('div',{class:'e-msg-time'},fmtTime(m.createdAtUtc)));
        wrap.appendChild(bubble);
        msgArea.appendChild(wrap);
      });
      msgArea.scrollTop = msgArea.scrollHeight;
    } catch { msgArea.replaceChildren(el('div',{style:'color:#ef4444;font-size:11.5px;text-align:center'},'Failed to load messages')); }
  };

  const renderConvList = () => {
    convList.replaceChildren();
    if (!state.conversations.length) {
      convList.appendChild(el('div',{class:'e-empty'},el('div',{class:'e-empty-icon'},'📭'),el('div',{class:'e-empty-title'},'No conversations yet'),el('div',{class:'e-empty-desc'},'Conversations appear here when visitors chat on your site.')));
      return;
    }
    // Update hero stats
    hConvs.textContent = String(state.conversations.length);
    hLeads.textContent = String(state.conversations.filter(c=>c.hasLead).length);
    hTickets.textContent = String(state.conversations.filter(c=>c.hasTicket).length);

    state.conversations.forEach(conv => {
      const item = el('div', { class: `e-conv-item${state.selectedConv?.sessionId===conv.sessionId?' active':''}` });
      const top = el('div', { class: 'e-conv-item-top' });
      top.append(el('div',{class:'e-conv-name'},`Session ${(conv.sessionId||'').slice(0,8)}…`), el('div',{class:'e-conv-time'},fmtAgo(conv.updatedAtUtc||conv.createdAtUtc)));
      item.appendChild(top);

      const preview = el('div', { class: 'e-conv-preview' }, 'Click to view conversation');
      item.appendChild(preview);

      const badges = el('div', { class: 'e-conv-badges' });
      if (conv.hasLead)   badges.appendChild(el('span',{class:'e-badge e-badge-lead'},'⭐ Lead'));
      if (conv.hasTicket) badges.appendChild(el('span',{class:'e-badge e-badge-ticket'},'🎫 Ticket'));
      const isNew = new Date(conv.createdAtUtc) > new Date(Date.now()-24*60*60*1000);
      if (isNew) badges.appendChild(el('span',{class:'e-badge e-badge-new'},'NEW'));
      if (badges.children.length) item.appendChild(badges);

      item.addEventListener('click', () => { renderThread(conv); renderConvList(); });
      convList.appendChild(item);
    });
  };

  // ══════════════════════════════════════════════════════════════════════════
  // TAB: WIDGET CUSTOMISATION
  // ══════════════════════════════════════════════════════════════════════════

  const { body: widgetBody } = mkPanel('🎨 Widget Appearance', 'Changes take effect after saving', panelEls.widget);

  const wForm = el('div', { class: 'e-form-grid' });

  const mkField = (lbl, hint='') => {
    const wrap = el('div', { class: 'e-field' });
    wrap.appendChild(el('div',{class:'e-field-label'},lbl));
    const input = el('input', { class: 'e-input' });
    wrap.appendChild(input);
    if (hint) wrap.appendChild(el('div',{class:'e-field-hint'},hint));
    return { wrap, input };
  };
  const mkSelect = (lbl, options, hint='') => {
    const wrap = el('div', { class: 'e-field' });
    wrap.appendChild(el('div',{class:'e-field-label'},lbl));
    const sel = el('select', { class: 'e-input' });
    options.forEach(([val,lab]) => sel.appendChild(el('option',{value:val},lab)));
    wrap.appendChild(sel);
    if (hint) wrap.appendChild(el('div',{class:'e-field-hint'},hint));
    return { wrap, input: sel };
  };

  const { wrap: botNameWrap, input: botNameInput }       = mkField('Bot Name', 'Shown in the chat header');
  const colorWrap  = el('div', { class: 'e-field' });
  const colorInput = el('input', { type: 'color', class: 'e-input', style: 'height:38px;padding:4px;cursor:pointer' });
  colorWrap.append(el('div',{class:'e-field-label'},'Primary Colour'), colorInput);
  const { wrap: posWrap, input: posInput }               = mkSelect('Position', [['bottom-right','Bottom Right'],['bottom-left','Bottom Left']]);
  const { wrap: iconWrap, input: iconInput }             = mkField('Launcher Icon', 'Emoji or short text, e.g. 💬');
  const { wrap: greetWrap, input: greetInput }           = mkField('Greeting Message', 'Shown in bubble before chat opens');
  const launchVisWrap = el('div', { class: 'e-field' });
  launchVisWrap.appendChild(el('div',{class:'e-field-label'},'Launcher Visible'));
  const launchVisToggle = el('label', { class: 'e-toggle' });
  const launchVisCb = el('input', { type: 'checkbox' }); launchVisCb.checked = true;
  launchVisToggle.append(launchVisCb, el('span',{class:'e-toggle-slider'}));
  launchVisWrap.appendChild(launchVisToggle);

  wForm.append(botNameWrap, colorWrap, posWrap, iconWrap, greetWrap, launchVisWrap);
  widgetBody.appendChild(wForm);

  // Branding section
  widgetBody.appendChild(el('div', { style: 'font-size:11px;font-weight:700;color:#94a3b8;text-transform:uppercase;letter-spacing:.06em;margin:18px 0 8px' }, 'Branding'));
  const brandingWrap = el('div', { class: 'e-form-grid' });
  const hideBrandingWrap = el('div', { class: 'e-field' });
  hideBrandingWrap.appendChild(el('div', { class: 'e-field-label' }, "Hide 'Powered by Hven' footer"));
  const hideBrandingToggle = el('label', { class: 'e-toggle' });
  const hideBrandingCb = el('input', { type: 'checkbox' });
  hideBrandingToggle.append(hideBrandingCb, el('span', { class: 'e-toggle-slider' }));
  hideBrandingWrap.appendChild(hideBrandingToggle);
  hideBrandingWrap.appendChild(el('div', { class: 'e-field-hint' }, 'Remove the Hven attribution from the chat widget footer.'));
  const { wrap: customBrandWrap, input: customBrandInput } = mkField('Custom branding text (optional)', 'e.g. Powered by Acme AI — leave blank to hide footer entirely');
  customBrandWrap.style.display = 'none';
  hideBrandingCb.addEventListener('change', () => {
    customBrandWrap.style.display = hideBrandingCb.checked ? '' : 'none';
  });
  brandingWrap.append(hideBrandingWrap, customBrandWrap);
  widgetBody.appendChild(brandingWrap);

  // A/B Test section
  widgetBody.appendChild(el('div', { style: 'font-size:11px;font-weight:700;color:#94a3b8;text-transform:uppercase;letter-spacing:.06em;margin:18px 0 8px' }, 'A/B Test Opening Messages'));
  const abWrap = el('div', { class: 'e-form-grid' });

  const abEnabledWrap = el('div', { class: 'e-field' });
  abEnabledWrap.appendChild(el('div', { class: 'e-field-label' }, 'Enable A/B test'));
  const abEnabledToggle = el('label', { class: 'e-toggle' });
  const abEnabledCb = el('input', { type: 'checkbox' });
  abEnabledToggle.append(abEnabledCb, el('span', { class: 'e-toggle-slider' }));
  abEnabledWrap.appendChild(abEnabledToggle);
  abEnabledWrap.appendChild(el('div', { class: 'e-field-hint' }, 'Randomly show one of two opening messages and track which converts more leads.'));

  const { wrap: msgAWrap, input: msgAInput } = mkField('Opening Message A', 'e.g. Hi! Can I help you find the right plan?');
  const { wrap: msgBWrap, input: msgBInput } = mkField('Opening Message B', 'e.g. Welcome! What brings you here today?');
  const abInputsWrap = el('div');
  abInputsWrap.append(msgAWrap, msgBWrap);

  const abResultsCard = el('div', { style: 'background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:14px 16px;margin-top:8px;display:none' });
  const abResultsContent = el('div');
  abResultsCard.appendChild(el('div', { style: 'font-size:11px;font-weight:700;color:#64748b;text-transform:uppercase;letter-spacing:.06em;margin-bottom:10px' }, 'A/B Test Results'));
  abResultsCard.appendChild(abResultsContent);
  const abResetBtn = el('button', { class: 'e-btn e-btn-outline', style: 'margin-top:10px;font-size:12px;padding:5px 12px' }, '🔄 Reset Counters');

  const renderAbResults = results => {
    if (!results) { abResultsCard.style.display = 'none'; return; }
    abResultsCard.style.display = '';
    const fmtRate = r => (r * 100).toFixed(1) + '%';
    const winnerLabel = results.winner === 'tie' ? '🤝 Tie' : results.winner === 'A' ? '🏆 A wins' : '🏆 B wins';
    abResultsContent.innerHTML = '';
    const grid = el('div', { style: 'display:grid;grid-template-columns:1fr 1fr;gap:10px' });
    const mkStat = (label, val, highlight) => {
      const card = el('div', { style: `background:#fff;border:1px solid ${highlight ? '#6366f1' : '#e2e8f0'};border-radius:8px;padding:10px 12px` });
      card.appendChild(el('div', { style: 'font-size:10px;color:#94a3b8;text-transform:uppercase;letter-spacing:.06em;margin-bottom:4px' }, label));
      card.appendChild(el('div', { style: `font-size:18px;font-weight:700;color:${highlight ? '#6366f1' : '#1e293b'}` }, val));
      return card;
    };
    grid.append(
      mkStat('Variant A Impressions', results.impressionsA, false),
      mkStat('Variant B Impressions', results.impressionsB, false),
      mkStat('Variant A Conversions', results.conversionsA + ' (' + fmtRate(results.conversionRateA) + ')', results.winner === 'A'),
      mkStat('Variant B Conversions', results.conversionsB + ' (' + fmtRate(results.conversionRateB) + ')', results.winner === 'B'),
    );
    abResultsContent.appendChild(grid);
    abResultsContent.appendChild(el('div', { style: 'margin-top:10px;font-size:13px;font-weight:600;color:#1e293b' }, winnerLabel));
    abResultsCard.appendChild(abResetBtn);
  };

  const loadAbResults = async () => {
    if (!state.siteId) return;
    try {
      const r = await client.engage.getAbTestResults(state.siteId);
      renderAbResults(r);
    } catch { abResultsCard.style.display = 'none'; }
  };

  abEnabledCb.addEventListener('change', () => {
    abInputsWrap.style.display = abEnabledCb.checked ? '' : 'none';
  });
  abInputsWrap.style.display = 'none';

  abResetBtn.addEventListener('click', async () => {
    if (!state.siteId) return;
    abResetBtn.disabled = true; abResetBtn.textContent = '⏳ Resetting…';
    try {
      await client.engage.resetAbTest(state.siteId);
      notifier.show({ message: 'A/B test counters reset', variant: 'success' });
      await loadAbResults();
    } catch (err) { notifier.show({ message: mapApiError(err).message, variant: 'danger' }); }
    finally { abResetBtn.disabled = false; abResetBtn.textContent = '🔄 Reset Counters'; }
  });

  abWrap.append(abEnabledWrap, abInputsWrap, abResultsCard);
  widgetBody.appendChild(abWrap);

  // Exit Intent section
  widgetBody.appendChild(el('div', { style: 'font-size:11px;font-weight:700;color:#94a3b8;text-transform:uppercase;letter-spacing:.06em;margin:18px 0 8px' }, 'Exit Intent'));
  const exitWrap = el('div', { class: 'e-form-grid' });

  const exitEnabledWrap = el('div', { class: 'e-field' });
  exitEnabledWrap.appendChild(el('div', { class: 'e-field-label' }, 'Enable exit intent trigger'));
  const exitEnabledToggle = el('label', { class: 'e-toggle' });
  const exitEnabledCb = el('input', { type: 'checkbox' });
  exitEnabledToggle.append(exitEnabledCb, el('span', { class: 'e-toggle-slider' }));
  exitEnabledWrap.appendChild(exitEnabledToggle);
  exitEnabledWrap.appendChild(el('div', { class: 'e-field-hint' }, 'Fires when a visitor moves their cursor to leave the page. One-time per visit.'));

  const { wrap: exitMsgWrap, input: exitMsgInput } = mkField('Exit intent message', 'e.g. Before you go — can I help you find what you\'re looking for?');
  exitMsgWrap.style.display = 'none';

  exitEnabledCb.addEventListener('change', () => {
    exitMsgWrap.style.display = exitEnabledCb.checked ? '' : 'none';
  });

  exitWrap.append(exitEnabledWrap, exitMsgWrap);
  widgetBody.appendChild(exitWrap);

  // Live preview
  widgetBody.appendChild(el('div', { style: 'font-size:11px;font-weight:700;color:#94a3b8;text-transform:uppercase;letter-spacing:.06em;margin:20px 0 8px' }, 'Live Preview'));
  const previewWrap = el('div', { class: 'e-preview-wrap' });
  previewWrap.appendChild(el('div', { class: 'e-preview-site' }, '✦ Your website'));
  const widgetPreview = el('div', { class: 'e-widget-preview' });
  const greetBubble = el('div', { class: 'e-widget-bubble' }, 'Need help? Chat with us!');
  const launcherBtn = el('div', { class: 'e-widget-launcher', style: 'background:#6366f1' }, '💬');
  widgetPreview.append(greetBubble, launcherBtn);
  previewWrap.appendChild(widgetPreview);
  widgetBody.appendChild(previewWrap);

  const updatePreview = () => {
    const color = colorInput.value || '#6366f1';
    const pos   = posInput.value || 'bottom-right';
    const icon  = iconInput.value || '💬';
    const greet = greetInput.value;
    launcherBtn.style.background = color;
    launcherBtn.textContent = icon;
    greetBubble.textContent = greet || 'Need help? Chat with us!';
    greetBubble.classList.toggle('show', !!greet);
    widgetPreview.className = `e-widget-preview${pos==='bottom-left'?' left':''}`;
  };
  [colorInput, posInput, iconInput, greetInput].forEach(i => i.addEventListener('input', updatePreview));

  const widgetSaveBtn = el('button', { class: 'e-btn e-btn-primary', style: 'margin-top:16px' }, '💾 Save Widget Settings');
  widgetBody.appendChild(widgetSaveBtn);

  // ══════════════════════════════════════════════════════════════════════════
  // TAB: AUTO-TRIGGERS
  // ══════════════════════════════════════════════════════════════════════════

  const { body: triggerBody } = mkPanel('⚡ Auto-Trigger Rules', 'Show the widget automatically when visitors match these conditions', panelEls.triggers);

  const triggerNote = el('div', { style: 'background:#fef3c7;border:1px solid #fcd34d;border-radius:8px;padding:10px 14px;font-size:12.5px;color:#92400e;margin-bottom:16px;line-height:1.6' },
    '⚡ Auto-triggers make Engage feel like an automated sales assistant. When a visitor matches a rule, the widget opens with a custom message automatically.');
  triggerBody.appendChild(triggerNote);

  const triggerList = el('div', { class: 'e-trigger-list' });
  triggerBody.appendChild(triggerList);

  const addTriggerBtn = el('button', { class: 'e-btn e-btn-outline', style: 'margin-top:12px' }, '+ Add Rule');
  const triggerSaveBtn = el('button', { class: 'e-btn e-btn-primary', style: 'margin-top:12px;margin-left:8px' }, '💾 Save Rules');
  triggerBody.append(addTriggerBtn, triggerSaveBtn);

  const renderTriggers = () => {
    triggerList.replaceChildren();
    if (!state.triggers.length) {
      triggerList.appendChild(el('div',{style:'color:#94a3b8;font-size:12.5px;padding:16px 0'},'No auto-trigger rules yet. Add one to start automatically engaging visitors.'));
      return;
    }
    state.triggers.forEach((rule, idx) => {
      const tcfg = TRIGGER_TYPES.find(t => t.value === rule.type) || TRIGGER_TYPES[0];
      const row = el('div', { class: 'e-trigger-item' });

      // Type select
      const typeSel = el('select', { class: 'e-input', style: 'font-size:12px' });
      TRIGGER_TYPES.forEach(t => { const opt=el('option',{value:t.value},t.label); if (t.value===rule.type) opt.selected=true; typeSel.appendChild(opt); });
      typeSel.addEventListener('change', () => { state.triggers[idx].type = typeSel.value; renderTriggers(); });
      row.appendChild(typeSel);

      // Value input (hidden for exit_intent)
      const valInput = el('input', { class: 'e-input', style: 'font-size:12px', placeholder: tcfg.placeholder, value: rule.value||'' });
      valInput.style.display = rule.type === 'exit_intent' ? 'none' : '';
      valInput.addEventListener('input', () => { state.triggers[idx].value = valInput.value; });
      row.appendChild(valInput);

      // Message input
      const msgInput = el('input', { class: 'e-input', style: 'font-size:12px', placeholder: 'Message to show…', value: rule.message||'' });
      msgInput.addEventListener('input', () => { state.triggers[idx].message = msgInput.value; });
      row.appendChild(msgInput);

      // Enable toggle
      const tog = el('label', { class: 'e-toggle' });
      const togCb = el('input', { type: 'checkbox' }); togCb.checked = rule.enabled !== false;
      togCb.addEventListener('change', () => { state.triggers[idx].enabled = togCb.checked; });
      tog.append(togCb, el('span',{class:'e-toggle-slider'}));
      row.appendChild(tog);

      // Delete
      const delBtn = el('button', { class: 'e-btn e-btn-danger e-btn-sm' }, '✕');
      delBtn.addEventListener('click', () => { state.triggers.splice(idx,1); renderTriggers(); });
      row.appendChild(delBtn);

      triggerList.appendChild(row);
    });
  };

  // ══════════════════════════════════════════════════════════════════════════
  // TAB: BOT CONFIG
  // ══════════════════════════════════════════════════════════════════════════

  const { body: botBody } = mkPanel('🤖 Bot Configuration', 'These settings shape how the AI assistant responds', panelEls.bot);

  // Site selector shown in Bot Config tab when multiple sites exist
  const botSiteRow = el('div', { style: 'display:none;align-items:center;gap:10px;padding:14px 20px;border-bottom:1px solid #f1f5f9' });
  botSiteRow.appendChild(el('span', { style: 'font-size:12px;font-weight:600;color:#64748b;white-space:nowrap' }, 'Configuring for:'));
  const botSiteSelect = el('select', { class: 'e-input', style: 'width:auto' });
  botSiteRow.appendChild(botSiteSelect);
  botBody.appendChild(botSiteRow);

  const botForm = el('div', { class: 'e-form-grid' });
  const mkBotField = (lbl, hint='', type='text') => {
    const wrap = el('div', { class: 'e-field' });
    wrap.appendChild(el('div',{class:'e-field-label'},lbl));
    const input = type === 'textarea'
      ? el('textarea', { class: 'e-input e-textarea' })
      : el('input', { class: 'e-input', type });
    wrap.appendChild(input);
    if (hint) wrap.appendChild(el('div',{class:'e-field-hint'},hint));
    return { wrap, input };
  };

  const { wrap: bDescWrap, input: bDescInput } = mkBotField('Business Description', 'What does your business do?', 'textarea');
  bDescWrap.style.gridColumn = '1 / -1';
  const { wrap: bInduWrap, input: bInduInput } = mkSelect('Industry', [
    ['','— Select industry —'],['Technology','Technology'],['Cybersecurity','Cybersecurity'],
    ['Web Design','Web Design'],['Marketing Agency','Marketing Agency'],['E-commerce','E-commerce'],
    ['Healthcare','Healthcare'],['Legal','Legal'],['Finance','Finance'],
    ['Real Estate','Real Estate'],['Education','Education'],['Consulting','Consulting'],['Other','Other'],
  ]);
  const { wrap: bSvcWrap,  input: bSvcInput  } = mkBotField('Services', 'Core services offered');
  const { wrap: bGeoWrap,  input: bGeoInput  } = mkBotField('Target Region', 'e.g. United Kingdom — where most of your customers are');
  const { wrap: bPersWrap, input: bPersInput } = mkSelect('Personality', [
    ['','— Select personality —'],['Friendly','Friendly'],['Premium','Premium'],
    ['Professional','Professional'],['Authoritative','Authoritative'],['Approachable','Approachable'],
    ['Expert','Expert'],['Casual','Casual'],
  ]);
  const { wrap: bToneWrap, input: bToneInput } = mkSelect('Tone', [
    ['','— Select tone —'],['Conversational','Conversational'],['Formal','Formal'],
    ['Friendly','Friendly'],['Professional','Professional'],['Direct','Direct'],
    ['Empathetic','Empathetic'],['Playful','Playful'],
  ]);
  const bDigWrap = el('div',{class:'e-field'});
  bDigWrap.appendChild(el('div',{class:'e-field-label'},'Weekly Digest Email'));
  const bDigToggle = el('label',{class:'e-toggle'}); const bDigCb = el('input',{type:'checkbox'});
  bDigToggle.append(bDigCb, el('span',{class:'e-toggle-slider'})); bDigWrap.appendChild(bDigToggle);

  // Email pill input for digest recipients
  const bRecipEmails = [];
  const bRecipWrap = el('div', { class: 'e-field', style: 'grid-column:1/-1' });
  bRecipWrap.appendChild(el('div',{class:'e-field-label'},'Digest Recipients'));
  const pillContainer = el('div', { style: 'display:flex;flex-wrap:wrap;gap:5px;align-items:center;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:6px 8px;min-height:38px;transition:border .14s,background .14s;cursor:text' });
  const pillEmailInput = el('input', { type:'email', style:'border:none;outline:none;background:transparent;font-family:inherit;font-size:13px;color:#1e293b;min-width:140px;flex:1', placeholder:'Add email and press Enter…' });
  const addEmailPill = email => {
    const v = email.trim().toLowerCase();
    if (!v || !v.includes('@') || bRecipEmails.includes(v)) return;
    bRecipEmails.push(v);
    const pill = el('span', { style:'display:inline-flex;align-items:center;gap:3px;background:#ede9fe;color:#4f46e5;border-radius:999px;font-size:11.5px;font-weight:500;padding:2px 8px' }, v);
    const x = el('button', { type:'button', style:'background:none;border:none;cursor:pointer;color:#7c3aed;font-size:13px;padding:0;line-height:1;margin-left:2px' }, '×');
    x.addEventListener('click', () => { bRecipEmails.splice(bRecipEmails.indexOf(v),1); pill.remove(); });
    pill.appendChild(x);
    pillContainer.insertBefore(pill, pillEmailInput);
  };
  pillEmailInput.addEventListener('keydown', e => {
    if (e.key==='Enter'||e.key===',') { e.preventDefault(); addEmailPill(pillEmailInput.value); pillEmailInput.value=''; }
    else if (e.key==='Backspace'&&!pillEmailInput.value&&bRecipEmails.length) {
      const siblings=[...pillContainer.children].filter(c=>c!==pillEmailInput);
      if (siblings.length) { siblings[siblings.length-1].remove(); bRecipEmails.pop(); }
    }
  });
  pillEmailInput.addEventListener('blur', () => { if (pillEmailInput.value.trim()) { addEmailPill(pillEmailInput.value); pillEmailInput.value=''; } });
  pillContainer.appendChild(pillEmailInput);
  pillContainer.addEventListener('click', () => pillEmailInput.focus());
  pillEmailInput.addEventListener('focus', () => { pillContainer.style.borderColor='#6366f1'; pillContainer.style.boxShadow='0 0 0 3px rgba(99,102,241,.1)'; pillContainer.style.background='#fff'; });
  pillEmailInput.addEventListener('blur', () => { setTimeout(()=>{ pillContainer.style.borderColor='#e2e8f0'; pillContainer.style.boxShadow=''; pillContainer.style.background='#f8fafc'; },50); });
  bRecipWrap.appendChild(pillContainer);
  bRecipWrap.appendChild(el('div',{class:'e-field-hint'},'Press Enter or comma to add an email address'));

  botForm.append(bDescWrap, bInduWrap, bSvcWrap, bGeoWrap, bPersWrap, bToneWrap, bDigWrap, bRecipWrap);
  botBody.appendChild(botForm);

  const botSaveBtn = el('button', { class: 'e-btn e-btn-primary', style: 'margin-top:16px' }, '💾 Save Bot Settings');
  botBody.appendChild(botSaveBtn);

  // ── Weekly Digest Preview ──────────────────────────────────────────────────
  botBody.appendChild(el('hr', { style: 'border:none;border-top:1px solid #f1f5f9;margin:20px 0' }));
  botBody.appendChild(el('div', { style: 'font-size:10.5px;font-weight:700;letter-spacing:.07em;text-transform:uppercase;color:#94a3b8;margin-bottom:6px' }, 'Weekly Digest Preview'));
  botBody.appendChild(el('div', { style: 'font-size:12px;color:#64748b;margin-bottom:12px;line-height:1.6' },
    'Your digest runs every Monday at 08:00 and includes an AI-written summary of your week\'s key insights. Click below to send a test digest to the configured recipients now.'
  ));

  const digestBtn = el('button', { class: 'e-btn e-btn-outline', style: 'font-size:12.5px' }, '📨 Send digest now');
  digestBtn.addEventListener('click', async () => {
    if (!state.siteId) { notifier.show({ message: 'Select a site first.', variant: 'warning' }); return; }
    digestBtn.disabled = true;
    digestBtn.textContent = '⏳ Sending…';
    try {
      await client.engage.sendDigest(state.siteId);
      notifier.show({ message: 'Digest sent successfully', variant: 'success' });
    } catch (err) {
      notifier.show({ message: mapApiError(err).message, variant: 'danger' });
    } finally {
      digestBtn.disabled = false;
      digestBtn.textContent = '📨 Send digest now';
    }
  });
  botBody.appendChild(digestBtn);

  // ══════════════════════════════════════════════════════════════════════════
  // TAB: SURVEY
  // ══════════════════════════════════════════════════════════════════════════

  const { body: surveyBody } = mkPanel('📋 Micro-Survey', 'Capture visitor intent before the first AI message', panelEls.survey);

  // Enable toggle
  const surveyEnabledWrap = el('div', { class: 'e-field' });
  surveyEnabledWrap.appendChild(el('div', { class: 'e-field-label' }, 'Enable micro-survey'));
  const surveyEnabledToggle = el('label', { class: 'e-toggle' });
  const surveyEnabledCb = el('input', { type: 'checkbox' });
  surveyEnabledToggle.append(surveyEnabledCb, el('span', { class: 'e-toggle-slider' }));
  surveyEnabledWrap.appendChild(surveyEnabledToggle);
  surveyEnabledWrap.appendChild(el('div', { class: 'e-field-hint' }, 'Show button options below the opening message to capture zero-party intent data before chat begins.'));
  surveyBody.appendChild(surveyEnabledWrap);

  // Survey inputs (hidden when disabled)
  const surveyInputsWrap = el('div', { style: 'display:none' });

  const surveyQWrap = el('div', { class: 'e-field', style: 'margin-top:10px' });
  surveyQWrap.appendChild(el('div', { class: 'e-field-label' }, 'Survey Question'));
  const surveyQInput = el('input', { class: 'e-input', placeholder: 'e.g. What brings you here today?' });
  surveyQWrap.appendChild(surveyQInput);
  surveyQWrap.appendChild(el('div', { class: 'e-field-hint' }, 'Shown above the option buttons. Keep it short.'));
  surveyInputsWrap.appendChild(surveyQWrap);

  // Options builder (up to 4)
  const SURVEY_DEFAULTS = ['Just browsing', 'Checking prices', 'Ready to buy', 'Comparing options'];
  surveyInputsWrap.appendChild(el('div', { style: 'font-size:11px;font-weight:700;color:#94a3b8;text-transform:uppercase;letter-spacing:.06em;margin:14px 0 8px' }, 'Answer Options'));
  const surveyOptsContainer = el('div', { style: 'display:flex;flex-direction:column;gap:6px' });
  const surveyOptInputs = SURVEY_DEFAULTS.map((def, i) => {
    const row = el('div', { style: 'display:flex;align-items:center;gap:6px' });
    row.appendChild(el('span', { style: 'font-size:11.5px;color:#94a3b8;min-width:16px' }, `${i + 1}.`));
    const inp = el('input', { class: 'e-input', placeholder: def, style: 'flex:1' });
    row.appendChild(inp);
    surveyOptsContainer.appendChild(row);
    return inp;
  });
  surveyInputsWrap.appendChild(surveyOptsContainer);
  surveyInputsWrap.appendChild(el('div', { class: 'e-field-hint', style: 'margin-top:4px' }, 'Leave blank to skip that option. Up to 4 options.'));

  const surveySaveBtn = el('button', { class: 'e-btn e-btn-primary', style: 'margin-top:16px' }, '💾 Save Survey Settings');
  surveyInputsWrap.appendChild(surveySaveBtn);
  surveyBody.appendChild(surveyInputsWrap);

  surveyEnabledCb.addEventListener('change', () => {
    surveyInputsWrap.style.display = surveyEnabledCb.checked ? '' : 'none';
  });

  // Survey results section
  surveyBody.appendChild(el('hr', { style: 'border:none;border-top:1px solid #f1f5f9;margin:20px 0' }));
  surveyBody.appendChild(el('div', { style: 'font-size:10.5px;font-weight:700;letter-spacing:.07em;text-transform:uppercase;color:#94a3b8;margin-bottom:8px' }, 'Survey Results'));

  const surveyResultsWrap = el('div');
  surveyBody.appendChild(surveyResultsWrap);

  const renderSurveyResults = results => {
    surveyResultsWrap.replaceChildren();
    if (!results || results.totalResponses === 0) {
      surveyResultsWrap.appendChild(el('div', { style: 'color:#94a3b8;font-size:12.5px;padding:10px 0' }, 'No survey responses yet.'));
      return;
    }
    const totalEl = el('div', { style: 'font-size:12px;color:#64748b;margin-bottom:10px' }, `Based on ${results.totalResponses} response${results.totalResponses === 1 ? '' : 's'}`);
    surveyResultsWrap.appendChild(totalEl);
    (results.breakdown || []).forEach(row => {
      const barWrap = el('div', { style: 'margin-bottom:8px' });
      const labelRow = el('div', { style: 'display:flex;justify-content:space-between;align-items:center;margin-bottom:3px' });
      labelRow.appendChild(el('span', { style: 'font-size:12.5px;color:#1e293b;font-weight:500' }, row.option));
      labelRow.appendChild(el('span', { style: 'font-size:11.5px;color:#64748b' }, `${row.count} · ${row.pct}%`));
      barWrap.appendChild(labelRow);
      const track = el('div', { style: 'background:#f1f5f9;border-radius:999px;height:8px;overflow:hidden' });
      const fill = el('div', { style: `background:#6366f1;height:100%;border-radius:999px;width:${row.pct}%;transition:width .4s ease` });
      track.appendChild(fill);
      barWrap.appendChild(track);
      surveyResultsWrap.appendChild(barWrap);
    });
  };

  const loadSurveyResults = async () => {
    if (!state.siteId) return;
    surveyResultsWrap.replaceChildren(el('div', { style: 'color:#94a3b8;font-size:12.5px;padding:10px 0' }, '⏳ Loading…'));
    try {
      const res = await client.engage.getSurveyResults(state.siteId);
      renderSurveyResults(res);
    } catch { surveyResultsWrap.replaceChildren(el('div', { style: 'color:#ef4444;font-size:12.5px' }, 'Failed to load results.')); }
  };

  surveySaveBtn.addEventListener('click', async () => {
    const opts = surveyOptInputs.map(i => i.value.trim()).filter(Boolean);
    await saveBot({
      surveyEnabled: surveyEnabledCb.checked,
      surveyQuestion: surveyQInput.value.trim() || undefined,
      surveyOptions: opts.length ? JSON.stringify(opts) : undefined,
    });
    loadSurveyResults();
  });

  // Load survey results when the survey tab is clicked
  if (tabEls.survey) tabEls.survey.addEventListener('click', loadSurveyResults);

  // ─── Helper ────────────────────────────────────────────────────────────────
  function mkPanel(title, meta, parent) {
    const panel = el('div', { class: 'e-panel' });
    const hd = el('div', { class: 'e-panel-hd' });
    hd.appendChild(el('div',{class:'e-panel-title'},title));
    if (meta) hd.appendChild(el('div',{class:'e-panel-meta'},meta));
    panel.appendChild(hd);
    const body = el('div', { class: 'e-panel-body', style: 'padding:0' });
    panel.appendChild(body);
    if (parent) parent.appendChild(panel);
    return { panel, body, hd };
  }

  // ─── Load bot settings into forms ─────────────────────────────────────────
  const applyBot = bot => {
    if (!bot) return;
    botNameInput.value  = bot.name || '';
    colorInput.value    = bot.primaryColor || '#6366f1';
    posInput.value      = bot.widgetPosition || 'bottom-right';
    iconInput.value     = bot.launcherIcon || '';
    greetInput.value    = bot.greetingMessage || '';
    launchVisCb.checked     = bot.launcherVisible !== false;
    hideBrandingCb.checked  = bot.hideBranding || false;
    customBrandInput.value  = bot.customBrandingText || '';
    customBrandWrap.style.display = hideBrandingCb.checked ? '' : 'none';
    abEnabledCb.checked     = bot.abTestEnabled || false;
    msgAInput.value         = bot.openingMessageA || '';
    msgBInput.value         = bot.openingMessageB || '';
    abInputsWrap.style.display = abEnabledCb.checked ? '' : 'none';
    exitEnabledCb.checked   = bot.exitIntentEnabled || false;
    exitMsgInput.value      = bot.exitIntentMessage || '';
    exitMsgWrap.style.display = exitEnabledCb.checked ? '' : 'none';
    bDescInput.value    = bot.businessDescription || '';
    bInduInput.value    = bot.industry || '';
    bSvcInput.value     = bot.servicesDescription || '';
    bGeoInput.value     = bot.geoFocus || '';
    bPersInput.value    = bot.personalityDescriptor || '';
    bToneInput.value    = bot.tone || '';
    bDigCb.checked      = bot.digestEmailEnabled || false;
    bRecipEmails.length = 0;
    [...pillContainer.children].filter(c => c !== pillEmailInput).forEach(p => p.remove());
    (bot.digestEmailRecipients || '').split(',').map(e => e.trim()).filter(Boolean).forEach(addEmailPill);

    try { state.triggers = JSON.parse(bot.autoTriggerRulesJson || '[]'); } catch { state.triggers = []; }
    renderTriggers();
    updatePreview();
    // Survey fields
    surveyEnabledCb.checked = bot.surveyEnabled || false;
    surveyInputsWrap.style.display = surveyEnabledCb.checked ? '' : 'none';
    surveyQInput.value = bot.surveyQuestion || '';
    const surveyOpts = (() => { try { return JSON.parse(bot.surveyOptions || '[]'); } catch { return []; } })();
    surveyOptInputs.forEach((inp, i) => { inp.value = surveyOpts[i] || ''; inp.placeholder = SURVEY_DEFAULTS[i]; });
  };

  const buildBotPayload = () => ({
    name:                  botNameInput.value.trim() || 'Assistant',
    primaryColor:          colorInput.value,
    widgetPosition:        posInput.value,
    launcherIcon:          iconInput.value.trim(),
    greetingMessage:       greetInput.value.trim(),
    launcherVisible:       launchVisCb.checked,
    hideBranding:          hideBrandingCb.checked,
    customBrandingText:    customBrandInput.value.trim() || undefined,
    abTestEnabled:         abEnabledCb.checked,
    openingMessageA:       msgAInput.value.trim() || undefined,
    openingMessageB:       msgBInput.value.trim() || undefined,
    exitIntentEnabled:     exitEnabledCb.checked,
    exitIntentMessage:     exitMsgInput.value.trim() || undefined,
    businessDescription:   bDescInput.value.trim(),
    industry:              bInduInput.value.trim(),
    servicesDescription:   bSvcInput.value.trim(),
    geoFocus:              bGeoInput.value.trim(),
    personalityDescriptor: bPersInput.value.trim(),
    tone:                  bToneInput.value.trim(),
    digestEmailEnabled:    bDigCb.checked,
    digestEmailRecipients: bRecipEmails.join(','),
    autoTriggerRulesJson:  JSON.stringify(state.triggers),
  });

  const saveBot = async (extra = {}) => {
    if (!state.siteId) { notifier.show({ message: 'Select a site first.', variant: 'warning' }); return; }
    state.saving = true;
    [widgetSaveBtn, botSaveBtn, triggerSaveBtn].forEach(b => { b.disabled = true; b.textContent = '⏳ Saving…'; });
    try {
      await client.engage.updateBot(state.siteId, { ...buildBotPayload(), ...extra });
      notifier.show({ message: 'Bot settings saved successfully', variant: 'success' });
      renderTriggers();
    } catch (err) { notifier.show({ message: mapApiError(err).message, variant: 'danger' }); }
    finally {
      state.saving = false;
      widgetSaveBtn.disabled = false; widgetSaveBtn.textContent = '💾 Save Widget Settings';
      botSaveBtn.disabled = false;    botSaveBtn.textContent = '💾 Save Bot Settings';
      triggerSaveBtn.disabled = false; triggerSaveBtn.textContent = '💾 Save Rules';
    }
  };

  // ─── API calls ─────────────────────────────────────────────────────────────
  const loadConversations = async () => {
    if (!state.siteId) return;
    try {
      const res = await client.engage.listConversations(state.siteId);
      state.conversations = Array.isArray(res) ? res.sort((a,b) => new Date(b.updatedAtUtc||b.createdAtUtc) - new Date(a.updatedAtUtc||a.createdAtUtc)) : [];
      renderConvList();
    } catch {}
  };

  const loadBot = async () => {
    if (!state.siteId) return;
    try { state.bot = await client.engage.getBot(state.siteId); applyBot(state.bot); } catch {}
    loadAbResults();
    if (state.activeTab === 'survey') loadSurveyResults();
  };

  const syncSites = sites => {
    siteSelect.innerHTML = '';
    botSiteSelect.innerHTML = '';
    if (!sites.length) { siteSelect.appendChild(el('option',{value:''},'No sites')); return; }
    sites.forEach(s => {
      const id=getSiteId(s); const label=s.domain||id;
      const opt=el('option',{value:id},label);
      const botOpt=el('option',{value:id},label);
      if (id===state.siteId) { opt.selected=true; botOpt.selected=true; }
      siteSelect.appendChild(opt);
      botSiteSelect.appendChild(botOpt);
    });
    if (!state.siteId || !sites.find(s=>getSiteId(s)===state.siteId)) state.siteId=getSiteId(sites[0]);
    siteSelect.value = state.siteId;
    botSiteSelect.value = state.siteId;
    botSiteRow.style.display = sites.length > 1 ? 'flex' : 'none';
  };

  // ─── Wire events ───────────────────────────────────────────────────────────
  siteSelect.addEventListener('change', () => { state.siteId=siteSelect.value; botSiteSelect.value=state.siteId; saveSiteId(state.siteId); loadConversations(); loadBot(); });
  botSiteSelect.addEventListener('change', () => { state.siteId=botSiteSelect.value; siteSelect.value=state.siteId; saveSiteId(state.siteId); loadBot(); });
  refreshBtn.addEventListener('click', () => { loadConversations(); loadBot(); });
  addTriggerBtn.addEventListener('click', () => { state.triggers.push({ type:'time_on_page', value:'30', message:'Need help? We\'re here!', enabled:true }); renderTriggers(); });
  widgetSaveBtn.addEventListener('click', () => saveBot());
  botSaveBtn.addEventListener('click', () => saveBot());
  triggerSaveBtn.addEventListener('click', () => saveBot());

  // ─── Init ──────────────────────────────────────────────────────────────────
  const init = async () => {
    try {
      const sites = await client.sites.list();
      state.sites = Array.isArray(sites) ? sites : [];
      syncSites(state.sites);
      await Promise.all([loadConversations(), loadBot()]);
    } catch (err) { notifier.show({ message: 'Could not load sites.', variant: 'danger' }); }
  };

  init();
};
