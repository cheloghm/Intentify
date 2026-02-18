import { createBadge, createCard, createInput, createToastManager } from '../shared/ui/index.js';
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
    if (!normalized) {
      return;
    }

    const key = normalized.toLowerCase();
    if (!unique.has(key)) {
      unique.set(key, normalized);
    }
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

const createButton = ({ label, variant = 'default', type = 'button' } = {}) => {
  const button = document.createElement('button');
  button.type = type;
  button.textContent = label;
  button.style.padding = '8px 12px';
  button.style.borderRadius = '6px';
  button.style.border = variant === 'primary' ? 'none' : '1px solid #e2e8f0';
  button.style.background = variant === 'primary' ? '#2563eb' : '#ffffff';
  button.style.color = variant === 'primary' ? '#ffffff' : '#1e293b';
  button.style.cursor = 'pointer';
  button.style.fontSize = '13px';
  return button;
};

const createField = ({ label, placeholder }) => {
  const { wrapper, input } = createInput({ label, placeholder });
  const error = document.createElement('div');
  error.className = 'ui-field-error';
  wrapper.appendChild(error);
  return { wrapper, input, error };
};

const getSiteId = (site) => site.siteId || site.id;

const loadCachedKeys = (siteId) => {
  if (!siteId) {
    return null;
  }

  try {
    const raw = localStorage.getItem(`intentify.siteKeys.${siteId}`);
    if (!raw) {
      return null;
    }

    const parsed = JSON.parse(raw);
    if (
      parsed &&
      typeof parsed.siteKey === 'string' &&
      typeof parsed.widgetKey === 'string' &&
      parsed.siteKey &&
      parsed.widgetKey
    ) {
      return parsed;
    }
  } catch (error) {
    return null;
  }

  return null;
};

const saveCachedKeys = (siteId, { siteKey, widgetKey }) => {
  if (!siteId || !siteKey || !widgetKey) {
    return;
  }

  localStorage.setItem(
    `intentify.siteKeys.${siteId}`,
    JSON.stringify({
      siteKey,
      widgetKey,
      cachedAtUtc: new Date().toISOString(),
    })
  );
};

