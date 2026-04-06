/**
 * flows.js — Intentify Flows
 * Revamped to match visitors.js design language exactly.
 * All original functionality preserved: flow builder, conditions, actions,
 * run history, templates, enable/disable toggle.
 */

import { createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

// ─── Constants (identical to original) ───────────────────────────────────────

const TRIGGER_TYPES = [
  { value: 'CollectorPageView',              label: 'Page View'                },
  { value: 'IntelligenceTrendsUpdated',      label: 'Intelligence Updated'     },
  { value: 'engage_lead_captured',           label: 'Lead Captured'            },
  { value: 'engage_ticket_created',          label: 'Ticket Created'           },
  { value: 'engage_conversation_completed',  label: 'Conversation Completed'   },
  { value: 'visitor_return',                 label: 'Return Visitor'           },
  { value: 'exit_intent',                    label: 'Exit Intent'              },
];

const OPERATOR_OPTIONS = [
  { value: 1, label: 'Equals'   },
  { value: 2, label: 'Contains' },
];

const ACTION_TYPES = [
  { value: 'LogRun',                label: 'Log Event'           },
  { value: 'SendWebhook',           label: 'Send Webhook'        },
  { value: 'SendSlackNotification', label: 'Slack Notification'  },
  { value: 'SendEmail',             label: 'Send Email'          },
  { value: 'CreateTicket',          label: 'Create Ticket'       },
  { value: 'UpdateLeadStage',       label: 'Update Lead Stage'   },
  { value: 'TagLead',               label: 'Tag Lead'            },
  { value: 'AddNote',               label: 'Add Note to Ticket'  },
  { value: 'NotifyTeam',            label: 'Notify Team'         },
];

const TAG_LEAD_LABELS = [
  { value: 'evaluating', label: 'Evaluating' },
  { value: 'deciding',   label: 'Deciding'   },
  { value: 'won',        label: 'Won'        },
  { value: 'lost',       label: 'Lost'       },
];

const TRIGGER_ICON = (type) => ({
  CollectorPageView:'👁', IntelligenceTrendsUpdated:'📡',
  engage_lead_captured:'⭐', engage_ticket_created:'🎫',
  engage_conversation_completed:'💬', visitor_return:'🔄', exit_intent:'🚪',
}[type] || '⚡');

const getSiteId = s => s?.siteId || s?.id || '';

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

const fmtTime = v => { if (!v) return '—'; const d = new Date(v); return isNaN(d) ? '—' : d.toLocaleString('en-GB'); };

// ─── Styles ───────────────────────────────────────────────────────────────────

const injectStyles = () => {
  if (document.getElementById('_flows2_css')) return;
  const s = document.createElement('style');
  s.id = '_flows2_css';
  s.textContent = `
@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap');

.fl-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;gap:20px;width:100%;max-width:1100px;padding-bottom:60px}

/* Hero */
.fl-hero{background:linear-gradient(135deg,#0f172a 0%,#1e293b 100%);border-radius:16px;padding:28px 36px;position:relative;overflow:hidden}
.fl-hero::before{content:'';position:absolute;top:-30px;right:-30px;width:180px;height:180px;background:radial-gradient(circle,rgba(245,158,11,.15) 0%,transparent 70%);pointer-events:none}
.fl-hero-top{display:flex;align-items:flex-start;justify-content:space-between;gap:16px;flex-wrap:wrap}
.fl-hero-title{font-size:24px;font-weight:700;color:#f8fafc;letter-spacing:-.02em;margin-bottom:6px}
.fl-hero-sub{font-size:13px;color:#94a3b8;margin-bottom:18px}
.fl-hero-stats{display:flex;gap:28px;flex-wrap:wrap}
.fl-stat{display:flex;flex-direction:column;gap:2px}
.fl-stat-val{font-family:'JetBrains Mono',monospace;font-size:22px;font-weight:700;color:#f1f5f9;letter-spacing:-.02em}
.fl-stat-lbl{font-size:10px;color:#64748b;text-transform:uppercase;letter-spacing:.07em}

/* Controls */
.fl-controls{display:flex;align-items:center;gap:10px;flex-wrap:wrap}
.fl-select{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;color:#1e293b;background:#fff;border:1px solid #e2e8f0;border-radius:8px;padding:7px 11px;outline:none;min-width:220px}
.fl-select:focus{border-color:#6366f1;box-shadow:0 0 0 3px rgba(99,102,241,.1)}

/* Buttons */
.fl-btn{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;font-weight:600;padding:7px 16px;border-radius:8px;border:none;cursor:pointer;transition:all .14s;display:inline-flex;align-items:center;gap:5px;white-space:nowrap}
.fl-btn-primary{background:#6366f1;color:#fff}
.fl-btn-primary:hover:not(:disabled){background:#4f46e5;transform:translateY(-1px);box-shadow:0 4px 12px rgba(99,102,241,.25)}
.fl-btn-primary:disabled{opacity:.5;cursor:not-allowed}
.fl-btn-outline{background:#fff;color:#64748b;border:1px solid #e2e8f0}
.fl-btn-outline:hover{background:#f8fafc;color:#1e293b}
.fl-btn-danger{background:#fee2e2;color:#dc2626;border:none}
.fl-btn-danger:hover{background:#fecaca}
.fl-btn-sm{padding:5px 12px;font-size:12px}

/* Flow cards grid */
.fl-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(340px,1fr));gap:14px}

/* Panel — same pattern as v-panel */
.fl-panel{background:#fff;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden;transition:box-shadow .16s,transform .16s}
.fl-panel:hover{box-shadow:0 4px 16px rgba(0,0,0,.07);transform:translateY(-1px)}
.fl-panel.fl-disabled{opacity:.6}
.fl-panel-hd{display:flex;align-items:flex-start;justify-content:space-between;padding:16px 18px;border-bottom:1px solid #f1f5f9;gap:10px}
.fl-panel-body{padding:12px 18px;display:flex;flex-direction:column;gap:8px}
.fl-panel-foot{display:flex;gap:6px;padding:10px 18px;border-top:1px solid #f1f5f9;flex-wrap:wrap}
.fl-panel-icon{width:38px;height:38px;border-radius:9px;background:#fef3c7;display:flex;align-items:center;justify-content:center;font-size:18px;flex-shrink:0}
.fl-panel-name{font-size:13.5px;font-weight:700;color:#0f172a;margin-bottom:3px}
.fl-panel-trigger{font-size:11px;color:#94a3b8;display:flex;align-items:center;gap:4px}
.fl-panel-right{display:flex;align-items:flex-start;gap:8px}

/* Toggle — same pattern as engage.js */
.fl-toggle{position:relative;width:36px;height:19px;flex-shrink:0}
.fl-toggle input{opacity:0;width:0;height:0;position:absolute}
.fl-toggle-slider{position:absolute;inset:0;background:#e2e8f0;border-radius:999px;cursor:pointer;transition:.18s}
.fl-toggle-slider::before{content:'';position:absolute;left:3px;top:3px;width:13px;height:13px;background:#fff;border-radius:50%;transition:.18s;box-shadow:0 1px 3px rgba(0,0,0,.2)}
.fl-toggle input:checked+.fl-toggle-slider{background:#10b981}
.fl-toggle input:checked+.fl-toggle-slider::before{transform:translateX(17px)}

/* Pills */
.fl-pill{display:inline-flex;align-items:center;gap:3px;padding:2px 8px;border-radius:999px;font-size:10px;font-weight:700}
.fl-pill-green{background:#d1fae5;color:#065f46}
.fl-pill-gray{background:#f1f5f9;color:#475569}
.fl-pill-amber{background:#fef3c7;color:#92400e}
.fl-pill-blue{background:#dbeafe;color:#1e40af}

/* Action chips */
.fl-chip{display:inline-flex;align-items:center;gap:3px;padding:3px 9px;background:#f1f5f9;border:1px solid #e2e8f0;border-radius:6px;font-size:11px;color:#475569;font-weight:500}

/* Modal */
.fl-overlay{position:fixed;inset:0;background:rgba(15,23,42,.55);z-index:200;display:flex;align-items:center;justify-content:center;padding:20px;backdrop-filter:blur(3px)}
.fl-modal{background:#fff;border-radius:16px;width:100%;max-width:660px;max-height:90vh;overflow-y:auto;box-shadow:0 24px 64px rgba(0,0,0,.2)}
.fl-modal-hd{display:flex;align-items:center;justify-content:space-between;padding:20px 24px;border-bottom:1px solid #f1f5f9;position:sticky;top:0;background:#fff;z-index:1}
.fl-modal-title{font-size:15px;font-weight:700;color:#0f172a}
.fl-modal-body{padding:20px 24px;display:flex;flex-direction:column;gap:14px}
.fl-field{display:flex;flex-direction:column;gap:5px}
.fl-field-lbl{font-size:10.5px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:#94a3b8}
.fl-input{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;color:#1e293b;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:8px 11px;outline:none;width:100%;box-sizing:border-box}
.fl-input:focus{border-color:#6366f1;background:#fff;box-shadow:0 0 0 3px rgba(99,102,241,.1)}
.fl-section-hd{font-size:12px;font-weight:700;color:#0f172a;padding-bottom:8px;border-bottom:1px solid #f1f5f9;margin-top:4px}
.fl-cond-row,.fl-act-row{display:grid;grid-template-columns:1fr 100px 1fr auto;gap:8px;align-items:start;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:10px 12px;margin-top:8px}
.fl-act-row{grid-template-columns:180px 1fr auto}
.fl-act-params{display:flex;flex-direction:column;gap:6px}
.fl-err{font-size:11.5px;color:#dc2626;background:#fee2e2;border-radius:6px;padding:6px 10px}

/* Run history */
.fl-run-row{display:grid;grid-template-columns:160px 1fr 90px;gap:10px;padding:9px 0;border-bottom:1px solid #f8fafc;align-items:center;font-size:12px}
.fl-run-row:last-child{border-bottom:none}

/* Templates */
.fl-tpl{padding:14px;border:1px solid #e2e8f0;border-radius:10px;display:flex;align-items:flex-start;justify-content:space-between;gap:12px;cursor:pointer;transition:all .14s}
.fl-tpl:hover{background:#f8fafc;border-color:#c7d2fe}
.fl-tpl-name{font-size:13px;font-weight:600;color:#1e293b;margin-bottom:3px}
.fl-tpl-desc{font-size:12px;color:#64748b;line-height:1.5}

/* Empty */
.fl-empty{background:#fff;border:1px solid #e2e8f0;border-radius:14px;text-align:center;padding:56px 24px}
.fl-empty-icon{font-size:42px;opacity:.3;margin-bottom:14px}
.fl-empty-title{font-size:16px;font-weight:700;color:#334155;margin-bottom:6px}
.fl-empty-desc{font-size:13px;color:#94a3b8;max-width:300px;margin:0 auto 20px;line-height:1.65}
.fl-skel{background:linear-gradient(90deg,#f1f5f9 25%,#e2e8f0 50%,#f1f5f9 75%);background-size:200% 100%;animation:_sh 1.4s infinite;border-radius:14px;height:140px}
@keyframes _sh{0%{background-position:200% 0}100%{background-position:-200% 0}}
  `;
  document.head.appendChild(s);
};

// ─── Modal factory ────────────────────────────────────────────────────────────

const makeModal = (title) => {
  const overlay  = el('div', { class: 'fl-overlay' });
  const modal    = el('div', { class: 'fl-modal' });
  const mhd      = el('div', { class: 'fl-modal-hd' });
  const titleEl  = el('div', { class: 'fl-modal-title' }, title);
  const closeBtn = el('button', { class: 'fl-btn fl-btn-outline fl-btn-sm' }, '✕');
  mhd.append(titleEl, closeBtn);
  const body = el('div', { class: 'fl-modal-body' });
  modal.append(mhd, body);
  overlay.appendChild(modal);
  const hide = () => overlay.remove();
  const show = () => { document.body.appendChild(overlay); };
  closeBtn.addEventListener('click', hide);
  overlay.addEventListener('click', e => { if (e.target === overlay) hide(); });
  return { body, show, hide, setTitle: t => { titleEl.textContent = t; }, modal };
};

const makeSelect = (options, val = '') => {
  const s = el('select', { class: 'fl-input', style: 'padding:7px 10px' });
  options.forEach(o => {
    const opt = el('option', { value: String(o.value) }, o.label);
    if (String(o.value) === String(val)) opt.selected = true;
    s.appendChild(opt);
  });
  return s;
};

// ─── Conditions builder ───────────────────────────────────────────────────────

function makeConditionsSection(initial = []) {
  const wrap   = el('div', {});
  const listEl = el('div', { style: 'display:flex;flex-direction:column;gap:0' });
  const addBtn = el('button', { class: 'fl-btn fl-btn-outline fl-btn-sm', style: 'margin-top:10px;align-self:flex-start' }, '+ Add Condition');
  wrap.append(listEl, addBtn);

  const items = [];
  const addCondition = (cond = null) => {
    const row = el('div', { class: 'fl-cond-row' });
    const fieldIn = el('input', { class: 'fl-input', placeholder: 'Field (e.g. url)', value: cond?.field || '' });
    const opSel   = makeSelect(OPERATOR_OPTIONS, cond?.operator || 2);
    const valIn   = el('input', { class: 'fl-input', placeholder: 'Value', value: cond?.value || '' });
    const delBtn  = el('button', { class: 'fl-btn fl-btn-danger fl-btn-sm' }, '✕');
    row.append(fieldIn, opSel, valIn, delBtn);
    listEl.appendChild(row);
    const entry = { row, fieldIn, opSel, valIn };
    items.push(entry);
    delBtn.addEventListener('click', () => { row.remove(); items.splice(items.indexOf(entry), 1); });
  };

  initial.forEach(addCondition);
  addBtn.addEventListener('click', () => addCondition());

  const getValues = () => items.map(i => ({
    field: i.fieldIn.value.trim(), operator: parseInt(i.opSel.value, 10), value: i.valIn.value.trim(),
  })).filter(c => c.field && c.value);

  return { wrap, getValues };
}

// ─── Actions builder ──────────────────────────────────────────────────────────

function makeActionsSection(initial = []) {
  const wrap   = el('div', {});
  const listEl = el('div', { style: 'display:flex;flex-direction:column;gap:0' });
  const addBtn = el('button', { class: 'fl-btn fl-btn-outline fl-btn-sm', style: 'margin-top:10px;align-self:flex-start' }, '+ Add Action');
  wrap.append(listEl, addBtn);

  const items = [];

  const addAction = (action = null) => {
    const row    = el('div', { class: 'fl-act-row' });
    const typeSel = makeSelect(ACTION_TYPES, action?.actionType || 'LogRun');
    const params  = el('div', { class: 'fl-act-params' });
    const delBtn  = el('button', { class: 'fl-btn fl-btn-danger fl-btn-sm' }, '✕');

    const entry = { typeSel, params, extraInputs: {} };

    const renderParams = () => {
      params.replaceChildren();
      entry.extraInputs = {};
      const t = typeSel.value;
      const mkP = (key, ph, val = '') => {
        const inp = el('input', { class: 'fl-input', placeholder: ph, value: action?.params?.[key] || val });
        inp.addEventListener('input', () => { entry.extraInputs[key] = inp.value; });
        entry.extraInputs[key] = inp.value;
        params.appendChild(inp);
        return inp;
      };
      if (t === 'SendEmail')           { mkP('to','To email'); mkP('subject','Subject'); mkP('body','Body'); }
      else if (t === 'SendWebhook')    { mkP('url','Webhook URL'); }
      else if (t === 'SendSlackNotification') { mkP('webhookUrl','Slack webhook URL'); mkP('message','Message'); }
      else if (t === 'CreateTicket')   { mkP('subject','Ticket subject'); mkP('description','Description'); }
      else if (t === 'UpdateLeadStage') { mkP('leadId','Lead ID'); mkP('stage','Stage'); }
      else if (t === 'TagLead') {
        const sel = makeSelect(TAG_LEAD_LABELS, action?.params?.label || 'evaluating');
        sel.addEventListener('change', () => { entry.extraInputs.label = sel.value; });
        entry.extraInputs.label = sel.value;
        params.appendChild(sel);
      }
      else if (t === 'AddNote')        { mkP('ticketId','Ticket ID'); mkP('content','Note content'); }
      else if (t === 'NotifyTeam')     { mkP('message','Message'); }
      else { params.appendChild(el('div',{style:'font-size:11.5px;color:#94a3b8;padding:4px 0'},'No parameters needed.')); }
    };

    typeSel.addEventListener('change', renderParams);
    renderParams();
    row.append(typeSel, params, delBtn);
    listEl.appendChild(row);
    items.push(entry);
    delBtn.addEventListener('click', () => { row.remove(); items.splice(items.indexOf(entry), 1); });
  };

  (initial.length ? initial : [{ actionType: 'LogRun', params: {} }]).forEach(addAction);
  addBtn.addEventListener('click', () => addAction());

  const getValues = () => items.map(i => ({ actionType: i.typeSel.value, params: { ...i.extraInputs } }));
  return { wrap, getValues };
}

// ─── Flow builder modal ───────────────────────────────────────────────────────

function openFlowModal(client, notifier, siteId, existing, onSaved) {
  const { body, show, hide } = makeModal(existing?.id ? `Edit — ${existing.name}` : '⚡ New Flow');

  const nameField = el('div',{class:'fl-field'}, el('div',{class:'fl-field-lbl'},'Flow Name'));
  const nameInput = el('input',{class:'fl-input',placeholder:'My automation',value:existing?.name||''});
  nameField.appendChild(nameInput);

  const enabledRow = el('div',{style:'display:flex;align-items:center;gap:10px'});
  const togLbl = el('label',{class:'fl-toggle'});
  const togCb  = el('input',{type:'checkbox'}); togCb.checked = existing?.enabled !== false;
  togLbl.append(togCb, el('span',{class:'fl-toggle-slider'}));
  enabledRow.append(togLbl, el('span',{style:'font-size:13px;color:#334155'},'Enabled'));

  const trigField = el('div',{class:'fl-field'}, el('div',{class:'fl-field-lbl'},'Trigger'));
  const trigSel   = makeSelect(TRIGGER_TYPES, existing?.trigger?.type || existing?.triggerType || 'CollectorPageView');
  trigField.appendChild(trigSel);

  const numRow = el('div',{style:'display:grid;grid-template-columns:1fr 1fr;gap:12px'});
  const prioField = el('div',{class:'fl-field'}, el('div',{class:'fl-field-lbl'},'Priority (0–100)'));
  const prioInput = el('input',{class:'fl-input',type:'number',placeholder:'50',value:existing?.priority??''});
  prioField.appendChild(prioInput);
  const maxField  = el('div',{class:'fl-field'}, el('div',{class:'fl-field-lbl'},'Max runs / hour'));
  const maxInput  = el('input',{class:'fl-input',type:'number',placeholder:'Unlimited',value:existing?.maxRunsPerHour??''});
  maxField.appendChild(maxInput);
  numRow.append(prioField, maxField);

  const condHd   = el('div',{class:'fl-section-hd'},'Conditions (optional)');
  const condSec  = makeConditionsSection(existing?.conditions || []);

  const actHd    = el('div',{class:'fl-section-hd'},'Actions');
  const actSec   = makeActionsSection(existing?.actions || []);

  const errEl    = el('div',{class:'fl-err',style:'display:none'});
  const saveBtn  = el('button',{class:'fl-btn fl-btn-primary',style:'align-self:flex-end'}, existing?.id ? '💾 Save Flow' : '⚡ Create Flow');

  saveBtn.addEventListener('click', async () => {
    errEl.style.display = 'none';
    if (!nameInput.value.trim()) { errEl.textContent='Name is required.'; errEl.style.display=''; return; }
    const payload = {
      name: nameInput.value.trim(), enabled: togCb.checked, siteId,
      trigger: { type: trigSel.value },
      conditions: condSec.getValues(),
      actions:    actSec.getValues(),
      priority:        prioInput.value ? Number(prioInput.value) : undefined,
      maxRunsPerHour:  maxInput.value  ? Number(maxInput.value)  : undefined,
    };
    saveBtn.disabled = true; saveBtn.textContent = '⏳ Saving…';
    try {
      if (existing?.id) await client.flows.update(existing.id, payload);
      else              await client.flows.create(payload);
      hide(); onSaved();
    } catch (err) {
      errEl.textContent = mapApiError(err).message; errEl.style.display = '';
      saveBtn.disabled = false; saveBtn.textContent = existing?.id ? '💾 Save Flow' : '⚡ Create Flow';
    }
  });

  body.append(nameField, enabledRow, trigField, numRow, condHd, condSec.wrap, actHd, actSec.wrap, errEl, saveBtn);
  show();
  nameInput.focus();
}

// ─── Run history modal ────────────────────────────────────────────────────────

async function openRunHistoryModal(client, flowId, flowName) {
  const { body, show } = makeModal(`📋 Run History — ${flowName}`);
  body.appendChild(el('div',{style:'color:#94a3b8;font-size:13px;padding:8px 0'},'⏳ Loading…'));
  show();

  try {
    const runs = await client.flows.listRuns(flowId, 100);
    body.replaceChildren();

    const filterRow = el('div',{style:'display:flex;gap:10px;margin-bottom:14px;flex-wrap:wrap'});
    const statusSel = makeSelect([{value:'',label:'All statuses'},{value:'Succeeded',label:'✅ Succeeded'},{value:'Failed',label:'❌ Failed'}]);
    const dateSel   = makeSelect([{value:'',label:'All time'},{value:'24h',label:'Last 24h'},{value:'7d',label:'Last 7 days'},{value:'30d',label:'Last 30 days'}]);
    filterRow.append(statusSel, dateSel);
    body.appendChild(filterRow);

    const listEl = el('div',{});
    body.appendChild(listEl);

    const render = () => {
      const sv=statusSel.value, dv=dateSel.value, now=Date.now();
      const cutMs = dv==='24h'?86400000:dv==='7d'?604800000:dv==='30d'?2592000000:0;
      const filtered = runs.filter(r => {
        if (sv && r.status!==sv) return false;
        if (cutMs>0 && r.executedAtUtc && now-new Date(r.executedAtUtc).getTime()>cutMs) return false;
        return true;
      });
      listEl.replaceChildren();
      if (!filtered.length) { listEl.appendChild(el('div',{style:'color:#94a3b8;font-size:12.5px;padding:12px 0;text-align:center'},'No runs match the current filters.')); return; }
      filtered.forEach(run => {
        const row = el('div',{class:'fl-run-row'});
        const cls = run.status==='Succeeded' ? 'fl-pill fl-pill-green' : 'fl-pill fl-pill-amber';
        row.append(
          el('div',{style:'font-family:JetBrains Mono,monospace;font-size:10.5px;color:#94a3b8'},fmtTime(run.executedAtUtc)),
          el('div',{style:'font-size:11.5px;color:#475569;overflow:hidden;text-overflow:ellipsis;white-space:nowrap'},run.triggerSummary||run.errorMessage||'—'),
          el('span',{class:cls},run.status||'—')
        );
        listEl.appendChild(row);
      });
    };
    statusSel.addEventListener('change', render);
    dateSel.addEventListener('change', render);
    render();
  } catch { body.replaceChildren(el('div',{style:'color:#dc2626'},'Failed to load run history.')); }
}

// ─── Templates modal ──────────────────────────────────────────────────────────

function openTemplateModal(client, notifier, templates, siteId, onSaved) {
  const { body, show, hide } = makeModal('📋 Flow Templates');

  if (!templates?.length) {
    body.appendChild(el('div',{style:'color:#94a3b8;text-align:center;padding:24px;font-size:13px'},'No templates available.'));
    show(); return;
  }

  const list = el('div',{style:'display:flex;flex-direction:column;gap:10px'});
  templates.forEach(tpl => {
    const card = el('div',{class:'fl-tpl'});
    const info = el('div',{style:'flex:1'});
    info.appendChild(el('div',{class:'fl-tpl-name'},tpl.name||'Template'));
    info.appendChild(el('div',{class:'fl-tpl-desc'},tpl.description||''));
    const trigLbl = TRIGGER_TYPES.find(t=>t.value===tpl.trigger?.triggerType||tpl.trigger?.type)?.label || '—';
    info.appendChild(el('span',{class:'fl-pill fl-pill-blue',style:'margin-top:6px;display:inline-flex'},trigLbl));
    const useBtn = el('button',{class:'fl-btn fl-btn-primary fl-btn-sm'},'Use This');
    useBtn.addEventListener('click',()=>{ hide(); openFlowModal(client,notifier,siteId,{...tpl,id:null},onSaved); });
    card.append(info,useBtn);
    list.appendChild(card);
  });
  body.appendChild(list);
  show();
}

// ─── Main export ──────────────────────────────────────────────────────────────

export const renderFlowsView = async (container, { apiClient, toast } = {}) => {
  injectStyles();
  const client   = apiClient || createApiClient();
  const notifier = toast     || createToastManager();
  const state    = { sites: [], siteId: '', flows: [] };

  const root = el('div', { class: 'fl-root' });
  container.appendChild(root);

  // ── Hero ───────────────────────────────────────────────────────────────────
  const hero = el('div', { class: 'fl-hero' });
  const heroTop = el('div', { class: 'fl-hero-top' });
  const heroLeft = el('div', {});
  heroLeft.appendChild(el('div',{class:'fl-hero-title'},'⚡ Flows'));
  heroLeft.appendChild(el('div',{class:'fl-hero-sub'},'Automate actions triggered by visitor events, lead captures, and intelligence updates'));
  const heroStats = el('div',{class:'fl-hero-stats'});
  const mkStat = lbl => { const w=el('div',{class:'fl-stat'}); const v=el('div',{class:'fl-stat-val'},'—'); w.append(v,el('div',{class:'fl-stat-lbl'},lbl)); heroStats.appendChild(w); return v; };
  const hTotal   = mkStat('Total Flows');
  const hEnabled = mkStat('Active');
  heroLeft.appendChild(heroStats);
  heroTop.appendChild(heroLeft);
  hero.appendChild(heroTop);
  root.appendChild(hero);

  // ── Controls ───────────────────────────────────────────────────────────────
  const controls = el('div',{class:'fl-controls'});
  const siteSelect  = el('select',{class:'fl-select'},el('option',{value:''},'Loading sites…'));
  const newBtn      = el('button',{class:'fl-btn fl-btn-primary'},'⚡ Create Flow');
  const templateBtn = el('button',{class:'fl-btn fl-btn-outline'},'📋 Templates');
  controls.append(siteSelect, newBtn, templateBtn);
  root.appendChild(controls);

  // ── Grid ───────────────────────────────────────────────────────────────────
  const gridEl = el('div',{class:'fl-grid'});
  root.appendChild(gridEl);

  // ── Build card ─────────────────────────────────────────────────────────────
  const buildCard = (flow) => {
    const card = el('div',{class:`fl-panel${flow.enabled===false?' fl-disabled':''}`});

    const hd = el('div',{class:'fl-panel-hd'});
    const icon = el('div',{class:'fl-panel-icon'},TRIGGER_ICON(flow.trigger?.type||flow.triggerType));
    const info = el('div',{style:'flex:1;min-width:0'});
    info.appendChild(el('div',{class:'fl-panel-name'},flow.name||'Untitled'));
    const trigLabel = TRIGGER_TYPES.find(t=>t.value===(flow.trigger?.type||flow.triggerType))?.label||(flow.trigger?.type||flow.triggerType||'—');
    info.appendChild(el('div',{class:'fl-panel-trigger'},TRIGGER_ICON(flow.trigger?.type||flow.triggerType),' ',trigLabel));

    const pills = el('div',{style:'display:flex;gap:5px;margin-top:6px;flex-wrap:wrap'});
    pills.appendChild(el('span',{class:`fl-pill ${flow.enabled!==false?'fl-pill-green':'fl-pill-gray'}`},flow.enabled!==false?'Active':'Paused'));
    if (flow.priority)        pills.appendChild(el('span',{class:'fl-pill fl-pill-gray'},`P${flow.priority}`));
    if (flow.maxRunsPerHour)  pills.appendChild(el('span',{class:'fl-pill fl-pill-amber'},`≤${flow.maxRunsPerHour}/hr`));
    info.appendChild(pills);

    const right = el('div',{class:'fl-panel-right'});
    const togLbl = el('label',{class:'fl-toggle'});
    const togCb  = el('input',{type:'checkbox'}); togCb.checked = flow.enabled!==false;
    togCb.addEventListener('change', async () => {
      try {
        if (togCb.checked) await client.flows.enable(flow.id);
        else               await client.flows.disable(flow.id);
        flow.enabled = togCb.checked;
        card.classList.toggle('fl-disabled', !togCb.checked);
        notifier.show({message:`Flow ${togCb.checked?'enabled':'paused'}.`,variant:'success'});
      } catch (err) { togCb.checked=!togCb.checked; notifier.show({message:mapApiError(err).message,variant:'danger'}); }
    });
    togLbl.append(togCb,el('span',{class:'fl-toggle-slider'}));
    right.appendChild(togLbl);

    hd.append(icon, info, right);
    card.appendChild(hd);

    // Actions chips
    if (flow.actions?.length) {
      const body2 = el('div',{class:'fl-panel-body'});
      body2.appendChild(el('div',{style:'font-size:10px;color:#94a3b8;text-transform:uppercase;letter-spacing:.06em;font-weight:700;margin-bottom:4px'},'Actions'));
      const chips = el('div',{style:'display:flex;gap:5px;flex-wrap:wrap'});
      flow.actions.forEach(a=>{ chips.appendChild(el('span',{class:'fl-chip'},ACTION_TYPES.find(t=>t.value===a.actionType)?.label||a.actionType)); });
      body2.appendChild(chips);
      card.appendChild(body2);
    }

    // Footer actions
    const foot = el('div',{class:'fl-panel-foot'});
    const editBtn  = el('button',{class:'fl-btn fl-btn-outline fl-btn-sm'},'✏️ Edit');
    const runsBtn  = el('button',{class:'fl-btn fl-btn-outline fl-btn-sm'},'📋 History');
    editBtn.addEventListener('click', async () => {
      try { const d = await client.flows.get(flow.id); openFlowModal(client,notifier,state.siteId,d,loadFlows); }
      catch (err) { notifier.show({message:mapApiError(err).message,variant:'danger'}); }
    });
    runsBtn.addEventListener('click', () => openRunHistoryModal(client, flow.id, flow.name));
    foot.append(editBtn, runsBtn);
    card.appendChild(foot);
    return card;
  };

  // ── Load ───────────────────────────────────────────────────────────────────
  const loadFlows = async () => {
    gridEl.replaceChildren(el('div',{class:'fl-skel'}),el('div',{class:'fl-skel'}),el('div',{class:'fl-skel'}));
    if (!state.siteId) { gridEl.replaceChildren(); return; }
    try {
      const flows = await client.flows.list(state.siteId);
      state.flows = Array.isArray(flows) ? flows : [];
      hTotal.textContent   = String(state.flows.length);
      hEnabled.textContent = String(state.flows.filter(f=>f.enabled!==false).length);
      gridEl.replaceChildren();
      if (!state.flows.length) {
        const empty = el('div',{class:'fl-empty',style:'grid-column:1/-1'});
        empty.append(el('div',{class:'fl-empty-icon'},'⚡'),el('div',{class:'fl-empty-title'},'No flows yet'),el('div',{class:'fl-empty-desc'},'Create your first automation to trigger actions when visitors arrive, leads are captured, or intelligence updates.'),el('button',{class:'fl-btn fl-btn-primary','@click':()=>openFlowModal(client,notifier,state.siteId,null,loadFlows)},'⚡ Create Your First Flow'));
        gridEl.appendChild(empty);
        return;
      }
      state.flows.forEach(f => gridEl.appendChild(buildCard(f)));
    } catch (err) { gridEl.replaceChildren(); notifier.show({message:mapApiError(err).message,variant:'danger'}); }
  };

  // ── Wire ───────────────────────────────────────────────────────────────────
  siteSelect.addEventListener('change', () => { state.siteId=siteSelect.value; loadFlows(); });
  newBtn.addEventListener('click', () => {
    if (!state.siteId) { notifier.show({message:'Select a site first.',variant:'warning'}); return; }
    openFlowModal(client, notifier, state.siteId, null, loadFlows);
  });
  templateBtn.addEventListener('click', async () => {
    if (!state.siteId) { notifier.show({message:'Select a site first.',variant:'warning'}); return; }
    try { const templates = await client.flows.getTemplates(); openTemplateModal(client,notifier,templates,state.siteId,loadFlows); }
    catch (err) { notifier.show({message:mapApiError(err).message,variant:'danger'}); }
  });

  // ── Init ───────────────────────────────────────────────────────────────────
  try {
    const sites = await client.sites.list();
    state.sites = Array.isArray(sites) ? sites : [];
    siteSelect.innerHTML = '';
    if (!state.sites.length) { siteSelect.appendChild(el('option',{value:''},'No sites available')); return; }
    state.sites.forEach(s => { const id=getSiteId(s); siteSelect.appendChild(el('option',{value:id},s.name||s.domain||id)); });
    state.siteId = getSiteId(state.sites[0]);
    siteSelect.value = state.siteId;
    await loadFlows();
  } catch (err) { siteSelect.innerHTML='<option value="">Failed to load sites</option>'; }
};
