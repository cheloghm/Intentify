/**
 * linktree.js — Hven Link Hub
 * Real Link Hub management: profile, links CRUD, live preview, analytics.
 */

import { createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const PLATFORM_ICONS = {
  instagram: '📸', facebook: '👥', tiktok: '🎵', x: '🐦',
  linkedin: '💼', youtube: '▶️', snapchat: '👻', pinterest: '📌',
  whatsapp: '💬', website: '🌐', email: '✉️', custom: '🔗',
};
const PLATFORM_PLACEHOLDERS = {
  instagram: 'https://instagram.com/yourusername',
  facebook:  'https://facebook.com/yourpage',
  tiktok:    'https://tiktok.com/@yourusername',
  x:         'https://x.com/yourusername',
  linkedin:  'https://linkedin.com/in/yourname',
  youtube:   'https://youtube.com/@yourchannel',
  snapchat:  'https://snapchat.com/add/yourusername',
  pinterest: 'https://pinterest.com/yourusername',
  whatsapp:  'https://wa.me/yournumber',
  website:   'https://yourwebsite.com',
  email:     'mailto:you@example.com',
  custom:    'https://',
};

const el = (tag, attrs = {}, ...kids) => {
  const e = document.createElement(tag);
  Object.entries(attrs).forEach(([k, v]) => {
    if (k === 'class')          e.className = v;
    else if (k === 'style')     typeof v === 'string' ? (e.style.cssText = v) : Object.assign(e.style, v);
    else if (k.startsWith('@')) e.addEventListener(k.slice(1), v);
    else                        e.setAttribute(k, v);
  });
  kids.flat(Infinity).forEach(c => c != null && e.append(typeof c === 'string' ? document.createTextNode(c) : c));
  return e;
};

const injectStyles = () => {
  if (document.getElementById('_lh_css')) return;
  const s = document.createElement('style');
  s.id = '_lh_css';
  s.textContent = `
@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&family=JetBrains+Mono:wght@400;500;700&display=swap');
.lh-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;gap:20px;width:100%;max-width:860px}
.lh-hero{background:linear-gradient(135deg,#0f172a 0%,#1e293b 100%);border-radius:16px;padding:28px 32px;color:#fff}
.lh-hero-title{font-size:22px;font-weight:800;letter-spacing:-.02em;margin-bottom:4px}
.lh-hero-sub{font-size:13px;color:#94a3b8;line-height:1.5;margin-bottom:16px}
.lh-hero-url{display:inline-flex;align-items:center;gap:8px;background:rgba(255,255,255,0.07);border:1px solid rgba(255,255,255,0.12);border-radius:8px;padding:6px 14px;font-size:12px;font-family:'JetBrains Mono',monospace;color:#a5b4fc}
.lh-hero-url a{color:#a5b4fc;text-decoration:none}.lh-hero-url a:hover{color:#c7d2fe}
.lh-hero-row{display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:10px}
.lh-layout{display:grid;grid-template-columns:1fr 340px;gap:20px;align-items:start}
@media(max-width:860px){.lh-layout{grid-template-columns:1fr}}
.lh-panel{background:#fff;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden;margin-bottom:16px}
.lh-panel:last-child{margin-bottom:0}
.lh-panel-hd{display:flex;align-items:center;justify-content:space-between;padding:14px 18px;border-bottom:1px solid #f1f5f9}
.lh-panel-title{font-size:13px;font-weight:700;color:#0f172a}
.lh-panel-body{padding:18px}
.lh-field{display:flex;flex-direction:column;gap:4px;margin-bottom:12px}
.lh-field label{font-size:11px;font-weight:600;color:#64748b;text-transform:uppercase;letter-spacing:.04em}
.lh-input{padding:8px 12px;border:1.5px solid #e2e8f0;border-radius:8px;font-size:13px;font-family:inherit;outline:none;transition:border-color .14s;width:100%;box-sizing:border-box}
.lh-input:focus{border-color:#6366f1}
.lh-select{padding:8px 12px;border:1.5px solid #e2e8f0;border-radius:8px;font-size:13px;font-family:inherit;outline:none;background:#fff;width:100%;cursor:pointer;box-sizing:border-box}
.lh-select:focus{border-color:#6366f1}
.lh-btn{display:inline-flex;align-items:center;gap:6px;padding:8px 16px;border-radius:8px;border:none;font-size:13px;font-weight:600;cursor:pointer;font-family:inherit;transition:all .14s}
.lh-btn-primary{background:#6366f1;color:#fff}.lh-btn-primary:hover:not(:disabled){background:#4f46e5}
.lh-btn-primary:disabled{opacity:.5;cursor:not-allowed}
.lh-btn-outline{background:#fff;color:#475569;border:1.5px solid #e2e8f0}.lh-btn-outline:hover{background:#f8fafc}
.lh-btn-danger{background:#fff;color:#ef4444;border:1.5px solid #fecaca}.lh-btn-danger:hover{background:#fef2f2}
.lh-btn-sm{padding:4px 10px;font-size:11.5px}
.lh-btn-full{width:100%;justify-content:center}
.lh-toggle-row{display:flex;align-items:center;justify-content:space-between;padding:10px 0;border-bottom:1px solid #f8fafc}
.lh-toggle-row:last-child{border-bottom:none}
.lh-toggle-label{font-size:13px;font-weight:500;color:#1e293b}
.lh-toggle-sub{font-size:11.5px;color:#94a3b8;margin-top:1px}
.lh-toggle{position:relative;display:inline-block;width:36px;height:20px;flex-shrink:0}
.lh-toggle input{opacity:0;width:0;height:0}
.lh-toggle-slider{position:absolute;cursor:pointer;inset:0;background:#e2e8f0;border-radius:20px;transition:.2s}
.lh-toggle-slider:before{content:'';position:absolute;width:14px;height:14px;left:3px;bottom:3px;background:#fff;border-radius:50%;transition:.2s}
.lh-toggle input:checked + .lh-toggle-slider{background:#6366f1}
.lh-toggle input:checked + .lh-toggle-slider:before{transform:translateX(16px)}
.lh-link-card{border:1.5px solid #e2e8f0;border-radius:10px;padding:10px 12px;display:flex;align-items:center;gap:10px;background:#fafbff;margin-bottom:8px}
.lh-link-card:hover{border-color:#c7d2fe;background:#eef2ff}
.lh-link-icon{font-size:18px;flex-shrink:0}
.lh-link-info{flex:1;min-width:0}
.lh-link-label{font-size:13px;font-weight:600;color:#0f172a;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.lh-link-url{font-size:11px;color:#94a3b8;font-family:'JetBrains Mono',monospace;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:200px}
.lh-link-actions{display:flex;gap:4px;flex-shrink:0}
.lh-add-form{background:#f8fafc;border:1.5px dashed #e2e8f0;border-radius:10px;padding:14px;margin-top:10px}
.lh-grid-2{display:grid;grid-template-columns:1fr 1fr;gap:8px}
.lh-preview{border:1px solid #e2e8f0;border-radius:14px;overflow:hidden;background:#f8fafc;margin-bottom:16px}
.lh-preview-hd{padding:10px 14px;font-size:11px;font-weight:700;color:#94a3b8;text-transform:uppercase;letter-spacing:.07em;border-bottom:1px solid #e2e8f0}
.lh-preview-frame{padding:20px;display:flex;flex-direction:column;align-items:center;gap:8px;min-height:200px}
.lh-preview-avatar{width:64px;height:64px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:28px;margin-bottom:4px;color:#fff;font-weight:700}
.lh-preview-name{font-size:15px;font-weight:700;color:#0f172a;text-align:center}
.lh-preview-bio{font-size:12px;color:#64748b;text-align:center;line-height:1.5;max-width:240px}
.lh-preview-link{width:100%;max-width:280px;padding:10px 14px;border-radius:8px;font-size:13px;font-weight:600;text-align:center;border:none;cursor:default;display:flex;align-items:center;justify-content:center;gap:8px}
.lh-stats-grid{display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-bottom:14px}
.lh-stat-card{background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:12px 14px;text-align:center}
.lh-stat-val{font-family:'JetBrains Mono',monospace;font-size:22px;font-weight:700;color:#0f172a;line-height:1}
.lh-stat-lbl{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.07em;color:#94a3b8;margin-top:3px}
.lh-platform-row{display:flex;align-items:center;justify-content:space-between;padding:6px 0;border-bottom:1px solid #f8fafc;font-size:12px}
.lh-platform-bar{height:6px;background:#e2e8f0;border-radius:999px;overflow:hidden;margin-left:10px;flex:1;max-width:80px}
.lh-platform-fill{height:100%;border-radius:999px;background:#6366f1;transition:width .4s}
.lh-empty{padding:28px;text-align:center;color:#94a3b8;font-size:13px}
.lh-hint{font-size:11px;color:#94a3b8;line-height:1.5;margin-top:3px}
.lh-color-row{display:flex;align-items:center;gap:10px}
.lh-color-input{width:40px;height:32px;border:1.5px solid #e2e8f0;border-radius:6px;padding:2px;cursor:pointer}
.lh-badge-live{display:inline-flex;align-items:center;gap:5px;background:rgba(16,185,129,.12);border:1px solid rgba(16,185,129,.25);border-radius:999px;padding:3px 10px;font-size:10px;font-weight:700;color:#10b981;margin-left:10px}
.lh-badge-draft{display:inline-flex;align-items:center;gap:5px;background:rgba(100,116,139,.1);border:1px solid rgba(100,116,139,.2);border-radius:999px;padding:3px 10px;font-size:10px;font-weight:700;color:#64748b;margin-left:10px}
.lh-slug-preview{font-size:11px;color:#6366f1;font-family:'JetBrains Mono',monospace;margin-top:3px}
  `;
  document.head.appendChild(s);
};

const debounce = (fn, ms) => { let t; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); }; };

