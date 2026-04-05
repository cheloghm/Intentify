/**
 * sites.js — Intentify Sites
 * Revamped to match visitors.js design language exactly.
 * All original functionality preserved: keys modal, origins modal, add/delete site,
 * installation status check, copy to clipboard, key regeneration.
 */

import { createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

// ─── Helpers (identical to original) ─────────────────────────────────────────

const ORIGIN_HELPER_TEXT = 'Paste the website origin (scheme + host + port). No paths.';

const normalizeOrigin = (value) => {
  const input = typeof value === 'string' ? value.trim() : '';
  if (!input) return { value: '', error: ORIGIN_HELPER_TEXT };
  const withoutTrailingSlash = input.replace(/\/+$/, '');
  let normalized = withoutTrailingSlash;
  if (withoutTrailingSlash.includes('/')) {
    try { const p = new URL(withoutTrailingSlash); normalized = `${p.protocol}//${p.host}`; }
    catch { normalized = withoutTrailingSlash; }
  }
  if (!/^https?:\/\//i.test(normalized)) return { value: '', error: ORIGIN_HELPER_TEXT };
  try {
    const p = new URL(normalized);
    if (!p.hostname) return { value: '', error: ORIGIN_HELPER_TEXT };
    return { value: `${p.protocol}//${p.host}`, error: '' };
  } catch { return { value: '', error: ORIGIN_HELPER_TEXT }; }
};

const normalizeOrigins = (origins) => {
  const unique = new Map();
  origins.forEach((o) => {
    const r = normalizeOrigin(o);
    if (!r.value) return;
    const k = r.value.toLowerCase();
    if (!unique.has(k)) unique.set(k, r.value);
  });
  return Array.from(unique.values());
};

const copyToClipboard = async (value) => {
  if (navigator.clipboard?.writeText) { await navigator.clipboard.writeText(value); return; }
  const t = document.createElement('textarea');
  t.value = value; t.style.cssText = 'position:fixed;opacity:0;';
  document.body.appendChild(t); t.focus(); t.select();
  document.execCommand('copy'); t.remove();
};

const getSiteId = (site) => site?.siteId || site?.id || '';
const fmtDate   = (v) => { if (!v) return '—'; const d = new Date(v); return isNaN(d) ? '—' : d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' }); };

const saveCachedKeys = (siteId, { siteKey }) => {
  if (!siteId || !siteKey) return;
  try { localStorage.setItem(`intentify.siteKeys.${siteId}`, JSON.stringify({ siteKey, cachedAtUtc: new Date().toISOString() })); } catch {}
};

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
  if (document.getElementById('_sites2_css')) return;
  const s = document.createElement('style');
  s.id = '_sites2_css';
  s.textContent = `
@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap');

.si-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;gap:20px;width:100%;max-width:960px;padding-bottom:60px}

/* Hero — identical pattern to .v-hero */
.si-hero{background:linear-gradient(135deg,#0f172a 0%,#1e293b 100%);border-radius:16px;padding:28px 36px;position:relative;overflow:hidden}
.si-hero::before{content:'';position:absolute;top:-30px;right:-30px;width:180px;height:180px;background:radial-gradient(circle,rgba(99,102,241,.18) 0%,transparent 70%);pointer-events:none}
.si-hero-top{display:flex;align-items:flex-start;justify-content:space-between;gap:16px;flex-wrap:wrap}
.si-hero-title{font-size:24px;font-weight:700;color:#f8fafc;letter-spacing:-.02em;margin-bottom:6px}
.si-hero-sub{font-size:13px;color:#94a3b8;margin-bottom:18px}
.si-hero-stats{display:flex;gap:28px;flex-wrap:wrap}
.si-stat{display:flex;flex-direction:column;gap:2px}
.si-stat-val{font-family:'JetBrains Mono',monospace;font-size:22px;font-weight:700;color:#f1f5f9;letter-spacing:-.02em}
.si-stat-lbl{font-size:10px;color:#64748b;text-transform:uppercase;letter-spacing:.07em}

/* Buttons — same as .v-btn */
.si-btn{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;font-weight:600;padding:7px 16px;border-radius:8px;border:none;cursor:pointer;transition:all .14s;display:inline-flex;align-items:center;gap:5px;white-space:nowrap}
.si-btn-primary{background:#6366f1;color:#fff}
.si-btn-primary:hover:not(:disabled){background:#4f46e5;transform:translateY(-1px);box-shadow:0 4px 12px rgba(99,102,241,.25)}
.si-btn-primary:disabled{opacity:.5;cursor:not-allowed}
.si-btn-outline{background:#fff;color:#64748b;border:1px solid #e2e8f0}
.si-btn-outline:hover{background:#f8fafc;color:#1e293b}
.si-btn-danger{background:#fee2e2;color:#dc2626;border:none}
.si-btn-danger:hover{background:#fecaca}
.si-btn-sm{padding:5px 12px;font-size:12px}

/* Panel — same as .v-panel */
.si-panel{background:#fff;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden;transition:box-shadow .16s}
.si-panel:hover{box-shadow:0 4px 16px rgba(0,0,0,.06)}
.si-panel-hd{display:flex;align-items:flex-start;justify-content:space-between;padding:18px 22px;border-bottom:1px solid #f1f5f9;gap:12px}
.si-panel-title{font-size:15px;font-weight:700;color:#0f172a;margin-bottom:2px}
.si-panel-sub{font-size:11px;color:#94a3b8;font-family:'JetBrains Mono',monospace}
.si-panel-body{padding:18px 22px;display:flex;flex-direction:column;gap:12px}
.si-panel-actions{display:flex;gap:6px;flex-shrink:0;flex-wrap:wrap}

/* Health dot */
.si-health{display:inline-flex;align-items:center;gap:5px;font-size:10.5px;font-weight:700;padding:3px 8px;border-radius:999px;margin-top:4px}
.si-health-ok{background:#d1fae5;color:#065f46}
.si-health-warn{background:#fef3c7;color:#92400e}
.si-health-unknown{background:#f1f5f9;color:#64748b}
.si-dot{width:6px;height:6px;border-radius:50%;flex-shrink:0}
.si-dot-green{background:#10b981;box-shadow:0 0 0 2px rgba(16,185,129,.25)}
.si-dot-amber{background:#f59e0b}
.si-dot-gray{background:#94a3b8}
.si-live-dot{animation:_ldp 2s infinite}
@keyframes _ldp{0%{box-shadow:0 0 0 0 rgba(16,185,129,.5)}70%{box-shadow:0 0 0 6px rgba(16,185,129,0)}100%{box-shadow:0 0 0 0 rgba(16,185,129,0)}}

/* Key row */
.si-key-row{display:flex;align-items:center;gap:8px}
.si-key-lbl{font-size:11.5px;color:#64748b;min-width:110px;flex-shrink:0;font-weight:500}
.si-key-val{font-family:'JetBrains Mono',monospace;font-size:11px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:6px;padding:5px 10px;flex:1;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;color:#334155}

/* Origin rows */
.si-origin-row{display:flex;align-items:center;justify-content:space-between;gap:8px;padding:7px 0;border-bottom:1px solid #f8fafc}
.si-origin-row:last-child{border-bottom:none}
.si-origin-text{font-family:'JetBrains Mono',monospace;font-size:11.5px;color:#334155;flex:1;word-break:break-all}
.si-add-row{display:flex;gap:8px;align-items:center;margin-top:8px;flex-wrap:wrap}

/* Input */
.si-input{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;color:#1e293b;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:8px 11px;outline:none;width:100%;box-sizing:border-box;transition:border .14s}
.si-input:focus{border-color:#6366f1;background:#fff;box-shadow:0 0 0 3px rgba(99,102,241,.1)}
.si-err{font-size:11.5px;color:#dc2626;background:#fee2e2;border-radius:6px;padding:6px 10px;margin-top:4px}

/* Stats row inside card */
.si-meta{display:flex;gap:20px;flex-wrap:wrap}
.si-meta-item-lbl{font-size:10px;text-transform:uppercase;letter-spacing:.06em;color:#94a3b8;font-weight:600;margin-bottom:2px}
.si-meta-item-val{font-size:12.5px;color:#334155;font-family:'JetBrains Mono',monospace}

/* Pill */
.si-pill{display:inline-flex;align-items:center;gap:3px;padding:2px 8px;border-radius:999px;font-size:10px;font-weight:700}
.si-pill-green{background:#d1fae5;color:#065f46}
.si-pill-amber{background:#fef3c7;color:#92400e}

/* Overlay / modal */
.si-overlay{position:fixed;inset:0;background:rgba(15,23,42,.55);z-index:200;display:flex;align-items:center;justify-content:center;padding:20px;backdrop-filter:blur(3px)}
.si-modal{background:#fff;border-radius:16px;width:100%;max-width:560px;max-height:90vh;overflow-y:auto;box-shadow:0 24px 64px rgba(0,0,0,.2)}
.si-modal-hd{display:flex;align-items:center;justify-content:space-between;padding:20px 24px;border-bottom:1px solid #f1f5f9;position:sticky;top:0;background:#fff;z-index:1}
.si-modal-title{font-size:15px;font-weight:700;color:#0f172a}
.si-modal-body{padding:20px 24px;display:flex;flex-direction:column;gap:14px}
.si-form-field{display:flex;flex-direction:column;gap:5px}
.si-form-lbl{font-size:10.5px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:#94a3b8}
.si-form-hint{font-size:11px;color:#94a3b8}
.si-warning{background:#fef3c7;border:1px solid #fcd34d;border-radius:8px;padding:10px 12px;font-size:12px;color:#92400e;line-height:1.5}

/* Empty state */
.si-empty{background:#fff;border:1px solid #e2e8f0;border-radius:14px;text-align:center;padding:56px 24px}
.si-empty-icon{font-size:42px;opacity:.3;margin-bottom:14px}
.si-empty-title{font-size:16px;font-weight:700;color:#334155;margin-bottom:6px}
.si-empty-desc{font-size:13px;color:#94a3b8;max-width:300px;margin:0 auto 20px;line-height:1.65}

/* Skeleton */
.si-skel{background:linear-gradient(90deg,#f1f5f9 25%,#e2e8f0 50%,#f1f5f9 75%);background-size:200% 100%;animation:_sh 1.4s infinite;border-radius:14px;height:140px}
@keyframes _sh{0%{background-position:200% 0}100%{background-position:-200% 0}}
  `;
  document.head.appendChild(s);
};

// ─── Modal factory ────────────────────────────────────────────────────────────

const makeModal = (title) => {
  const overlay = el('div', { class: 'si-overlay' });
  const modal   = el('div', { class: 'si-modal' });
  const mhd     = el('div', { class: 'si-modal-hd' });
  const titleEl = el('div', { class: 'si-modal-title' }, title);
  const closeBtn = el('button', { class: 'si-btn si-btn-outline si-btn-sm' }, '✕');
  mhd.append(titleEl, closeBtn);
  const body = el('div', { class: 'si-modal-body' });
  modal.append(mhd, body);
  overlay.appendChild(modal);

  const show = () => { document.body.appendChild(overlay); };
  const hide = () => overlay.remove();
  closeBtn.addEventListener('click', hide);
  overlay.addEventListener('click', (e) => { if (e.target === overlay) hide(); });

  return { body, show, hide, setTitle: (t) => { titleEl.textContent = t; } };
};

// ─── Main export ──────────────────────────────────────────────────────────────

export const renderSitesView = (container, { apiClient, toast } = {}) => {
  injectStyles();
  const client   = apiClient || createApiClient();
  const notifier = toast     || createToastManager();
  const state    = { sites: [], loading: true, error: null };

  const root = el('div', { class: 'si-root' });
  container.appendChild(root);

  // ── Hero ───────────────────────────────────────────────────────────────────
  const hero = el('div', { class: 'si-hero' });
  const heroTop = el('div', { class: 'si-hero-top' });
  const heroLeft = el('div', {});
  heroLeft.appendChild(el('div', { class: 'si-hero-title' }, '🌐 Sites'));
  heroLeft.appendChild(el('div', { class: 'si-hero-sub' }, 'Manage tracked websites, API keys, and allowed origins'));

  const heroStats = el('div', { class: 'si-hero-stats' });
  const mkStat = (lbl) => {
    const w = el('div', { class: 'si-stat' });
    const v = el('div', { class: 'si-stat-val' }, '—');
    w.append(v, el('div', { class: 'si-stat-lbl' }, lbl));
    heroStats.appendChild(w);
    return v;
  };
  const hTotal = mkStat('Sites');
  const hConfigured = mkStat('Configured');
  heroLeft.appendChild(heroStats);

  const addSiteBtn = el('button', { class: 'si-btn si-btn-primary', style: 'align-self:flex-start;margin-top:4px' }, '+ Add Site');
  heroTop.append(heroLeft);
  hero.append(heroTop, addSiteBtn);
  root.appendChild(hero);

  // ── List ───────────────────────────────────────────────────────────────────
  const listEl = el('div', { style: 'display:flex;flex-direction:column;gap:14px' });
  root.appendChild(listEl);

  // ── Keys modal ─────────────────────────────────────────────────────────────
  const keysModal = makeModal('Site Keys');

  const openKeysModal = async (site) => {
    const siteId = getSiteId(site);
    keysModal.setTitle(`🔑 Keys — ${site.domain || site.name || 'Site'}`);
    keysModal.body.replaceChildren(el('div', { style: 'color:#94a3b8;padding:12px 0' }, '⏳ Loading keys…'));
    keysModal.show();

    try {
      const keys = await client.sites.getKeys(siteId);
      keysModal.body.replaceChildren();

      keysModal.body.appendChild(el('div', { class: 'si-warning' },
        '⚠ Keep these keys safe. Regenerating will invalidate existing ones — update any installed snippets.'));

      const mkKeyRow = (label, value) => {
        const row = el('div', { class: 'si-key-row' });
        const lbl = el('div', { class: 'si-key-lbl' }, label);
        const val = el('div', { class: 'si-key-val', title: value || '' }, value || '(not available)');
        const copyBtn = el('button', { class: 'si-btn si-btn-outline si-btn-sm' }, '📋 Copy');
        copyBtn.addEventListener('click', async () => {
          try { await copyToClipboard(value); notifier.show({ message: `${label} copied.`, variant: 'success' }); copyBtn.textContent = '✓ Copied'; setTimeout(() => { copyBtn.textContent = '📋 Copy'; }, 1500); }
          catch { notifier.show({ message: 'Unable to copy.', variant: 'danger' }); }
        });
        row.append(lbl, val, copyBtn);
        return row;
      };

      keysModal.body.append(
        mkKeyRow('Site Key (tracker)', keys.siteKey),
        mkKeyRow('Widget Key (engage)', keys.widgetKey),
      );

      const footerRow = el('div', { style: 'display:flex;gap:8px;margin-top:4px;flex-wrap:wrap' });

      const installBtn = el('button', { class: 'si-btn si-btn-outline si-btn-sm' }, '📦 Install Guide');
      installBtn.addEventListener('click', () => {
        const p = new URLSearchParams({ siteId });
        if (site.domain) p.set('domain', site.domain);
        if (keys.siteKey) p.set('siteKey', keys.siteKey);
        window.location.hash = `#/install?${p.toString()}`;
        keysModal.hide();
      });

      const regenBtn = el('button', { class: 'si-btn si-btn-danger si-btn-sm' }, '🔄 Regenerate Keys');
      regenBtn.addEventListener('click', async () => {
        if (!confirm('Regenerating keys will invalidate existing ones. Continue?')) return;
        regenBtn.disabled = true; regenBtn.textContent = '⏳ Regenerating…';
        try {
          const resp = await client.sites.regenerateKeys(siteId);
          saveCachedKeys(siteId, { siteKey: resp.siteKey });
          notifier.show({ message: 'Keys regenerated.', variant: 'success' });
          keysModal.hide();
          openKeysModal(site);
        } catch (err) {
          notifier.show({ message: mapApiError(err).message, variant: 'danger' });
          regenBtn.disabled = false; regenBtn.textContent = '🔄 Regenerate Keys';
        }
      });

      footerRow.append(installBtn, regenBtn);
      keysModal.body.appendChild(footerRow);
    } catch (err) {
      keysModal.body.replaceChildren(el('div', { style: 'color:#dc2626' }, `Failed: ${mapApiError(err).message}`));
    }
  };

  // ── Origins modal ──────────────────────────────────────────────────────────
  const originsModal = makeModal('Edit Origins');

  const openOriginsModal = (site) => {
    const siteId = getSiteId(site);
    originsModal.setTitle(`🛡 Origins — ${site.domain || site.name || 'Site'}`);
    originsModal.body.replaceChildren();

    const originList = [...(site.allowedOrigins || [])];
    let originInputVal = '';
    let saving = false;

    const renderEditor = () => {
      originsModal.body.replaceChildren();

      originsModal.body.appendChild(el('div', { style: 'font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:#94a3b8;margin-bottom:8px' }, 'Allowed Origins'));

      if (!originList.length) {
        originsModal.body.appendChild(el('div', { style: 'font-size:13px;color:#94a3b8;margin-bottom:12px' }, 'No origins configured yet.'));
      } else {
        const listWrap = el('div', { style: 'display:flex;flex-direction:column;margin-bottom:14px' });
        originList.forEach((origin, idx) => {
          const row = el('div', { class: 'si-origin-row' });
          row.appendChild(el('div', { class: 'si-origin-text' }, origin));
          const rmBtn = el('button', { class: 'si-btn si-btn-danger si-btn-sm' }, '✕ Remove');
          rmBtn.addEventListener('click', () => { originList.splice(idx, 1); renderEditor(); });
          row.appendChild(rmBtn);
          listWrap.appendChild(row);
        });
        originsModal.body.appendChild(listWrap);
      }

      // Add input
      const addLbl = el('div', { style: 'font-size:10.5px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:#94a3b8;margin-bottom:5px' }, 'Add Origin');
      const addInput = el('input', { class: 'si-input', style: 'flex:1', placeholder: 'https://app.example.com', value: originInputVal });
      addInput.addEventListener('input', () => { originInputVal = addInput.value; });
      const addBtn  = el('button', { class: 'si-btn si-btn-outline si-btn-sm' }, '+ Add');
      const localBtn = el('button', { class: 'si-btn si-btn-outline si-btn-sm' }, '+ localhost');
      const errEl   = el('div', { class: 'si-err', style: 'display:none' });

      const doAdd = (raw) => {
        const result = normalizeOrigin(raw);
        if (!result.value) { errEl.textContent = result.error; errEl.style.display = ''; return; }
        if (originList.some(o => o.toLowerCase() === result.value.toLowerCase())) {
          notifier.show({ message: 'Origin already listed.', variant: 'warning' }); return;
        }
        originInputVal = ''; originList.push(result.value); renderEditor();
      };

      addBtn.addEventListener('click', () => doAdd(originInputVal));
      localBtn.addEventListener('click', () => doAdd(`http://localhost:${window.location.port || 80}`));
      addInput.addEventListener('keydown', (e) => { if (e.key === 'Enter') doAdd(originInputVal); });

      const addRow = el('div', { class: 'si-add-row' });
      addRow.append(addInput, addBtn, localBtn);
      originsModal.body.append(addLbl, addRow, errEl);

      // Save
      const saveBtn = el('button', { class: 'si-btn si-btn-primary', style: 'align-self:flex-start;margin-top:8px' }, saving ? '⏳ Saving…' : '💾 Save Origins');
      saveBtn.disabled = saving;
      saveBtn.addEventListener('click', async () => {
        saving = true; renderEditor();
        try {
          const normalized = normalizeOrigins(originList);
          const resp = await client.request(`/sites/${siteId}/origins`, {
            method: 'PUT', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ allowedOrigins: normalized }),
          });
          state.sites = state.sites.map(s => getSiteId(s) === siteId ? { ...s, ...resp, allowedOrigins: resp.allowedOrigins || normalized } : s);
          notifier.show({ message: 'Origins updated.', variant: 'success' });
          originsModal.hide();
          renderSites();
        } catch (err) {
          notifier.show({ message: mapApiError(err).message, variant: 'danger' });
        } finally { saving = false; renderEditor(); }
      });
      originsModal.body.appendChild(saveBtn);
    };

    renderEditor();
    originsModal.show();
  };

  // ── Add site modal ─────────────────────────────────────────────────────────
  const addSiteModal = makeModal('Add Site');

  const openAddSiteModal = () => {
    addSiteModal.body.replaceChildren();

    const nameField = el('div', { class: 'si-form-field' });
    nameField.append(el('div',{class:'si-form-lbl'},'Site Name'), el('div',{class:'si-form-hint'},'A friendly label for this site'));
    const nameInput = el('input', { class: 'si-input', placeholder: 'My Website' });
    const nameErr   = el('div', { class: 'si-err', style: 'display:none' });
    nameField.append(nameInput, nameErr);

    const domainField = el('div', { class: 'si-form-field' });
    domainField.append(el('div',{class:'si-form-lbl'},'Domain'), el('div',{class:'si-form-hint'},'e.g. example.com'));
    const domainInput = el('input', { class: 'si-input', placeholder: 'example.com' });
    const domainErr   = el('div', { class: 'si-err', style: 'display:none' });
    domainField.append(domainInput, domainErr);

    const descField = el('div', { class: 'si-form-field' });
    descField.append(el('div',{class:'si-form-lbl'},'Description (optional)'));
    const descInput = el('input', { class: 'si-input', placeholder: 'What this site is about' });
    descField.appendChild(descInput);

    const saveBtn = el('button', { class: 'si-btn si-btn-primary', style: 'align-self:flex-start' }, '🌐 Create Site');
    if (state.sites.length > 0) { saveBtn.disabled = true; saveBtn.textContent = 'Site limit reached'; }

    saveBtn.addEventListener('click', async () => {
      nameErr.style.display = 'none'; domainErr.style.display = 'none';
      const name   = nameInput.value.trim();
      const domain = domainInput.value.trim();
      if (!name)   { nameErr.textContent = 'Name is required.';   nameErr.style.display = '';   return; }
      if (!domain) { domainErr.textContent = 'Domain is required.'; domainErr.style.display = ''; return; }

      saveBtn.disabled = true; saveBtn.textContent = '⏳ Creating…';
      try {
        const resp = await client.sites.create({ name, domain, description: descInput.value.trim() });
        const newId = resp.siteId || resp.id;
        if (resp.siteKey) saveCachedKeys(newId, { siteKey: resp.siteKey });
        notifier.show({ message: 'Site created.', variant: 'success' });
        addSiteModal.hide();
        await loadSites();
      } catch (err) {
        const uiErr = mapApiError(err);
        const dMsg  = uiErr.details?.errors?.domain?.[0];
        if (dMsg) { domainErr.textContent = dMsg; domainErr.style.display = ''; }
        notifier.show({ message: uiErr.message, variant: 'danger' });
        saveBtn.disabled = false; saveBtn.textContent = '🌐 Create Site';
      }
    });

    addSiteModal.body.append(nameField, domainField, descField, saveBtn);
    addSiteModal.show();
    nameInput.focus();
  };

  addSiteBtn.addEventListener('click', openAddSiteModal);

  // ── Render sites list ──────────────────────────────────────────────────────
  const renderSites = () => {
    listEl.replaceChildren();

    if (state.loading) {
      listEl.append(el('div',{class:'si-skel'}), el('div',{class:'si-skel'}));
      return;
    }
    if (state.error) {
      listEl.appendChild(el('div', { style: 'color:#dc2626;font-size:13px;padding:12px 0' }, state.error));
      return;
    }
    if (!state.sites.length) {
      const empty = el('div', { class: 'si-empty' });
      empty.append(
        el('div',{class:'si-empty-icon'},'🌐'),
        el('div',{class:'si-empty-title'},'No sites yet'),
        el('div',{class:'si-empty-desc'},'Add your first site to start tracking visitors and deploying the chat widget.'),
        el('button',{class:'si-btn si-btn-primary','@click':openAddSiteModal},'+ Add Your First Site')
      );
      listEl.appendChild(empty);
      return;
    }

    state.sites.forEach(site => {
      const siteId = getSiteId(site);
      const card   = el('div', { class: 'si-panel' });

      // Header
      const hd = el('div', { class: 'si-panel-hd' });
      const hdLeft = el('div', { style: 'flex:1;min-width:0' });
      hdLeft.appendChild(el('div', { class: 'si-panel-title' }, site.domain || site.name || 'Unnamed'));
      hdLeft.appendChild(el('div', { class: 'si-panel-sub' }, `ID: ${siteId}`));

      // Installation health dot — updated async
      const health = el('div', { class: 'si-health si-health-unknown' }, el('div',{class:'si-dot si-dot-gray'}), '⏳ Checking…');
      hdLeft.appendChild(health);

      const hdActions = el('div', { class: 'si-panel-actions' });
      const keysBtn    = el('button', { class: 'si-btn si-btn-outline si-btn-sm' }, '🔑 Keys');
      const originsBtn = el('button', { class: 'si-btn si-btn-outline si-btn-sm' }, '🛡 Origins');
      const deleteBtn  = el('button', { class: 'si-btn si-btn-danger si-btn-sm' }, '🗑 Delete');

      keysBtn.addEventListener('click', () => openKeysModal(site));
      originsBtn.addEventListener('click', () => openOriginsModal(site));
      deleteBtn.addEventListener('click', async () => {
        if (!confirm('Deleting this site will permanently remove all knowledge sources and data. This cannot be undone.')) return;
        deleteBtn.disabled = true; deleteBtn.textContent = '⏳…';
        try {
          await client.sites.delete(siteId);
          state.sites = state.sites.filter(s => getSiteId(s) !== siteId);
          notifier.show({ message: 'Site deleted.', variant: 'success' });
          renderSites();
          updateHeroStats();
        } catch (err) {
          notifier.show({ message: mapApiError(err).message, variant: 'danger' });
          deleteBtn.disabled = false; deleteBtn.textContent = '🗑 Delete';
        }
      });
      hdActions.append(keysBtn, originsBtn, deleteBtn);
      hd.append(hdLeft, hdActions);
      card.appendChild(hd);

      // Body — meta row
      const body = el('div', { class: 'si-panel-body' });
      const metaRow = el('div', { class: 'si-meta' });
      const mkMeta = (lbl, val) => {
        const w = el('div', {});
        w.append(el('div',{class:'si-meta-item-lbl'},lbl), el('div',{class:'si-meta-item-val'},val));
        metaRow.appendChild(w);
      };
      mkMeta('Allowed Origins', String((site.allowedOrigins || []).length));
      mkMeta('Created',         fmtDate(site.createdAtUtc));
      const configured = site.installationStatus?.isConfigured ?? ((site.allowedOrigins || []).length > 0);
      metaRow.appendChild(el('span', { class: `si-pill ${configured ? 'si-pill-green' : 'si-pill-amber'}` }, configured ? '✓ Configured' : '⚠ Not configured'));
      body.appendChild(metaRow);
      card.appendChild(body);
      listEl.appendChild(card);

      // Fetch installation status async
      client.request(`/sites/${siteId}/installation-status`).then(status => {
        const active = status?.isInstalled || (status?.eventsReceived ?? 0) > 0;
        health.className = `si-health ${active ? 'si-health-ok' : 'si-health-warn'}`;
        health.replaceChildren(
          el('div', { class: `si-dot ${active ? 'si-dot-green si-live-dot' : 'si-dot-amber'}` }),
          document.createTextNode(active ? ' Tracker active' : ' Not detected yet')
        );
        // update configured pill
        state.sites = state.sites.map(s => getSiteId(s) === siteId ? { ...s, installationStatus: status } : s);
      }).catch(() => {
        health.className = 'si-health si-health-unknown';
        health.replaceChildren(el('div',{class:'si-dot si-dot-gray'}), document.createTextNode(' Status unknown'));
      });
    });
  };

  const updateHeroStats = () => {
    hTotal.textContent = String(state.sites.length);
    hConfigured.textContent = String(state.sites.filter(s =>
      s.installationStatus?.isConfigured ?? ((s.allowedOrigins || []).length > 0)
    ).length);
  };

  // ── Load ───────────────────────────────────────────────────────────────────
  const loadSites = async () => {
    state.loading = true; state.error = null;
    renderSites();
    try {
      const sites = await client.sites.list();
      state.sites = Array.isArray(sites) ? sites : [];
      state.loading = false;
      updateHeroStats();
      renderSites();
    } catch (err) {
      state.loading = false;
      state.error = mapApiError(err).message;
      renderSites();
    }
  };

  loadSites();
};
