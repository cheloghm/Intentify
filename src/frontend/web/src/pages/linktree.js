/**
 * linktree.js — Hven Link Hub
 * Three-panel layout: Editor tab | Analytics tab (left) + Sticky Preview (right).
 * Profile image upload, display modes, background customisation, live preview.
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
const REFERRER_EMOJI = {
  facebook: '👥', instagram: '📸', tiktok: '🎵', x: '🐦',
  linkedin: '💼', youtube: '▶️', google: '🔍', snapchat: '👻',
  pinterest: '📌', whatsapp: '💬', direct: '🏠', other: '🌐',
};

// ── DOM helpers ───────────────────────────────────────────────────────────────
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

function mkField(labelText, ...inputs) {
  const f = el('div', { class: 'lh-field' });
  f.appendChild(el('label', {}, labelText));
  inputs.forEach(i => f.appendChild(i));
  return f;
}

function mkToggleRow(label, sub, cb) {
  const row  = el('div', { class: 'lh-toggle-row' });
  const info = el('div');
  info.appendChild(el('div', { class: 'lh-toggle-label' }, label));
  if (sub) info.appendChild(el('div', { class: 'lh-toggle-sub' }, sub));
  const tog = el('label', { class: 'lh-toggle' });
  tog.append(cb, el('span', { class: 'lh-toggle-slider' }));
  row.append(info, tog);
  return row;
}

// ── Styles ────────────────────────────────────────────────────────────────────
const injectStyles = () => {
  if (document.getElementById('_lh_css')) return;
  const s = document.createElement('style');
  s.id = '_lh_css';
  s.textContent = `
@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&family=JetBrains+Mono:wght@400;500;700&display=swap');
.lh-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;gap:20px;width:100%;max-width:960px}
.lh-hero{background:linear-gradient(135deg,#0f172a 0%,#1e293b 100%);border-radius:16px;padding:28px 32px;color:#fff}
.lh-hero-title{font-size:22px;font-weight:800;letter-spacing:-.02em;margin-bottom:4px}
.lh-hero-sub{font-size:13px;color:#94a3b8;line-height:1.5;margin-bottom:16px}
.lh-hero-url{display:inline-flex;align-items:center;gap:8px;background:rgba(255,255,255,0.07);border:1px solid rgba(255,255,255,0.12);border-radius:8px;padding:6px 14px;font-size:12px;font-family:'JetBrains Mono',monospace;color:#a5b4fc}
.lh-hero-url a{color:#a5b4fc;text-decoration:none}.lh-hero-url a:hover{color:#c7d2fe}
.lh-hero-row{display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:10px}
.lh-page-wrap{display:grid;grid-template-columns:1fr 320px;gap:20px;align-items:start}
@media(max-width:860px){.lh-page-wrap{grid-template-columns:1fr}}
.lh-sticky-col{position:sticky;top:20px}
.lh-tab-bar{display:flex;background:#fff;border:1px solid #e2e8f0;border-radius:12px 12px 0 0;border-bottom:none;overflow:hidden;margin-bottom:0}
.lh-tab-btn{flex:1;padding:12px 16px;font-size:13px;font-weight:600;color:#64748b;background:none;border:none;border-bottom:2px solid transparent;cursor:pointer;font-family:inherit;transition:all .14s}
.lh-tab-btn.active{color:#6366f1;background:#fafbff;border-bottom-color:#6366f1}
.lh-tab-btn:hover:not(.active){background:#f8fafc;color:#475569}
.lh-tab-panel{display:none}.lh-tab-panel.active{display:block}
.lh-panel{background:#fff;border:1px solid #e2e8f0;border-radius:0 0 0 0;overflow:hidden;margin-bottom:0;border-top:none}
.lh-panel+.lh-panel{border-top:1px solid #f1f5f9}
.lh-panel:last-child{border-radius:0 0 12px 12px}
.lh-tab-panel.active > .lh-panel:first-child{border-radius:0}
.lh-panel-hd{display:flex;align-items:center;justify-content:space-between;padding:14px 18px;border-bottom:1px solid #f1f5f9;background:#fff}
.lh-panel-title{font-size:13px;font-weight:700;color:#0f172a}
.lh-panel-body{padding:18px}
.lh-field{display:flex;flex-direction:column;gap:4px;margin-bottom:12px}
.lh-field label{font-size:11px;font-weight:600;color:#64748b;text-transform:uppercase;letter-spacing:.04em}
.lh-input{padding:8px 12px;border:1.5px solid #e2e8f0;border-radius:8px;font-size:13px;font-family:inherit;outline:none;transition:border-color .14s;width:100%;box-sizing:border-box}
.lh-input:focus{border-color:#6366f1}
.lh-input.error{border-color:#ef4444}
.lh-select{padding:8px 12px;border:1.5px solid #e2e8f0;border-radius:8px;font-size:13px;font-family:inherit;outline:none;background:#fff;width:100%;cursor:pointer;box-sizing:border-box}
.lh-select:focus{border-color:#6366f1}
.lh-btn{display:inline-flex;align-items:center;gap:6px;padding:8px 16px;border-radius:8px;border:none;font-size:13px;font-weight:600;cursor:pointer;font-family:inherit;transition:all .14s}
.lh-btn-primary{background:#6366f1;color:#fff}.lh-btn-primary:hover:not(:disabled){background:#4f46e5}
.lh-btn-primary:disabled{opacity:.5;cursor:not-allowed}
.lh-btn-outline{background:#fff;color:#475569;border:1.5px solid #e2e8f0}.lh-btn-outline:hover{background:#f8fafc}
.lh-btn-danger{background:#fff;color:#ef4444;border:1.5px solid #fecaca}.lh-btn-danger:hover{background:#fef2f2}
.lh-btn-sm{padding:4px 10px;font-size:11.5px}
.lh-btn-link{background:none;border:none;color:#6366f1;font-size:11px;cursor:pointer;padding:0;font-family:inherit;text-decoration:underline}
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
.lh-add-form{background:#f8fafc;border:1.5px dashed #e2e8f0;border-radius:10px;padding:14px;margin-top:10px}
.lh-preview{border:1px solid #e2e8f0;border-radius:14px;overflow:hidden;background:#f8fafc}
.lh-preview-hd{padding:10px 14px;font-size:11px;font-weight:700;color:#94a3b8;text-transform:uppercase;letter-spacing:.07em;border-bottom:1px solid #e2e8f0;display:flex;align-items:center;justify-content:space-between;background:#fff}
.lh-preview-frame{padding:20px;display:flex;flex-direction:column;align-items:center;gap:8px;min-height:220px;transition:background .3s}
.lh-preview-avatar{width:64px;height:64px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:28px;margin-bottom:4px;font-weight:700;overflow:hidden;flex-shrink:0}
.lh-preview-avatar img{width:100%;height:100%;object-fit:cover}
.lh-preview-name{font-size:15px;font-weight:700;text-align:center}
.lh-preview-bio{font-size:12px;text-align:center;line-height:1.5;max-width:240px}
.lh-preview-link{max-width:280px;padding:10px 14px;border-radius:8px;font-size:13px;font-weight:600;text-align:center;border:none;cursor:default;display:flex;align-items:center;justify-content:center;gap:8px;color:#fff}
.lh-preview-link.icon-only{width:44px;height:44px;border-radius:50%;padding:0;font-size:18px}
.lh-preview-link.full-width{width:100%}
.lh-preview-note{font-size:10px;color:#94a3b8;text-align:center;padding:8px 14px;border-top:1px solid #f1f5f9;background:#fff}
.lh-stats-grid{display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-bottom:14px}
.lh-stat-card{background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:12px 14px;text-align:center}
.lh-stat-val{font-family:'JetBrains Mono',monospace;font-size:22px;font-weight:700;color:#0f172a;line-height:1}
.lh-stat-lbl{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.07em;color:#94a3b8;margin-top:3px}
.lh-platform-row{display:flex;align-items:center;justify-content:space-between;padding:6px 0;border-bottom:1px solid #f8fafc;font-size:12px}
.lh-platform-bar{height:6px;background:#e2e8f0;border-radius:999px;overflow:hidden;margin-left:10px;flex:1;max-width:80px}
.lh-platform-fill{height:100%;border-radius:999px;background:#6366f1;transition:width .4s}
.lh-empty{padding:28px;text-align:center;color:#94a3b8;font-size:13px}
.lh-color-row{display:flex;align-items:center;gap:10px}
.lh-color-input{width:40px;height:32px;border:1.5px solid #e2e8f0;border-radius:6px;padding:2px;cursor:pointer}
.lh-badge-live{display:inline-flex;align-items:center;gap:5px;background:rgba(16,185,129,.12);border:1px solid rgba(16,185,129,.25);border-radius:999px;padding:3px 10px;font-size:10px;font-weight:700;color:#10b981;margin-left:10px}
.lh-badge-draft{display:inline-flex;align-items:center;gap:5px;background:rgba(100,116,139,.1);border:1px solid rgba(100,116,139,.2);border-radius:999px;padding:3px 10px;font-size:10px;font-weight:700;color:#64748b;margin-left:10px}
.lh-slug-preview{font-size:11px;color:#6366f1;font-family:'JetBrains Mono',monospace;margin-top:3px}
.lh-slug-error{font-size:11px;color:#ef4444;margin-top:3px;display:none}
.lh-section-sep{border:none;border-top:1px solid #f1f5f9;margin:14px 0}
.lh-char-count{font-size:10px;text-align:right;margin-top:2px;color:#94a3b8}
.lh-char-count.warn{color:#ef4444}
.lh-bg-preview{width:100%;height:44px;border-radius:8px;border:1.5px solid #e2e8f0;margin-top:6px;transition:all .3s}
.lh-bg-type-row{display:flex;gap:4px;margin-bottom:8px}
.lh-bg-type-btn{flex:1;padding:5px 0;border:1.5px solid #e2e8f0;border-radius:6px;font-size:11px;font-weight:600;color:#64748b;background:#fff;cursor:pointer;text-align:center;transition:all .14s}
.lh-bg-type-btn.active{background:#eef2ff;border-color:#6366f1;color:#4f46e5}
.lh-section-label{font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:#94a3b8;margin-bottom:10px}
.lh-dm-btn{flex:1;padding:4px 6px;border:1.5px solid #e2e8f0;border-radius:6px;font-size:10px;font-weight:600;color:#64748b;background:#fff;cursor:pointer;text-align:center;transition:all .14s}
.lh-dm-btn.active{background:#eef2ff;border-color:#6366f1;color:#4f46e5}
.lh-display-mode-row{display:flex;gap:4px;margin-top:5px}
.lh-favicon{width:18px;height:18px;border-radius:3px;flex-shrink:0}
.lh-avatar-upload-wrap{display:flex;align-items:flex-start;gap:14px;margin-bottom:12px}
.lh-avatar-upload-circle{width:72px;height:72px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:28px;cursor:pointer;overflow:hidden;border:2px dashed #c7d2fe;transition:border-color .14s,box-shadow .14s;flex-shrink:0;background:#f8fafc}
.lh-avatar-upload-circle:hover{border-color:#6366f1;box-shadow:0 0 0 4px rgba(99,102,241,0.1)}
.lh-avatar-upload-circle img{width:100%;height:100%;object-fit:cover;border-radius:50%}
.lh-avatar-upload-meta{display:flex;flex-direction:column;justify-content:center;gap:4px}
.lh-avatar-upload-hint{font-size:11px;color:#94a3b8;line-height:1.5}
.lh-avatar-upload-remove{font-size:11px;color:#ef4444;background:none;border:none;cursor:pointer;padding:0;font-family:inherit;text-align:left;display:none}
  `;
  document.head.appendChild(s);
};

// ── Utilities ─────────────────────────────────────────────────────────────────
const debounce = (fn, ms) => { let t; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); }; };

const toSlug = (name) => (name || '').toLowerCase()
  .replace(/[àáâãäå]/g, 'a').replace(/[èéêë]/g, 'e')
  .replace(/[ìíîï]/g, 'i').replace(/[òóôõö]/g, 'o').replace(/[ùúûü]/g, 'u')
  .replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 50);

const SLUG_RE = /^[a-z0-9][a-z0-9\-]{1,48}[a-z0-9]$/;

const getFaviconUrl = (platform, url) => {
  if ((platform === 'website' || platform === 'custom') && url) {
    try {
      const u = new URL(url);
      if (u.protocol === 'https:' || u.protocol === 'http:')
        return `https://www.google.com/s2/favicons?sz=32&domain=${u.hostname}`;
    } catch { /* invalid URL */ }
  }
  return null;
};

