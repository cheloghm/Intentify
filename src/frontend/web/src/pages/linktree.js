import { createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const LS_KEY = 'hven_notify_linkhub_registered';

const injectStyles = () => {
  if (document.getElementById('_lt_css')) return;
  const s = document.createElement('style');
  s.id = '_lt_css';
  s.textContent = `
@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&display=swap');
.lt-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;align-items:center;gap:32px;width:100%;max-width:960px;padding:40px 20px 60px}
.lt-hero{text-align:center;max-width:600px}
.lt-icon{font-size:48px;line-height:1;margin-bottom:12px}
.lt-title{font-size:28px;font-weight:800;color:#0f172a;letter-spacing:-.03em;margin-bottom:8px}
.lt-sub{font-size:15px;color:#64748b;line-height:1.6;margin-bottom:16px}
.lt-badge{display:inline-flex;align-items:center;gap:6px;background:#eef2ff;color:#6366f1;font-size:11px;font-weight:700;letter-spacing:.07em;text-transform:uppercase;padding:4px 12px;border-radius:99px;border:1px solid #c7d2fe}
.lt-body{display:grid;grid-template-columns:1fr 1fr;gap:32px;width:100%;align-items:start}
@media(max-width:700px){.lt-body{grid-template-columns:1fr}}
.lt-features{background:#fff;border:1px solid #e2e8f0;border-radius:16px;padding:24px 28px}
.lt-features-title{font-size:13px;font-weight:700;color:#94a3b8;text-transform:uppercase;letter-spacing:.07em;margin-bottom:16px}
.lt-feature{display:flex;align-items:flex-start;gap:10px;padding:10px 0;border-bottom:1px solid #f1f5f9}
.lt-feature:last-child{border-bottom:none}
.lt-feature-icon{width:28px;height:28px;border-radius:8px;background:#eef2ff;display:flex;align-items:center;justify-content:center;font-size:14px;flex-shrink:0;margin-top:1px}
.lt-feature-text{font-size:13.5px;color:#334155;line-height:1.5}
.lt-phone-wrap{display:flex;justify-content:center;padding:8px}
.lt-phone{width:180px;height:340px;border:6px solid #1e293b;border-radius:32px;background:#f8fafc;position:relative;overflow:hidden;box-shadow:0 20px 48px rgba(15,23,42,.2)}
.lt-phone::before{content:'';position:absolute;top:0;left:50%;transform:translateX(-50%);width:60px;height:18px;background:#1e293b;border-radius:0 0 12px 12px;z-index:2}
.lt-phone-inner{padding:28px 14px 14px;display:flex;flex-direction:column;align-items:center;gap:8px;height:100%;box-sizing:border-box}
.lt-phone-avatar{width:44px;height:44px;border-radius:50%;background:linear-gradient(135deg,#6366f1,#8b5cf6);flex-shrink:0}
.lt-phone-name{font-size:10px;font-weight:700;color:#1e293b}
.lt-phone-bio{font-size:8px;color:#64748b;text-align:center;line-height:1.4}
.lt-phone-btn{width:100%;height:28px;border-radius:8px;background:#6366f1;display:flex;align-items:center;justify-content:center;font-size:8px;font-weight:600;color:#fff}
.lt-phone-btn.outline{background:#fff;color:#6366f1;border:1px solid #6366f1}
.lt-phone-btn.muted{background:#f1f5f9;color:#475569}
.lt-cta{background:#fff;border:1px solid #e2e8f0;border-radius:16px;padding:24px 28px;width:100%;max-width:480px;text-align:center}
.lt-cta-title{font-size:15px;font-weight:700;color:#0f172a;margin-bottom:6px}
.lt-cta-sub{font-size:13px;color:#64748b;margin-bottom:16px}
.lt-notify-btn{display:inline-flex;align-items:center;gap:8px;padding:11px 22px;background:#6366f1;color:#fff;border:none;border-radius:10px;font-size:14px;font-weight:600;cursor:pointer;font-family:inherit;transition:background .14s,transform .14s}
.lt-notify-btn:hover:not(:disabled){background:#4f46e5;transform:translateY(-1px)}
.lt-notify-btn:disabled{opacity:.7;cursor:default}
.lt-notify-btn.success{background:#10b981}
`;
  document.head.appendChild(s);
};

export function renderLinkTreeView(container) {
  injectStyles();
  const toast = createToastManager();
  const api = createApiClient();

  const root = document.createElement('div');
  root.className = 'lt-root';

  // Hero
  const hero = document.createElement('div');
  hero.className = 'lt-hero';
  hero.innerHTML = `
    <div class="lt-icon">🔗</div>
    <div class="lt-title">Link Hub</div>
    <div class="lt-sub">Create a branded link page for your social bio — one link that does everything.</div>
    <span class="lt-badge">✦ Coming Soon</span>
  `;
  root.appendChild(hero);

  // Body
  const body = document.createElement('div');
  body.className = 'lt-body';

  // Feature list
  const features = document.createElement('div');
  features.className = 'lt-features';
  const featTitle = document.createElement('div');
  featTitle.className = 'lt-features-title';
  featTitle.textContent = "What you'll be able to do";
  features.appendChild(featTitle);

  const items = [
    ['🔗', 'Add unlimited links with custom labels and icons'],
    ['🎨', 'Match your brand — custom colours, logo, background'],
    ['📊', 'Track which links visitors click most'],
    ['💬', 'Embed your Hven chat widget on your link page'],
    ['📱', 'Share one link everywhere — Instagram bio, Twitter, email'],
  ];
  items.forEach(([icon, text]) => {
    const row = document.createElement('div');
    row.className = 'lt-feature';
    row.innerHTML = `<div class="lt-feature-icon">${icon}</div><div class="lt-feature-text">${text}</div>`;
    features.appendChild(row);
  });
  body.appendChild(features);

  // Phone mockup
  const phoneWrap = document.createElement('div');
  phoneWrap.className = 'lt-phone-wrap';
  const phone = document.createElement('div');
  phone.className = 'lt-phone';
  phone.innerHTML = `
    <div class="lt-phone-inner">
      <div class="lt-phone-avatar"></div>
      <div class="lt-phone-name">@yourbrand</div>
      <div class="lt-phone-bio">Building things that matter ✦</div>
      <div class="lt-phone-btn">🌐 Visit our website</div>
      <div class="lt-phone-btn outline">📸 Latest Instagram post</div>
      <div class="lt-phone-btn muted">📩 Contact us</div>
      <div class="lt-phone-btn muted">⭐ Leave a review</div>
    </div>
  `;
  phoneWrap.appendChild(phone);
  body.appendChild(phoneWrap);
  root.appendChild(body);

  // CTA
  const cta = document.createElement('div');
  cta.className = 'lt-cta';
  cta.innerHTML = `<div class="lt-cta-title">Get notified when this launches</div><div class="lt-cta-sub">Be the first to know when Link Hub goes live.</div>`;

  const alreadyRegistered = (() => { try { return !!localStorage.getItem(LS_KEY); } catch { return false; } })();

  const notifyBtn = document.createElement('button');
  notifyBtn.className = 'lt-notify-btn' + (alreadyRegistered ? ' success' : '');
  notifyBtn.textContent = alreadyRegistered ? "✓ You're on the list!" : '🔔 Get notified when this launches';
  notifyBtn.disabled = alreadyRegistered;

  notifyBtn.addEventListener('click', async () => {
    notifyBtn.disabled = true;
    notifyBtn.textContent = 'Registering…';
    try {
      await api.notify.registerFeatureInterest('linkhub');
      try { localStorage.setItem(LS_KEY, '1'); } catch {}
      notifyBtn.className = 'lt-notify-btn success';
      notifyBtn.textContent = "✓ You're on the list!";
    } catch (err) {
      notifyBtn.disabled = false;
      notifyBtn.textContent = '🔔 Get notified when this launches';
      toast.show(mapApiError(err)?.message || 'Could not register — try again', 'error');
    }
  });

  cta.appendChild(notifyBtn);
  root.appendChild(cta);

  container.innerHTML = '';
  if (toast.element) container.appendChild(toast.element);
  container.appendChild(root);
}
