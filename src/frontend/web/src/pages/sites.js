import { createBadge, createCard, createInput, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const normalizeOriginInput = (value) => {
  if (!value) {
    return '';
  }

  return value.trim().replace(/\/+$/, '');
};

const normalizeOrigins = (origins) => {
  const unique = new Map();
  origins.forEach((origin) => {
    const normalized = normalizeOriginInput(origin);
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

export const renderSitesView = (container, { apiClient, toast } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();

  const state = {
    sites: [],
    loading: true,
    error: null,
    keys: null,
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
        window.location.hash = `#/install?${params.toString()}`;
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
      summaryActions.append(installButton, regenButton);

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

      const originList = [...(site.allowedOrigins || [])];
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
          const row = document.createElement('div');
          row.style.display = 'flex';
          row.style.alignItems = 'center';
          row.style.justifyContent = 'space-between';
          row.style.gap = '12px';
          row.style.padding = '8px 10px';
          row.style.border = '1px solid #e2e8f0';
          row.style.borderRadius = '6px';

          const text = document.createElement('span');
          text.textContent = origin;
          text.style.fontSize = '13px';

          const removeButton = createButton({ label: 'Remove' });
          removeButton.addEventListener('click', () => {
            originList.splice(index, 1);
            renderOriginList();
          });

          row.append(text, removeButton);
          originListContainer.appendChild(row);
        });
      };

      renderOriginList();

      const { wrapper: originInputWrapper, input: originInput } = createInput({
        label: 'Add origin',
        placeholder: 'https://app.example.com',
      });
      const addButton = createButton({ label: 'Add' });
      addButton.style.alignSelf = 'flex-start';

      const addRow = document.createElement('div');
      addRow.style.display = 'flex';
      addRow.style.flexDirection = 'column';
      addRow.style.gap = '8px';
      addRow.append(originInputWrapper, addButton);

      addButton.addEventListener('click', () => {
        const normalized = normalizeOriginInput(originInput.value);
        if (!normalized) {
          notifier.show({ message: 'Enter a valid origin.', variant: 'warning' });
          return;
        }

        const existing = originList.find(
          (origin) => origin.toLowerCase() === normalized.toLowerCase()
        );
        if (existing) {
          notifier.show({ message: 'Origin already listed.', variant: 'warning' });
          return;
        }

        originList.push(normalized);
        originInput.value = '';
        renderOriginList();
      });

      const saveButton = createButton({ label: 'Save origins', variant: 'primary' });
      saveButton.style.alignSelf = 'flex-start';
      saveButton.addEventListener('click', async () => {
        saveButton.disabled = true;
        saveButton.textContent = 'Saving...';

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
          notifier.show({ message: 'Allowed origins updated.', variant: 'success' });
          renderSites();
        } catch (error) {
          const uiError = mapApiError(error);
          notifier.show({ message: uiError.message, variant: 'danger' });
        } finally {
          saveButton.disabled = false;
          saveButton.textContent = 'Save origins';
        }
      });

      originsEditor.append(
        originsHeader,
        originListContainer,
        addRow,
        saveButton
      );

      siteCardBody.append(summaryRow, originsEditor);

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