const mkLinkIcon = (link, size = 18) => {
  const faviconUrl = getFaviconUrl(link.platform, link.url);
  if (faviconUrl) {
    const img = el('img', { class: 'lh-favicon', src: faviconUrl, width: String(size), height: String(size) });
    img.onerror = () => { img.style.display = 'none'; };
    return img;
  }
  return el('span', { class: 'lh-link-icon', style: `font-size:${size}px` },
    PLATFORM_ICONS[link.platform || ''] || link.iconEmoji || '🔗');
};

// ── Main view ─────────────────────────────────────────────────────────────────
export function renderLinkHubView(container, options = {}) {
  injectStyles();
  const client   = options.apiClient || createApiClient();
  const notifier = options.toast     || createToastManager();

  if (!client || !client.linkHub) {
    container.innerHTML = '<div style="padding:40px;text-align:center;color:#ef4444">Could not connect to the API. Please refresh.</div>';
    return;
  }

  let slugManuallyEdited = false;
  let analyticsLoaded    = false;

  const state = {
    profile: null, links: [], saving: false, addingLink: false, analytics: null,
    displayName: '', slug: '', bio: '', avatarEmoji: '👤',
    brandColor: '#6366f1', theme: 'light', isPublished: false,
    profilePictureUrl: '', backgroundType: 'color', backgroundValue: '#ffffff',
    engageBotEnabled: false, widgetKey: '', siteKey: '',
  };

  const root = el('div', { class: 'lh-root' });
  container.innerHTML = '';
  container.appendChild(root);

  // ── Hero ──────────────────────────────────────────────────────────────────
  const hero     = el('div', { class: 'lh-hero' });
  const heroRow  = el('div', { class: 'lh-hero-row' });
  const heroLeft = el('div');
  heroLeft.appendChild(el('div', { class: 'lh-hero-title' }, '🔗 Link Hub'));
  heroLeft.appendChild(el('div', { class: 'lh-hero-sub' }, 'Your personal link page — one URL for all your links, social profiles, and contact info.'));
  const heroUrlWrap = el('div');
  heroLeft.appendChild(heroUrlWrap);
  heroRow.appendChild(heroLeft);
  const heroOpenBtn = el('a', {
    class: 'lh-btn lh-btn-outline',
    style: 'color:#6366f1;background:rgba(255,255,255,.07);border-color:rgba(99,102,241,.4);font-size:12px',
    target: '_blank',
  }, '↗ Open page');
  heroOpenBtn.style.display = 'none';
  heroRow.appendChild(el('div', { style: 'display:flex;align-items:center;gap:8px;flex-shrink:0' }, heroOpenBtn));
  hero.appendChild(heroRow);
  root.appendChild(hero);

  const updateHeroUrl = () => {
    heroUrlWrap.replaceChildren();
    if (!state.slug) return;
    const badge = state.isPublished
      ? el('span', { class: 'lh-badge-live' }, '● Live')
      : el('span', { class: 'lh-badge-draft' }, '○ Draft');
    const urlBox = el('div', { class: 'lh-hero-url', style: 'margin-top:10px' });
    urlBox.appendChild(el('a', { href: `https://hven.io/hub/${state.slug}`, target: '_blank' }, `hven.io/hub/${state.slug}`));
    urlBox.appendChild(badge);
    heroUrlWrap.appendChild(urlBox);
    heroOpenBtn.href          = `https://hven.io/hub/${state.slug}`;
    heroOpenBtn.style.display = state.isPublished ? '' : 'none';
    // also update preview note slug
    if (previewNote) previewNote.textContent = state.slug
      ? `What visitors see at hven.io/hub/${state.slug}`
      : 'Fill in your profile to preview';
  };

  // ── Page wrap ─────────────────────────────────────────────────────────────
  const pageWrap = el('div', { class: 'lh-page-wrap' });
  const leftCol  = el('div');
  const rightCol = el('div', { class: 'lh-sticky-col' });
  pageWrap.append(leftCol, rightCol);
  root.appendChild(pageWrap);

  // ── Tab bar ───────────────────────────────────────────────────────────────
  const tabBar       = el('div', { class: 'lh-tab-bar' });
  const editorTabBtn = el('button', { class: 'lh-tab-btn active', type: 'button' }, '✏️ Editor');
  const analyticsTabBtn = el('button', { class: 'lh-tab-btn', type: 'button' }, '📊 Analytics');
  tabBar.append(editorTabBtn, analyticsTabBtn);
  leftCol.appendChild(tabBar);

  const editorTabPanel    = el('div', { class: 'lh-tab-panel active' });
  const analyticsTabPanel = el('div', { class: 'lh-tab-panel' });
  leftCol.appendChild(editorTabPanel);
  leftCol.appendChild(analyticsTabPanel);

  const switchTab = (tab) => {
    editorTabBtn.classList.toggle('active', tab === 'editor');
    analyticsTabBtn.classList.toggle('active', tab === 'analytics');
    editorTabPanel.classList.toggle('active', tab === 'editor');
    analyticsTabPanel.classList.toggle('active', tab === 'analytics');
    if (tab === 'analytics' && !analyticsLoaded) {
      analyticsLoaded = true;
      loadAnalytics();
    }
  };

  editorTabBtn.addEventListener('click', () => switchTab('editor'));
  analyticsTabBtn.addEventListener('click', () => switchTab('analytics'));

  // ═══════════════════════════════════════════════════════════════════════════
  // EDITOR TAB — PANEL 1: Profile
  // ═══════════════════════════════════════════════════════════════════════════
  const profilePanel = el('div', { class: 'lh-panel' });
  const profileHd    = el('div', { class: 'lh-panel-hd' });
  profileHd.appendChild(el('div', { class: 'lh-panel-title' }, 'Profile'));
  const saveBtn = el('button', { class: 'lh-btn lh-btn-primary lh-btn-sm' }, '💾 Save');
  profileHd.appendChild(saveBtn);
  profilePanel.appendChild(profileHd);
  const profileBody = el('div', { class: 'lh-panel-body' });

  // ── Avatar upload ─────────────────────────────────────────────────────────
  const avatarFileInput = el('input', { type: 'file', accept: 'image/*', style: 'display:none' });
  const avatarCircle    = el('div', { class: 'lh-avatar-upload-circle' }, '👤');
  const avatarHint      = el('div', { class: 'lh-avatar-upload-hint' }, 'Click to upload · Max 5MB · JPG, PNG, GIF, WebP');
  const avatarRemoveBtn = el('button', { class: 'lh-avatar-upload-remove', type: 'button' }, '× Remove photo');

  const updateAvatarCircle = () => {
    avatarCircle.replaceChildren();
    if (state.profilePictureUrl) {
      const img = el('img', { src: state.profilePictureUrl });
      img.onerror = () => {
        state.profilePictureUrl = '';
        updateAvatarCircle();
        debouncedPreview();
      };
      avatarCircle.appendChild(img);
      avatarRemoveBtn.style.display = '';
    } else {
      avatarCircle.appendChild(document.createTextNode(state.avatarEmoji || '👤'));
      avatarRemoveBtn.style.display = 'none';
    }
  };

  avatarCircle.addEventListener('click', () => avatarFileInput.click());
  avatarFileInput.addEventListener('change', async () => {
    const file = avatarFileInput.files[0];
    if (!file) return;
    if (file.size > 5 * 1024 * 1024) {
      notifier.show({ message: 'Image must be under 5MB.', variant: 'warning' }); return;
    }
    if (!file.type.startsWith('image/')) {
      notifier.show({ message: 'Please select an image file.', variant: 'warning' }); return;
    }
    avatarCircle.replaceChildren(el('div', { style: 'font-size:20px' }, '⏳'));
    try {
      const result = await client.linkHub.uploadAvatar(file);
      state.profilePictureUrl = result.url;
      updateAvatarCircle();
      debouncedPreview();
      notifier.show({ message: '✓ Profile picture updated.', variant: 'success' });
    } catch (err) {
      notifier.show({ message: mapApiError(err).message, variant: 'danger' });
      updateAvatarCircle();
    }
    avatarFileInput.value = '';
  });

  avatarRemoveBtn.addEventListener('click', () => {
    state.profilePictureUrl = '';
    updateAvatarCircle();
    debouncedPreview();
  });

  const avatarUploadWrap = el('div', { class: 'lh-avatar-upload-wrap' },
    el('div', {}, avatarFileInput, avatarCircle),
    el('div', { class: 'lh-avatar-upload-meta' }, avatarHint, avatarRemoveBtn),
  );

  // ── Identity fields ───────────────────────────────────────────────────────
  const nameInput   = el('input', { class: 'lh-input', placeholder: 'Your name or brand' });
  const slugInput   = el('input', { class: 'lh-input', placeholder: 'e.g. yourname' });
  const slugPreview = el('div', { class: 'lh-slug-preview' });
  const slugError   = el('div', { class: 'lh-slug-error' }, 'Slug must be 3–50 chars: lowercase letters, numbers and hyphens only.');
  const bioInput    = el('textarea', { class: 'lh-input', placeholder: 'Short bio or tagline (max 160 chars)', rows: '3', style: 'resize:vertical' });
  const bioCount    = el('div', { class: 'lh-char-count' }, '0 / 160');

  // ── Appearance fields ─────────────────────────────────────────────────────
  const emojiInput      = el('input', { class: 'lh-input', placeholder: '👤', style: 'width:80px;display:inline-block' });
  const brandColorInput = el('input', { class: 'lh-color-input', type: 'color', value: '#6366f1' });
  const brandHexInput   = el('input', { class: 'lh-input', style: 'width:90px', placeholder: '#6366f1', maxlength: '7' });

  const bgTypeBtns = ['Colour', 'Gradient', 'Image'].map((label, i) => {
    const modes = ['color', 'gradient', 'image'];
    const btn = el('button', { class: 'lh-bg-type-btn' + (i === 0 ? ' active' : ''), type: 'button' }, label);
    btn.addEventListener('click', () => {
      state.backgroundType = modes[i];
      bgTypeBtns.forEach((b, j) => b.classList.toggle('active', j === i));
      updateBgValueInput();
      debouncedPreview();
    });
    return btn;
  });
  const bgTypeRow     = el('div', { class: 'lh-bg-type-row' }, ...bgTypeBtns);
  const bgValueInput  = el('input', { class: 'lh-input', placeholder: '#ffffff' });
  const bgColorPicker = el('input', { class: 'lh-color-input', type: 'color', value: '#ffffff' });
  const bgPreview     = el('div', { class: 'lh-bg-preview', style: 'background:#ffffff' });

  const updateBgValueInput = () => {
    bgColorPicker.style.display = state.backgroundType === 'color' ? '' : 'none';
    bgValueInput.placeholder = state.backgroundType === 'gradient'
      ? 'linear-gradient(135deg, #667eea, #764ba2)'
      : state.backgroundType === 'image' ? 'https://example.com/background.jpg' : '#ffffff';
    if (state.backgroundType !== 'color') bgValueInput.value = state.backgroundValue;
    updateBgPreview();
  };
  const updateBgPreview = () => {
    const v = state.backgroundValue || '#ffffff';
    if (state.backgroundType === 'gradient')
      bgPreview.style.cssText = `background:${v};border-radius:8px;border:1.5px solid #e2e8f0;height:44px;margin-top:6px;transition:all .3s`;
    else if (state.backgroundType === 'image')
      bgPreview.style.cssText = `background-image:url('${v}');background-size:cover;background-position:center;border-radius:8px;border:1.5px solid #e2e8f0;height:44px;margin-top:6px;transition:all .3s`;
    else
      bgPreview.style.cssText = `background-color:${v};border-radius:8px;border:1.5px solid #e2e8f0;height:44px;margin-top:6px;transition:all .3s`;
  };

  const themeLight = el('button', { class: 'lh-bg-type-btn active', type: 'button' }, 'Light');
  const themeDark  = el('button', { class: 'lh-bg-type-btn', type: 'button' }, 'Dark');
  themeLight.addEventListener('click', () => { state.theme = 'light'; themeLight.classList.add('active'); themeDark.classList.remove('active'); debouncedPreview(); });
  themeDark.addEventListener('click',  () => { state.theme = 'dark';  themeDark.classList.add('active');  themeLight.classList.remove('active'); debouncedPreview(); });

  const publishedCb    = el('input', { type: 'checkbox' });
  const engageCb       = el('input', { type: 'checkbox' });
  const widgetKeyInput = el('input', { class: 'lh-input', placeholder: 'Paste your Widget Key from Engage → Widget tab' });
  const siteKeyInput   = el('input', { class: 'lh-input', placeholder: 'Paste your Site Key from Sites' });
  const widgetWrap     = el('div', { style: 'display:none' });

  // ── Event wiring ──────────────────────────────────────────────────────────
  nameInput.addEventListener('input', () => {
    state.displayName = nameInput.value;
    if (!slugManuallyEdited) {
      state.slug = toSlug(state.displayName);
      slugInput.value = state.slug;
      updateSlugUi();
    }
    debouncedPreview();
  });
  slugInput.addEventListener('input', () => {
    slugManuallyEdited = true;
    state.slug = slugInput.value;
    updateSlugUi();
    debouncedPreview();
  });
  slugInput.addEventListener('blur', () => validateSlugUi());
  bioInput.addEventListener('input', () => {
    const len = bioInput.value.length;
    bioCount.textContent = `${len} / 160`;
    bioCount.classList.toggle('warn', len >= 150);
    state.bio = bioInput.value;
    debouncedPreview();
  });
  emojiInput.addEventListener('input', () => {
    state.avatarEmoji = emojiInput.value;
    if (!state.profilePictureUrl) updateAvatarCircle();
    debouncedPreview();
  });
  brandColorInput.addEventListener('input', () => {
    state.brandColor = brandColorInput.value;
    brandHexInput.value = brandColorInput.value;
    debouncedPreview();
  });
  brandHexInput.addEventListener('input', () => {
    if (/^#[0-9a-fA-F]{6}$/.test(brandHexInput.value)) {
      state.brandColor = brandHexInput.value;
      brandColorInput.value = brandHexInput.value;
      debouncedPreview();
    }
  });
  bgColorPicker.addEventListener('input', () => {
    state.backgroundValue = bgColorPicker.value;
    bgValueInput.value    = bgColorPicker.value;
    updateBgPreview(); debouncedPreview();
  });
  bgValueInput.addEventListener('input', () => {
    state.backgroundValue = bgValueInput.value.trim();
    if (state.backgroundType === 'color' && /^#[0-9a-fA-F]{6}$/.test(state.backgroundValue))
      bgColorPicker.value = state.backgroundValue;
    updateBgPreview(); debouncedPreview();
  });
  engageCb.addEventListener('change', () => {
    state.engageBotEnabled = engageCb.checked;
    widgetWrap.style.display = engageCb.checked ? '' : 'none';
  });
  publishedCb.addEventListener('change', () => {
    state.isPublished = publishedCb.checked;
    updateHeroUrl();
  });
  widgetKeyInput.addEventListener('input', () => { state.widgetKey = widgetKeyInput.value; });
  siteKeyInput.addEventListener('input',   () => { state.siteKey   = siteKeyInput.value; });

  const updateSlugUi = () => {
    slugPreview.textContent = state.slug ? `hven.io/hub/${state.slug}` : '';
    slugError.style.display = 'none';
    slugInput.classList.remove('error');
    updateHeroUrl();
  };
  const validateSlugUi = () => {
    const valid = SLUG_RE.test(state.slug);
    slugError.style.display = (valid || !state.slug) ? 'none' : '';
    slugInput.classList.toggle('error', !valid && !!state.slug);
    return valid;
  };

  widgetWrap.append(mkField('Widget Key', widgetKeyInput), mkField('Site Key', siteKeyInput));

  // ── Profile form assembly ─────────────────────────────────────────────────
  const slugWrap = el('div', { class: 'lh-field' });
  slugWrap.append(el('label', {}, 'Public URL slug'), slugInput, slugPreview, slugError);

  const brandRow = el('div', { class: 'lh-color-row' });
  brandRow.append(
    el('div', {}, emojiInput),
    el('div', { style: 'display:flex;align-items:center;gap:6px;font-size:12px;color:#64748b' }, 'Brand colour', brandColorInput, brandHexInput),
  );

  const bgValueWrap = el('div', { style: 'display:flex;align-items:center;gap:6px' }, bgColorPicker, bgValueInput);

  profileBody.append(
    el('div', { class: 'lh-section-label' }, 'Identity'),
    avatarUploadWrap,
    mkField('Display Name', nameInput),
    slugWrap,
    mkField('Bio', bioInput, bioCount),
    el('hr', { class: 'lh-section-sep' }),
    el('div', { class: 'lh-section-label' }, 'Appearance'),
    mkField('Fallback emoji (shown if no photo)', emojiInput),
    mkField('Brand colour', brandRow),
    mkField('Background', bgTypeRow, bgValueWrap, bgPreview),
    mkField('Theme', el('div', { class: 'lh-bg-type-row', style: 'max-width:160px' }, themeLight, themeDark)),
    mkToggleRow('Published', 'Make your Link Hub public at hven.io/hub/[slug]', publishedCb),
    el('hr', { class: 'lh-section-sep' }),
    mkToggleRow('Embed AI Chat Bot', 'Your Hven AI bot will appear on your public Link Hub page', engageCb),
    widgetWrap,
  );
  profilePanel.appendChild(profileBody);
  editorTabPanel.appendChild(profilePanel);

  // ── Populate form ─────────────────────────────────────────────────────────
  const populateForm = () => {
    const p = state.profile;
    if (!p) return;
    state.displayName       = p.displayName       || '';
    state.slug              = p.slug              || '';
    state.bio               = p.bio               || '';
    state.avatarEmoji       = p.avatarEmoji       || '👤';
    state.brandColor        = p.brandColor        || '#6366f1';
    state.theme             = p.theme             || 'light';
    state.isPublished       = !!p.isPublished;
    state.profilePictureUrl = p.profilePictureUrl || '';
    state.backgroundType    = p.backgroundType    || 'color';
    state.backgroundValue   = p.backgroundValue   || '#ffffff';
    state.engageBotEnabled  = !!p.engageBotEnabled;
    state.widgetKey         = p.widgetKey         || '';
    state.siteKey           = p.siteKey           || '';

    nameInput.value       = state.displayName;
    slugInput.value       = state.slug;
    bioInput.value        = state.bio;
    bioCount.textContent  = `${state.bio.length} / 160`;
    emojiInput.value      = state.avatarEmoji;
    brandColorInput.value = state.brandColor;
    brandHexInput.value   = state.brandColor;
    themeLight.classList.toggle('active', state.theme !== 'dark');
    themeDark.classList.toggle('active', state.theme === 'dark');
    publishedCb.checked   = state.isPublished;
    engageCb.checked      = state.engageBotEnabled;
    widgetKeyInput.value  = state.widgetKey;
    siteKeyInput.value    = state.siteKey;
    widgetWrap.style.display = state.engageBotEnabled ? '' : 'none';

    const bgIdx = ['color', 'gradient', 'image'].indexOf(state.backgroundType);
    bgTypeBtns.forEach((b, i) => b.classList.toggle('active', i === bgIdx));
    bgColorPicker.value = state.backgroundType === 'color' ? (state.backgroundValue || '#ffffff') : '#ffffff';
    bgValueInput.value  = state.backgroundValue || '#ffffff';
    updateBgValueInput();

    updateAvatarCircle();
    slugManuallyEdited = !!state.slug;
    updateSlugUi();
    updateHeroUrl();
    renderPreview();
  };

  // ── Save handler ──────────────────────────────────────────────────────────
  saveBtn.addEventListener('click', async () => {
    if (state.saving) return;
    if (!validateSlugUi()) {
      notifier.show({ message: 'Please fix the slug before saving.', variant: 'warning' });
      return;
    }
    state.saving = true; saveBtn.disabled = true; saveBtn.textContent = '⏳ Saving…';
    try {
      const payload = {
        slug:              state.slug,
        displayName:       state.displayName,
        bio:               state.bio.slice(0, 160) || null,
        avatarEmoji:       state.avatarEmoji || '👤',
        avatarInitials:    state.displayName.slice(0, 2).toUpperCase() || null,
        profilePictureUrl: state.profilePictureUrl || null,
        backgroundType:    state.backgroundType,
        backgroundValue:   state.backgroundValue,
        brandColor:        state.brandColor,
        theme:             state.theme,
        isPublished:       state.isPublished,
        engageBotEnabled:  state.engageBotEnabled,
        widgetKey:         state.widgetKey || null,
        siteKey:           state.siteKey   || null,
        links:             state.links.map((l, i) => ({ ...l, order: i })),
      };
      const result  = await client.linkHub.saveProfile(payload);
      state.profile = result;
      state.links   = [...(result.links || [])];
      populateForm();
      renderLinkList();
      notifier.show({ message: '✓ Link Hub saved.', variant: 'success' });
    } catch (err) {
      notifier.show({ message: mapApiError(err).message, variant: 'danger' });
    } finally {
      state.saving = false; saveBtn.disabled = false; saveBtn.textContent = '💾 Save';
    }
  });

  // ═══════════════════════════════════════════════════════════════════════════
  // EDITOR TAB — PANEL 2: Links
  // ═══════════════════════════════════════════════════════════════════════════
  const linksPanel   = el('div', { class: 'lh-panel' });
  const linksPanelHd = el('div', { class: 'lh-panel-hd' });
  linksPanelHd.appendChild(el('div', { class: 'lh-panel-title' }, 'Links'));
  const addLinkBtn   = el('button', { class: 'lh-btn lh-btn-outline lh-btn-sm' }, '＋ Add');
  linksPanelHd.appendChild(addLinkBtn);
  linksPanel.appendChild(linksPanelHd);
  const linkListWrap = el('div', { class: 'lh-panel-body', style: 'padding-bottom:8px' });
  linksPanel.appendChild(linkListWrap);
  editorTabPanel.appendChild(linksPanel);

  const renderLinkList = () => {
    linkListWrap.replaceChildren();
    if (!state.links.length && !state.addingLink) {
      linkListWrap.appendChild(el('div', { class: 'lh-empty' }, 'No links yet — click ＋ Add to get started.'));
    }
    state.links.forEach((link, i) => {
      const card   = el('div', { class: 'lh-link-card' });
      const iconEl = mkLinkIcon(link);
      const info   = el('div', { class: 'lh-link-info' },
        el('div', { class: 'lh-link-label' }, link.label || 'Untitled'),
        el('div', { class: 'lh-link-url' }, link.url || ''),
      );
      const dmRow = el('div', { class: 'lh-display-mode-row' });
      [['🖼', 'icon-only'], ['🖼+A', 'icon-label'], ['A', 'label-only']].forEach(([label, mode]) => {
        const b = el('button', { class: 'lh-dm-btn' + ((link.displayMode || 'icon-label') === mode ? ' active' : ''), type: 'button', title: mode }, label);
        b.addEventListener('click', () => {
          state.links[i] = { ...state.links[i], displayMode: mode };
          renderLinkList(); debouncedPreview();
        });
        dmRow.appendChild(b);
      });
      const delBtn = el('button', { class: 'lh-btn lh-btn-danger lh-btn-sm' }, '✕');
      delBtn.addEventListener('click', () => { state.links.splice(i, 1); renderLinkList(); debouncedPreview(); });
      card.append(iconEl, el('div', { style: 'flex:1;min-width:0' }, info, dmRow), delBtn);
      linkListWrap.appendChild(card);
    });

    if (state.addingLink) {
      const form       = el('div', { class: 'lh-add-form' });
      const platSelect = el('select', { class: 'lh-select', style: 'margin-bottom:8px' },
        el('option', { value: '' }, '— Platform —'),
        ...Object.keys(PLATFORM_ICONS).map(p => el('option', { value: p }, p.charAt(0).toUpperCase() + p.slice(1))),
      );
      const labelInp  = el('input', { class: 'lh-input', placeholder: 'Link label', style: 'margin-bottom:8px' });
      const urlInp    = el('input', { class: 'lh-input', placeholder: 'https://', style: 'margin-bottom:8px' });
      let newDM = 'icon-label';
      const dmAddRow = el('div', { class: 'lh-display-mode-row', style: 'margin-bottom:10px' });
      [['🖼 Icon only', 'icon-only'], ['🖼+A Icon + Label', 'icon-label'], ['A Label only', 'label-only']].forEach(([label, mode]) => {
        const b = el('button', { class: 'lh-dm-btn' + (mode === 'icon-label' ? ' active' : ''), type: 'button' }, label);
        b.addEventListener('click', () => {
          newDM = mode;
          dmAddRow.querySelectorAll('.lh-dm-btn').forEach(x => x.classList.remove('active'));
          b.classList.add('active');
        });
        dmAddRow.appendChild(b);
      });
      platSelect.addEventListener('change', () => {
        const p = platSelect.value;
        if (p && !labelInp.value) labelInp.value = p.charAt(0).toUpperCase() + p.slice(1);
        if (p) urlInp.placeholder = PLATFORM_PLACEHOLDERS[p] || 'https://';
      });
      const actRow    = el('div', { style: 'display:flex;gap:8px' });
      const addBtn2   = el('button', { class: 'lh-btn lh-btn-primary lh-btn-sm' }, 'Add link');
      const cancelBtn = el('button', { class: 'lh-btn lh-btn-outline lh-btn-sm' }, 'Cancel');
      cancelBtn.addEventListener('click', () => { state.addingLink = false; renderLinkList(); });
      addBtn2.addEventListener('click', () => {
        if (!labelInp.value.trim() || !urlInp.value.trim()) {
          notifier.show({ message: 'Label and URL are required.', variant: 'warning' }); return;
        }
        const p = platSelect.value;
        state.links.push({
          id: crypto.randomUUID().replace(/-/g, ''), label: labelInp.value.trim(),
          url: urlInp.value.trim(), platform: p || null,
          iconEmoji: PLATFORM_ICONS[p] || '🔗', order: state.links.length,
          isActive: true, clickCount: 0, displayMode: newDM,
        });
        state.addingLink = false;
        renderLinkList(); debouncedPreview();
      });
      actRow.append(addBtn2, cancelBtn);
      form.append(platSelect, labelInp, urlInp, dmAddRow, actRow);
      linkListWrap.appendChild(form);
    }
  };
  addLinkBtn.addEventListener('click', () => { state.addingLink = true; renderLinkList(); });

  // ═══════════════════════════════════════════════════════════════════════════
  // ANALYTICS TAB (lazy loaded)
  // ═══════════════════════════════════════════════════════════════════════════
  const analyticsPanel = el('div', { class: 'lh-panel' });
  const analyticsHd    = el('div', { class: 'lh-panel-hd' });
  analyticsHd.appendChild(el('div', { class: 'lh-panel-title' }, '📊 Analytics — Last 30 Days'));
  const refreshBtn = el('button', { class: 'lh-btn lh-btn-outline lh-btn-sm' }, '↻ Refresh');
  analyticsHd.appendChild(refreshBtn);
  analyticsPanel.appendChild(analyticsHd);
  const analyticsBody = el('div', { class: 'lh-panel-body' });
  analyticsBody.appendChild(el('div', { class: 'lh-empty' }, 'Click the Analytics tab to load data.'));
  analyticsPanel.appendChild(analyticsBody);
  analyticsTabPanel.appendChild(analyticsPanel);

  const renderAnalytics = () => {
    analyticsBody.replaceChildren();
    const a = state.analytics;
    if (!a) {
      analyticsBody.appendChild(el('div', { class: 'lh-empty' }, 'Share your Link Hub to start seeing analytics.'));
      return;
    }
    const clickRate = a.totalViews > 0 ? ((a.totalClicks / a.totalViews) * 100).toFixed(1) + '%' : '—';
    const topRef    = a.referrerBreakdown?.[0]?.platform || '—';
    const statsGrid = el('div', { class: 'lh-stats-grid' });
    [['Total Views', a.totalViews], ['Link Clicks', a.totalClicks], ['Click Rate', clickRate],
     ['Top Referrer', (REFERRER_EMOJI[topRef] || '') + ' ' + topRef]]
      .forEach(([lbl, val]) => statsGrid.appendChild(el('div', { class: 'lh-stat-card' },
        el('div', { class: 'lh-stat-val' }, String(val)),
        el('div', { class: 'lh-stat-lbl' }, lbl))));
    analyticsBody.appendChild(statsGrid);

    if (a.referrerBreakdown?.length) {
      analyticsBody.appendChild(el('div', { class: 'lh-section-label' }, 'Referrers'));
      a.referrerBreakdown.slice(0, 6).forEach(r => {
        analyticsBody.appendChild(el('div', { class: 'lh-platform-row' },
          el('span', {}, (REFERRER_EMOJI[r.platform] || '🌐') + ' ' + r.platform),
          el('span', { style: 'color:#64748b;font-size:11px' }, String(r.count)),
          el('div', { class: 'lh-platform-bar' },
            el('div', { class: 'lh-platform-fill', style: `width:${r.pct}%` }))));
      });
    }

    if (a.deviceBreakdown?.length) {
      analyticsBody.appendChild(el('div', { class: 'lh-section-label', style: 'margin-top:14px' }, 'Devices'));
      const devGrid = el('div', { class: 'lh-stats-grid' });
      a.deviceBreakdown.slice(0, 3).forEach(d => {
        const emoji = d.platform === 'mobile' ? '📱' : d.platform === 'tablet' ? '📟' : '🖥';
        devGrid.appendChild(el('div', { class: 'lh-stat-card', style: 'padding:8px' },
          el('div', { class: 'lh-stat-val', style: 'font-size:16px' }, `${emoji} ${d.pct}%`),
          el('div', { class: 'lh-stat-lbl' }, d.platform)));
      });
      analyticsBody.appendChild(devGrid);
    }

    if (a.topLinks?.length) {
      analyticsBody.appendChild(el('div', { class: 'lh-section-label', style: 'margin-top:14px' }, 'Top Links'));
      a.topLinks.slice(0, 5).forEach(l => {
        analyticsBody.appendChild(el('div', { class: 'lh-platform-row' },
          el('span', {}, (PLATFORM_ICONS[l.platform || ''] || '🔗') + ' ' + (l.label || l.linkId)),
          el('span', { style: 'color:#6366f1;font-weight:700;font-size:11px' }, l.count + ' clicks')));
      });
    }
  };

  const loadAnalytics = async () => {
    analyticsBody.replaceChildren(el('div', { class: 'lh-empty' }, '⏳ Loading analytics…'));
    try {
      state.analytics = await client.linkHub.getAnalytics(30);
      renderAnalytics();
    } catch {
      analyticsBody.replaceChildren(el('div', { class: 'lh-empty' }, 'Could not load analytics.'));
    }
  };

  refreshBtn.addEventListener('click', () => { analyticsLoaded = true; loadAnalytics(); });

  // ═══════════════════════════════════════════════════════════════════════════
  // RIGHT: Sticky Live Preview
  // ═══════════════════════════════════════════════════════════════════════════
  const previewPanel = el('div', { class: 'lh-preview' });
  const previewHd    = el('div', { class: 'lh-preview-hd' });
  previewHd.appendChild(el('span', {}, 'Live Preview'));
  const previewOpenBtn = el('a', {
    style: 'font-size:11px;color:#6366f1;text-decoration:none;font-weight:600',
    target: '_blank',
  }, 'Open →');
  previewOpenBtn.style.display = 'none';
  previewHd.appendChild(previewOpenBtn);
  const previewFrame = el('div', { class: 'lh-preview-frame' });
  let previewNote = el('div', { class: 'lh-preview-note' }, 'Fill in your profile to preview');
  previewPanel.append(previewHd, previewFrame, previewNote);
  rightCol.appendChild(previewPanel);

  const renderPreview = () => {
    previewFrame.replaceChildren();

    const bt  = state.backgroundType  || 'color';
    const bv  = state.backgroundValue || '#ffffff';
    if (bt === 'gradient')
      previewFrame.style.cssText = `padding:20px;display:flex;flex-direction:column;align-items:center;gap:8px;min-height:220px;background:${bv};transition:background .3s`;
    else if (bt === 'image')
      previewFrame.style.cssText = `padding:20px;display:flex;flex-direction:column;align-items:center;gap:8px;min-height:220px;background-image:url('${bv}');background-size:cover;background-position:center;transition:background .3s`;
    else
      previewFrame.style.cssText = `padding:20px;display:flex;flex-direction:column;align-items:center;gap:8px;min-height:220px;background:${bv};transition:background .3s`;

    const isDark    = state.theme === 'dark';
    const textCol   = isDark ? '#f1f5f9' : '#0f172a';
    const bioCol    = isDark ? '#94a3b8' : '#64748b';
    const cardBg    = isDark ? 'rgba(30,41,59,0.9)' : 'rgba(255,255,255,0.9)';
    const color     = state.brandColor || '#6366f1';

    const inner = el('div', { style: `background:${cardBg};border-radius:16px;padding:20px;width:100%;max-width:280px;display:flex;flex-direction:column;align-items:center;gap:6px` });

    const avatar = el('div', { class: 'lh-preview-avatar', style: `background:${color}` });
    if (state.profilePictureUrl) {
      const img = el('img', { src: state.profilePictureUrl });
      img.onerror = () => { avatar.replaceChildren(document.createTextNode(state.avatarEmoji || '👤')); };
      avatar.appendChild(img);
    } else {
      avatar.appendChild(document.createTextNode(state.avatarEmoji || '👤'));
    }

    inner.appendChild(avatar);
    inner.appendChild(el('div', { class: 'lh-preview-name', style: `color:${textCol}` }, state.displayName || 'Your Name'));
    if (state.bio) inner.appendChild(el('div', { class: 'lh-preview-bio', style: `color:${bioCol}` }, state.bio));

    state.links.filter(l => l.isActive !== false).slice(0, 6).forEach(link => {
      const mode = link.displayMode || 'icon-label';
      const faviconUrl = getFaviconUrl(link.platform, link.url);
      const makeIcon = () => {
        if (faviconUrl) {
          const img = el('img', { src: faviconUrl, width: '16', height: '16', style: 'border-radius:2px' });
          img.onerror = () => { img.style.display = 'none'; };
          return img;
        }
        return document.createTextNode(PLATFORM_ICONS[link.platform || ''] || link.iconEmoji || '🔗');
      };
      if (mode === 'icon-only') {
        const btn = el('div', { class: 'lh-preview-link icon-only', style: `background:${color}` });
        btn.appendChild(makeIcon());
        inner.appendChild(btn);
      } else if (mode === 'label-only') {
        inner.appendChild(el('div', { class: 'lh-preview-link full-width', style: `background:${color}` },
          el('span', {}, link.label || 'Link')));
      } else {
        const btn = el('div', { class: 'lh-preview-link full-width', style: `background:${color}` });
        btn.appendChild(makeIcon());
        btn.appendChild(el('span', {}, link.label || 'Link'));
        inner.appendChild(btn);
      }
    });

    inner.appendChild(el('div', { style: `font-size:10px;color:${bioCol};margin-top:8px;opacity:.6` }, 'Powered by Hven'));
    previewFrame.appendChild(inner);

    // Update open button
    if (state.slug && state.isPublished) {
      previewOpenBtn.href         = `https://hven.io/hub/${state.slug}`;
      previewOpenBtn.style.display = '';
    } else {
      previewOpenBtn.style.display = 'none';
    }
  };
  const debouncedPreview = debounce(renderPreview, 300);

  // ── Init ──────────────────────────────────────────────────────────────────
  const init = async () => {
    try {
      state.profile = await client.linkHub.getProfile();
      state.links   = [...(state.profile.links || [])];
      populateForm();
      renderLinkList();
      renderPreview();
    } catch (err) {
      notifier.show({ message: mapApiError(err).message, variant: 'danger' });
    }
  };

  init();
}
