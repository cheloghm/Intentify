import { createApiClient, mapApiError } from '../shared/apiClient.js';
import { setToken } from '../shared/auth.js';

const app = document.getElementById('app');
const apiClient = createApiClient();

// ── Inject styles ────────────────────────────────────────────────────────────
(function injectStyles() {
  if (!document.getElementById('itfy-reg-fonts')) {
    const link = document.createElement('link');
    link.id = 'itfy-reg-fonts';
    link.rel = 'stylesheet';
    link.href = 'https://fonts.googleapis.com/css2?family=Playfair+Display:ital,wght@0,500;0,700;1,400&family=DM+Sans:wght@300;400;500&display=swap';
    document.head.appendChild(link);
  }
  const style = document.createElement('style');
  style.textContent = `
    *,*::before,*::after{box-sizing:border-box}
    body,html{margin:0;padding:0;background:#f9f8f6;font-family:'DM Sans',sans-serif;-webkit-font-smoothing:antialiased}
    #app{min-height:100vh;display:flex;flex-direction:column;align-items:center;justify-content:center;padding:32px 24px;background:#f9f8f6}
    .hv-card{width:100%;max-width:520px;background:#ffffff;border:1px solid rgba(15,14,12,.1);border-radius:16px;padding:44px 40px;box-shadow:0 4px 32px rgba(15,14,12,.06)}
    .hv-brand{font-family:'Playfair Display',serif;font-size:24px;font-weight:500;color:#0f0e0c;letter-spacing:-.02em;text-align:center;margin-bottom:6px}
    .hv-tagline{font-size:13px;font-weight:300;color:#8a8680;text-align:center;margin-bottom:8px}
    .hv-free-note{font-size:12px;font-weight:500;color:#6366f1;text-align:center;margin-bottom:28px}
    .hv-google{width:100%;display:flex;align-items:center;justify-content:center;gap:10px;background:#fff;color:#1f2937;border:1px solid rgba(15,14,12,.16);border-radius:8px;font-family:'DM Sans',sans-serif;font-size:14px;font-weight:500;padding:12px 20px;cursor:pointer;transition:all .18s;margin-bottom:20px}
    .hv-google:hover{background:#f9f8f6;box-shadow:0 2px 12px rgba(15,14,12,.1)}
    .hv-divider{display:flex;align-items:center;gap:12px;color:#c4c0bb;font-size:12px;font-weight:300;margin-bottom:20px}
    .hv-divider::before,.hv-divider::after{content:'';flex:1;height:1px;background:rgba(15,14,12,.08)}
    .hv-field{margin-bottom:14px}
    .hv-label{display:block;font-size:11px;font-weight:600;letter-spacing:.07em;text-transform:uppercase;color:#8a8680;margin-bottom:6px}
    .hv-input{width:100%;font-family:'DM Sans',sans-serif;font-size:14px;font-weight:300;color:#0f0e0c;background:#f9f8f6;border:1px solid rgba(15,14,12,.12);border-radius:8px;padding:11px 14px;outline:none;transition:border-color .15s}
    .hv-input:focus{border-color:#6366f1;background:#fff;box-shadow:0 0 0 3px rgba(99,102,241,.08)}
    .hv-input::placeholder{color:#c4c0bb}
    .hv-err{font-size:12px;color:#dc2626;min-height:16px;margin-top:4px}
    /* Plan selector */
    .hv-plan-label{font-size:11px;font-weight:600;letter-spacing:.07em;text-transform:uppercase;color:#8a8680;margin-bottom:10px}
    .hv-plans{display:grid;grid-template-columns:repeat(3,1fr);gap:10px;margin-bottom:8px}
    .hv-plan{background:#f9f8f6;border:1.5px solid rgba(15,14,12,.1);border-radius:10px;padding:14px 12px;cursor:pointer;transition:all .18s;text-align:center}
    .hv-plan:hover{border-color:rgba(99,102,241,.35)}
    .hv-plan.sel{border-color:#6366f1;background:rgba(99,102,241,.05)}
    .hv-plan-name{font-family:'Playfair Display',serif;font-size:14px;font-weight:500;color:#0f0e0c;margin-bottom:4px}
    .hv-plan-price{font-size:12px;font-weight:600;color:#6366f1;margin-bottom:8px}
    .hv-plan-feats{list-style:none;padding:0;margin:0;display:flex;flex-direction:column;gap:3px}
    .hv-plan-feats li{font-size:11px;color:#8a8680;font-weight:300}
    .hv-plan-free{font-size:11.5px;font-weight:500;color:#059669;text-align:center;margin-bottom:18px}
    .hv-submit{width:100%;font-family:'DM Sans',sans-serif;font-size:14px;font-weight:500;color:#fff;background:#6366f1;border:none;border-radius:8px;padding:13px;cursor:pointer;transition:all .18s;margin-top:4px}
    .hv-submit:hover:not(:disabled){background:#4f46e5;transform:translateY(-1px)}
    .hv-submit:disabled{opacity:.55;cursor:not-allowed}
    .hv-links{display:flex;flex-direction:column;gap:10px;text-align:center;margin-top:22px}
    .hv-links a{font-size:13.5px;font-weight:300;color:#6366f1;text-decoration:none;transition:color .15s}
    .hv-links a:hover{color:#4f46e5}
    .hv-back{font-size:12.5px;color:#c4c0bb !important}
    .hv-back:hover{color:#8a8680 !important}
    .hv-toast{position:fixed;bottom:24px;right:24px;z-index:9999;background:#0f0e0c;border:1px solid rgba(255,255,255,.08);border-radius:10px;padding:12px 18px;font-family:'DM Sans',sans-serif;font-size:13.5px;color:#f9f8f6;box-shadow:0 8px 32px rgba(0,0,0,.3);display:none;max-width:320px}
    .hv-toast.show{display:block;animation:slideUp .22s ease}
    .hv-toast.danger{border-color:rgba(220,38,38,.3)}
    .hv-toast.warning{border-color:rgba(245,158,11,.3)}
    @keyframes slideUp{from{opacity:0;transform:translateY(8px)}to{opacity:1;transform:translateY(0)}}
    .hv-footer-credit{margin-top:20px;text-align:center;font-size:11.5px;font-weight:300;color:#c4c0bb}
    .hv-footer-credit a{color:#8a8680;text-decoration:none;transition:color .15s}
    .hv-footer-credit a:hover{color:#6366f1}
    @media(max-width:480px){.hv-card{padding:28px 20px}.hv-plans{grid-template-columns:1fr}}
  `;
  document.head.appendChild(style);
})();

