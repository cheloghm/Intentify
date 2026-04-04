import { createBadge, createToastManager, ensureUiStyles } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const ORIGIN_HELPER_TEXT = 'Paste the website origin (scheme + host + port). No paths.';

const normalizeOrigin = (value) => {
  const input = typeof value === 'string' ? value.trim() : '';
  if (!input) {
    return { value: '', error: ORIGIN_HELPER_TEXT };
  }

  const withoutTrailingSlash = input.replace(/\/+$/, '');
  let normalized = withoutTrailingSlash;

  if (withoutTrailingSlash.includes('/')) {
    try {
      const parsed = new URL(withoutTrailingSlash);
      normalized = `${parsed.protocol}//${parsed.host}`;
    } catch (error) {
      normalized = withoutTrailingSlash;
    }
  }

  if (!/^https?:\/\//i.test(normalized)) {
    return { value: '', error: ORIGIN_HELPER_TEXT };
  }

  try {
    const parsed = new URL(normalized);
    if (!parsed.hostname) {
      return { value: '', error: ORIGIN_HELPER_TEXT };
    }
    return { value: `${parsed.protocol}//${parsed.host}`, error: '' };
  } catch (error) {
    return { value: '', error: ORIGIN_HELPER_TEXT };
  }
};

const normalizeOrigins = (origins) => {
  const unique = new Map();
  origins.forEach((origin) => {
    const normalizedResult = normalizeOrigin(origin);
    const normalized = normalizedResult.value;
    if (!normalized) return;
    const key = normalized.toLowerCase();
    if (!unique.has(key)) unique.set(key, normalized);
  });
  return Array.from(unique.values());
};

const copyToClipboard = async (value) => {
  if (navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(value);
    return;
  }
  const textarea = document.createElement('textarea');
  textarea.value = value;
  textarea.style.position = 'fixed';
  textarea.style.opacity = '0';
  document.body.appendChild(textarea);
  textarea.focus();
  textarea.select();
  document.execCommand('copy');
  textarea.remove();
};

const getSiteId = (site) => site.siteId || site.id;

const saveCachedKeys = (siteId, { siteKey }) => {
  if (!siteId || !siteKey) return;
  localStorage.setItem(
    `intentify.siteKeys.${siteId}`,
    JSON.stringify({ siteKey, cachedAtUtc: new Date().toISOString() })
  );
};

const createModal = (title) => {
  ensureUiStyles();
  const overlay = document.createElement('div');
  overlay.style.cssText = 'position:fixed;inset:0;background:rgba(15,23,42,0.5);z-index:200;display:flex;align-items:center;justify-content:center;padding:16px;';

  const dialog = document.createElement('div');
  dialog.className = 'card';
  dialog.style.cssText = 'width:100%;max-width:540px;max-height:90vh;overflow-y:auto;';

  const header = document.createElement('div');
  header.className = 'card-header';
  const heading = document.createElement('h3');
  heading.className = 'card-title';
  heading.textContent = title;
  const closeBtn = document.createElement('button');
  closeBtn.className = 'btn btn-secondary btn-sm';
  closeBtn.textContent = '✕';
  header.append(heading, closeBtn);

  const body = document.createElement('div');

  dialog.append(header, body);
  overlay.appendChild(dialog);

  const hide = () => overlay.remove();
  const show = () => document.body.appendChild(overlay);

  closeBtn.addEventListener('click', hide);
  overlay.addEventListener('click', (e) => { if (e.target === overlay) hide(); });

  return { overlay, body, show, hide, setTitle: (t) => { heading.textContent = t; } };
};

const makeFormGroup = (labelText, inputEl) => {
  const group = document.createElement('div');
  group.className = 'form-group';
  const label = document.createElement('label');
  label.className = 'form-label';
  label.textContent = labelText;
  group.append(label, inputEl);
  return group;
};

const makeInput = (placeholder = '', type = 'text') => {
  const input = document.createElement('input');
  input.type = type;
  input.className = 'form-input';
  input.placeholder = placeholder;
  return input;
};

const makeTextarea = (placeholder = '', rows = 4) => {
  const ta = document.createElement('textarea');
  ta.className = 'form-textarea';
  ta.placeholder = placeholder;
  ta.rows = rows;
  return ta;
};

