/**
 * integrations.js — Hven Webhooks & Integrations
 * Manage outgoing webhooks for lead.created and visitor.identified events.
 */

import { createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

// ─── Styles ───────────────────────────────────────────────────────────────────

const injectStyles = () => {
  if (document.getElementById('_int_css')) return;
  const s = document.createElement('style');
  s.id = '_int_css';
  s.textContent = `
@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&family=JetBrains+Mono:wght@400;500&display=swap');
.int-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;gap:24px;width:100%;max-width:860px}
.int-hero{background:linear-gradient(135deg,#0f172a 0%,#1e293b 100%);border-radius:16px;padding:28px 32px;color:#fff;display:flex;align-items:flex-start;justify-content:space-between;gap:16px;flex-wrap:wrap}
.int-hero-title{font-size:22px;font-weight:800;letter-spacing:-.02em;margin-bottom:4px}
.int-hero-sub{font-size:13px;color:#94a3b8;line-height:1.5;max-width:480px}
.int-hero-badge{background:rgba(255,255,255,.08);border:1px solid rgba(255,255,255,.12);border-radius:10px;padding:10px 16px;text-align:center;flex-shrink:0}
.int-hero-badge-num{font-size:28px;font-weight:800;color:#fff;line-height:1}
.int-hero-badge-lbl{font-size:10px;color:#64748b;text-transform:uppercase;letter-spacing:.07em;margin-top:2px}
.int-panel{background:#fff;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden}
.int-panel-hd{display:flex;align-items:center;justify-content:space-between;padding:14px 20px;border-bottom:1px solid #f1f5f9}
.int-panel-title{font-size:13px;font-weight:700;color:#0f172a;letter-spacing:-.01em}
.int-panel-meta{font-size:11px;color:#94a3b8}
.int-panel-body{padding:20px}
.int-section-label{font-size:10.5px;font-weight:700;text-transform:uppercase;letter-spacing:.07em;color:#94a3b8;margin-bottom:10px}
.int-form-row{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin-bottom:10px}
.int-form-full{margin-bottom:10px}
@media(max-width:600px){.int-form-row{grid-template-columns:1fr}}
.int-field{display:flex;flex-direction:column;gap:4px}
.int-field label{font-size:11px;font-weight:600;color:#64748b;text-transform:uppercase;letter-spacing:.04em}
.int-input{padding:8px 12px;border:1.5px solid #e2e8f0;border-radius:8px;font-size:13px;font-family:inherit;outline:none;transition:border-color .14s;background:#fff;color:#0f172a;width:100%}
.int-input:focus{border-color:#6366f1}
.int-input::placeholder{color:#94a3b8}
.int-checkbox-group{display:flex;flex-direction:column;gap:6px}
.int-checkbox-row{display:flex;align-items:center;gap:8px;font-size:13px;color:#334155;cursor:pointer;user-select:none}
.int-checkbox-row input{width:14px;height:14px;accent-color:#6366f1;cursor:pointer;flex-shrink:0}
.int-btn{display:inline-flex;align-items:center;gap:6px;padding:8px 16px;border-radius:8px;border:none;font-size:13px;font-weight:600;cursor:pointer;font-family:inherit;transition:all .14s;white-space:nowrap}
.int-btn-primary{background:#6366f1;color:#fff}.int-btn-primary:hover{background:#4f46e5}
.int-btn-primary:disabled{opacity:.6;cursor:default}
.int-btn-outline{background:#fff;color:#475569;border:1.5px solid #e2e8f0}.int-btn-outline:hover{background:#f8fafc}
.int-btn-danger{background:#fff;color:#ef4444;border:1.5px solid #fecaca}.int-btn-danger:hover{background:#fef2f2}
.int-btn-sm{padding:5px 10px;font-size:11.5px}
.int-webhook-list{display:flex;flex-direction:column;gap:8px}
.int-webhook-card{border:1px solid #e2e8f0;border-radius:10px;padding:12px 16px;display:flex;align-items:flex-start;justify-content:space-between;gap:12px;background:#fafbff}
.int-webhook-info{flex:1;min-width:0}
.int-webhook-label{font-size:13px;font-weight:600;color:#0f172a;margin-bottom:2px}
.int-webhook-url{font-size:11px;color:#64748b;font-family:'JetBrains Mono',monospace;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;max-width:400px}
.int-webhook-pills{display:flex;flex-wrap:wrap;gap:4px;margin-top:6px}
.int-pill{display:inline-flex;align-items:center;padding:2px 8px;border-radius:999px;font-size:10px;font-weight:700}
.int-pill-slack{background:#4a154b;color:#fff}
.int-pill-generic{background:#eef2ff;color:#4f46e5}
.int-pill-event{background:#f1f5f9;color:#475569}
.int-webhook-actions{display:flex;gap:6px;flex-shrink:0;align-items:flex-start;padding-top:2px}
.int-empty{padding:40px 20px;text-align:center;color:#94a3b8;font-size:13px;display:flex;flex-direction:column;align-items:center;gap:8px}
.int-empty-icon{font-size:32px;opacity:.4}
.int-guide{background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:16px 20px}
.int-guide-hd{display:flex;align-items:center;justify-content:space-between;cursor:pointer;margin-bottom:0}
.int-guide-title{font-size:12px;font-weight:700;color:#0f172a}
.int-guide-toggle{font-size:11px;color:#6366f1;font-weight:600}
.int-guide-body{margin-top:12px;display:none}
.int-guide-body.open{display:block}
.int-guide-steps{font-size:12.5px;color:#475569;line-height:1.8;padding-left:18px;margin-bottom:10px}
.int-guide-steps li{margin-bottom:2px}
.int-guide-link{font-size:12px;color:#6366f1;font-weight:600;text-decoration:none}
.int-guide-link:hover{text-decoration:underline}
.int-select{padding:8px 12px;border:1.5px solid #e2e8f0;border-radius:8px;font-size:13px;font-family:inherit;outline:none;background:#fff;color:#0f172a;cursor:pointer;width:100%}
.int-select:focus{border-color:#6366f1}
.int-divider{border:none;border-top:1px solid #f1f5f9;margin:16px 0}
.int-site-row{display:flex;align-items:center;gap:10px;margin-bottom:0}
.int-site-row label{font-size:12px;font-weight:600;color:#64748b;white-space:nowrap}
`;
  document.head.appendChild(s);
};

// ─── Helpers ──────────────────────────────────────────────────────────────────

const el = (tag, attrs = {}, ...kids) => {
  const e = document.createElement(tag);
  Object.entries(attrs).forEach(([k, v]) => {
    if (k === 'class')           e.className = v;
    else if (k === 'html')       e.innerHTML = v;
    else if (k === 'style')      typeof v === 'string' ? (e.style.cssText = v) : Object.assign(e.style, v);
    else if (k.startsWith('@'))  e.addEventListener(k.slice(1), v);
    else                         e.setAttribute(k, v);
  });
  kids.flat(Infinity).forEach(c => c != null && e.append(typeof c === 'string' ? document.createTextNode(c) : c));
  return e;
};

const getSiteId = s => s?.siteId || s?.id || '';

const EVENT_OPTIONS = [
  { value: 'lead.created',       label: '⭐ Lead captured'        },
  { value: 'visitor.identified', label: '👁 Visitor identified'  },
];

// ─── Main export ──────────────────────────────────────────────────────────────

export async function renderIntegrationsView(container, { apiClient: clientArg, toast: toastArg } = {}) {
  injectStyles();
  const toast = toastArg || createToastManager();
  const api   = clientArg || createApiClient();

  let currentSite  = null;
  let webhookCount = 0;

  // ── Root ───────────────────────────────────────────────────────────────────
  container.innerHTML = '';
  if (toast.element) container.appendChild(toast.element);

  const root = el('div', { class: 'int-root' });
  container.appendChild(root);

  // ── Hero ───────────────────────────────────────────────────────────────────
  const badgeNum = el('div', { class: 'int-hero-badge-num' }, '0');
  const hero = el('div', { class: 'int-hero' },
    el('div', {},
      el('div', { class: 'int-hero-title' }, '🔗 Integrations'),
      el('div', { class: 'int-hero-sub' }, 'Connect Hven to your existing tools. Send leads and visitor events to Slack, HubSpot, Zapier, or any webhook endpoint.')
    ),
    el('div', { class: 'int-hero-badge' },
      badgeNum,
      el('div', { class: 'int-hero-badge-lbl' }, 'active webhooks')
    )
  );
  root.appendChild(hero);

  // ── Site selector ──────────────────────────────────────────────────────────
  const siteSelect = el('select', { class: 'int-select', style: 'max-width:260px' });
  const sitePanel = el('div', { class: 'int-panel' },
    el('div', { class: 'int-panel-hd' }, el('div', { class: 'int-panel-title' }, 'Site')),
    el('div', { class: 'int-panel-body' },
      el('div', { class: 'int-site-row' },
        el('label', {}, 'Showing webhooks for:'),
        siteSelect
      )
    )
  );
  root.appendChild(sitePanel);

  // ── Add Webhook panel ──────────────────────────────────────────────────────
  const urlInput   = el('input',  { class: 'int-input', type: 'url',  placeholder: 'https://hooks.slack.com/services/…' });
  const labelInput = el('input',  { class: 'int-input', type: 'text', placeholder: 'e.g. Slack #leads' });
  const typeSelect = el('select', { class: 'int-select' });
  [['generic', 'Generic JSON'], ['slack', 'Slack']].forEach(([v, l]) =>
    typeSelect.appendChild(el('option', { value: v }, l)));

  const eventCbs = EVENT_OPTIONS.map(opt => {
    const cb  = el('input',  { type: 'checkbox', value: opt.value });
    const lbl = el('label',  { class: 'int-checkbox-row' }, cb, ` ${opt.label}`);
    return { cb, lbl };
  });

  const saveBtn = el('button', { class: 'int-btn int-btn-primary', type: 'button' }, '＋ Add Webhook');

  const addPanel = el('div', { class: 'int-panel' },
    el('div', { class: 'int-panel-hd' },
      el('div', { class: 'int-panel-title' }, 'Add Webhook')
    ),
    el('div', { class: 'int-panel-body' },
      el('div', { class: 'int-form-full' },
        el('div', { class: 'int-field' },
          el('label', {}, 'Endpoint URL'),
          urlInput
        )
      ),
      el('div', { class: 'int-form-row' },
        el('div', { class: 'int-field' },
          el('label', {}, 'Friendly name'),
          labelInput
        ),
        el('div', { class: 'int-field' },
          el('label', {}, 'Type'),
          typeSelect
        )
      ),
      el('div', { style: 'margin-bottom:16px' },
        el('div', { class: 'int-section-label' }, 'Subscribe to events'),
        el('div', { class: 'int-checkbox-group' }, ...eventCbs.map(x => x.lbl))
      ),
      saveBtn
    )
  );
  root.appendChild(addPanel);

  // ── Active Webhooks panel ──────────────────────────────────────────────────
  const webhookCountBadge = el('div', { class: 'int-panel-meta' }, '0 webhooks');
  const webhookListEl = el('div', {});
  const activePanel = el('div', { class: 'int-panel' },
    el('div', { class: 'int-panel-hd' },
      el('div', { class: 'int-panel-title' }, 'Active Webhooks'),
      webhookCountBadge
    ),
    el('div', { class: 'int-panel-body', style: 'padding-top:4px' }, webhookListEl)
  );
  root.appendChild(activePanel);

  // ── Slack Setup Guide panel ────────────────────────────────────────────────
  const guideBody = el('div', { class: 'int-guide-body' },
    el('ol', { class: 'int-guide-steps' },
      el('li', {}, 'Go to ', el('a', { href: 'https://api.slack.com/apps', target: '_blank', rel: 'noopener', class: 'int-guide-link' }, 'api.slack.com/apps'), ' and click "Create New App"'),
      el('li', {}, 'Choose "From scratch", name it "Hven Alerts"'),
      el('li', {}, 'Under "Incoming Webhooks", activate it and click "Add New Webhook to Workspace"'),
      el('li', {}, 'Select a channel (e.g. #leads), copy the Webhook URL'),
      el('li', {}, 'Paste the URL above, select type "Slack", check your events, click Add')
    ),
    el('a', { href: 'https://api.slack.com/apps', target: '_blank', rel: 'noopener', class: 'int-guide-link' }, 'Open Slack API →')
  );
  const guideToggleBtn = el('span', { class: 'int-guide-toggle' }, '▸ Show');
  const guideHd = el('div', { class: 'int-guide-hd' },
    el('div', { class: 'int-guide-title' }, '🔔 How to connect Slack'),
    guideToggleBtn
  );
  guideHd.addEventListener('click', () => {
    const open = guideBody.classList.toggle('open');
    guideToggleBtn.textContent = open ? '▾ Hide' : '▸ Show';
  });
  const slackPanel = el('div', { class: 'int-panel' },
    el('div', { class: 'int-panel-hd' }, el('div', { class: 'int-panel-title' }, 'Slack Setup Guide')),
    el('div', { class: 'int-panel-body' },
      el('div', { class: 'int-guide' }, guideHd, guideBody)
    )
  );
  root.appendChild(slackPanel);

  // ── Load sites ─────────────────────────────────────────────────────────────
  try {
    const sites = await api.sites.list();
    if (!sites?.length) {
      siteSelect.appendChild(el('option', {}, 'No sites found'));
    } else {
      sites.forEach(s => {
        siteSelect.appendChild(el('option', { value: getSiteId(s) }, s.domain || getSiteId(s)));
      });
      currentSite = sites[0];
      await loadWebhooks();
    }
  } catch (err) {
    toast.show(mapApiError(err)?.message || 'Failed to load sites.', 'error');
  }

  siteSelect.addEventListener('change', async () => {
    const sites = await api.sites.list().catch(() => []);
    currentSite = sites.find(s => getSiteId(s) === siteSelect.value) || null;
    await loadWebhooks();
  });

  // ── Load webhooks ──────────────────────────────────────────────────────────
  async function loadWebhooks() {
    if (!currentSite) return;
    webhookListEl.innerHTML = '<div style="padding:16px;font-size:13px;color:#94a3b8">Loading…</div>';
    try {
      const items = await api.integrations.listWebhooks(getSiteId(currentSite));
      renderWebhooks(items || []);
    } catch (err) {
      webhookListEl.innerHTML = `<div style="padding:16px;font-size:13px;color:#ef4444">${mapApiError(err)?.message || 'Failed to load webhooks.'}</div>`;
    }
  }

  function renderWebhooks(items) {
    webhookCount = items.length;
    badgeNum.textContent = String(webhookCount);
    webhookCountBadge.textContent = `${webhookCount} webhook${webhookCount !== 1 ? 's' : ''}`;
    webhookListEl.innerHTML = '';

    if (!items.length) {
      webhookListEl.appendChild(
        el('div', { class: 'int-empty' },
          el('div', { class: 'int-empty-icon' }, '🔗'),
          el('div', {}, 'No webhooks yet. Add one above to start receiving events.')
        )
      );
      return;
    }

    const list = el('div', { class: 'int-webhook-list' });
    items.forEach(item => {
      const events = Array.isArray(item.events)
        ? item.events
        : (item.events || '').split(',').filter(Boolean);

      const testBtn = el('button', { class: 'int-btn int-btn-outline int-btn-sm', type: 'button' }, 'Test');
      testBtn.addEventListener('click', async () => {
        testBtn.disabled = true;
        testBtn.textContent = '…';
        try {
          await api.integrations.testWebhook(item.id);
          toast.show('✓ Test payload sent', 'success');
        } catch (err) {
          toast.show('✗ Test failed — ' + (mapApiError(err)?.message || 'unknown error'), 'error');
        } finally {
          testBtn.disabled = false;
          testBtn.textContent = 'Test';
        }
      });

      const delBtn = el('button', { class: 'int-btn int-btn-danger int-btn-sm', type: 'button' }, 'Delete');
      delBtn.addEventListener('click', async () => {
        if (!confirm(`Delete webhook "${item.label}"?`)) return;
        try {
          await api.integrations.deleteWebhook(item.id);
          toast.show('Webhook deleted.', 'success');
          await loadWebhooks();
        } catch (err) {
          toast.show(mapApiError(err)?.message || 'Delete failed.', 'error');
        }
      });

      const typePill = el('span', { class: `int-pill ${item.type === 'slack' ? 'int-pill-slack' : 'int-pill-generic'}` },
        item.type === 'slack' ? 'Slack' : 'Generic');

      const pills = el('div', { class: 'int-webhook-pills' },
        typePill,
        ...events.map(ev => el('span', { class: 'int-pill int-pill-event' }, ev))
      );

      list.appendChild(
        el('div', { class: 'int-webhook-card' },
          el('div', { class: 'int-webhook-info' },
            el('div', { class: 'int-webhook-label' }, item.label),
            el('div', { class: 'int-webhook-url', title: item.url }, item.url),
            pills
          ),
          el('div', { class: 'int-webhook-actions' }, testBtn, delBtn)
        )
      );
    });
    webhookListEl.appendChild(list);
  }

  // ── Save webhook ───────────────────────────────────────────────────────────
  saveBtn.addEventListener('click', async () => {
    if (!currentSite) { toast.show('Select a site first.', 'error'); return; }

    const url    = urlInput.value.trim();
    const label  = labelInput.value.trim();
    const type   = typeSelect.value;
    const events = eventCbs.filter(x => x.cb.checked).map(x => x.cb.value);

    if (!url)           { toast.show('URL is required.', 'error'); return; }
    if (!label)         { toast.show('Label is required.', 'error'); return; }
    if (!events.length) { toast.show('Select at least one event.', 'error'); return; }

    saveBtn.disabled = true;
    saveBtn.textContent = 'Adding…';
    try {
      await api.integrations.createWebhook({ siteId: getSiteId(currentSite), url, label, type, events });
      urlInput.value = '';
      labelInput.value = '';
      eventCbs.forEach(x => { x.cb.checked = false; });
      toast.show('Webhook added.', 'success');
      await loadWebhooks();
    } catch (err) {
      toast.show(mapApiError(err)?.message || 'Failed to add webhook.', 'error');
    } finally {
      saveBtn.disabled = false;
      saveBtn.textContent = '＋ Add Webhook';
    }
  });
}