// ── Toast ────────────────────────────────────────────────────────────────────
const toastEl = document.createElement('div');
toastEl.className = 'hv-toast';
document.body.appendChild(toastEl);
let toastTimer;
const toast = {
  show({ message, variant = 'info' }) {
    toastEl.textContent = message;
    toastEl.className = `hv-toast show ${variant}`;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => toastEl.classList.remove('show'), 4000);
  }
};

// ── Validation ───────────────────────────────────────────────────────────────
const validateEmail = v => {
  if (!v) return 'Email is required.';
  const at = v.indexOf('@'), dot = v.lastIndexOf('.');
  if (at < 1 || dot < at + 2 || dot === v.length - 1) return 'Enter a valid email address.';
  return '';
};
const validatePassword = v => {
  if (!v) return 'Password is required.';
  if (v.length < 10) return 'Password must be at least 10 characters.';
  if (!/[A-Za-z]/.test(v) || !/[0-9]/.test(v)) return 'Password must include a letter and a number.';
  return '';
};
const applyFieldErrors = (errors, fields) => {
  if (!errors || typeof errors !== 'object') return false;
  let applied = false;
  Object.entries(errors).forEach(([k, msgs]) => {
    const nk = k.toLowerCase().replace(/\s+/g, '');
    const match = Object.keys(fields).find(fk => fk.toLowerCase().replace(/\s+/g, '') === nk);
    if (match) { fields[match].error.textContent = Array.isArray(msgs) ? msgs.join(' ') : String(msgs); applied = true; }
  });
  return applied;
};