const makeErrorEl = () => {
  const el = document.createElement('div');
  el.className = 'ui-field-error';
  return el;
};

export const renderSitesView = (container, { apiClient, toast } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();

  const state = {
    sites: [],
    loading: true,
    error: null,
  };

  const page = document.createElement('div');
  page.style.cssText = 'display:flex;flex-direction:column;gap:20px;width:100%;max-width:960px;';

  // ── Page Header ──────────────────────────────────────────────────────────
  const pageHeader = document.createElement('div');
  pageHeader.className = 'page-header';

  const headerLeft = document.createElement('div');
  const pageTitle = document.createElement('h2');
  pageTitle.className = 'page-title';
  pageTitle.textContent = 'Sites';
  const pageSubtitle = document.createElement('p');
  pageSubtitle.className = 'page-subtitle';
  pageSubtitle.textContent = 'Manage your tracked websites and widget configurations';
  headerLeft.append(pageTitle, pageSubtitle);

  const addSiteBtn = document.createElement('button');
  addSiteBtn.className = 'btn btn-primary';
  addSiteBtn.textContent = '+ Add Site';

  pageHeader.append(headerLeft, addSiteBtn);

  // ── Sites List ───────────────────────────────────────────────────────────
  const sitesListEl = document.createElement('div');
  sitesListEl.className = 'stack';

  // ── Fetch installation status ────────────────────────────────────────────
  const fetchInstallationStatus = async (siteId) => {
    if (!siteId) return null;
    try {
      return await client.request(`/sites/${siteId}/installation-status`);
    } catch {
      return null;
    }
  };

  // ── Manage Keys Modal ────────────────────────────────────────────────────
  const keysModal = createModal('Site Keys');

  const openKeysModal = async (site) => {
    const siteId = getSiteId(site);
    keysModal.setTitle(`Keys — ${site.domain || site.name || 'Site'}`);
    keysModal.body.innerHTML = '<div style="color:var(--color-text-muted);padding:16px 0;">Loading keys…</div>';
    keysModal.show();

    try {
      const keys = await client.sites.getKeys(siteId);
      keysModal.body.innerHTML = '';

      const warning = document.createElement('p');
      warning.style.cssText = 'font-size:12px;color:var(--color-warning);margin-bottom:16px;';
      warning.textContent = 'Keep these keys safe. Regenerating keys will invalidate the old ones.';
      keysModal.body.appendChild(warning);

      const makeKeyRow = (label, value) => {
        const row = document.createElement('div');
        row.className = 'form-group';
        const lbl = document.createElement('label');
        lbl.className = 'form-label';
        lbl.textContent = label;
        const inputRow = document.createElement('div');
        inputRow.style.cssText = 'display:flex;gap:8px;align-items:center;';
        const inp = document.createElement('input');
        inp.className = 'form-input';
        inp.value = value || '(not available)';
        inp.readOnly = true;
        inp.style.fontFamily = 'monospace';
        const copyBtn = document.createElement('button');
        copyBtn.className = 'btn btn-secondary btn-sm';
        copyBtn.textContent = 'Copy';
        copyBtn.style.flexShrink = '0';
        copyBtn.addEventListener('click', async () => {
          try {
            await copyToClipboard(value);
            notifier.show({ message: `${label} copied.`, variant: 'success' });
          } catch {
            notifier.show({ message: 'Unable to copy.', variant: 'danger' });
          }
        });
        inputRow.append(inp, copyBtn);
        row.append(lbl, inputRow);
        return row;
      };

      keysModal.body.append(
        makeKeyRow('Site Key (tracker / sdk)', keys.siteKey),
        makeKeyRow('Widget Key (engage widget)', keys.widgetKey),
      );

      const footer = document.createElement('div');
      footer.style.cssText = 'display:flex;gap:8px;margin-top:8px;';

      const installBtn = document.createElement('button');
      installBtn.className = 'btn btn-secondary btn-sm';
      installBtn.textContent = 'Open Install Guide';
      installBtn.addEventListener('click', () => {
        const params = new URLSearchParams({ siteId });
        if (site.domain) params.set('domain', site.domain);
        if (keys.siteKey) params.set('siteKey', keys.siteKey);
        window.location.hash = `#/install?${params.toString()}`;
        keysModal.hide();
      });

      const regenBtn = document.createElement('button');
      regenBtn.className = 'btn btn-danger btn-sm';
      regenBtn.textContent = 'Regenerate Keys';
      regenBtn.addEventListener('click', async () => {
        const confirmed = window.confirm('Regenerating keys will invalidate existing ones. Your installed tracker and widget will need updating. Continue?');
        if (!confirmed) return;
        regenBtn.disabled = true;
        regenBtn.textContent = 'Regenerating…';
        try {
          const response = await client.sites.regenerateKeys(siteId);
          saveCachedKeys(siteId, { siteKey: response.siteKey });
          notifier.show({ message: 'Keys regenerated.', variant: 'success' });
          keysModal.hide();
          // Re-open with fresh keys
          openKeysModal(site);
        } catch (error) {
          notifier.show({ message: mapApiError(error).message, variant: 'danger' });
          regenBtn.disabled = false;
          regenBtn.textContent = 'Regenerate Keys';
        }
      });

      footer.append(installBtn, regenBtn);
      keysModal.body.appendChild(footer);
    } catch (error) {
      keysModal.body.innerHTML = `<p style="color:var(--color-danger);">Failed to load keys: ${mapApiError(error).message}</p>`;
    }
  };

  // ── Edit Origins Modal ───────────────────────────────────────────────────
  const originsModal = createModal('Edit Origins');

  const openOriginsModal = (site) => {
    const siteId = getSiteId(site);
    originsModal.setTitle(`Origins — ${site.domain || site.name || 'Site'}`);
    originsModal.body.innerHTML = '';

    const originList = [...(site.allowedOrigins || [])];
    let originInput = '';
    let originInputError = '';
    let saving = false;

    const renderOriginsEditor = () => {
      originsModal.body.innerHTML = '';

      // Origins list
      const listHeader = document.createElement('p');
      listHeader.style.cssText = 'font-size:13px;font-weight:600;color:var(--color-text-secondary);margin-bottom:8px;';
      listHeader.textContent = 'Allowed Origins';
      originsModal.body.appendChild(listHeader);

      if (!originList.length) {
        const empty = document.createElement('div');
        empty.style.cssText = 'font-size:13px;color:var(--color-text-muted);margin-bottom:12px;';
        empty.textContent = 'No origins configured yet.';
        originsModal.body.appendChild(empty);
      } else {
        const listEl = document.createElement('div');
        listEl.style.cssText = 'display:flex;flex-direction:column;gap:6px;margin-bottom:12px;';
        originList.forEach((origin, idx) => {
          const row = document.createElement('div');
          row.style.cssText = 'display:flex;align-items:center;justify-content:space-between;gap:8px;padding:8px 10px;border:1px solid var(--color-border);border-radius:var(--radius-sm);';
          const text = document.createElement('span');
          text.style.cssText = 'font-size:13px;word-break:break-all;';
          text.textContent = origin;
          const removeBtn = document.createElement('button');
          removeBtn.className = 'btn btn-danger btn-sm';
          removeBtn.textContent = 'Remove';
          removeBtn.style.flexShrink = '0';
          removeBtn.addEventListener('click', () => {
            originList.splice(idx, 1);
            renderOriginsEditor();
          });
          row.append(text, removeBtn);
          listEl.appendChild(row);
        });
        originsModal.body.appendChild(listEl);
      }

      // Add origin input
      const addGroup = document.createElement('div');
      addGroup.className = 'form-group';
      const addLabel = document.createElement('label');
      addLabel.className = 'form-label';
      addLabel.textContent = 'Add Origin';
      const addRow = document.createElement('div');
      addRow.style.cssText = 'display:flex;gap:8px;';
      const addInput = makeInput('https://app.example.com');
      addInput.value = originInput;
      addInput.addEventListener('input', () => {
        originInput = addInput.value;
        originInputError = '';
      });

      const addBtn = document.createElement('button');
      addBtn.className = 'btn btn-secondary btn-sm';
      addBtn.textContent = 'Add';
      addBtn.style.flexShrink = '0';

      const addLocalhostBtn = document.createElement('button');
      addLocalhostBtn.className = 'btn btn-secondary btn-sm';
      addLocalhostBtn.textContent = 'Add localhost';
      addLocalhostBtn.style.flexShrink = '0';

      const errEl = makeErrorEl();
      errEl.textContent = originInputError;

      const addOriginToList = (raw) => {
        const result = normalizeOrigin(raw);
        if (!result.value) {
          originInputError = result.error;
          errEl.textContent = result.error;
          return;
        }
        if (originList.find((o) => o.toLowerCase() === result.value.toLowerCase())) {
          notifier.show({ message: 'Origin already listed.', variant: 'warning' });
          return;
        }
        originInput = '';
        originInputError = '';
        originList.push(result.value);
        renderOriginsEditor();
      };

      addBtn.addEventListener('click', () => addOriginToList(originInput));
      addLocalhostBtn.addEventListener('click', () => {
        if (window.location.hostname !== 'localhost') {
          errEl.textContent = 'Not running on localhost.';
          return;
        }
        addOriginToList(`http://localhost:${window.location.port || 80}`);
      });

      addRow.append(addInput, addBtn, addLocalhostBtn);
      addGroup.append(addLabel, addRow, errEl);
      originsModal.body.appendChild(addGroup);

      // Save button
      const footer = document.createElement('div');
      footer.style.cssText = 'display:flex;gap:8px;margin-top:8px;';
      const saveBtn = document.createElement('button');
      saveBtn.className = 'btn btn-primary';
      saveBtn.textContent = saving ? 'Saving…' : 'Save Origins';
      saveBtn.disabled = saving;
      saveBtn.addEventListener('click', async () => {
        saving = true;
        renderOriginsEditor();
        try {
          const normalized = normalizeOrigins(originList);
          const response = await client.request(`/sites/${siteId}/origins`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ allowedOrigins: normalized }),
          });
          const updatedSite = {
            ...site,
            ...response,
            allowedOrigins: response.allowedOrigins || normalized,
          };
          state.sites = state.sites.map((item) =>
            getSiteId(item) === siteId ? updatedSite : item
          );
          notifier.show({ message: 'Allowed origins updated.', variant: 'success' });
          originsModal.hide();
          renderSites();
        } catch (error) {
          notifier.show({ message: mapApiError(error).message, variant: 'danger' });
        } finally {
          saving = false;
          renderOriginsEditor();
        }
      });
      footer.appendChild(saveBtn);
      originsModal.body.appendChild(footer);
    };

    renderOriginsEditor();
    originsModal.show();
  };

  // ── Add Site Modal ───────────────────────────────────────────────────────
  const addSiteModal = createModal('Add Site');

  const buildAddSiteForm = () => {
    addSiteModal.body.innerHTML = '';

    const nameInput = makeInput('My Site');
    const domainInput = makeInput('example.com');
    const descInput = makeInput('What this site is about');
    const categoryInput = makeInput('e.g. Ecommerce');
    const tagsInput = makeInput('comma,separated,tags');

    const nameErr = makeErrorEl();
    const domainErr = makeErrorEl();

    const nameGroup = makeFormGroup('Name *', nameInput);
    nameGroup.appendChild(nameErr);
    const domainGroup = makeFormGroup('Domain *', domainInput);
    domainGroup.appendChild(domainErr);

    addSiteModal.body.append(
      nameGroup,
      domainGroup,
      makeFormGroup('Description', descInput),
      makeFormGroup('Category', categoryInput),
      makeFormGroup('Tags', tagsInput),
    );

    const footer = document.createElement('div');
    footer.style.cssText = 'display:flex;gap:8px;margin-top:8px;';
    const submitBtn = document.createElement('button');
    submitBtn.className = 'btn btn-primary';
    submitBtn.textContent = 'Create Site';

    if (state.sites.length > 0) {
      submitBtn.disabled = true;
      submitBtn.textContent = 'Site limit reached';
    }

    submitBtn.addEventListener('click', async () => {
      nameErr.textContent = '';
      domainErr.textContent = '';
      const name = nameInput.value.trim();
      const domain = domainInput.value.trim();
      if (!name) { nameErr.textContent = 'Name is required.'; return; }
      if (!domain) { domainErr.textContent = 'Domain is required.'; return; }

      submitBtn.disabled = true;
      submitBtn.textContent = 'Creating…';
      try {
        const response = await client.sites.create({
          name,
          domain,
          description: descInput.value.trim(),
          category: categoryInput.value.trim(),
          tags: tagsInput.value.split(',').map((t) => t.trim()).filter(Boolean),
        });
        const siteId = response.siteId || response.id;
        const status = await fetchInstallationStatus(siteId);
        state.sites = [{
          ...response,
          siteId,
          installationStatus: status || response.installationStatus,
        }, ...state.sites];
        if (response.siteKey) {
          saveCachedKeys(siteId, { siteKey: response.siteKey });
        }
        notifier.show({ message: 'Site created.', variant: 'success' });
        addSiteModal.hide();
        renderSites();
      } catch (error) {
        const uiError = mapApiError(error);
        const domainMsg = uiError.details?.errors?.domain?.[0];
        if (domainMsg) domainErr.textContent = domainMsg;
        notifier.show({ message: uiError.message, variant: 'danger' });
        submitBtn.disabled = false;
        submitBtn.textContent = 'Create Site';
      }
    });

    footer.appendChild(submitBtn);
    addSiteModal.body.appendChild(footer);
  };

  addSiteBtn.addEventListener('click', () => {
    buildAddSiteForm();
    addSiteModal.show();
  });

  // ── Render Sites ─────────────────────────────────────────────────────────
  const renderSites = () => {
    sitesListEl.innerHTML = '';

    if (state.loading) {
      const loading = document.createElement('div');
      loading.style.cssText = 'color:var(--color-text-muted);padding:24px 0;';
      loading.textContent = 'Loading sites…';
      sitesListEl.appendChild(loading);
      return;
    }

    if (state.error) {
      const err = document.createElement('div');
      err.style.cssText = 'color:var(--color-danger);padding:16px 0;';
      err.textContent = state.error;
      sitesListEl.appendChild(err);
      return;
    }

    if (!state.sites.length) {
      const empty = document.createElement('div');
      empty.className = 'card';
      const emptyBody = document.createElement('div');
      emptyBody.className = 'empty-state';
      emptyBody.innerHTML = '<div class="empty-state-icon">🌐</div><div class="empty-state-title">No sites yet</div><div class="empty-state-desc">Add your first site to start tracking visitors and deploying the chat widget.</div>';
      const emptyBtn = document.createElement('button');
      emptyBtn.className = 'btn btn-primary';
      emptyBtn.textContent = '+ Add Your First Site';
      emptyBtn.style.marginTop = '16px';
      emptyBtn.addEventListener('click', () => {
        buildAddSiteForm();
        addSiteModal.show();
      });
      emptyBody.appendChild(emptyBtn);
      empty.appendChild(emptyBody);
      sitesListEl.appendChild(empty);
      return;
    }

    state.sites.forEach((site) => {
      const siteId = getSiteId(site);
      const card = document.createElement('div');
      card.className = 'card';

      // ── Card header row ──
      const cardHeader = document.createElement('div');
      cardHeader.className = 'card-header';

      const headerLeft = document.createElement('div');
      const domainTitle = document.createElement('h3');
      domainTitle.className = 'card-title';
      domainTitle.textContent = site.domain || site.name || 'Unnamed site';
      const nameSubtitle = document.createElement('div');
      nameSubtitle.className = 'card-subtitle';
      nameSubtitle.textContent = site.name || '';
      headerLeft.append(domainTitle, nameSubtitle);

      const installStatus = site.installationStatus;
      const isConfigured = installStatus?.isConfigured ?? ((site.allowedOrigins || []).length > 0);
      const statusBadge = createBadge({
        text: isConfigured ? 'Configured' : 'Not configured',
        variant: isConfigured ? 'success' : 'warning',
      });

      cardHeader.append(headerLeft, statusBadge);

      // ── Site ID row ──
      const idRow = document.createElement('div');
      idRow.style.cssText = 'display:flex;align-items:center;gap:8px;margin-bottom:12px;';
      const idLabel = document.createElement('span');
      idLabel.style.cssText = 'font-size:11px;color:var(--color-text-muted);font-weight:500;';
      idLabel.textContent = 'Site ID:';
      const idValue = document.createElement('span');
      idValue.style.cssText = 'font-family:monospace;font-size:12px;color:var(--color-text-secondary);';
      idValue.textContent = siteId || '—';
      const copyIdBtn = document.createElement('button');
      copyIdBtn.className = 'btn btn-secondary btn-sm';
      copyIdBtn.textContent = 'Copy';
      copyIdBtn.addEventListener('click', async () => {
        try {
          await copyToClipboard(siteId);
          notifier.show({ message: 'Site ID copied.', variant: 'success' });
        } catch {
          notifier.show({ message: 'Unable to copy.', variant: 'danger' });
        }
      });
      idRow.append(idLabel, idValue, copyIdBtn);

      // ── Stats row ──
      const statsRow = document.createElement('div');
      statsRow.style.cssText = 'display:flex;gap:24px;margin-bottom:16px;';
      const makeStatItem = (label, value) => {
        const item = document.createElement('div');
        const itemLabel = document.createElement('div');
        itemLabel.style.cssText = 'font-size:11px;color:var(--color-text-muted);font-weight:500;text-transform:uppercase;letter-spacing:0.05em;';
        itemLabel.textContent = label;
        const itemValue = document.createElement('div');
        itemValue.style.cssText = 'font-size:13px;color:var(--color-text-secondary);margin-top:2px;';
        itemValue.textContent = value;
        item.append(itemLabel, itemValue);
        return item;
      };
      const originsCount = (site.allowedOrigins || []).length;
      const createdDate = site.createdAtUtc
        ? new Date(site.createdAtUtc).toLocaleDateString()
        : '—';
      statsRow.append(
        makeStatItem('Allowed Origins', originsCount),
        makeStatItem('Created', createdDate),
      );

      // ── Action buttons ──
      const actions = document.createElement('div');
      actions.style.cssText = 'display:flex;gap:8px;flex-wrap:wrap;';

      const manageKeysBtn = document.createElement('button');
      manageKeysBtn.className = 'btn btn-secondary btn-sm';
      manageKeysBtn.textContent = 'Manage Keys';
      manageKeysBtn.addEventListener('click', () => openKeysModal(site));

      const editOriginsBtn = document.createElement('button');
      editOriginsBtn.className = 'btn btn-secondary btn-sm';
      editOriginsBtn.textContent = 'Edit Origins';
      editOriginsBtn.addEventListener('click', () => openOriginsModal(site));

      const deleteBtn = document.createElement('button');
      deleteBtn.className = 'btn btn-danger btn-sm';
      deleteBtn.textContent = 'Delete';
      deleteBtn.addEventListener('click', async () => {
        const confirmed = window.confirm(
          'Deleting this site will permanently delete all knowledge sources and their indexed content. This cannot be undone.'
        );
        if (!confirmed) return;
        deleteBtn.disabled = true;
        deleteBtn.textContent = 'Deleting…';
        try {
          await client.sites.delete(siteId);
          state.sites = state.sites.filter((item) => getSiteId(item) !== siteId);
          notifier.show({ message: 'Site deleted.', variant: 'success' });
          renderSites();
        } catch (error) {
          notifier.show({ message: mapApiError(error).message, variant: 'danger' });
          deleteBtn.disabled = false;
          deleteBtn.textContent = 'Delete';
        }
      });

      actions.append(manageKeysBtn, editOriginsBtn, deleteBtn);

      card.append(cardHeader, idRow, statsRow, actions);
      sitesListEl.appendChild(card);
    });
  };

  // ── Load Sites ───────────────────────────────────────────────────────────
  const loadSites = async () => {
    state.loading = true;
    state.error = null;
    renderSites();

    try {
      const sites = await client.sites.list();
      state.sites = Array.isArray(sites) ? sites : [];
      state.loading = false;
      renderSites();

      await Promise.all(
        state.sites.map(async (site) => {
          const siteId = getSiteId(site);
          const status = await fetchInstallationStatus(siteId);
          if (status) {
            state.sites = state.sites.map((item) =>
              getSiteId(item) === siteId ? { ...item, installationStatus: status } : item
            );
          }
        })
      );
      renderSites();
    } catch (error) {
      state.loading = false;
      state.error = mapApiError(error).message;
      renderSites();
    }
  };

  page.append(pageHeader, sitesListEl);
  container.appendChild(page);

  loadSites();
};