export const renderSitesView = (container, { apiClient, toast } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();

  const state = {
    sites: [],
    loading: true,
    error: null,
    keys: null,
    expandedSiteId: null,
    originsDraftBySiteId: {},
    originInputBySiteId: {},
    originInputErrorBySiteId: {},
    savingBySiteId: {},
    errorBySiteId: {},
    originEditingIndexBySiteId: {},
    originEditingValueBySiteId: {},
  };

  const page = document.createElement('div');
  page.style.display = 'flex';
  page.style.flexDirection = 'column';
  page.style.gap = '20px';
  page.style.width = '100%';
  page.style.maxWidth = '960px';

  const header = document.createElement('div');
  const title = document.createElement('h2');
  title.textContent = 'Sites';
  title.style.margin = '0';
  const subtitle = document.createElement('p');
  subtitle.textContent = 'Manage domains, keys, and installation status for your sites.';
  subtitle.style.margin = '6px 0 0';
  subtitle.style.color = '#64748b';
  header.append(title, subtitle);

  const keysSection = document.createElement('div');

  const renderKeys = () => {
    keysSection.innerHTML = '';
    if (!state.keys) {
      return;
    }

    const { domain, siteId, siteKey, widgetKey } = state.keys;

    const body = document.createElement('div');
    body.style.display = 'flex';
    body.style.flexDirection = 'column';
    body.style.gap = '12px';

    const warning = document.createElement('div');
    warning.textContent = 'Keys are shown once. Save them now.';
    warning.style.color = '#b45309';
    warning.style.fontSize = '13px';
    body.appendChild(warning);

    const createKeyRow = ({ label, value }) => {
      const row = document.createElement('div');
      row.style.display = 'flex';
      row.style.justifyContent = 'space-between';
      row.style.alignItems = 'center';
      row.style.gap = '12px';
      row.style.padding = '10px 12px';
      row.style.border = '1px solid #e2e8f0';
      row.style.borderRadius = '8px';

      const text = document.createElement('div');
      const textLabel = document.createElement('div');
      textLabel.textContent = label;
      textLabel.style.fontSize = '12px';
      textLabel.style.color = '#64748b';
      const textValue = document.createElement('div');
      textValue.textContent = value;
      textValue.style.fontWeight = '600';
      textValue.style.fontSize = '13px';
      text.append(textLabel, textValue);

      const copyButton = createButton({ label: 'Copy' });
      copyButton.addEventListener('click', async () => {
        try {
          await copyToClipboard(value);
          notifier.show({ message: 'Key copied to clipboard.', variant: 'success' });
        } catch (error) {
          notifier.show({ message: 'Unable to copy key.', variant: 'danger' });
        }
      });

      row.append(text, copyButton);
      return row;
    };

    body.append(
      createKeyRow({ label: 'Site key', value: siteKey }),
      createKeyRow({ label: 'Widget key', value: widgetKey })
    );

    const openInstallButton = createButton({ label: 'Open Install' });
    openInstallButton.style.alignSelf = 'flex-start';
    openInstallButton.addEventListener('click', () => {
      if (!siteId) {
        window.location.hash = '#/install';
        return;
      }

      const params = new URLSearchParams();
      params.set('siteId', siteId);
      if (domain) {
        params.set('domain', domain);
      }
      if (siteKey) {
        params.set('siteKey', siteKey);
      }
      if (widgetKey) {
        params.set('widgetKey', widgetKey);
      }
      window.location.hash = `#/install?${params.toString()}`;
    });
    body.appendChild(openInstallButton);

    const card = createCard({
      title: domain ? `Keys for ${domain}` : 'Keys',
      body,
    });
    keysSection.appendChild(card);
  };

  const createSiteFormCard = () => {
    const domainField = createField({
      label: 'Domain',
      placeholder: 'example.com',
    });

    const submitButton = createButton({ label: 'Create site', variant: 'primary', type: 'submit' });

    const form = document.createElement('form');
    form.style.display = 'flex';
    form.style.flexDirection = 'column';
    form.style.gap = '12px';
    form.append(domainField.wrapper, submitButton);

    form.addEventListener('submit', async (event) => {
      event.preventDefault();
      domainField.error.textContent = '';
      const domain = domainField.input.value.trim();
      if (!domain) {
        domainField.error.textContent = 'Domain is required.';
        return;
      }

      submitButton.disabled = true;
      submitButton.textContent = 'Creating...';

      try {
        const response = await client.request('/sites', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({ domain }),
        });

        const siteId = response.siteId || response.id;
        const status = await fetchInstallationStatus(siteId);
        state.sites = [{
          ...response,
          siteId,
          installationStatus: status || response.installationStatus,
        }, ...state.sites];
        state.keys = {
          domain: response.domain,
          siteId,
          siteKey: response.siteKey,
          widgetKey: response.widgetKey,
        };
        saveCachedKeys(siteId, {
          siteKey: response.siteKey,
          widgetKey: response.widgetKey,
        });

        notifier.show({ message: 'Site created.', variant: 'success' });
        domainField.input.value = '';
        renderKeys();
        renderSites();
      } catch (error) {
        const uiError = mapApiError(error);
        const message = uiError.details?.errors?.domain?.[0];
        if (message) {
          domainField.error.textContent = message;
        }
        notifier.show({ message: uiError.message, variant: 'danger' });
      } finally {
        submitButton.disabled = false;
        submitButton.textContent = 'Create site';
      }
    });

    const card = createCard({
      title: 'Create a new site',
      body: form,
    });
    return card;
  };

  const sitesSection = document.createElement('div');
  sitesSection.style.display = 'flex';
  sitesSection.style.flexDirection = 'column';
  sitesSection.style.gap = '16px';

  const fetchInstallationStatus = async (siteId) => {
    if (!siteId) {
      return null;
    }

    try {
      return await client.request(`/sites/${siteId}/installation-status`);
    } catch (error) {
      return null;
    }
  };

  const renderSites = () => {
    sitesSection.innerHTML = '';

    if (state.loading) {
      const loading = document.createElement('div');
      loading.textContent = 'Loading sites...';
      loading.style.color = '#64748b';
      sitesSection.appendChild(loading);
      return;
    }

    if (state.error) {
      const errorMessage = document.createElement('div');
      errorMessage.textContent = state.error;
      errorMessage.style.color = '#dc2626';
      sitesSection.appendChild(errorMessage);
      return;
    }

    if (!state.sites.length) {
      const empty = document.createElement('div');
      empty.textContent = 'No sites yet. Create one to get started.';
      empty.style.color = '#64748b';
      sitesSection.appendChild(empty);
      return;
    }

    state.sites.forEach((site) => {
      const siteId = getSiteId(site);
      const siteCardBody = document.createElement('div');
      siteCardBody.style.display = 'flex';
      siteCardBody.style.flexDirection = 'column';
      siteCardBody.style.gap = '16px';

      const summaryRow = document.createElement('div');
      summaryRow.style.display = 'flex';
      summaryRow.style.justifyContent = 'space-between';
      summaryRow.style.alignItems = 'center';
      summaryRow.style.flexWrap = 'wrap';
      summaryRow.style.gap = '12px';

      const summaryLeft = document.createElement('div');
      const domainLabel = document.createElement('div');
      domainLabel.textContent = site.domain || 'Unknown domain';
      domainLabel.style.fontWeight = '600';
      domainLabel.style.fontSize = '15px';
      const originsCount = document.createElement('div');
      originsCount.textContent = `Allowed origins: ${(site.allowedOrigins || []).length}`;
      originsCount.style.fontSize = '13px';
      originsCount.style.color = '#64748b';
      summaryLeft.append(domainLabel, originsCount);

      const status = site.installationStatus;
      const isConfigured = status?.isConfigured ?? ((site.allowedOrigins || []).length > 0);
      const statusBadge = createBadge({
        text: `Configured: ${isConfigured ? 'Yes' : 'No'}`,
        variant: isConfigured ? 'success' : 'warning',
      });

      const summaryActions = document.createElement('div');
      summaryActions.style.display = 'flex';
      summaryActions.style.gap = '8px';
      summaryActions.style.flexWrap = 'wrap';

      const installButton = createButton({ label: 'Install' });
      installButton.addEventListener('click', () => {
        if (!siteId) {
          window.location.hash = '#/install';
          return;
        }

        const params = new URLSearchParams();
        params.set('siteId', siteId);
        if (site.domain) {
          params.set('domain', site.domain);
        }
        const cachedKeys = loadCachedKeys(siteId);
        if (cachedKeys?.siteKey && cachedKeys?.widgetKey) {
          params.set('siteKey', cachedKeys.siteKey);
          params.set('widgetKey', cachedKeys.widgetKey);
        }
        window.location.hash = `#/install?${params.toString()}`;
      });

      const configureButton = createButton({
        label: state.expandedSiteId === siteId ? 'Hide' : 'Configure',
      });
      configureButton.addEventListener('click', () => {
        if (state.expandedSiteId === siteId) {
          state.expandedSiteId = null;
          renderSites();
          return;
        }

        state.expandedSiteId = siteId;
        state.originsDraftBySiteId[siteId] = [...(site.allowedOrigins || [])];
        if (typeof state.originInputBySiteId[siteId] !== 'string') {
          state.originInputBySiteId[siteId] = '';
        }
        if (typeof state.originInputErrorBySiteId[siteId] !== 'string') {
          state.originInputErrorBySiteId[siteId] = '';
        }
        if (typeof state.savingBySiteId[siteId] !== 'boolean') {
          state.savingBySiteId[siteId] = false;
        }
        if (typeof state.errorBySiteId[siteId] === 'undefined') {
          state.errorBySiteId[siteId] = null;
        }
        renderSites();
      });

      const regenButton = createButton({ label: 'Regenerate keys' });
      regenButton.addEventListener('click', async () => {
        regenButton.disabled = true;
        regenButton.textContent = 'Regenerating...';
        try {
          const response = await client.request(`/sites/${siteId}/keys/regenerate`, {
            method: 'POST',
          });
          state.keys = {
            domain: site.domain,
            siteId,
            siteKey: response.siteKey,
            widgetKey: response.widgetKey,
          };
          saveCachedKeys(siteId, {
            siteKey: response.siteKey,
            widgetKey: response.widgetKey,
          });
          renderKeys();
          notifier.show({ message: 'Keys regenerated.', variant: 'success' });
        } catch (error) {
          const uiError = mapApiError(error);
          notifier.show({ message: uiError.message, variant: 'danger' });
        } finally {
          regenButton.disabled = false;
          regenButton.textContent = 'Regenerate keys';
        }
      });
      summaryActions.append(installButton, configureButton, regenButton);

      summaryRow.append(summaryLeft, statusBadge, summaryActions);

      const originsEditor = document.createElement('div');
      originsEditor.style.display = 'flex';
      originsEditor.style.flexDirection = 'column';
      originsEditor.style.gap = '10px';

      const originsHeader = document.createElement('div');
      originsHeader.textContent = 'Allowed origins';
      originsHeader.style.fontWeight = '600';
      originsHeader.style.fontSize = '13px';
      originsHeader.style.color = '#475569';

      const originList = state.originsDraftBySiteId[siteId] || [...(site.allowedOrigins || [])];
      state.originsDraftBySiteId[siteId] = originList;

      const originListContainer = document.createElement('div');
      originListContainer.style.display = 'flex';
      originListContainer.style.flexDirection = 'column';
      originListContainer.style.gap = '8px';

      const renderOriginList = () => {
        originListContainer.innerHTML = '';

        if (!originList.length) {
          const emptyOrigin = document.createElement('div');
          emptyOrigin.textContent = 'No origins configured yet.';
          emptyOrigin.style.color = '#94a3b8';
          emptyOrigin.style.fontSize = '13px';
          originListContainer.appendChild(emptyOrigin);
          return;
        }

        originList.forEach((origin, index) => {
          const editingIndex = state.originEditingIndexBySiteId[siteId];
          const isEditing = editingIndex === index;

          const row = document.createElement('div');
          row.style.display = 'flex';
          row.style.alignItems = 'center';
          row.style.justifyContent = 'space-between';
          row.style.gap = '12px';
          row.style.padding = '8px 10px';
          row.style.border = '1px solid #e2e8f0';
          row.style.borderRadius = '6px';

          const text = document.createElement('span');
          text.style.fontSize = '13px';

          if (isEditing) {
            const editInput = document.createElement('input');
            editInput.type = 'text';
            editInput.value = state.originEditingValueBySiteId[siteId] || origin;
            editInput.style.flex = '1';
            editInput.style.padding = '6px 8px';
            editInput.style.border = '1px solid #cbd5e1';
            editInput.style.borderRadius = '6px';
            editInput.addEventListener('input', () => {
              state.originEditingValueBySiteId[siteId] = editInput.value;
            });
            text.appendChild(editInput);
          } else {
            text.textContent = origin;
          }

          const actions = document.createElement('div');
          actions.style.display = 'flex';
          actions.style.gap = '8px';

          const editButton = createButton({ label: isEditing ? 'Save edit' : 'Edit' });
          editButton.addEventListener('click', () => {
            if (!isEditing) {
              state.originEditingIndexBySiteId[siteId] = index;
              state.originEditingValueBySiteId[siteId] = origin;
              renderSites();
              return;
            }

            const normalizedResult = normalizeOrigin(state.originEditingValueBySiteId[siteId]);
            if (!normalizedResult.value) {
              state.originInputErrorBySiteId[siteId] = normalizedResult.error;
              renderSites();
              return;
            }

            originList[index] = normalizedResult.value;
            state.originsDraftBySiteId[siteId] = [...originList];
            state.originEditingIndexBySiteId[siteId] = null;
            state.originEditingValueBySiteId[siteId] = '';
            state.originInputErrorBySiteId[siteId] = '';
            renderSites();
          });

          if (isEditing) {
            const cancelEditButton = createButton({ label: 'Cancel' });
            cancelEditButton.addEventListener('click', () => {
              state.originEditingIndexBySiteId[siteId] = null;
              state.originEditingValueBySiteId[siteId] = '';
              renderSites();
            });
            actions.appendChild(cancelEditButton);
          }

          const removeButton = createButton({ label: 'Remove' });
          removeButton.addEventListener('click', () => {
            originList.splice(index, 1);
            state.originsDraftBySiteId[siteId] = [...originList];
            renderOriginList();
          });

          actions.append(editButton, removeButton);
          row.append(text, actions);
          originListContainer.appendChild(row);
        });
      };

      renderOriginList();

      const { wrapper: originInputWrapper, input: originInput } = createInput({
        label: 'Add origin',
        placeholder: 'https://app.example.com',
      });
      const helperText = document.createElement('div');
      helperText.style.fontSize = '12px';
      helperText.style.color = '#64748b';
      helperText.textContent = ORIGIN_HELPER_TEXT;
      originInputWrapper.appendChild(helperText);

      const originInputError = document.createElement('div');
      originInputError.className = 'ui-field-error';
      originInputError.textContent = state.originInputErrorBySiteId[siteId] || '';
      originInputWrapper.appendChild(originInputError);

      const addButton = createButton({ label: 'Add' });
      addButton.style.alignSelf = 'flex-start';

      const addLocalhostButton = createButton({ label: 'Add localhost current port' });
      addLocalhostButton.style.alignSelf = 'flex-start';

      const addRow = document.createElement('div');
      addRow.style.display = 'flex';
      addRow.style.flexDirection = 'column';
      addRow.style.gap = '8px';
      addRow.append(originInputWrapper, addButton, addLocalhostButton);

      const addOriginToList = (rawOrigin) => {
        const normalizedResult = normalizeOrigin(rawOrigin);
        if (!normalizedResult.value) {
          state.originInputErrorBySiteId[siteId] = normalizedResult.error;
          renderSites();
          return;
        }

        const normalized = normalizedResult.value;
        const existing = originList.find(
          (origin) => origin.toLowerCase() === normalized.toLowerCase()
        );
        if (existing) {
          notifier.show({ message: 'Origin already listed.', variant: 'warning' });
          return;
        }

        state.originInputErrorBySiteId[siteId] = '';
        originList.push(normalized);
        state.originsDraftBySiteId[siteId] = [...originList];
        state.originInputBySiteId[siteId] = '';
        renderSites();
      };

      addButton.addEventListener('click', () => {
        addOriginToList(originInput.value);
      });

      addLocalhostButton.addEventListener('click', () => {
        if (window.location.hostname !== 'localhost') {
          state.originInputErrorBySiteId[siteId] = ORIGIN_HELPER_TEXT;
          renderSites();
          return;
        }

        const localhostOrigin = `http://localhost:${window.location.port || 80}`;
        addOriginToList(localhostOrigin);
      });

      originInput.value = state.originInputBySiteId[siteId] || '';
      originInput.addEventListener('input', () => {
        state.originInputBySiteId[siteId] = originInput.value;
        state.originInputErrorBySiteId[siteId] = '';
      });

      const saveButton = createButton({ label: 'Save origins', variant: 'primary' });
      saveButton.style.alignSelf = 'flex-start';
      const saveError = document.createElement('div');
      saveError.style.fontSize = '13px';
      saveError.style.color = '#dc2626';
      saveError.textContent = state.errorBySiteId[siteId] || '';
      saveButton.disabled = Boolean(state.savingBySiteId[siteId]);
      if (state.savingBySiteId[siteId]) {
        saveButton.textContent = 'Saving...';
      }
      saveButton.addEventListener('click', async () => {
        state.savingBySiteId[siteId] = true;
        state.errorBySiteId[siteId] = null;
        renderSites();

        try {
          const normalized = normalizeOrigins(originList);
          const response = await client.request(`/sites/${siteId}/origins`, {
            method: 'PUT',
            headers: {
              'Content-Type': 'application/json',
            },
            body: JSON.stringify({ allowedOrigins: normalized }),
          });

          const updatedSite = {
            ...site,
            ...response,
            allowedOrigins: response.allowedOrigins || normalized,
            installationStatus: response.installationStatus || site.installationStatus,
          };

          state.sites = state.sites.map((item) =>
            getSiteId(item) === siteId ? updatedSite : item
          );
          state.originsDraftBySiteId[siteId] = [...(updatedSite.allowedOrigins || normalized)];
          notifier.show({ message: 'Allowed origins updated.', variant: 'success' });
          renderSites();
        } catch (error) {
          const uiError = mapApiError(error);
          state.errorBySiteId[siteId] = uiError.message;
          notifier.show({ message: uiError.message, variant: 'danger' });
        } finally {
          state.savingBySiteId[siteId] = false;
          renderSites();
        }
      });

      originsEditor.append(originsHeader, originListContainer, addRow, saveButton, saveError);

      siteCardBody.append(summaryRow);
      if (state.expandedSiteId === siteId) {
        siteCardBody.append(originsEditor);
      }

      const card = createCard({
        title: 'Site details',
        body: siteCardBody,
      });

      sitesSection.appendChild(card);
    });
  };

  const loadSites = async () => {
    state.loading = true;
    state.error = null;
    renderSites();

    try {
      const sites = await client.request('/sites');
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
      const uiError = mapApiError(error);
      state.loading = false;
      state.error = uiError.message;
      renderSites();
    }
  };

  const formCard = createSiteFormCard();
  page.append(header, formCard, keysSection, sitesSection);
  container.appendChild(page);

  loadSites();
};
