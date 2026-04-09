const injectStyles = () => {
  if (document.getElementById('_ms_css')) return;
  const s = document.createElement('style');
  s.id = '_ms_css';
  s.textContent = `
.ms-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;align-items:center;gap:32px;width:100%;max-width:960px;padding:40px 20px 60px}
.ms-hero{text-align:center;max-width:600px}
.ms-icon{font-size:48px;line-height:1;margin-bottom:12px}
.ms-title{font-size:28px;font-weight:800;color:#0f172a;letter-spacing:-.03em;margin-bottom:8px}
.ms-sub{font-size:15px;color:#64748b;line-height:1.6;margin-bottom:16px}
.ms-badge{display:inline-flex;align-items:center;gap:6px;background:#eef2ff;color:#6366f1;font-size:11px;font-weight:700;letter-spacing:.07em;text-transform:uppercase;padding:4px 12px;border-radius:99px;border:1px solid #c7d2fe}
.ms-body{display:grid;grid-template-columns:1fr 1fr;gap:32px;width:100%;align-items:start}
@media(max-width:700px){.ms-body{grid-template-columns:1fr}}
.ms-features{background:#fff;border:1px solid #e2e8f0;border-radius:16px;padding:24px 28px}
.ms-features-title{font-size:13px;font-weight:700;color:#94a3b8;text-transform:uppercase;letter-spacing:.07em;margin-bottom:16px}
.ms-feature{display:flex;align-items:flex-start;gap:10px;padding:10px 0;border-bottom:1px solid #f1f5f9}
.ms-feature:last-child{border-bottom:none}
.ms-feature-icon{width:28px;height:28px;border-radius:8px;background:#eef2ff;display:flex;align-items:center;justify-content:center;font-size:14px;flex-shrink:0;margin-top:1px}
.ms-feature-text{font-size:13.5px;color:#334155;line-height:1.5}
.ms-laptop-wrap{display:flex;justify-content:center;padding:8px}
.ms-laptop{width:260px;position:relative}
.ms-laptop-screen{width:100%;height:160px;background:#1e293b;border-radius:10px 10px 0 0;padding:10px;box-sizing:border-box;overflow:hidden;border:3px solid #334155}
.ms-laptop-base{height:14px;background:#334155;border-radius:0 0 6px 6px;position:relative}
.ms-laptop-base::after{content:'';position:absolute;bottom:0;left:50%;transform:translateX(-50%);width:60px;height:6px;background:#475569;border-radius:3px 3px 0 0}
.ms-laptop-foot{height:8px;background:#475569;border-radius:0 0 10px 10px;width:110%;margin-left:-5%}
.ms-page{background:#fff;height:100%;border-radius:4px;overflow:hidden;display:flex;flex-direction:column}
.ms-page-nav{height:18px;background:#6366f1;display:flex;align-items:center;padding:0 8px;gap:4px;flex-shrink:0}
.ms-page-nav-dot{width:4px;height:4px;border-radius:50%;background:rgba(255,255,255,0.5)}
.ms-page-nav-bar{flex:1;height:6px;background:rgba(255,255,255,0.15);border-radius:3px;margin:0 6px}
.ms-page-hero-block{height:40px;background:linear-gradient(135deg,#f8fafc,#eef2ff);display:flex;flex-direction:column;align-items:center;justify-content:center;gap:3px;flex-shrink:0}
.ms-page-h{width:60%;height:5px;background:#6366f1;border-radius:2px;opacity:.7}
.ms-page-p{width:80%;height:3px;background:#cbd5e1;border-radius:2px}
.ms-page-body{flex:1;padding:6px 8px;display:flex;flex-direction:column;gap:4px}
.ms-page-row{display:grid;grid-template-columns:1fr 1fr;gap:4px}
.ms-page-card{height:20px;background:#f1f5f9;border-radius:3px}
.ms-page-cta{height:14px;background:#6366f1;border-radius:3px;opacity:.85}
.ms-cta{background:#fff;border:1px solid #e2e8f0;border-radius:16px;padding:24px 28px;width:100%;max-width:480px;text-align:center}
.ms-cta-title{font-size:15px;font-weight:700;color:#0f172a;margin-bottom:6px}
.ms-cta-sub{font-size:13px;color:#64748b;margin-bottom:16px}
.ms-cta-row{display:flex;gap:8px;flex-wrap:wrap;justify-content:center}
.ms-cta-input{flex:1;min-width:180px;padding:9px 13px;border:1px solid #e2e8f0;border-radius:8px;font-size:13px;font-family:inherit;outline:none}
.ms-cta-input:focus{border-color:#6366f1;box-shadow:0 0 0 3px rgba(99,102,241,.1)}
.ms-cta-btn{padding:9px 18px;background:#6366f1;color:#fff;border:none;border-radius:8px;font-size:13px;font-weight:600;cursor:pointer;font-family:inherit;transition:background .14s}
.ms-cta-btn:hover{background:#4f46e5}
.ms-cta-success{font-size:13px;color:#10b981;font-weight:600;margin-top:10px;display:none}
`;
  document.head.appendChild(s);
};