const slugify = s => (s || '').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '').slice(0, 50);

export function renderLinkHubView(container, { apiClient, toast } = {}) {
  injectStyles();
  const client   = apiClient || createApiClient();
  const notifier = toast     || createToastManager();

  const state = {
    profile: null, links: [], saving: false,
    addingLink: false, editingLinkId: null, analytics: null,
  };

  const root = el('div', { class: 'lh-root' });
  container.innerHTML = '';
  container.appendChild(root);

  // ── Hero ──────────────────────────────────────────────────────────────────
  const hero    = el('div', { class: 'lh-hero' });
  const heroRow = el('div', { class: 'lh-hero-row' });
  const heroLeft = el('div');
  heroLeft.appendChild(el('div', { class: 'lh-hero-title' }, '🔗 Link Hub'));
  heroLeft.appendChild(el('div', { class: 'lh-hero-sub' }, 'Your personal link page — one URL for all your links, social profiles, and contact info.'));
  const heroUrlWrap = el('div');
  heroLeft.appendChild(heroUrlWrap);
  heroRow.appendChild(heroLeft);
  const heroRight = el('div', { style: 'display:flex;align-items:center;gap:8px;flex-shrink:0' });
  const openBtn   = el('a', { class: 'lh-btn lh-btn-outline', style: 'color:#6366f1;background:rgba(255,255,255,.07);border-color:rgba(99,102,241,.4);font-size:12px', target: '_blank' }, '↗ Open page');
  openBtn.style.display = 'none';
  heroRight.appendChild(openBtn);
  heroRow.appendChild(heroRight);
  hero.appendChild(heroRow);
  root.appendChild(hero);

  const updateHeroUrl = () => {
    heroUrlWrap.replaceChildren();
    if (!state.profile?.slug) return;
    const badge = state.profile.isPublished
      ? el('span', { class: 'lh-badge-live' }, '● Live')
      : el('span', { class: 'lh-badge-draft' }, '○ Draft');
    const urlBox = el('div', { class: 'lh-hero-url', style: 'margin-top:10px' });
    const link   = el('a', { href: `https://hven.io/hub/${state.profile.slug}`, target: '_blank' },
      `hven.io/hub/${state.profile.slug}`);
    urlBox.appendChild(link);
    urlBox.appendChild(badge);
    heroUrlWrap.appendChild(urlBox);
    openBtn.href         = `https://hven.io/hub/${state.profile.slug}`;
    openBtn.style.display = state.profile.isPublished ? '' : 'none';
  };

  // ── Two-column layout ─────────────────────────────────────────────────────
  const layout   = el('div', { class: 'lh-layout' });
  const leftCol  = el('div');
  const rightCol = el('div');
  layout.append(leftCol, rightCol);
  root.appendChild(layout);

  // ── Profile panel ─────────────────────────────────────────────────────────
  const profilePanel    = el('div', { class: 'lh-panel' });
  const profilePanelHd  = el('div', { class: 'lh-panel-hd' });
  profilePanelHd.appendChild(el('div', { class: 'lh-panel-title' }, 'Profile'));
  const saveBtn = el('button', { class: 'lh-btn lh-btn-primary lh-btn-sm' }, '💾 Save');
  profilePanelHd.appendChild(saveBtn);
  profilePanel.appendChild(profilePanelHd);

  const profileBody = el('div', { class: 'lh-panel-body' });

  const displayNameInput = el('input', { class: 'lh-input', placeholder: 'Your name or brand' });
  const slugInput        = el('input', { class: 'lh-input', placeholder: 'e.g. yourname' });
  const slugPreview      = el('div',   { class: 'lh-slug-preview' });
  const bioInput         = el('textarea', { class: 'lh-input', placeholder: 'Short bio or tagline', rows: '2', style: 'resize:vertical' });
  const emojiInput       = el('input', { class: 'lh-input', placeholder: '👤', style: 'width:80px;display:inline-block' });
  const brandColorInput  = el('input', { class: 'lh-color-input', type: 'color', value: '#6366f1' });
  const themeSelect      = el('select', { class: 'lh-select', style: 'width:auto' },
    el('option', { value: 'light' }, 'Light'),
    el('option', { value: 'dark' },  'Dark'),
  );
  const publishedCb  = el('input', { type: 'checkbox' });
  const engageCb     = el('input', { type: 'checkbox' });
  const widgetKeyInput = el('input', { class: 'lh-input', placeholder: 'Paste your Widget Key from Engage → Widget tab' });
  const siteKeyInput   = el('input', { class: 'lh-input', placeholder: 'Paste your Site Key from Sites' });
  const widgetWrap     = el('div', { style: 'display:none' });

  // Auto-slug from display name
  displayNameInput.addEventListener('input', () => {
    if (!state.profile?.slug || slugInput.value === slugify(state.profile?.displayName || '')) {
      slugInput.value = slugify(displayNameInput.value);
    }
    updateSlugPreview();
    debouncedPreview();
  });

  slugInput.addEventListener('input', () => { updateSlugPreview(); debouncedPreview(); });
  bioInput.addEventListener('input', debouncedPreview);
  emojiInput.addEventListener('input', debouncedPreview);
  brandColorInput.addEventListener('input', debouncedPreview);
  themeSelect.addEventListener('change', debouncedPreview);
  engageCb.addEventListener('change', () => { widgetWrap.style.display = engageCb.checked ? '' : 'none'; });

  const updateSlugPreview = () => {
    slugPreview.textContent = slugInput.value ? `hven.io/hub/${slugInput.value}` : '';
  };

  const mkField = (labelText, ...inputs) => {
    const f = el('div', { class: 'lh-field' });
    f.appendChild(el('label', {}, labelText));
    inputs.forEach(i => f.appendChild(i));
    return f;
  };
  const mkToggleRow = (label, sub, cb) => {
    const row = el('div', { class: 'lh-toggle-row' });
    const info = el('div');
    info.appendChild(el('div', { class: 'lh-toggle-label' }, label));
    if (sub) info.appendChild(el('div', { class: 'lh-toggle-sub' }, sub));
    const tog = el('label', { class: 'lh-toggle' });
    tog.append(cb, el('span', { class: 'lh-toggle-slider' }));
    row.append(info, tog);
    return row;
  };

  const slugFieldWrap = el('div', { class: 'lh-field' });
  slugFieldWrap.appendChild(el('label', {}, 'Public URL slug'));
  slugFieldWrap.appendChild(slugInput);
  slugFieldWrap.appendChild(slugPreview);

  const emojiColorRow = el('div', { class: 'lh-color-row' });
  emojiColorRow.append(
    el('div', { style: 'flex:1' }, emojiInput),
    el('div', { style: 'display:flex;align-items:center;gap:6px;font-size:12px;color:#64748b' }, 'Brand colour', brandColorInput),
    el('div', { style: 'display:flex;align-items:center;gap:6px;font-size:12px;color:#64748b' }, 'Theme', themeSelect),
  );

  widgetWrap.append(
    mkField('Widget Key', widgetKeyInput),
    mkField('Site Key', siteKeyInput),
  );

  profileBody.append(
    mkField('Display Name', displayNameInput),
    slugFieldWrap,
    mkField('Bio', bioInput),
    mkField('Avatar Emoji + Brand Colour', emojiColorRow),
    mkToggleRow('Published', 'Make your Link Hub public at hven.io/hub/[slug]', publishedCb),
    mkToggleRow('Embed AI Chat Bot', 'Your Hven AI bot will appear on your public Link Hub page', engageCb),
    widgetWrap,
  );
  profilePanel.appendChild(profileBody);
  leftCol.appendChild(profilePanel);

  const populateProfileForm = () => {
    const p = state.profile;
    if (!p) return;
    displayNameInput.value  = p.displayName || '';
    slugInput.value         = p.slug || '';
    bioInput.value          = p.bio || '';
    emojiInput.value        = p.avatarEmoji || '👤';
    brandColorInput.value   = p.brandColor || '#6366f1';
    themeSelect.value       = p.theme || 'light';
    publishedCb.checked     = !!p.isPublished;
    engageCb.checked        = !!p.engageBotEnabled;
    widgetKeyInput.value    = p.widgetKey || '';
    siteKeyInput.value      = p.siteKey || '';
    widgetWrap.style.display = p.engageBotEnabled ? '' : 'none';
    updateSlugPreview();
    updateHeroUrl();
    renderPreview();
  };

  saveBtn.addEventListener('click', async () => {
    if (state.saving) return;
    state.saving = true; saveBtn.disabled = true; saveBtn.textContent = '⏳…';
    try {
      const payload = {
        slug:            slugInput.value.trim(),
        displayName:     displayNameInput.value.trim(),
        bio:             bioInput.value.trim() || null,
        avatarEmoji:     emojiInput.value.trim() || '👤',
        avatarInitials:  null,
        brandColor:      brandColorInput.value,
        theme:           themeSelect.value,
        isPublished:     publishedCb.checked,
        engageBotEnabled: engageCb.checked,
        widgetKey:       widgetKeyInput.value.trim() || null,
        siteKey:         siteKeyInput.value.trim() || null,
        links:           state.links.map((l, i) => ({ ...l, order: i })),
      };
      state.profile = await client.linkHub.saveProfile(payload);
      state.links   = state.profile.links || [];
      populateProfileForm();
      renderLinkList();
      notifier.show({ message: 'Profile saved.', variant: 'success' });
    } catch (err) {
      notifier.show({ message: mapApiError(err).message, variant: 'danger' });
    } finally {
      state.saving = false; saveBtn.disabled = false; saveBtn.textContent = '💾 Save';
    }
  });

  // ── Links panel ───────────────────────────────────────────────────────────
  const linksPanel   = el('div', { class: 'lh-panel' });
  const linksPanelHd = el('div', { class: 'lh-panel-hd' });
  linksPanelHd.appendChild(el('div', { class: 'lh-panel-title' }, 'Links'));
  const addLinkBtn = el('button', { class: 'lh-btn lh-btn-outline lh-btn-sm' }, '＋ Add link');
  linksPanelHd.appendChild(addLinkBtn);
  linksPanel.appendChild(linksPanelHd);
  const linkListWrap = el('div', { class: 'lh-panel-body', style: 'padding-bottom:8px' });
  linksPanel.appendChild(linkListWrap);
  leftCol.appendChild(linksPanel);

  const renderLinkList = () => {
    linkListWrap.replaceChildren();
    if (!state.links.length && !state.addingLink) {
      linkListWrap.appendChild(el('div', { class: 'lh-empty' }, 'No links yet — click ＋ Add link to get started.'));
    }
    state.links.forEach((link, i) => {
      const icon = PLATFORM_ICONS[link.platform || ''] || link.iconEmoji || '🔗';
      const card = el('div', { class: 'lh-link-card' });
      card.append(
        el('span', { class: 'lh-link-icon' }, icon),
        el('div', { class: 'lh-link-info' },
          el('div', { class: 'lh-link-label' }, link.label || 'Untitled'),
          el('div', { class: 'lh-link-url' }, link.url || ''),
        ),
        el('div', { class: 'lh-link-actions' },
          (() => {
            const b = el('button', { class: 'lh-btn lh-btn-outline lh-btn-sm' }, '✕');
            b.addEventListener('click', () => { state.links.splice(i, 1); renderLinkList(); debouncedPreview(); });
            return b;
          })(),
        ),
      );
      linkListWrap.appendChild(card);
    });

    if (state.addingLink) {
      const form = el('div', { class: 'lh-add-form' });

      const platSelect = el('select', { class: 'lh-select', style: 'margin-bottom:8px' },
        el('option', { value: '' }, '— Platform —'),
        ...Object.keys(PLATFORM_ICONS).map(p =>
          el('option', { value: p }, p.charAt(0).toUpperCase() + p.slice(1))
        ),
      );
      const labelInput = el('input', { class: 'lh-input', placeholder: 'Link label', style: 'margin-bottom:8px' });
      const urlInput   = el('input', { class: 'lh-input', placeholder: 'https://', style: 'margin-bottom:8px' });

      platSelect.addEventListener('change', () => {
        const p = platSelect.value;
        if (p && !labelInput.value) labelInput.value = p.charAt(0).toUpperCase() + p.slice(1);
        if (p) urlInput.placeholder = PLATFORM_PLACEHOLDERS[p] || 'https://';
      });

      const actRow  = el('div', { style: 'display:flex;gap:8px' });
      const addBtn2 = el('button', { class: 'lh-btn lh-btn-primary lh-btn-sm' }, 'Add');
      const cancelBtn = el('button', { class: 'lh-btn lh-btn-outline lh-btn-sm' }, 'Cancel');
      cancelBtn.addEventListener('click', () => { state.addingLink = false; renderLinkList(); });
      addBtn2.addEventListener('click', () => {
        if (!labelInput.value.trim() || !urlInput.value.trim()) {
          notifier.show({ message: 'Label and URL are required.', variant: 'warning' });
          return;
        }
        const p = platSelect.value;
        state.links.push({
          id:        crypto.randomUUID().replace(/-/g, ''),
          label:     labelInput.value.trim(),
          url:       urlInput.value.trim(),
          platform:  p || null,
          iconEmoji: PLATFORM_ICONS[p] || '🔗',
          order:     state.links.length,
          isActive:  true,
          clickCount: 0,
        });
        state.addingLink = false;
        renderLinkList();
        debouncedPreview();
      });
      actRow.append(addBtn2, cancelBtn);
      form.append(platSelect, labelInput, urlInput, actRow);
      linkListWrap.appendChild(form);
    }
  };

  addLinkBtn.addEventListener('click', () => { state.addingLink = true; renderLinkList(); });

  // ── Engage bot panel ──────────────────────────────────────────────────────
  // (handled inline in profile panel above)

  // ── RIGHT: Live preview ───────────────────────────────────────────────────
  const previewPanel = el('div', { class: 'lh-preview' });
  const previewHd    = el('div', { class: 'lh-preview-hd' });
  previewHd.innerHTML = 'Live Preview <span style="font-size:10px;font-weight:400;color:#b0bec5;float:right">updates as you type</span>';
  const previewFrame = el('div', { class: 'lh-preview-frame' });
  previewPanel.append(previewHd, previewFrame);
  rightCol.appendChild(previewPanel);

  const renderPreview = () => {
    previewFrame.replaceChildren();
    const name  = displayNameInput.value || 'Your Name';
    const bio   = bioInput.value;
    const emoji = emojiInput.value || '👤';
    const color = brandColorInput.value || '#6366f1';
    const isDark = themeSelect.value === 'dark';
    const textCol = isDark ? '#f1f5f9' : '#0f172a';
    const bioCol  = isDark ? '#94a3b8' : '#64748b';

    const avatar = el('div', { class: 'lh-preview-avatar', style: `background:${color}` }, emoji);
    const nameEl = el('div', { class: 'lh-preview-name', style: `color:${textCol}` }, name);
    previewFrame.append(avatar, nameEl);
    if (bio) previewFrame.appendChild(el('div', { class: 'lh-preview-bio', style: `color:${bioCol}` }, bio));

    const activeLinks = state.links.filter(l => l.isActive !== false).slice(0, 5);
    activeLinks.forEach(link => {
      const icon = PLATFORM_ICONS[link.platform || ''] || link.iconEmoji || '🔗';
      const btn  = el('div', { class: 'lh-preview-link', style: `background:${color};color:#fff` },
        el('span', {}, icon),
        el('span', {}, link.label || 'Link'),
      );
      previewFrame.appendChild(btn);
    });
    if (!activeLinks.length && !name) {
      previewFrame.appendChild(el('div', { style: 'color:#94a3b8;font-size:12px;text-align:center' }, 'Fill in your profile to see a preview'));
    }
  };
  const debouncedPreview = debounce(renderPreview, 300);

  // ── RIGHT: Analytics panel ────────────────────────────────────────────────
  const analyticsPanel = el('div', { class: 'lh-panel' });
  const analyticsHd    = el('div', { class: 'lh-panel-hd' });
  analyticsHd.appendChild(el('div', { class: 'lh-panel-title' }, '📊 Analytics'));
  const refreshBtn = el('button', { class: 'lh-btn lh-btn-outline lh-btn-sm' }, '↻');
  analyticsHd.appendChild(refreshBtn);
  analyticsPanel.appendChild(analyticsHd);
  const analyticsBody = el('div', { class: 'lh-panel-body' });
  analyticsPanel.appendChild(analyticsBody);
  rightCol.appendChild(analyticsPanel);

  const renderAnalytics = () => {
    analyticsBody.replaceChildren();
    const a = state.analytics;
    if (!a) {
      analyticsBody.appendChild(el('div', { class: 'lh-empty', style: 'padding:20px' }, 'Share your Link Hub to start seeing analytics.'));
      return;
    }
    const clickRate = a.totalViews > 0 ? ((a.totalClicks / a.totalViews) * 100).toFixed(1) + '%' : '—';
    const topRef    = a.referrerBreakdown?.[0]?.platform || '—';
    const statsGrid = el('div', { class: 'lh-stats-grid' });
    [['Total Views', a.totalViews], ['Link Clicks', a.totalClicks], ['Click Rate', clickRate], ['Top Referrer', topRef]]
      .forEach(([lbl, val]) => {
        statsGrid.appendChild(el('div', { class: 'lh-stat-card' },
          el('div', { class: 'lh-stat-val' }, String(val)),
          el('div', { class: 'lh-stat-lbl' }, lbl),
        ));
      });
    analyticsBody.appendChild(statsGrid);

    if (a.referrerBreakdown?.length) {
      analyticsBody.appendChild(el('div', { style: 'font-size:11px;font-weight:700;color:#94a3b8;text-transform:uppercase;letter-spacing:.07em;margin-bottom:6px' }, 'Referrers'));
      a.referrerBreakdown.slice(0, 5).forEach(r => {
        const row = el('div', { class: 'lh-platform-row' },
          el('span', {}, r.platform),
          el('span', { style: 'color:#64748b;font-size:11px' }, String(r.count)),
          el('div', { class: 'lh-platform-bar' },
            el('div', { class: 'lh-platform-fill', style: `width:${r.pct}%` })
          ),
        );
        analyticsBody.appendChild(row);
      });
    }

    if (a.topLinks?.length) {
      analyticsBody.appendChild(el('div', { style: 'font-size:11px;font-weight:700;color:#94a3b8;text-transform:uppercase;letter-spacing:.07em;margin:10px 0 6px' }, 'Top Links'));
      a.topLinks.slice(0, 5).forEach(l => {
        analyticsBody.appendChild(el('div', { class: 'lh-platform-row' },
          el('span', {}, l.label || l.linkId),
          el('span', { style: 'color:#6366f1;font-weight:700;font-size:11px' }, String(l.count) + ' clicks'),
        ));
      });
    }
  };

  const loadAnalytics = async () => {
    try {
      state.analytics = await client.linkHub.getAnalytics(30);
      renderAnalytics();
    } catch {
      // analytics failure is non-critical; leave empty state
    }
  };

  refreshBtn.addEventListener('click', loadAnalytics);

  // ── Init ──────────────────────────────────────────────────────────────────
  const init = async () => {
    try {
      state.profile = await client.linkHub.getProfile();
      state.links   = [...(state.profile.links || [])];
      populateProfileForm();
      renderLinkList();
      renderPreview();
      await loadAnalytics();
    } catch (err) {
      notifier.show({ message: mapApiError(err).message, variant: 'danger' });
    }
  };

  init();
}

// Export under old name too for backward compat
export const renderLinkTreeView = renderLinkHubView;
