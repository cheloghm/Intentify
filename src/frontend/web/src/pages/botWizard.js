/**
 * botWizard.js — First-time bot setup wizard.
 * Renders as a full-screen overlay when a new user has no bot config set.
 * Export: showBotWizard(container, { apiClient, siteId, toast, onComplete })
 */

const INDUSTRY_OPTIONS = [
  ['', '— Select industry —'],
  ['Technology', 'Technology'],
  ['Cybersecurity', 'Cybersecurity'],
  ['Web Design', 'Web Design'],
  ['Marketing Agency', 'Marketing Agency'],
  ['E-commerce', 'E-commerce'],
  ['Healthcare', 'Healthcare'],
  ['Legal', 'Legal'],
  ['Finance', 'Finance'],
  ['Real Estate', 'Real Estate'],
  ['Education', 'Education'],
  ['Consulting', 'Consulting'],
  ['Other', 'Other'],
];

const TONE_OPTIONS = [
  { value: 'Conversational', label: 'Conversational', desc: 'Friendly, everyday language' },
  { value: 'Professional',   label: 'Professional',   desc: 'Formal and authoritative' },
  { value: 'Friendly',       label: 'Friendly',       desc: 'Warm and approachable' },
  { value: 'Direct',         label: 'Direct',         desc: 'Straight to the point' },
  { value: 'Empathetic',     label: 'Empathetic',     desc: 'Understanding and caring' },
  { value: 'Playful',        label: 'Playful',        desc: 'Light-hearted and fun' },
];

const PURPOSE_OPTIONS = [
  { value: 'capture leads',    label: 'Capture leads',    desc: 'Qualify visitors and collect contact details', icon: '🎯' },
  { value: 'answer questions', label: 'Answer questions', desc: 'Handle FAQs and product queries',             icon: '💬' },
  { value: 'book demos',       label: 'Book demos',       desc: 'Get visitors to schedule a call',             icon: '📅' },
  { value: 'general support',  label: 'General support',  desc: 'Help visitors find what they need',           icon: '🤝' },
];

const TOTAL_STEPS = 5;

function injectWizardStyles() {
  if (document.getElementById('_wz_css')) return;
  const s = document.createElement('style');
  s.id = '_wz_css';
  s.textContent = `
    @keyframes wz-fadein  { from { opacity:0 }                         to { opacity:1 } }
    @keyframes wz-step-in { from { opacity:0;transform:translateY(10px) } to { opacity:1;transform:translateY(0) } }
    .wz-overlay  { position:fixed;top:0;left:0;right:0;bottom:0;background:rgba(0,0,0,0.55);z-index:9999;display:flex;align-items:center;justify-content:center;padding:20px;animation:wz-fadein .2s ease }
    .wz-card     { background:#fff;border-radius:16px;padding:40px;max-width:560px;width:100%;box-shadow:0 20px 60px rgba(0,0,0,0.25);max-height:90vh;overflow-y:auto }
    .wz-progress { display:flex;gap:8px;margin-bottom:22px }
    .wz-dot      { width:10px;height:10px;border-radius:50%;background:#e2e8f0;transition:background .2s;flex-shrink:0 }
    .wz-dot.done { background:#10b981 }
    .wz-dot.active { background:#6366f1 }
    .wz-counter  { font-size:11px;color:#94a3b8;font-weight:500;letter-spacing:.06em;text-transform:uppercase;margin-bottom:10px }
    .wz-question { font-family:Georgia,serif;font-size:22px;font-style:italic;color:#0f172a;margin-bottom:22px;line-height:1.35 }
    .wz-body     { animation:wz-step-in .22s ease }
    .wz-textarea { width:100%;box-sizing:border-box;font-family:system-ui,sans-serif;font-size:14px;color:#1e293b;background:#f8fafc;border:1.5px solid #e2e8f0;border-radius:10px;padding:12px 14px;outline:none;resize:vertical;min-height:96px;transition:border-color .15s }
    .wz-textarea:focus { border-color:#6366f1;background:#fff;box-shadow:0 0 0 3px rgba(99,102,241,.1) }
    .wz-textarea::placeholder { color:#94a3b8 }
    .wz-select   { width:100%;box-sizing:border-box;font-family:system-ui,sans-serif;font-size:14px;color:#1e293b;background:#f8fafc;border:1.5px solid #e2e8f0;border-radius:10px;padding:12px 14px;outline:none;cursor:pointer;transition:border-color .15s }
    .wz-select:focus { border-color:#6366f1;box-shadow:0 0 0 3px rgba(99,102,241,.1) }
    .wz-tone-grid   { display:grid;grid-template-columns:1fr 1fr;gap:10px }
    .wz-tone-card   { padding:14px 16px;border:1.5px solid #e2e8f0;border-radius:10px;cursor:pointer;transition:all .15s;background:#f8fafc }
    .wz-tone-card:hover { border-color:#a5b4fc;background:#f5f3ff }
    .wz-tone-card.sel   { border-color:#6366f1;background:rgba(99,102,241,.06) }
    .wz-tone-name   { font-size:14px;font-weight:600;color:#1e293b;margin-bottom:3px }
    .wz-tone-desc   { font-size:12px;color:#64748b }
    .wz-radio-list  { display:flex;flex-direction:column;gap:10px }
    .wz-radio-card  { display:flex;align-items:center;gap:14px;padding:16px;border:1.5px solid #e2e8f0;border-radius:10px;cursor:pointer;transition:all .15s;background:#f8fafc }
    .wz-radio-card:hover { border-color:#a5b4fc;background:#f5f3ff }
    .wz-radio-card.sel   { border-color:#6366f1;background:rgba(99,102,241,.06) }
    .wz-radio-icon  { font-size:22px;flex-shrink:0 }
    .wz-radio-text  { flex:1 }
    .wz-radio-label { font-size:14px;font-weight:600;color:#1e293b }
    .wz-radio-desc  { font-size:12px;color:#64748b;margin-top:2px }
    .wz-radio-check { width:18px;height:18px;border-radius:50%;border:2px solid #e2e8f0;flex-shrink:0;transition:all .15s }
    .wz-radio-card.sel .wz-radio-check { background:#6366f1;border-color:#6366f1 }
    .wz-actions  { display:flex;align-items:center;justify-content:space-between;margin-top:28px }
    .wz-left     { display:flex;flex-direction:column;align-items:flex-start;gap:8px }
    .wz-right    { display:flex;align-items:center;gap:10px }
    .wz-btn-primary { font-family:system-ui,sans-serif;font-size:14px;font-weight:600;color:#fff;background:#6366f1;border:none;border-radius:8px;padding:11px 24px;cursor:pointer;transition:all .15s;white-space:nowrap }
    .wz-btn-primary:hover:not(:disabled) { background:#4f46e5;transform:translateY(-1px) }
    .wz-btn-primary:disabled { opacity:.55;cursor:not-allowed }
    .wz-btn-back { font-family:system-ui,sans-serif;font-size:14px;color:#64748b;background:none;border:none;padding:8px 0;cursor:pointer;transition:color .15s }
    .wz-btn-back:hover { color:#1e293b }
    .wz-skip     { font-size:12px;color:#94a3b8;background:none;border:none;cursor:pointer;text-decoration:underline;padding:0 }
    .wz-skip:hover { color:#64748b }
  `;
  document.head.appendChild(s);
}

