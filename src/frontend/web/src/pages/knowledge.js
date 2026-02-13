import { createBadge, createCard, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

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

const getSiteId = (site) => site?.siteId || site?.id || '';

const getStatusVariant = (status) => {
  const normalized = String(status || '').toUpperCase();
  if (normalized === 'INDEXED') {
    return 'success';
  }
  if (normalized === 'FAILED') {
    return 'danger';
  }
  if (normalized === 'PROCESSING' || normalized === 'PENDING') {
    return 'warning';
  }
  return '';
};

export const renderKnowledgeView = (container, { apiClient, toast } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();

  const state = {
    sites: [],
    siteId: '',
    activeTab: 'URL',
    sources: [],
    loadingSources: false,
    retrieveQuery: '',
    retrieveResults: [],
  };

  const page = document.createElement('div');
  page.style.display = 'flex';
  page.style.flexDirection = 'column';
  page.style.gap = '20px';
  page.style.width = '100%';
  page.style.maxWidth = '980px';

  const header = document.createElement('div');
  const title = document.createElement('h2');
  title.textContent = 'Knowledge';
  title.style.margin = '0';
  const subtitle = document.createElement('p');
  subtitle.textContent = 'Add sources, index content, and test retrieval.';
  subtitle.style.margin = '6px 0 0';
  subtitle.style.color = '#64748b';
  header.append(title, subtitle);

  const siteRow = document.createElement('div');
  siteRow.style.display = 'flex';
  siteRow.style.gap = '10px';
  siteRow.style.alignItems = 'flex-end';

  const siteField = document.createElement('label');
  siteField.style.display = 'flex';
  siteField.style.flexDirection = 'column';
  siteField.style.gap = '6px';
  siteField.style.flex = '1';

  const siteLabel = document.createElement('span');
  siteLabel.textContent = 'Site';
  siteLabel.style.fontSize = '13px';

  const siteSelect = document.createElement('select');
  siteSelect.style.padding = '8px 10px';
  siteSelect.style.borderRadius = '6px';
  siteSelect.style.border = '1px solid #cbd5e1';
  siteSelect.style.background = '#ffffff';

  siteField.append(siteLabel, siteSelect);
  siteRow.appendChild(siteField);

  const workspace = document.createElement('div');
  workspace.style.display = 'grid';
  workspace.style.gridTemplateColumns = '1fr 1fr';
  workspace.style.gap = '16px';

  const addSourceBody = document.createElement('div');
  addSourceBody.style.display = 'flex';
  addSourceBody.style.flexDirection = 'column';
  addSourceBody.style.gap = '10px';

  const tabRow = document.createElement('div');
  tabRow.style.display = 'flex';
  tabRow.style.gap = '8px';

  const form = document.createElement('form');
  form.style.display = 'flex';
  form.style.flexDirection = 'column';
  form.style.gap = '10px';

  const sourcesBody = document.createElement('div');
  sourcesBody.style.display = 'flex';
  sourcesBody.style.flexDirection = 'column';
  sourcesBody.style.gap = '10px';

  const refreshButton = createButton({ label: 'Refresh list' });

  const retrieveBody = document.createElement('div');
  retrieveBody.style.display = 'flex';
  retrieveBody.style.flexDirection = 'column';
  retrieveBody.style.gap = '10px';

  const retrieveForm = document.createElement('form');
  retrieveForm.style.display = 'flex';
  retrieveForm.style.gap = '8px';

  const retrieveInput = document.createElement('input');
  retrieveInput.type = 'text';
  retrieveInput.placeholder = 'Ask a question about indexed content';
  retrieveInput.style.flex = '1';
  retrieveInput.style.padding = '8px 10px';
  retrieveInput.style.borderRadius = '6px';
  retrieveInput.style.border = '1px solid #cbd5e1';

  const retrieveButton = createButton({ label: 'Retrieve', variant: 'primary', type: 'submit' });
  const retrieveResults = document.createElement('div');
  retrieveResults.style.display = 'flex';
  retrieveResults.style.flexDirection = 'column';
  retrieveResults.style.gap = '8px';

  retrieveForm.append(retrieveInput, retrieveButton);
  retrieveBody.append(retrieveForm, retrieveResults);

  const setSiteOptions = () => {
    siteSelect.innerHTML = '';
    const placeholder = document.createElement('option');
    placeholder.value = '';
    placeholder.textContent = state.sites.length ? 'Select a site' : 'No sites available';
    siteSelect.appendChild(placeholder);

    state.sites.forEach((site) => {
      const option = document.createElement('option');
      option.value = getSiteId(site);
      option.textContent = site.domain || getSiteId(site);
      siteSelect.appendChild(option);
    });

    siteSelect.value = state.siteId;
  };

  const renderForm = () => {
    tabRow.innerHTML = '';
    ['URL', 'TEXT', 'PDF'].forEach((tab) => {
      const button = createButton({ label: tab });
      if (state.activeTab === tab) {
        button.style.background = '#eff6ff';
        button.style.borderColor = '#bfdbfe';
      }
      button.addEventListener('click', () => {
        state.activeTab = tab;
        renderForm();
      });
      tabRow.appendChild(button);
    });

    form.innerHTML = '';

    const nameInput = document.createElement('input');
    nameInput.type = 'text';
    nameInput.placeholder = 'Source name (optional)';
    nameInput.style.padding = '8px 10px';
    nameInput.style.borderRadius = '6px';
    nameInput.style.border = '1px solid #cbd5e1';

    const submit = createButton({ label: 'Add source', variant: 'primary', type: 'submit' });

    if (state.activeTab === 'URL') {
      const urlInput = document.createElement('input');
      urlInput.type = 'url';
      urlInput.placeholder = 'https://example.com/docs';
      urlInput.required = true;
      urlInput.style.padding = '8px 10px';
      urlInput.style.borderRadius = '6px';
      urlInput.style.border = '1px solid #cbd5e1';
      form.append(nameInput, urlInput, submit);

      form.onsubmit = async (event) => {
        event.preventDefault();
        if (!state.siteId) {
          notifier.show({ message: 'Select a site first.', variant: 'warning' });
          return;
        }

        submit.disabled = true;
        submit.textContent = 'Adding...';
        try {
          await client.knowledge.createSource({
            siteId: state.siteId,
            type: 'URL',
            name: nameInput.value.trim(),
            url: urlInput.value.trim(),
          });
          notifier.show({ message: 'URL source added.', variant: 'success' });
          await loadSources();
          form.reset();
        } catch (error) {
          notifier.show({ message: mapApiError(error).message, variant: 'danger' });
        } finally {
          submit.disabled = false;
          submit.textContent = 'Add source';
        }
      };
      return;
    }

    if (state.activeTab === 'TEXT') {
      const textInput = document.createElement('textarea');
      textInput.placeholder = 'Paste raw text to index';
      textInput.required = true;
      textInput.rows = 6;
      textInput.style.padding = '8px 10px';
      textInput.style.borderRadius = '6px';
      textInput.style.border = '1px solid #cbd5e1';
      textInput.style.resize = 'vertical';
      form.append(nameInput, textInput, submit);

      form.onsubmit = async (event) => {
        event.preventDefault();
        if (!state.siteId) {
          notifier.show({ message: 'Select a site first.', variant: 'warning' });
          return;
        }

        submit.disabled = true;
        submit.textContent = 'Adding...';
        try {
          await client.knowledge.createSource({
            siteId: state.siteId,
            type: 'TEXT',
            name: nameInput.value.trim(),
            text: textInput.value,
          });
          notifier.show({ message: 'Text source added.', variant: 'success' });
          await loadSources();
          form.reset();
        } catch (error) {
          notifier.show({ message: mapApiError(error).message, variant: 'danger' });
        } finally {
          submit.disabled = false;
          submit.textContent = 'Add source';
        }
      };
      return;
    }

    const fileInput = document.createElement('input');
    fileInput.type = 'file';
    fileInput.accept = 'application/pdf';
    fileInput.required = true;
    fileInput.style.padding = '8px 10px';
    fileInput.style.borderRadius = '6px';
    fileInput.style.border = '1px solid #cbd5e1';
    form.append(nameInput, fileInput, submit);

    form.onsubmit = async (event) => {
      event.preventDefault();
      if (!state.siteId) {
        notifier.show({ message: 'Select a site first.', variant: 'warning' });
        return;
      }

      const file = fileInput.files?.[0];
      if (!file) {
        notifier.show({ message: 'Choose a PDF file.', variant: 'warning' });
        return;
      }

      submit.disabled = true;
      submit.textContent = 'Uploading...';
      try {
        const created = await client.knowledge.createSource({
          siteId: state.siteId,
          type: 'PDF',
          name: nameInput.value.trim() || file.name,
        });
        await client.knowledge.uploadPdf(created.sourceId, file);
        notifier.show({ message: 'PDF source uploaded.', variant: 'success' });
        await loadSources();
        form.reset();
      } catch (error) {
        notifier.show({ message: mapApiError(error).message, variant: 'danger' });
      } finally {
        submit.disabled = false;
        submit.textContent = 'Add source';
      }
    };
  };

  const loadSources = async () => {
    if (!state.siteId) {
      state.sources = [];
      renderSources();
      return;
    }

    state.loadingSources = true;
    renderSources();
    try {
      state.sources = await client.knowledge.listSources(state.siteId);
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    } finally {
      state.loadingSources = false;
      renderSources();
    }
  };

  const renderSources = () => {
    sourcesBody.innerHTML = '';
    sourcesBody.appendChild(refreshButton);

    if (state.loadingSources) {
      const loading = document.createElement('div');
      loading.textContent = 'Loading sources...';
      loading.style.color = '#64748b';
      sourcesBody.appendChild(loading);
      return;
    }

    if (!state.sources.length) {
      const empty = document.createElement('div');
      empty.textContent = state.siteId ? 'No sources yet.' : 'Select a site to see sources.';
      empty.style.color = '#64748b';
      sourcesBody.appendChild(empty);
      return;
    }

    const table = document.createElement('table');
    table.className = 'ui-table';
    const thead = document.createElement('thead');
    const headerRow = document.createElement('tr');
    ['Name', 'Type', 'Status', 'Updated', ''].forEach((label) => {
      const th = document.createElement('th');
      th.textContent = label;
      headerRow.appendChild(th);
    });
    thead.appendChild(headerRow);

    const tbody = document.createElement('tbody');
    state.sources.forEach((source) => {
      const tr = document.createElement('tr');
      const nameCell = document.createElement('td');
      nameCell.textContent = source.name || source.url || source.sourceId;
      const typeCell = document.createElement('td');
      typeCell.textContent = source.type || '—';
      const statusCell = document.createElement('td');
      statusCell.appendChild(
        createBadge({ text: source.status || 'Unknown', variant: getStatusVariant(source.status) })
      );
      const updatedCell = document.createElement('td');
      updatedCell.textContent = source.updatedAtUtc
        ? new Date(source.updatedAtUtc).toLocaleString()
        : '—';
      const actionCell = document.createElement('td');
      const indexButton = createButton({ label: 'Index' });
      indexButton.addEventListener('click', async () => {
        indexButton.disabled = true;
        indexButton.textContent = 'Indexing...';
        try {
          const response = await client.knowledge.indexSource(source.sourceId);
          notifier.show({
            message: `Indexed (${response.chunkCount ?? 0} chunks).`,
            variant: 'success',
          });
          await loadSources();
        } catch (error) {
          notifier.show({ message: mapApiError(error).message, variant: 'danger' });
        } finally {
          indexButton.disabled = false;
          indexButton.textContent = 'Index';
        }
      });
      actionCell.appendChild(indexButton);
      tr.append(nameCell, typeCell, statusCell, updatedCell, actionCell);
      tbody.appendChild(tr);
    });

    table.append(thead, tbody);
    sourcesBody.appendChild(table);
  };

  const renderRetrieveResults = () => {
    retrieveResults.innerHTML = '';
    if (!state.retrieveResults.length) {
      const empty = document.createElement('div');
      empty.textContent = 'No results yet.';
      empty.style.color = '#64748b';
      retrieveResults.appendChild(empty);
      return;
    }

    state.retrieveResults.forEach((result) => {
      const row = document.createElement('div');
      row.style.border = '1px solid #e2e8f0';
      row.style.borderRadius = '8px';
      row.style.padding = '10px 12px';
      row.style.background = '#ffffff';

      const meta = document.createElement('div');
      meta.textContent = `Source ${result.sourceId} • Chunk ${result.chunkIndex} • Score ${result.score}`;
      meta.style.fontSize = '12px';
      meta.style.color = '#64748b';

      const content = document.createElement('div');
      content.textContent = result.content;
      content.style.marginTop = '6px';
      content.style.whiteSpace = 'pre-wrap';

      row.append(meta, content);
      retrieveResults.appendChild(row);
    });
  };

  refreshButton.addEventListener('click', loadSources);

  siteSelect.addEventListener('change', async () => {
    state.siteId = siteSelect.value;
    await loadSources();
  });

  retrieveForm.addEventListener('submit', async (event) => {
    event.preventDefault();
    if (!state.siteId) {
      notifier.show({ message: 'Select a site first.', variant: 'warning' });
      return;
    }

    const query = retrieveInput.value.trim();
    if (!query) {
      notifier.show({ message: 'Enter a query.', variant: 'warning' });
      return;
    }

    retrieveButton.disabled = true;
    retrieveButton.textContent = 'Retrieving...';
    try {
      state.retrieveResults = await client.knowledge.retrieve({
        siteId: state.siteId,
        query,
        top: 5,
      });
      renderRetrieveResults();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    } finally {
      retrieveButton.disabled = false;
      retrieveButton.textContent = 'Retrieve';
    }
  });

  const loadSites = async () => {
    try {
      state.sites = await client.sites.list();
      if (!state.siteId && state.sites[0]) {
        state.siteId = getSiteId(state.sites[0]);
      }
      setSiteOptions();
      await loadSources();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  addSourceBody.append(tabRow, form);
  workspace.append(
    createCard({ title: 'Add Source', body: addSourceBody }),
    createCard({ title: 'Sources', body: sourcesBody })
  );

  page.append(
    header,
    siteRow,
    workspace,
    createCard({ title: 'Retrieve', body: retrieveBody })
  );

  container.appendChild(page);

  renderForm();
  renderSources();
  renderRetrieveResults();
  void loadSites();
};
