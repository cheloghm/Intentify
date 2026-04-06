import { createApiClient, mapApiError } from '../shared/apiClient.js';
import { setToken } from '../shared/auth.js';

const app = document.getElementById('app');
const toast = createToastManager();
const apiClient = createApiClient();

// ── Inject fonts + styles ──────────────────────────────────────────────────
(function injectStyles() {
  if (!document.getElementById('itfy-reg-fonts')) {
    const link = document.createElement('link');
    link.id = 'itfy-reg-fonts';
    link.rel = 'stylesheet';
    link.href = 'https://fonts.googleapis.com/css2?family=Syne:wght@400;600;700;800&family=DM+Sans:wght@400;500;600&display=swap';
    document.head.appendChild(link);
  }
  const style = document.createElement('style');
  style.textContent = `
    *, *::before, *::after { box-sizing: border-box; }
    body, html {
      margin: 0; padding: 0;
      background: #0a0a0f;
      font-family: 'DM Sans', sans-serif;
      -webkit-font-smoothing: antialiased;
    }
    #app {
      min-height: 100vh;
      display: flex; align-items: flex-start; justify-content: center;
      padding: 40px 24px;
      background: #0a0a0f;
    }
    .itfy-card {
      width: 100%; max-width: 520px;
      background: #13131f;
      border: 1px solid rgba(255,255,255,.08);
      border-radius: 20px;
      padding: 40px 36px;
    }
    .itfy-logo {
      display: flex; align-items: center; justify-content: center;
      gap: 10px; margin-bottom: 28px;
    }
    .itfy-logo img { height: 36px; }
    .itfy-logo span {
      font-family: 'Syne', sans-serif; font-weight: 700; font-size: 22px;
      color: #f1f5f9;
    }
    .itfy-title {
      font-family: 'Syne', sans-serif; font-weight: 700;
      font-size: 24px; color: #f1f5f9;
      text-align: center; margin-bottom: 6px;
    }
    .itfy-sub {
      font-size: 14px; color: #64748b;
      text-align: center; margin-bottom: 28px;
    }
    .itfy-google-btn {
      width: 100%;
      display: flex; align-items: center; justify-content: center;
      gap: 10px;
      background: #fff; color: #1f2937;
      border: none; border-radius: 10px;
      font-family: 'DM Sans', sans-serif; font-size: 15px; font-weight: 600;
      padding: 12px 20px; cursor: pointer;
      transition: background .2s, box-shadow .2s;
      margin-bottom: 20px;
    }
    .itfy-google-btn:hover { background: #f9fafb; box-shadow: 0 4px 16px rgba(0,0,0,.3); }
    .itfy-divider {
      display: flex; align-items: center; gap: 12px;
      color: #334155; font-size: 13px;
      margin-bottom: 20px;
    }
    .itfy-divider::before, .itfy-divider::after {
      content: ''; flex: 1; height: 1px; background: rgba(255,255,255,.08);
    }
    .itfy-field { margin-bottom: 16px; }
    .itfy-field label {
      display: block; font-size: 13px; font-weight: 500;
      color: #94a3b8; margin-bottom: 6px;
    }
    .itfy-field input {
      width: 100%;
      background: #0f0f18; border: 1px solid rgba(255,255,255,.08);
      border-radius: 8px; color: #f1f5f9;
      font-family: 'DM Sans', sans-serif; font-size: 14px;
      padding: 10px 14px; outline: none;
      transition: border-color .2s;
    }
    .itfy-field input:focus { border-color: #6366f1; }
    .itfy-field-error { font-size: 12px; color: #f87171; margin-top: 4px; min-height: 16px; }
    /* Plan selector */
    .itfy-plan-label {
      font-size: 13px; font-weight: 500; color: #94a3b8;
      margin-bottom: 10px;
    }
    .itfy-plans {
      display: grid; grid-template-columns: repeat(3, 1fr);
      gap: 10px; margin-bottom: 10px;
    }
    .itfy-plan {
      background: #0f0f18; border: 1px solid rgba(255,255,255,.08);
      border-radius: 10px; padding: 14px 12px;
      cursor: pointer; transition: border-color .2s, background .2s;
      text-align: center;
    }
    .itfy-plan:hover { border-color: rgba(99,102,241,.4); }
    .itfy-plan.selected {
      border-color: #6366f1;
      background: rgba(99,102,241,.1);
    }
    .itfy-plan-name {
      font-family: 'Syne', sans-serif; font-size: 14px; font-weight: 700;
      color: #f1f5f9; margin-bottom: 4px;
    }
    .itfy-plan-price {
      font-size: 13px; font-weight: 600; color: #6366f1; margin-bottom: 8px;
    }
    .itfy-plan-features {
      list-style: none; padding: 0; margin: 0;
      display: flex; flex-direction: column; gap: 3px;
    }
    .itfy-plan-features li {
      font-size: 11px; color: #64748b;
    }
    .itfy-plan-free-note {
      font-size: 12px; color: #4ade80;
      text-align: center; margin-bottom: 20px;
    }
    .itfy-submit {
      width: 100%;
      background: #6366f1; color: #fff; border: none; border-radius: 10px;
      font-family: 'DM Sans', sans-serif; font-size: 15px; font-weight: 600;
      padding: 12px 20px; cursor: pointer; margin-top: 8px;
      transition: background .2s, transform .1s;
    }
    .itfy-submit:hover:not(:disabled) { background: #4f46e5; transform: translateY(-1px); }
    .itfy-submit:disabled { opacity: .6; cursor: not-allowed; }
    .itfy-links {
      display: flex; flex-direction: column; gap: 10px;
      text-align: center; margin-top: 20px;
    }
    .itfy-links a { font-size: 14px; color: #6366f1; transition: color .2s; }
    .itfy-links a:hover { color: #818cf8; }
    .itfy-links .itfy-back { font-size: 13px; color: #475569; }
    .itfy-links .itfy-back:hover { color: #94a3b8; }
    .itfy-toast {
      position: fixed; bottom: 24px; right: 24px; z-index: 9999;
      background: #1e1e2e; border: 1px solid rgba(255,255,255,.1);
      border-radius: 10px; padding: 12px 18px;
      font-size: 14px; color: #f1f5f9;
      box-shadow: 0 8px 32px rgba(0,0,0,.4);
      display: none;
      max-width: 340px;
    }
    .itfy-toast.show { display: block; animation: slideIn .25s ease; }
    .itfy-toast.danger { border-color: rgba(248,113,113,.3); }
    .itfy-toast.warning { border-color: rgba(251,191,36,.3); }
    @keyframes slideIn { from { opacity: 0; transform: translateY(8px); } to { opacity: 1; transform: translateY(0); } }
    @media (max-width: 480px) {
      .itfy-card { padding: 28px 20px; }
      .itfy-plans { grid-template-columns: 1fr; }
    }
  `;
  document.head.appendChild(style);
})();