export function renderMicroSiteView(container) {
  injectStyles();

  const root = document.createElement('div');
  root.className = 'ms-root';

  // Hero
  const hero = document.createElement('div');
  hero.className = 'ms-hero';
  hero.innerHTML = `
    <div class="ms-icon">🌐</div>
    <div class="ms-title">Micro-Site Builder</div>
    <div class="ms-sub">Build a fast, AI-powered landing page for your business — no design skills required.</div>
    <span class="ms-badge">✦ Coming Soon</span>
  `;
  root.appendChild(hero);

  // Body
  const body = document.createElement('div');
  body.className = 'ms-body';

  // Feature list
  const features = document.createElement('div');
  features.className = 'ms-features';
  const featTitle = document.createElement('div');
  featTitle.className = 'ms-features-title';
  featTitle.textContent = 'What you\'ll be able to do';
  features.appendChild(featTitle);

  const items = [
    ['⚡', 'Launch a professional landing page in under 5 minutes'],
    ['🤖', "AI generates your copy from your bot's knowledge base"],
    ['📋', 'Built-in contact form connected to your Leads pipeline'],
    ['🌐', 'Custom domain support'],
    ['💬', 'Hven Engage widget pre-installed'],
  ];
  items.forEach(([icon, text]) => {
    const row = document.createElement('div');
    row.className = 'ms-feature';
    row.innerHTML = `<div class="ms-feature-icon">${icon}</div><div class="ms-feature-text">${text}</div>`;
    features.appendChild(row);
  });
  body.appendChild(features);

  // Laptop mockup
  const laptopWrap = document.createElement('div');
  laptopWrap.className = 'ms-laptop-wrap';
  const laptop = document.createElement('div');
  laptop.className = 'ms-laptop';
  laptop.innerHTML = `
    <div class="ms-laptop-screen">
      <div class="ms-page">
        <div class="ms-page-nav">
          <div class="ms-page-nav-dot"></div>
          <div class="ms-page-nav-dot"></div>
          <div class="ms-page-nav-dot"></div>
          <div class="ms-page-nav-bar"></div>
        </div>
        <div class="ms-page-hero-block">
          <div class="ms-page-h"></div>
          <div class="ms-page-p"></div>
        </div>
        <div class="ms-page-body">
          <div class="ms-page-row">
            <div class="ms-page-card"></div>
            <div class="ms-page-card"></div>
          </div>
          <div class="ms-page-row">
            <div class="ms-page-card"></div>
            <div class="ms-page-card"></div>
          </div>
          <div class="ms-page-cta"></div>
        </div>
      </div>
    </div>
    <div class="ms-laptop-base"></div>
    <div class="ms-laptop-foot"></div>
  `;
  laptopWrap.appendChild(laptop);
  body.appendChild(laptopWrap);

  root.appendChild(body);

  // CTA
  const cta = document.createElement('div');
  cta.className = 'ms-cta';
  cta.innerHTML = `<div class="ms-cta-title">Get notified when this launches</div><div class="ms-cta-sub">Be the first to know when Micro-Site Builder goes live.</div>`;

  const ctaRow = document.createElement('div');
  ctaRow.className = 'ms-cta-row';
  const emailInput = document.createElement('input');
  emailInput.type = 'email';
  emailInput.placeholder = 'you@example.com';
  emailInput.className = 'ms-cta-input';

  const notifyBtn = document.createElement('button');
  notifyBtn.className = 'ms-cta-btn';
  notifyBtn.textContent = 'Notify me';

  const successMsg = document.createElement('div');
  successMsg.className = 'ms-cta-success';
  successMsg.textContent = "You'll be the first to know! 🎉";

  notifyBtn.addEventListener('click', () => {
    const email = emailInput.value.trim();
    if (!email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      emailInput.style.borderColor = '#ef4444';
      return;
    }
    emailInput.style.borderColor = '';
    try { localStorage.setItem('hven_notify_microsite', email); } catch {}
    ctaRow.style.display = 'none';
    successMsg.style.display = 'block';
  });

  ctaRow.append(emailInput, notifyBtn);
  cta.append(ctaRow, successMsg);
  root.appendChild(cta);

  container.appendChild(root);
}
