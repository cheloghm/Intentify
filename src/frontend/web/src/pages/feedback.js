const injectStyles = () => {
  if (document.getElementById('_fb_css')) return;
  const s = document.createElement('style');
  s.id = '_fb_css';
  s.textContent = `
.fb-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;gap:28px;width:100%;max-width:680px}
.fb-title{font-size:22px;font-weight:800;color:#0f172a;letter-spacing:-.02em;margin-bottom:4px}
.fb-sub{font-size:14px;color:#64748b}
.fb-section{background:#fff;border:1px solid #e2e8f0;border-radius:14px;padding:24px 28px}
.fb-section-title{font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:.07em;color:#94a3b8;margin-bottom:16px}
.fb-types{display:grid;grid-template-columns:repeat(3,1fr);gap:10px;margin-bottom:20px}
@media(max-width:500px){.fb-types{grid-template-columns:1fr}}
.fb-type-card{border:1.5px solid #e2e8f0;border-radius:10px;padding:14px 12px;cursor:pointer;transition:all .15s;text-align:center;background:#f8fafc}
.fb-type-card:hover{border-color:#a5b4fc;background:#eef2ff}
.fb-type-card.sel{border-color:#6366f1;background:rgba(99,102,241,.07)}
.fb-type-icon{font-size:22px;margin-bottom:6px}
.fb-type-label{font-size:12.5px;font-weight:700;color:#334155}
.fb-type-desc{font-size:11px;color:#64748b;margin-top:3px}
.fb-field{margin-bottom:14px}
.fb-label{display:block;font-size:11.5px;font-weight:600;color:#475569;margin-bottom:5px}
.fb-input,.fb-textarea,.fb-select{width:100%;font-family:inherit;font-size:13.5px;color:#1e293b;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:9px 12px;outline:none;transition:border-color .14s}
.fb-textarea{resize:vertical;min-height:100px}
.fb-input:focus,.fb-textarea:focus,.fb-select:focus{border-color:#6366f1;background:#fff;box-shadow:0 0 0 3px rgba(99,102,241,.08)}
.fb-priority-wrap{display:none}
.fb-btn{padding:10px 20px;background:#6366f1;color:#fff;border:none;border-radius:8px;font-size:13.5px;font-weight:600;cursor:pointer;font-family:inherit;transition:background .14s}
.fb-btn:hover:not(:disabled){background:#4f46e5}
.fb-btn:disabled{opacity:.55;cursor:not-allowed}
.fb-success{font-size:13.5px;color:#10b981;font-weight:600;margin-top:12px;display:none}
.fb-list{display:flex;flex-direction:column;gap:10px}
.fb-item{background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:14px 16px}
.fb-item-header{display:flex;align-items:center;gap:8px;margin-bottom:6px}
.fb-item-badge{font-size:10px;font-weight:700;letter-spacing:.06em;text-transform:uppercase;padding:2px 8px;border-radius:99px;background:#eef2ff;color:#6366f1}
.fb-item-badge.bug{background:#fef2f2;color:#ef4444}
.fb-item-badge.general{background:#f0fdf4;color:#10b981}
.fb-item-title{font-size:13.5px;font-weight:700;color:#1e293b;flex:1}
.fb-item-desc{font-size:12.5px;color:#475569;line-height:1.5}
.fb-item-meta{font-size:11px;color:#94a3b8;margin-top:6px}
.fb-empty{text-align:center;color:#94a3b8;font-size:13px;padding:20px 0}
`;
  document.head.appendChild(s);
};

const TYPE_OPTIONS = [
  { id: 'feature', icon: '💡', label: 'Feature Request', desc: 'Suggest an improvement' },
  { id: 'bug',     icon: '🐛', label: 'Bug Report',       desc: 'Something is broken' },
  { id: 'general', icon: '💬', label: 'General Feedback', desc: 'Thoughts or questions' },
];

const PRIORITY_OPTIONS = ['Low', 'Medium', 'High', 'Critical'];

const LOCAL_KEY = 'hven_feedback_submissions';

const loadLocal = () => { try { return JSON.parse(localStorage.getItem(LOCAL_KEY) || '[]'); } catch { return []; } };
const saveLocal = (items) => { try { localStorage.setItem(LOCAL_KEY, JSON.stringify(items)); } catch {} };