// ── Minimal toast manager ──────────────────────────────────────────────────
function createToastManager() {
  const el = document.createElement('div');
  el.className = 'itfy-toast';
  document.body.appendChild(el);
  let timer;
  return {
    show({ message, variant = 'info' }) {
      el.textContent = message;
      el.className = `itfy-toast show ${variant}`;
      clearTimeout(timer);
      timer = setTimeout(() => { el.classList.remove('show'); }, 4000);
    }
  };
}

// ── Validation helpers ─────────────────────────────────────────────────────
const validateEmail = (v) => {
  if (!v) return 'Email is required.';
  const at = v.indexOf('@'), dot = v.lastIndexOf('.');
  if (at < 1 || dot < at + 2 || dot === v.length - 1) return 'Enter a valid email address.';
  return '';
};
const validatePassword = (v) => {
  if (!v) return 'Password is required.';
  if (v.length < 10) return 'Password must be at least 10 characters.';
  if (!/[A-Za-z]/.test(v) || !/[0-9]/.test(v)) return 'Password must include at least one letter and one number.';
  return '';
};
const normalizeKey = (k) => k.toLowerCase().replace(/\s+/g, '');
const applyFieldErrors = (errors, fields) => {
  if (!errors || typeof errors !== 'object') return false;
  let applied = false;
  Object.entries(errors).forEach(([k, msgs]) => {
    const nk = normalizeKey(k);
    const match = Object.keys(fields).find(fk => normalizeKey(fk) === nk);
    if (match) {
      fields[match].error.textContent = Array.isArray(msgs) ? msgs.join(' ') : String(msgs);
      applied = true;
    }
  });
  return applied;
};

