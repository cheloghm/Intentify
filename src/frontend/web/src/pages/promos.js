import { createCard, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const button = (label, primary = false) => {
  const b = document.createElement('button');
  b.type = 'button';
  b.textContent = label;
  b.style.padding = '8px 12px';
  b.style.borderRadius = '6px';
  b.style.border = primary ? 'none' : '1px solid #cbd5e1';
  b.style.background = primary ? '#2563eb' : '#fff';
  b.style.color = primary ? '#fff' : '#1e293b';
  b.style.cursor = 'pointer';
  return b;
};

const getSiteId = (site) => site?.siteId || site?.id || '';
const getPromoId = (promo) => promo?.id || promo?.promoId || '';

const SUPPORTED_WIDGET_QUESTION_TYPES = ['text', 'email', 'phone', 'textarea', 'checkbox'];
const normalizeQuestionType = (value) => {
  const normalized = String(value || '').trim().toLowerCase();
  return SUPPORTED_WIDGET_QUESTION_TYPES.includes(normalized) ? normalized : 'text';
};

export const renderPromosView = (container, { apiClient, toast } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();
  const state = { sites: [], promos: [], entries: [], siteId: '', selectedPromoId: '', selectedPromo: null, questions: [] };

  const page = document.createElement('div');
  page.style.display = 'flex';
  page.style.flexDirection = 'column';
  page.style.gap = '16px';
  page.style.width = '100%';
  page.style.maxWidth = '1000px';

  const header = document.createElement('h2');
  header.textContent = 'Promos';
  header.style.margin = '0';
  page.appendChild(header);

  const createBody = document.createElement('form');
  createBody.style.display = 'grid';
  createBody.style.gridTemplateColumns = '1fr 1fr';
  createBody.style.gap = '8px';

  const siteSelect = document.createElement('select');
  const nameInput = document.createElement('input');
  const descriptionInput = document.createElement('input');
  const flyerInput = document.createElement('input');
  flyerInput.type = 'file';
  flyerInput.accept = 'image/*,.pdf';

  [siteSelect, nameInput, descriptionInput, flyerInput].forEach((el) => {
    el.style.padding = '8px 10px';
    el.style.border = '1px solid #cbd5e1';
    el.style.borderRadius = '6px';
  });

  nameInput.placeholder = 'Promo name';
  descriptionInput.placeholder = 'Description';

  const questionRows = document.createElement('div');
  questionRows.style.display = 'flex';
  questionRows.style.flexDirection = 'column';
  questionRows.style.gap = '6px';
  questionRows.style.gridColumn = '1 / -1';

  const addQuestionBtn = button('Add question');
  addQuestionBtn.style.gridColumn = '1 / -1';
  addQuestionBtn.addEventListener('click', () => {
    state.questions.push({ key: '', label: '', type: 'text', required: false });
    renderQuestions();
  });

  const createBtn = button('Create promo', true);
  createBtn.type = 'submit';
  createBody.append(siteSelect, nameInput, descriptionInput, flyerInput, addQuestionBtn, questionRows, createBtn);

  const promosBody = document.createElement('div');
  promosBody.style.display = 'flex';
  promosBody.style.flexDirection = 'column';
  promosBody.style.gap = '8px';

  const modal = document.createElement('div');
  modal.style.position = 'fixed';
  modal.style.inset = '0';
  modal.style.background = 'rgba(15, 23, 42, 0.45)';
  modal.style.display = 'none';
  modal.style.alignItems = 'center';
  modal.style.justifyContent = 'center';
  modal.style.zIndex = '1000';

  const modalCard = document.createElement('div');
  modalCard.style.background = '#fff';
  modalCard.style.borderRadius = '10px';
  modalCard.style.width = 'min(920px, 95vw)';
  modalCard.style.maxHeight = '90vh';
  modalCard.style.overflow = 'auto';
  modalCard.style.padding = '16px';
  modalCard.style.display = 'flex';
  modalCard.style.flexDirection = 'column';
  modalCard.style.gap = '10px';

  const modalHeader = document.createElement('div');
  modalHeader.style.display = 'flex';
  modalHeader.style.justifyContent = 'space-between';
  const modalTitle = document.createElement('h3');
  modalTitle.style.margin = '0';
  const closeBtn = button('Close');
  closeBtn.addEventListener('click', () => { modal.style.display = 'none'; });
  modalHeader.append(modalTitle, closeBtn);

  const modalInfo = document.createElement('div');
  const modalFlyer = document.createElement('div');
  const entriesBody = document.createElement('div');
  entriesBody.style.display = 'flex';
  entriesBody.style.flexDirection = 'column';
  entriesBody.style.gap = '8px';

  const exportBtn = button('Export CSV');
  exportBtn.addEventListener('click', async () => {
    if (!state.selectedPromoId) {
      notifier.show({ message: 'Select a promo first.', variant: 'warning' });
      return;
    }

    try {
      const blob = await client.promos.downloadCsv(state.selectedPromoId);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `promo-${state.selectedPromoId}-entries.csv`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  });

  modalCard.append(modalHeader, modalInfo, modalFlyer, exportBtn, entriesBody);
  modal.appendChild(modalCard);
  page.appendChild(modal);

  const renderQuestions = () => {
    questionRows.innerHTML = '';
    if (!state.questions.length) {
      const empty = document.createElement('div');
      empty.textContent = 'No questions configured.';
      empty.style.color = '#64748b';
      questionRows.appendChild(empty);
      return;
    }

    state.questions.forEach((question, index) => {
      const row = document.createElement('div');
      row.style.display = 'grid';
      row.style.gridTemplateColumns = '1fr 1fr 120px 100px auto';
      row.style.gap = '6px';

      const keyInput = document.createElement('input');
      keyInput.placeholder = 'key';
      keyInput.value = question.key;
      keyInput.addEventListener('input', () => { question.key = keyInput.value; });

      const labelInput = document.createElement('input');
      labelInput.placeholder = 'label';
      labelInput.value = question.label;
      labelInput.addEventListener('input', () => { question.label = labelInput.value; });

      const typeInput = document.createElement('select');
      SUPPORTED_WIDGET_QUESTION_TYPES.forEach((type) => {
        const option = document.createElement('option');
        option.value = type;
        option.textContent = type;
        typeInput.appendChild(option);
      });
      typeInput.value = normalizeQuestionType(question.type);
      typeInput.addEventListener('change', () => { question.type = normalizeQuestionType(typeInput.value); });

      const requiredInput = document.createElement('label');
      requiredInput.style.display = 'flex';
      requiredInput.style.alignItems = 'center';
      requiredInput.style.gap = '4px';
      const requiredCheckbox = document.createElement('input');
      requiredCheckbox.type = 'checkbox';
      requiredCheckbox.checked = question.required;
      requiredCheckbox.addEventListener('change', () => { question.required = requiredCheckbox.checked; });
      requiredInput.append(requiredCheckbox, document.createTextNode('Required'));

      const removeBtn = button('Remove');
      removeBtn.addEventListener('click', () => {
        state.questions.splice(index, 1);
        renderQuestions();
      });

      [keyInput, labelInput, typeInput].forEach((el) => {
        el.style.padding = '8px 10px';
        el.style.border = '1px solid #cbd5e1';
        el.style.borderRadius = '6px';
      });

      row.append(keyInput, labelInput, typeInput, requiredInput, removeBtn);
      questionRows.appendChild(row);
    });
  };

  const renderSites = () => {
    siteSelect.innerHTML = '';
    const empty = document.createElement('option');
    empty.value = '';
    empty.textContent = state.sites.length ? 'Select site' : 'No sites';
    siteSelect.appendChild(empty);
    state.sites.forEach((site) => {
      const opt = document.createElement('option');
      opt.value = getSiteId(site);
      opt.textContent = site.domain || opt.value;
      siteSelect.appendChild(opt);
    });
    siteSelect.value = state.siteId;
  };

  const renderPromos = () => {
    promosBody.innerHTML = '';
    if (!state.promos.length) {
      const empty = document.createElement('div');
      empty.textContent = 'No promos found.';
      empty.style.color = '#64748b';
      promosBody.appendChild(empty);
      return;
    }

    state.promos.forEach((promo) => {
      const row = document.createElement('div');
      row.style.display = 'flex';
      row.style.justifyContent = 'space-between';
      row.style.alignItems = 'center';
      row.style.padding = '8px';
      row.style.border = '1px solid #e2e8f0';
      row.style.borderRadius = '6px';

      const left = document.createElement('div');
      left.textContent = `${promo.name} · ${promo.publicKey}`;
      const viewBtn = button('Details');
      viewBtn.addEventListener('click', async () => {
        state.selectedPromoId = getPromoId(promo);
        await loadPromoDetail();
      });

      row.append(left, viewBtn);
      promosBody.appendChild(row);
    });
  };

  const renderEntries = () => {
    entriesBody.innerHTML = '';
    if (!state.entries.length) {
      const empty = document.createElement('div');
      empty.textContent = 'No entries.';
      empty.style.color = '#64748b';
      entriesBody.appendChild(empty);
      return;
    }

    const table = document.createElement('table');
    table.style.width = '100%';
    table.style.borderCollapse = 'collapse';
    table.innerHTML = '<thead><tr><th>Email</th><th>Name</th><th>Visitor</th><th>Answers</th><th>Created</th></tr></thead>';
    const body = document.createElement('tbody');
    state.entries.forEach((entry) => {
      const tr = document.createElement('tr');
      const answerText = Object.entries(entry.answers || {})
        .map(([key, value]) => `${key}: ${value}`)
        .join('; ');
      tr.innerHTML = `<td>${entry.email || ''}</td><td>${entry.name || ''}</td><td>${entry.visitorId || ''}</td><td>${answerText}</td><td>${entry.createdAtUtc || ''}</td>`;
      Array.from(tr.children).forEach((td) => {
        td.style.borderTop = '1px solid #e2e8f0';
        td.style.padding = '6px';
      });
      body.appendChild(tr);
    });
    table.appendChild(body);
    entriesBody.appendChild(table);
  };

  const loadPromos = async () => {
    try {
      state.promos = await client.promos.list(state.siteId || undefined);
      renderPromos();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  const loadPromoDetail = async () => {
    if (!state.selectedPromoId) return;

    try {
      const detail = await client.promos.getDetail(state.selectedPromoId);
      state.selectedPromo = detail.promo || detail;
      state.entries = detail.entries || [];

      modalTitle.textContent = state.selectedPromo?.name || 'Promo detail';
      modalInfo.textContent = state.selectedPromo?.description || 'No description';

      if (state.selectedPromo?.flyerFileName) {
        modalFlyer.innerHTML = '';
        const flyerName = document.createElement('div');
        flyerName.textContent = `Flyer: ${state.selectedPromo.flyerFileName}`;

        const flyerDownload = button('Download flyer');
        flyerDownload.addEventListener('click', async () => {
          try {
            const blob = await client.promos.downloadFlyer(state.selectedPromoId);
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = state.selectedPromo.flyerFileName || 'flyer';
            a.click();
            URL.revokeObjectURL(url);
          } catch (error) {
            notifier.show({ message: mapApiError(error).message, variant: 'danger' });
          }
        });

        modalFlyer.append(flyerName, flyerDownload);
      } else {
        modalFlyer.textContent = 'No flyer uploaded.';
      }

      renderEntries();
      modal.style.display = 'flex';
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  siteSelect.addEventListener('change', async () => {
    state.siteId = siteSelect.value;
    await loadPromos();
  });

  createBody.addEventListener('submit', async (event) => {
    event.preventDefault();
    if (!siteSelect.value || !nameInput.value.trim()) {
      notifier.show({ message: 'Site and name are required.', variant: 'warning' });
      return;
    }

    try {
      const formData = new FormData();
      formData.append('siteId', siteSelect.value);
      formData.append('name', nameInput.value.trim());
      formData.append('description', descriptionInput.value.trim());
      formData.append('isActive', 'true');
      formData.append('questions', JSON.stringify(state.questions.map((question, index) => ({
        key: question.key.trim(),
        label: question.label.trim() || question.key.trim(),
        type: normalizeQuestionType(question.type),
        required: Boolean(question.required),
        order: index,
      })).filter((question) => question.key)));

      if (flyerInput.files?.[0]) {
        formData.append('flyer', flyerInput.files[0]);
      }

      await client.promos.create(formData);
      nameInput.value = '';
      descriptionInput.value = '';
      flyerInput.value = '';
      state.questions = [];
      renderQuestions();
      await loadPromos();
      notifier.show({ message: 'Promo created.', variant: 'success' });
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  });

  const init = async () => {
    try {
      state.sites = await client.sites.list();
      state.siteId = getSiteId(state.sites[0]);
      renderSites();
      renderQuestions();
      await loadPromos();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  page.append(
    createCard({ title: 'Create promo', body: createBody }),
    createCard({ title: 'Promos', body: promosBody })
  );

  container.appendChild(page);
  init();
};