export function renderFeedbackView(container, { apiClient, toast } = {}) {
  injectStyles();

  const root = document.createElement('div');
  root.className = 'fb-root';

  // Header
  const header = document.createElement('div');
  header.innerHTML = `<div class="fb-title">Feedback</div><div class="fb-sub">Help us improve Hven — every submission is reviewed by the team.</div>`;
  root.appendChild(header);

  // ── Submit form ──────────────────────────────────────────────────────────────
  const formSection = document.createElement('div');
  formSection.className = 'fb-section';

  const formTitle = document.createElement('div');
  formTitle.className = 'fb-section-title';
  formTitle.textContent = 'Submit feedback';
  formSection.appendChild(formTitle);

  // Type selector
  let selectedType = 'feature';
  const typesGrid = document.createElement('div');
  typesGrid.className = 'fb-types';

  const typeCards = TYPE_OPTIONS.map(opt => {
    const card = document.createElement('div');
    card.className = 'fb-type-card' + (opt.id === selectedType ? ' sel' : '');
    card.innerHTML = `<div class="fb-type-icon">${opt.icon}</div><div class="fb-type-label">${opt.label}</div><div class="fb-type-desc">${opt.desc}</div>`;
    card.addEventListener('click', () => {
      selectedType = opt.id;
      typeCards.forEach(c => c.classList.remove('sel'));
      card.classList.add('sel');
      priorityWrap.style.display = selectedType === 'bug' ? '' : 'none';
    });
    return card;
  });
  typeCards.forEach(c => typesGrid.appendChild(c));
  formSection.appendChild(typesGrid);

  // Title field
  const titleField = document.createElement('div');
  titleField.className = 'fb-field';
  titleField.innerHTML = `<label class="fb-label">Title</label>`;
  const titleInput = document.createElement('input');
  titleInput.type = 'text';
  titleInput.className = 'fb-input';
  titleInput.placeholder = 'Brief summary of your feedback';
  titleField.appendChild(titleInput);
  formSection.appendChild(titleField);

  // Description field
  const descField = document.createElement('div');
  descField.className = 'fb-field';
  descField.innerHTML = `<label class="fb-label">Description</label>`;
  const descArea = document.createElement('textarea');
  descArea.className = 'fb-textarea';
  descArea.placeholder = 'Tell us more…';
  descField.appendChild(descArea);
  formSection.appendChild(descField);

  // Priority (bugs only)
  const priorityWrap = document.createElement('div');
  priorityWrap.className = 'fb-field fb-priority-wrap';
  priorityWrap.innerHTML = `<label class="fb-label">Priority</label>`;
  const prioritySelect = document.createElement('select');
  prioritySelect.className = 'fb-select';
  PRIORITY_OPTIONS.forEach(p => {
    const opt = document.createElement('option');
    opt.value = p.toLowerCase(); opt.textContent = p;
    prioritySelect.appendChild(opt);
  });
  prioritySelect.value = 'medium';
  priorityWrap.appendChild(prioritySelect);
  formSection.appendChild(priorityWrap);

  // Submit button + success
  const submitBtn = document.createElement('button');
  submitBtn.className = 'fb-btn';
  submitBtn.textContent = 'Submit feedback';

  const successMsg = document.createElement('div');
  successMsg.className = 'fb-success';
  successMsg.textContent = 'Thanks! Your feedback has been submitted.';

  submitBtn.addEventListener('click', async () => {
    const title = titleInput.value.trim();
    const description = descArea.value.trim();
    if (!title) { titleInput.style.borderColor = '#ef4444'; titleInput.focus(); return; }
    titleInput.style.borderColor = '';

    const payload = {
      type: selectedType,
      title,
      description,
      priority: selectedType === 'bug' ? prioritySelect.value : undefined,
      submittedAt: new Date().toISOString(),
    };

    submitBtn.disabled = true; submitBtn.textContent = 'Submitting…';
    let saved = false;
    try {
      if (apiClient?.feedback?.submit) {
        await apiClient.feedback.submit(payload);
        saved = true;
      }
    } catch (_) {}

    if (!saved) {
      const local = loadLocal();
      local.unshift({ ...payload, id: crypto.randomUUID(), status: 'pending' });
      saveLocal(local);
    }

    submitBtn.disabled = false; submitBtn.textContent = 'Submit feedback';
    titleInput.value = ''; descArea.value = '';
    successMsg.style.display = 'block';
    setTimeout(() => { successMsg.style.display = 'none'; }, 4000);
    renderRecent();
  });

  formSection.append(submitBtn, successMsg);
  root.appendChild(formSection);

  // ── Recent submissions ───────────────────────────────────────────────────────
  const recentSection = document.createElement('div');
  recentSection.className = 'fb-section';

  const recentTitle = document.createElement('div');
  recentTitle.className = 'fb-section-title';
  recentTitle.textContent = 'Your recent submissions';
  recentSection.appendChild(recentTitle);

  const listWrap = document.createElement('div');
  listWrap.className = 'fb-list';
  recentSection.appendChild(listWrap);
  root.appendChild(recentSection);

  const renderRecent = () => {
    listWrap.replaceChildren();
    const items = loadLocal().slice(0, 10);
    if (!items.length) {
      listWrap.innerHTML = `<div class="fb-empty">No submissions yet.</div>`;
      return;
    }
    items.forEach(item => {
      const typeOpt = TYPE_OPTIONS.find(t => t.id === item.type) || TYPE_OPTIONS[0];
      const el = document.createElement('div');
      el.className = 'fb-item';
      const badgeClass = item.type === 'bug' ? 'bug' : item.type === 'general' ? 'general' : '';
      el.innerHTML = `
        <div class="fb-item-header">
          <span class="fb-item-badge ${badgeClass}">${typeOpt.icon} ${typeOpt.label}</span>
          <div class="fb-item-title">${item.title}</div>
        </div>
        ${item.description ? `<div class="fb-item-desc">${item.description}</div>` : ''}
        <div class="fb-item-meta">${item.submittedAt ? new Date(item.submittedAt).toLocaleString() : ''}${item.priority ? ` · Priority: ${item.priority}` : ''}</div>
      `;
      listWrap.appendChild(el);
    });
  };

  renderRecent();
  container.appendChild(root);
}