// ── Google SVG ─────────────────────────────────────────────────────────────
const GOOGLE_SVG = `<svg width="18" height="18" viewBox="0 0 18 18"><path fill="#4285F4" d="M17.64 9.2c0-.637-.057-1.251-.164-1.84H9v3.481h4.844c-.209 1.125-.843 2.078-1.796 2.717v2.258h2.908c1.702-1.567 2.684-3.875 2.684-6.615z"/><path fill="#34A853" d="M9 18c2.43 0 4.467-.806 5.956-2.18l-2.908-2.259c-.806.54-1.837.86-3.048.86-2.344 0-4.328-1.584-5.036-3.711H.957v2.332A8.997 8.997 0 0 0 9 18z"/><path fill="#FBBC05" d="M3.964 10.71A5.41 5.41 0 0 1 3.682 9c0-.593.102-1.17.282-1.71V4.958H.957A8.996 8.996 0 0 0 0 9c0 1.452.348 2.827.957 4.042l3.007-2.332z"/><path fill="#EA4335" d="M9 3.58c1.321 0 2.508.454 3.44 1.345l2.582-2.58C13.463.891 11.426 0 9 0A8.997 8.997 0 0 0 .957 4.958L3.964 5.29C4.672 3.163 6.656 1.58 9 1.58z"/></svg>`;

// ── Plan data ──────────────────────────────────────────────────────────────
const PLANS = [
  {
    id: 'starter',
    name: 'Starter',
    price: '£35/mo',
    features: ['1 website', '5k visitors/mo', '500 AI conversations'],
  },
  {
    id: 'growth',
    name: 'Growth',
    price: '£65/mo',
    features: ['3 websites', '25k visitors/mo', 'Unlimited AI chats'],
  },
  {
    id: 'agency',
    name: 'Agency',
    price: '£159/mo',
    features: ['Unlimited sites', 'Unlimited visitors', 'RBAC + API access'],
  },
];