export function showBotWizard(container, { apiClient, siteId, toast, onComplete } = {}) {
  const DISMISS_KEY = `hven_wizard_dismissed_${siteId}`;
  if (localStorage.getItem(DISMISS_KEY)) return;

  injectWizardStyles();

  const answers = { businessDescription: '', industry: '', servicesDescription: '', tone: '', purpose: '' };
  let currentStep = 0;

  const overlay = document.createElement('div');
  overlay.className = 'wz-overlay';
  const card = document.createElement('div');
  card.className = 'wz-card';
  overlay.appendChild(card);
  container.appendChild(overlay);

  const dismiss = () => {
    localStorage.setItem(DISMISS_KEY, '1');
    overlay.remove();
  };

  const saveCurrentStep = () => {
    if (currentStep === 0) {
      answers.businessDescription = card.querySelector('.wz-capture')?.value?.trim() || '';
    } else if (currentStep === 1) {
      answers.industry = card.querySelector('.wz-capture')?.value?.trim() || '';
    } else if (currentStep === 2) {
      answers.servicesDescription = card.querySelector('.wz-capture')?.value?.trim() || '';
    } else if (currentStep === 3) {
      const sel = card.querySelector('.wz-tone-card.sel');
      answers.tone = sel ? sel.dataset.value : answers.tone;
    } else if (currentStep === 4) {
      const sel = card.querySelector('.wz-radio-card.sel');
      answers.purpose = sel ? sel.dataset.value : answers.purpose;
    }
  };

  const renderStep = () => {
    card.innerHTML = '';

    // Progress dots
    const prog = document.createElement('div');
    prog.className = 'wz-progress';
    for (let i = 0; i < TOTAL_STEPS; i++) {
      const dot = document.createElement('div');
      dot.className = 'wz-dot' + (i < currentStep ? ' done' : i === currentStep ? ' active' : '');
      prog.appendChild(dot);
    }
    card.appendChild(prog);

    // Step counter
    const counter = document.createElement('div');
    counter.className = 'wz-counter';
    counter.textContent = `Step ${currentStep + 1} of ${TOTAL_STEPS}`;
    card.appendChild(counter);

    // Step body (animated)
    const body = document.createElement('div');
    body.className = 'wz-body';

    const q = document.createElement('div');
    q.className = 'wz-question';

    if (currentStep === 0) {
      q.textContent = 'What does your business do?';
      const ta = document.createElement('textarea');
      ta.className = 'wz-textarea wz-capture';
      ta.placeholder = 'e.g. We help small businesses with cybersecurity and IT support';
      ta.value = answers.businessDescription;
      ta.rows = 4;
      body.append(q, ta);
      requestAnimationFrame(() => ta.focus());

    } else if (currentStep === 1) {
      q.textContent = 'What industry are you in?';
      const sel = document.createElement('select');
      sel.className = 'wz-select wz-capture';
      INDUSTRY_OPTIONS.forEach(([val, label]) => {
        const opt = document.createElement('option');
        opt.value = val; opt.textContent = label;
        sel.appendChild(opt);
      });
      sel.value = answers.industry;
      body.append(q, sel);

    } else if (currentStep === 2) {
      q.textContent = 'What are your main services?';
      const ta = document.createElement('textarea');
      ta.className = 'wz-textarea wz-capture';
      ta.placeholder = 'List your key services, one per line';
      ta.value = answers.servicesDescription;
      ta.rows = 5;
      body.append(q, ta);
      requestAnimationFrame(() => ta.focus());

    } else if (currentStep === 3) {
      q.textContent = "How would you describe your brand's tone?";
      const grid = document.createElement('div');
      grid.className = 'wz-tone-grid';
      TONE_OPTIONS.forEach(opt => {
        const tCard = document.createElement('div');
        tCard.className = 'wz-tone-card' + (answers.tone === opt.value ? ' sel' : '');
        tCard.dataset.value = opt.value;
        tCard.innerHTML = `<div class="wz-tone-name">${opt.label}</div><div class="wz-tone-desc">${opt.desc}</div>`;
        tCard.addEventListener('click', () => {
          grid.querySelectorAll('.wz-tone-card').forEach(c => c.classList.remove('sel'));
          tCard.classList.add('sel');
          answers.tone = opt.value;
        });
        grid.appendChild(tCard);
      });
      body.append(q, grid);

    } else if (currentStep === 4) {
      q.textContent = "What's the main goal for your AI assistant?";
      const list = document.createElement('div');
      list.className = 'wz-radio-list';
      PURPOSE_OPTIONS.forEach(opt => {
        const row = document.createElement('div');
        row.className = 'wz-radio-card' + (answers.purpose === opt.value ? ' sel' : '');
        row.dataset.value = opt.value;
        row.innerHTML = `
          <div class="wz-radio-icon">${opt.icon}</div>
          <div class="wz-radio-text">
            <div class="wz-radio-label">${opt.label}</div>
            <div class="wz-radio-desc">${opt.desc}</div>
          </div>
          <div class="wz-radio-check"></div>
        `;
        row.addEventListener('click', () => {
          list.querySelectorAll('.wz-radio-card').forEach(c => c.classList.remove('sel'));
          row.classList.add('sel');
          answers.purpose = opt.value;
        });
        list.appendChild(row);
      });
      body.append(q, list);
    }

    card.appendChild(body);

    // Actions row
    const actions = document.createElement('div');
    actions.className = 'wz-actions';

    const left = document.createElement('div');
    left.className = 'wz-left';

    if (currentStep > 0) {
      const backBtn = document.createElement('button');
      backBtn.className = 'wz-btn-back';
      backBtn.textContent = '← Back';
      backBtn.addEventListener('click', () => {
        saveCurrentStep();
        currentStep--;
        renderStep();
      });
      left.appendChild(backBtn);
    }

    const skipBtn = document.createElement('button');
    skipBtn.className = 'wz-skip';
    skipBtn.textContent = 'Skip for now';
    skipBtn.addEventListener('click', dismiss);
    left.appendChild(skipBtn);

    const right = document.createElement('div');
    right.className = 'wz-right';

    if (currentStep < TOTAL_STEPS - 1) {
      const nextBtn = document.createElement('button');
      nextBtn.className = 'wz-btn-primary';
      nextBtn.textContent = 'Next →';
      nextBtn.addEventListener('click', () => {
        saveCurrentStep();
        currentStep++;
        renderStep();
      });
      right.appendChild(nextBtn);
    } else {
      const doneBtn = document.createElement('button');
      doneBtn.className = 'wz-btn-primary';
      doneBtn.textContent = 'Set up my bot →';
      doneBtn.addEventListener('click', async () => {
        saveCurrentStep();
        doneBtn.disabled = true;
        doneBtn.textContent = '⏳ Saving…';
        const desc = answers.businessDescription
          + (answers.purpose ? ` Our primary goal is to ${answers.purpose}.` : '');
        try {
          await apiClient.engage.updateBot(siteId, {
            name: 'Assistant',
            businessDescription: desc,
            industry: answers.industry,
            servicesDescription: answers.servicesDescription,
            tone: answers.tone,
            primaryColor: '#6366f1',
            launcherVisible: true,
          });
          dismiss();
          toast?.show({ message: 'Your bot is set up and ready to go!', variant: 'success' });
          onComplete?.();
        } catch {
          toast?.show({ message: 'Failed to save bot settings. Please try again.', variant: 'danger' });
          doneBtn.disabled = false;
          doneBtn.textContent = 'Set up my bot →';
        }
      });
      right.appendChild(doneBtn);
    }

    actions.append(left, right);
    card.appendChild(actions);
  };

  renderStep();
}
