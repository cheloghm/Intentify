/**
 * integrations.js — Intentify Webhooks & Integrations
 * Manage outgoing webhooks for lead.created and visitor.identified events.
 * Supports generic JSON webhooks and Slack Incoming Webhooks.
 */

import { createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

// ─── Helpers ──────────────────────────────────────────────────────────────────

const el = (tag, attrs = {}, ...kids) => {
  const e = document.createElement(tag);
  Object.entries(attrs).forEach(([k, v]) => {
    if (k === 'class')          e.className = v;
    else if (k === 'html')      e.innerHTML = v;
    else if (k === 'style')     Object.assign(e.style, v);
    else if (k.startsWith('on')) e.addEventListener(k.slice(2), v);
    else                        e.setAttribute(k, v);
  });
  kids.flat().forEach(c => c != null && e.appendChild(typeof c === 'string' ? document.createTextNode(c) : c));
  return e;
};

const getSiteId = s => s?.siteId || s?.id || '';

const EVENT_OPTIONS = [
  { value: 'lead.created',       label: '⭐ Lead Created'       },
  { value: 'visitor.identified', label: '👁 Visitor Identified' },
];

// ─── Main render ─────────────────────────────────────────────────────────────

export async function renderIntegrationsView(container) {
  const toast    = createToastManager();
  const api      = createApiClient();
  const state    = {};

  let currentSite = null;

  // ── Site selector ──────────────────────────────────────────────────────────
  const siteSelector = el('select', { class: 'form-control', style: { maxWidth: '260px', marginBottom: '20px' } });
  const siteWrap = el('div', { class: 'card mb-4' },
    el('div', { class: 'card-body' },
      el('div', { class: 'flex items-center gap-2' },
        el('label', { class: 'form-label mb-0', style: { whiteSpace: 'nowrap' } }, 'Select site:'),
        siteSelector
      )
    )
  );

  // ── Webhook list ───────────────────────────────────────────────────────────
  const webhookList = el('div', { class: 'webhook-list' });

  // ── Add-webhook form ───────────────────────────────────────────────────────
  const urlInput   = el('input',  { type: 'url',  class: 'form-control', placeholder: 'https://hooks.slack.com/…  or  https://your-endpoint.com/hook' });
  const labelInput = el('input',  { type: 'text', class: 'form-control', placeholder: 'e.g. Slack #leads' });
  const typeSelect = el('select', { class: 'form-control' });
  ['generic', 'slack'].forEach(t => typeSelect.appendChild(el('option', { value: t }, t === 'slack' ? 'Slack' : 'Generic JSON')));

  const eventCheckboxes = EVENT_OPTIONS.map(opt => {
    const cb = el('input', { type: 'checkbox', id: `evt-${opt.value}`, value: opt.value, style: { marginRight: '6px' } });
    const lbl = el('label', { for: `evt-${opt.value}`, style: { marginRight: '16px', cursor: 'pointer' } }, cb, opt.label);
    return { cb, lbl };
  });

  const eventsRow = el('div', { style: { display: 'flex', flexWrap: 'wrap', gap: '4px', paddingTop: '4px' } },
    ...eventCheckboxes.map(x => x.lbl));

  const addBtn = el('button', { class: 'btn btn-primary', type: 'button' }, 'Add Webhook');

  const addForm = el('div', { class: 'card mb-4' },
    el('div', { class: 'card-header' }, el('h3', { class: 'card-title' }, '+ Add Webhook')),
    el('div', { class: 'card-body', style: { display: 'grid', gap: '12px' } },
      el('div', {},
        el('label', { class: 'form-label' }, 'Endpoint URL'),
        urlInput
      ),
      el('div', { style: { display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' } },
        el('div', {},
          el('label', { class: 'form-label' }, 'Label'),
          labelInput
        ),
        el('div', {},
          el('label', { class: 'form-label' }, 'Type'),
          typeSelect
        )
      ),
      el('div', {},
        el('label', { class: 'form-label' }, 'Subscribe to events'),
        eventsRow
      ),
      el('div', {}, addBtn)
    )
  );

  // ── Slack guide (collapsible) ─────────────────────────────────────────────
  const guideBody = el('div', {
    style: { display: 'none', padding: '16px 20px' },
    html: `
      <ol style="margin:0;padding-left:18px;line-height:1.9;font-size:14px">
        <li>Open Slack and go to <strong>Apps → Manage → Custom Integrations → Incoming WebHooks</strong>.</li>
        <li>Click <strong>Add to Slack</strong>, choose a channel (e.g. <code>#leads</code>), then click <strong>Add Incoming WebHooks Integration</strong>.</li>
        <li>Copy the <strong>Webhook URL</strong> (starts with <code>https://hooks.slack.com/services/…</code>).</li>
        <li>Paste it in the <em>Endpoint URL</em> field above, set <strong>Type → Slack</strong>, pick your events, and click <strong>Add Webhook</strong>.</li>
      </ol>
      <p style="margin:12px 0 0;font-size:13px;color:#64748b">
        Intentify will format messages with bold headings and relevant lead/visitor data for each event type.
      </p>
    `
  });

  const guideToggle = el('button', {
    class: 'btn btn-secondary btn-sm',
    type: 'button',
    onclick: () => {
      const open = guideBody.style.display !== 'none';
      guideBody.style.display = open ? 'none' : 'block';
      guideToggle.textContent = open ? '▸ How to set up Slack' : '▾ How to set up Slack';
    }
  }, '▸ How to set up Slack');

  const slackGuide = el('div', { class: 'card mb-4' },
    el('div', { class: 'card-header', style: { display: 'flex', alignItems: 'center', justifyContent: 'space-between' } },
      el('h3', { class: 'card-title', style: { margin: 0 } }, '🔔 Slack Integration'),
      guideToggle
    ),
    guideBody
  );

  // ── Page layout ────────────────────────────────────────────────────────────
  container.innerHTML = '';
  container.appendChild(toast.element || document.createComment('toast'));

  const heading = el('div', { style: { marginBottom: '24px' } },
    el('h1', { style: { margin: '0 0 4px', fontSize: '22px', fontWeight: '700' } }, '🔗 Integrations'),
    el('p',  { style: { margin: 0, color: '#64748b', fontSize: '14px' } },
      'Send real-time webhook notifications to Slack or any HTTP endpoint when leads are captured or visitors are identified.')
  );

  const webhookSection = el('div', { class: 'card mb-4' },
    el('div', { class: 'card-header' },
      el('h3', { class: 'card-title' }, 'Active Webhooks')
    ),
    el('div', { class: 'card-body p-0' }, webhookList)
  );

  container.append(heading, siteWrap, slackGuide, addForm, webhookSection);

  // ── Load sites ─────────────────────────────────────────────────────────────
  try {
    const sites = await api.sites.list();
    if (!sites?.length) {
      siteSelector.appendChild(el('option', {}, 'No sites found'));
      return;
    }
    sites.forEach(s => {
      const opt = el('option', { value: getSiteId(s) }, s.domain || getSiteId(s));
      siteSelector.appendChild(opt);
    });
    currentSite = sites[0];
    await loadWebhooks();
  } catch (err) {
    toast.show(mapApiError(err) || 'Failed to load sites.', 'error');
  }

  siteSelector.addEventListener('change', async () => {
    const sites = await api.sites.list().catch(() => []);
    currentSite = sites.find(s => getSiteId(s) === siteSelector.value) || null;
    await loadWebhooks();
  });

  // ── Load webhook list ──────────────────────────────────────────────────────
  async function loadWebhooks() {
    if (!currentSite) return;
    webhookList.innerHTML = '<div style="padding:16px;color:#64748b;font-size:13px">Loading…</div>';
    try {
      const items = await api.integrations.listWebhooks(getSiteId(currentSite));
      renderWebhooks(items || []);
    } catch (err) {
      webhookList.innerHTML = `<div style="padding:16px;color:#ef4444;font-size:13px">${mapApiError(err) || 'Failed to load webhooks.'}</div>`;
    }
  }

  function renderWebhooks(items) {
    webhookList.innerHTML = '';
    if (!items.length) {
      webhookList.appendChild(el('div', { style: { padding: '20px', textAlign: 'center', color: '#94a3b8', fontSize: '13px', fontStyle: 'italic' } },
        'No webhooks configured yet. Add one above.'));
      return;
    }

    items.forEach(item => {
      const events = Array.isArray(item.events) ? item.events : (item.events || '').split(',').filter(Boolean);
      const typeTag = el('span', {
        style: {
          display: 'inline-block', padding: '2px 8px', borderRadius: '12px', fontSize: '11px',
          fontWeight: '700', background: item.type === 'slack' ? '#E01E5A22' : '#6366f122',
          color: item.type === 'slack' ? '#E01E5A' : '#6366f1', marginLeft: '8px'
        }
      }, item.type === 'slack' ? 'Slack' : 'Generic');

      const testBtn = el('button', {
        class: 'btn btn-secondary btn-sm',
        type: 'button',
        onclick: async () => {
          testBtn.disabled = true;
          testBtn.textContent = 'Sending…';
          try {
            await api.integrations.testWebhook(item.id);
            toast.show('Test payload sent.', 'success');
          } catch (err) {
            toast.show(mapApiError(err) || 'Test failed.', 'error');
          } finally {
            testBtn.disabled = false;
            testBtn.textContent = 'Test';
          }
        }
      }, 'Test');

      const deleteBtn = el('button', {
        class: 'btn btn-danger btn-sm',
        type: 'button',
        onclick: async () => {
          if (!confirm(`Delete webhook "${item.label}"?`)) return;
          try {
            await api.integrations.deleteWebhook(item.id);
            toast.show('Webhook deleted.', 'success');
            await loadWebhooks();
          } catch (err) {
            toast.show(mapApiError(err) || 'Delete failed.', 'error');
          }
        }
      }, 'Delete');

      const row = el('div', {
        style: {
          display: 'flex', alignItems: 'center', justifyContent: 'space-between',
          padding: '12px 16px', borderBottom: '1px solid #f1f5f9', flexWrap: 'wrap', gap: '8px'
        }
      },
        el('div', { style: { flex: 1, minWidth: 0 } },
          el('div', { style: { display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: '6px', marginBottom: '4px' } },
            el('span', { style: { fontWeight: '600', fontSize: '14px', color: '#1e293b' } }, item.label),
            typeTag,
            ...events.map(ev => el('span', {
              style: {
                display: 'inline-block', padding: '1px 7px', borderRadius: '10px',
                fontSize: '10px', fontWeight: '600', background: '#f1f5f9', color: '#475569'
              }
            }, ev))
          ),
          el('div', { style: { fontSize: '12px', color: '#94a3b8', fontFamily: 'monospace', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' } }, item.url)
        ),
        el('div', { style: { display: 'flex', gap: '8px', flexShrink: 0 } }, testBtn, deleteBtn)
      );

      webhookList.appendChild(row);
    });
  }

  // ── Add webhook ────────────────────────────────────────────────────────────
  addBtn.addEventListener('click', async () => {
    if (!currentSite) { toast.show('Select a site first.', 'error'); return; }

    const url    = urlInput.value.trim();
    const label  = labelInput.value.trim();
    const type   = typeSelect.value;
    const events = eventCheckboxes.filter(x => x.cb.checked).map(x => x.cb.value);

    if (!url)         { toast.show('URL is required.', 'error'); return; }
    if (!label)       { toast.show('Label is required.', 'error'); return; }
    if (!events.length) { toast.show('Select at least one event.', 'error'); return; }

    addBtn.disabled = true;
    addBtn.textContent = 'Adding…';

    try {
      await api.integrations.createWebhook({
        siteId: getSiteId(currentSite),
        url,
        label,
        type,
        events,
      });
      urlInput.value  = '';
      labelInput.value = '';
      eventCheckboxes.forEach(x => { x.cb.checked = false; });
      toast.show('Webhook added.', 'success');
      await loadWebhooks();
    } catch (err) {
      toast.show(mapApiError(err) || 'Failed to add webhook.', 'error');
    } finally {
      addBtn.disabled = false;
      addBtn.textContent = 'Add Webhook';
    }
  });
}