// ── Render ─────────────────────────────────────────────────────────────────
const render = () => {
  if (!app) return;

  // Handle Google OAuth callback
  const urlParams = new URLSearchParams(window.location.search);
  const oauthToken = urlParams.get('token');
  const oauthError = urlParams.get('error');
  if (oauthToken) {
    setToken(oauthToken);
    window.location.href = '/public/index.html';
    return;
  }
  if (oauthError) {
    setTimeout(() => toast.show({
      message: 'Google sign-in is not yet configured. Please use email.',
      variant: 'warning'
    }), 100);
  }

  let selectedPlan = 'starter';

  // Card
  const card = document.createElement('div');
  card.className = 'itfy-card';

  // Logo
  card.innerHTML = `
    <div class="itfy-logo">
      <img src="/assets/logo_white.png" alt="Intentify" onerror="this.style.display='none'">
      <span>Intentify</span>
    </div>
    <div class="itfy-title">Create your free account</div>
    <div class="itfy-sub">First month free · No credit card required</div>
  `;

  // Google button
  const googleBtn = document.createElement('button');
  googleBtn.className = 'itfy-google-btn';
  googleBtn.innerHTML = `${GOOGLE_SVG} Sign up with Google`;
  googleBtn.addEventListener('click', () => { window.location.href = '/auth/google'; });
  card.appendChild(googleBtn);

  // Divider
  const divider = document.createElement('div');
  divider.className = 'itfy-divider';
  divider.textContent = 'or create account with email';
  card.appendChild(divider);

  // Form
  const form = document.createElement('form');

  const makeField = ({ label, type, placeholder }) => {
    const wrap = document.createElement('div');
    wrap.className = 'itfy-field';
    const lbl = document.createElement('label');
    lbl.textContent = label;
    const input = document.createElement('input');
    input.type = type;
    input.placeholder = placeholder;
    const error = document.createElement('div');
    error.className = 'itfy-field-error';
    wrap.append(lbl, input, error);
    return { wrap, input, error };
  };

  const displayNameField = makeField({ label: 'Full name', type: 'text', placeholder: 'Jane Doe' });
  const organizationNameField = makeField({ label: 'Company / Organisation', type: 'text', placeholder: 'Acme Inc' });
  const emailField = makeField({ label: 'Email', type: 'email', placeholder: 'you@example.com' });
  const passwordField = makeField({ label: 'Password', type: 'password', placeholder: 'At least 10 characters' });

  form.append(
    displayNameField.wrap,
    organizationNameField.wrap,
    emailField.wrap,
    passwordField.wrap,
  );

  // Plan selector
  const planLabel = document.createElement('div');
  planLabel.className = 'itfy-plan-label';
  planLabel.textContent = 'Choose your plan';
  form.appendChild(planLabel);

  const plansGrid = document.createElement('div');
  plansGrid.className = 'itfy-plans';

  const planEls = PLANS.map((plan) => {
    const el = document.createElement('div');
    el.className = 'itfy-plan' + (plan.id === 'starter' ? ' selected' : '');
    el.innerHTML = `
      <div class="itfy-plan-name">${plan.name}</div>
      <div class="itfy-plan-price">${plan.price}</div>
      <ul class="itfy-plan-features">
        ${plan.features.map(f => `<li>${f}</li>`).join('')}
      </ul>
    `;
    el.addEventListener('click', () => {
      selectedPlan = plan.id;
      planEls.forEach(p => p.classList.remove('selected'));
      el.classList.add('selected');
    });
    return el;
  });
  planEls.forEach(el => plansGrid.appendChild(el));
  form.appendChild(plansGrid);

  const freeNote = document.createElement('div');
  freeNote.className = 'itfy-plan-free-note';
  freeNote.textContent = '✦ First month free on any plan — no credit card required';
  form.appendChild(freeNote);

  const submitBtn = document.createElement('button');
  submitBtn.type = 'submit';
  submitBtn.className = 'itfy-submit';
  submitBtn.textContent = 'Create my free account →';
  form.appendChild(submitBtn);

  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    displayNameField.error.textContent = '';
    organizationNameField.error.textContent = '';
    emailField.error.textContent = '';
    passwordField.error.textContent = '';

    const displayName = displayNameField.input.value.trim();
    const organizationName = organizationNameField.input.value.trim();
    const email = emailField.input.value.trim();
    const password = passwordField.input.value;

    let hasError = false;
    if (!displayName) { displayNameField.error.textContent = 'Full name is required.'; hasError = true; }
    if (!organizationName) { organizationNameField.error.textContent = 'Company name is required.'; hasError = true; }
    const emailErr = validateEmail(email);
    if (emailErr) { emailField.error.textContent = emailErr; hasError = true; }
    const passErr = validatePassword(password);
    if (passErr) { passwordField.error.textContent = passErr; hasError = true; }

    if (hasError) {
      toast.show({ message: 'Please fix the highlighted fields.', variant: 'warning' });
      return;
    }

    submitBtn.disabled = true;
    submitBtn.textContent = 'Creating account…';
    try {
      const response = await apiClient.request('/auth/register', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ displayName, organizationName, email, password, plan: selectedPlan }),
      });
      setToken(response.accessToken);
      window.location.href = '/public/index.html';
    } catch (error) {
      const uiError = mapApiError(error);
      const applied = applyFieldErrors(uiError.details?.errors, {
        displayName: displayNameField,
        organizationName: organizationNameField,
        email: emailField,
        password: passwordField,
      });
      toast.show({
        message: applied ? 'Please review the highlighted errors.' : uiError.message,
        variant: 'danger',
      });
    } finally {
      submitBtn.disabled = false;
      submitBtn.textContent = 'Create my free account →';
    }
  });

  card.appendChild(form);

  // Links
  const links = document.createElement('div');
  links.className = 'itfy-links';
  links.innerHTML = `
    <a href="/public/login.html">Already have an account? Sign in</a>
    <a href="/" class="itfy-back">← Back to intentify.io</a>
  `;
  card.appendChild(links);

  app.innerHTML = '';
  app.appendChild(card);
};

render();