const GOOGLE_SVG = `<svg width="18" height="18" viewBox="0 0 18 18"><path fill="#4285F4" d="M17.64 9.2c0-.637-.057-1.251-.164-1.84H9v3.481h4.844c-.209 1.125-.843 2.078-1.796 2.717v2.258h2.908c1.702-1.567 2.684-3.875 2.684-6.615z"/><path fill="#34A853" d="M9 18c2.43 0 4.467-.806 5.956-2.18l-2.908-2.259c-.806.54-1.837.86-3.048.86-2.344 0-4.328-1.584-5.036-3.711H.957v2.332A8.997 8.997 0 0 0 9 18z"/><path fill="#FBBC05" d="M3.964 10.71A5.41 5.41 0 0 1 3.682 9c0-.593.102-1.17.282-1.71V4.958H.957A8.996 8.996 0 0 0 0 9c0 1.452.348 2.827.957 4.042l3.007-2.332z"/><path fill="#EA4335" d="M9 3.58c1.321 0 2.508.454 3.44 1.345l2.582-2.58C13.463.891 11.426 0 9 0A8.997 8.997 0 0 0 .957 4.958L3.964 5.29C4.672 3.163 6.656 1.58 9 1.58z"/></svg>`;

const PLANS = [
  { id: 'starter', name: 'Starter', price: '£35/mo', feats: ['1 website', '5k visitors/mo', '500 AI chats'] },
  { id: 'growth',  name: 'Growth',  price: '£65/mo', feats: ['3 websites', '25k visitors/mo', 'Unlimited AI'] },
  { id: 'agency',  name: 'Agency',  price: '£159/mo', feats: ['Unlimited sites', 'Unlimited visitors', 'RBAC + API'] },
];

// ── Render ───────────────────────────────────────────────────────────────────
const render = () => {
  if (!app) return;

  const params = new URLSearchParams(window.location.search);
  const oauthToken = params.get('token');
  const oauthError = params.get('error');
  if (oauthToken) { setToken(oauthToken); window.location.href = '/app'; return; }
  if (oauthError) setTimeout(() => toast.show({ message: 'Google sign-in is not yet configured. Please use email.', variant: 'warning' }), 100);

  let selectedPlan = params.get('plan') || 'starter';

  const card = document.createElement('div');
  card.className = 'hv-card';

  card.innerHTML = `
    <div class="hv-brand">Hven</div>
    <div class="hv-tagline">Create your free account</div>
    <div class="hv-free-note">✦ First month free — no credit card required</div>
  `;

  // Org name for Google signup
  const googleOrgWrap = document.createElement('div');
  googleOrgWrap.className = 'hv-field';
  googleOrgWrap.style.marginBottom = '8px';
  const googleOrgNote = document.createElement('div');
  googleOrgNote.style.cssText = 'font-size:11px;color:#8a8680;font-weight:300;margin-bottom:6px';
  googleOrgNote.textContent = 'Optional — you can update this later';
  const googleOrgInput = document.createElement('input');
  googleOrgInput.type = 'text';
  googleOrgInput.className = 'hv-input';
  googleOrgInput.placeholder = 'Company or organisation name';
  googleOrgWrap.append(googleOrgNote, googleOrgInput);
  card.appendChild(googleOrgWrap);

  // Google
  const googleBtn = document.createElement('button');
  googleBtn.type = 'button';
  googleBtn.className = 'hv-google';
  googleBtn.innerHTML = `${GOOGLE_SVG} Sign up with Google`;
  googleBtn.addEventListener('click', () => {
    const orgName = googleOrgInput.value.trim();
    const url = orgName ? `/api/auth/google?orgName=${encodeURIComponent(orgName)}` : '/api/auth/google';
    window.location.href = url;
  });
  card.appendChild(googleBtn);

  // Divider
  const divider = document.createElement('div');
  divider.className = 'hv-divider';
  divider.textContent = 'or create account with email';
  card.appendChild(divider);

  // Form
  const form = document.createElement('form');

  const mkField = (labelText, type, placeholder) => {
    const wrap = document.createElement('div');
    wrap.className = 'hv-field';
    const lbl = document.createElement('label');
    lbl.className = 'hv-label';
    lbl.textContent = labelText;
    const input = document.createElement('input');
    input.type = type; input.className = 'hv-input'; input.placeholder = placeholder;
    const error = document.createElement('div');
    error.className = 'hv-err';
    wrap.append(lbl, input, error);
    return { wrap, input, error };
  };

  const displayNameField    = mkField('Full name', 'text', 'Jane Doe');
  const organizationField   = mkField('Company / Organisation', 'text', 'Acme Corp');
  const emailField          = mkField('Email', 'email', 'you@company.com');
  const passwordField       = mkField('Password', 'password', 'At least 10 characters');

  // Plan selector
  const planLbl = document.createElement('div');
  planLbl.className = 'hv-plan-label';
  planLbl.textContent = 'Choose your plan';

  const plansGrid = document.createElement('div');
  plansGrid.className = 'hv-plans';

  const planEls = PLANS.map(plan => {
    const el = document.createElement('div');
    el.className = 'hv-plan' + (plan.id === selectedPlan ? ' sel' : '');
    el.innerHTML = `
      <div class="hv-plan-name">${plan.name}</div>
      <div class="hv-plan-price">${plan.price}</div>
      <ul class="hv-plan-feats">${plan.feats.map(f => `<li>${f}</li>`).join('')}</ul>
    `;
    el.addEventListener('click', () => {
      selectedPlan = plan.id;
      planEls.forEach(p => p.classList.remove('sel'));
      el.classList.add('sel');
    });
    return el;
  });
  planEls.forEach(el => plansGrid.appendChild(el));

  const freeNote = document.createElement('div');
  freeNote.className = 'hv-plan-free';
  freeNote.textContent = '✦ First month free on any plan';

  const submitBtn = document.createElement('button');
  submitBtn.type = 'submit';
  submitBtn.className = 'hv-submit';
  submitBtn.textContent = 'Create my free account →';

  form.append(
    displayNameField.wrap, organizationField.wrap,
    emailField.wrap, passwordField.wrap,
    planLbl, plansGrid, freeNote, submitBtn
  );

  form.addEventListener('submit', async e => {
    e.preventDefault();
    displayNameField.error.textContent = '';
    organizationField.error.textContent = '';
    emailField.error.textContent = '';
    passwordField.error.textContent = '';
    const displayName = displayNameField.input.value.trim();
    const organizationName = organizationField.input.value.trim();
    const email = emailField.input.value.trim();
    const password = passwordField.input.value;
    let hasError = false;
    if (!displayName) { displayNameField.error.textContent = 'Full name is required.'; hasError = true; }
    if (!organizationName) { organizationField.error.textContent = 'Company name is required.'; hasError = true; }
    const emailErr = validateEmail(email);
    if (emailErr) { emailField.error.textContent = emailErr; hasError = true; }
    const passErr = validatePassword(password);
    if (passErr) { passwordField.error.textContent = passErr; hasError = true; }
    if (hasError) { toast.show({ message: 'Please fix the highlighted fields.', variant: 'warning' }); return; }
    submitBtn.disabled = true; submitBtn.textContent = 'Creating account…';
    try {
      const response = await apiClient.request('/auth/register', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ displayName, organizationName, email, password, plan: selectedPlan }),
      });
      setToken(response.accessToken);
      window.location.href = '/app';
    } catch (error) {
      const uiError = mapApiError(error);
      const applied = applyFieldErrors(uiError.details?.errors, {
        displayName: displayNameField, organizationName: organizationField,
        email: emailField, password: passwordField,
      });
      toast.show({ message: applied ? 'Please review the highlighted errors.' : uiError.message, variant: 'danger' });
    } finally {
      submitBtn.disabled = false; submitBtn.textContent = 'Create my free account →';
    }
  });

  card.appendChild(form);

  // Links
  const links = document.createElement('div');
  links.className = 'hv-links';
  links.innerHTML = `
    <a href="/public/login.html">Already have an account? Sign in</a>
    <a href="/" class="hv-back">← Back to hven.io</a>
  `;
  card.appendChild(links);

  // Footer credit
  const credit = document.createElement('div');
  credit.className = 'hv-footer-credit';
  credit.innerHTML = `Designed by <a href="https://fortiguardian.com" target="_blank">FortiGuardian Consulting</a>`;
  card.appendChild(credit);

  app.innerHTML = '';
  app.appendChild(card);
};

render();
