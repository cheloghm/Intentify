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


const getFreshness = (source) => {
  const status = String(source?.status || '').toUpperCase();
  if (status === 'PROCESSING') {
    return { label: 'Indexing', variant: 'warning' };
  }

  if (status === 'FAILED') {
    return { label: 'Error', variant: 'danger' };
  }

  if (!source?.indexedAtUtc) {
    return { label: 'Stale', variant: 'warning' };
  }

  const indexedAt = Date.parse(source.indexedAtUtc);
  const updatedAt = source?.updatedAtUtc ? Date.parse(source.updatedAtUtc) : Number.NaN;
  if (Number.isNaN(indexedAt) || (!Number.isNaN(updatedAt) && updatedAt > indexedAt)) {
    return { label: 'Stale', variant: 'warning' };
  }

  return { label: 'Fresh', variant: 'success' };
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

  // ── Quick Facts ────────────────────────────────────────────────────────
  const quickFactsBody = document.createElement('div');
  quickFactsBody.className = 'stack';

  const quickFactsSubtitle = document.createElement('div');
  quickFactsSubtitle.className = 'card-subtitle';
  quickFactsSubtitle.textContent = 'Key facts your AI assistant knows about your business';

  const quickFactsList = document.createElement('div');
  quickFactsList.className = 'stack';

  const addFactRow = document.createElement('div');
  addFactRow.style.display = 'flex';
  addFactRow.style.gap = '8px';
  addFactRow.style.marginTop = '4px';

  const factInput = document.createElement('input');
  factInput.type = 'text';
  factInput.className = 'form-input';
  factInput.placeholder = 'e.g. We are open Mon–Sat 9am–6pm';
  factInput.style.flex = '1';

  const addFactBtn = document.createElement('button');
  addFactBtn.className = 'btn btn-primary btn-sm';
  addFactBtn.textContent = '+ Add';
  addFactBtn.style.whiteSpace = 'nowrap';

  addFactRow.append(factInput, addFactBtn);
  quickFactsBody.append(quickFactsSubtitle, quickFactsList, addFactRow);

  const renderQuickFacts = (facts) => {
    quickFactsList.innerHTML = '';
    const list = Array.isArray(facts) ? facts : [];
    if (!list.length) {
      const empty = document.createElement('div');
      empty.className = 'empty-state';
      empty.style.padding = '24px 0';
      const emptyDesc = document.createElement('div');
      emptyDesc.className = 'empty-state-desc';
      emptyDesc.textContent = 'No quick facts yet. Add facts to help your AI assistant answer common questions accurately.';
      empty.appendChild(emptyDesc);
      quickFactsList.appendChild(empty);
      return;
    }

    list.forEach((item) => {
      const row = document.createElement('div');
      row.style.display = 'flex';
      row.style.alignItems = 'flex-start';
      row.style.gap = '10px';
      row.style.padding = '10px 12px';
      row.style.borderRadius = 'var(--radius-sm)';
      row.style.border = '1px solid var(--color-border)';
      row.style.background = 'var(--color-surface)';

      const factText = document.createElement('div');
      factText.style.flex = '1';
      factText.style.fontSize = '13px';
      factText.style.color = 'var(--color-text-secondary)';
      factText.style.lineHeight = '1.5';
      factText.textContent = item.fact || item.text || item.content || String(item);

      const deleteBtn = document.createElement('button');
      deleteBtn.className = 'btn btn-secondary btn-sm';
      deleteBtn.textContent = '×';
      deleteBtn.setAttribute('aria-label', 'Delete fact');
      deleteBtn.addEventListener('click', async () => {
        deleteBtn.disabled = true;
        try {
          await client.knowledge.deleteQuickFact(state.siteId, item.id || item.factId || item._id);
          await loadQuickFacts();
        } catch (error) {
          notifier.show({ message: mapApiError(error).message, variant: 'danger' });
          deleteBtn.disabled = false;
        }
      });

      row.append(factText, deleteBtn);
      quickFactsList.appendChild(row);
    });
  };

  const loadQuickFacts = async () => {
    if (!state.siteId) {
      renderQuickFacts([]);
      return;
    }
    try {
      const facts = await client.knowledge.listQuickFacts(state.siteId);
      renderQuickFacts(Array.isArray(facts) ? facts : []);
    } catch (error) {
      // Gracefully handle missing endpoint (404) — show empty state
      renderQuickFacts([]);
    }
  };

  addFactBtn.addEventListener('click', async () => {
    if (!state.siteId) {
      notifier.show({ message: 'Select a site first.', variant: 'warning' });
      return;
    }
    const fact = factInput.value.trim();
    if (!fact) {
      notifier.show({ message: 'Enter a fact to add.', variant: 'warning' });
      return;
    }
    addFactBtn.disabled = true;
    addFactBtn.textContent = 'Adding…';
    try {
      await client.knowledge.addQuickFact(state.siteId, fact);
      factInput.value = '';
      notifier.show({ message: 'Fact added.', variant: 'success' });
      await loadQuickFacts();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    } finally {
      addFactBtn.disabled = false;
      addFactBtn.textContent = '+ Add';
    }
  });

  factInput.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') { e.preventDefault(); addFactBtn.click(); }
  });

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
  const reindexStaleButton = createButton({ label: 'Reindex stale' });

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
    const actionRow = document.createElement('div');
    actionRow.style.display = 'flex';
    actionRow.style.gap = '8px';
    actionRow.append(refreshButton, reindexStaleButton);
    sourcesBody.appendChild(actionRow);

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
    ['Name', 'Type', 'Status', 'Error', 'Chunks', 'Freshness', 'Last Indexed', 'Updated', ''].forEach((label) => {
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
      const freshness = getFreshness(source);
      const errorCell = document.createElement('td');
      errorCell.textContent = source.failureReason || '—';
      const chunkCountCell = document.createElement('td');
      chunkCountCell.textContent = Number.isFinite(source.chunkCount) ? String(source.chunkCount) : '0';
      const freshnessCell = document.createElement('td');
      freshnessCell.appendChild(createBadge({ text: freshness.label, variant: freshness.variant }));

      const indexedCell = document.createElement('td');
      indexedCell.textContent = source.indexedAtUtc
        ? new Date(source.indexedAtUtc).toLocaleString()
        : '—';

      const updatedCell = document.createElement('td');
      updatedCell.textContent = source.updatedAtUtc
        ? new Date(source.updatedAtUtc).toLocaleString()
        : '—';
      const actionCell = document.createElement('td');
      actionCell.style.display = 'flex';
      actionCell.style.gap = '8px';
      const indexButton = createButton({ label: 'Index' });
      indexButton.addEventListener('click', async () => {
        indexButton.disabled = true;
        indexButton.textContent = 'Indexing...';
        try {
          const response = await client.knowledge.indexSource(source.sourceId);
          const normalizedStatus = String(response.status || '').toUpperCase();

          if (normalizedStatus === 'FAILED') {
            notifier.show({
              message: response.failureReason
                ? `Index failed: ${response.failureReason}`
                : 'Index failed.',
              variant: 'danger',
            });
          } else if (normalizedStatus === 'PROCESSING') {
            notifier.show({
              message: 'Indexing is already in progress for this source.',
              variant: 'warning',
            });
          } else {
            notifier.show({
              message: `Indexed (${response.chunkCount ?? 0} chunks).`,
              variant: 'success',
            });
          }

          await loadSources();
        } catch (error) {
          notifier.show({ message: mapApiError(error).message, variant: 'danger' });
        } finally {
          indexButton.disabled = false;
          indexButton.textContent = 'Index';
        }
      });
      actionCell.appendChild(indexButton);
      const deleteButton = createButton({ label: 'Delete' });
      deleteButton.addEventListener('click', async () => {
        deleteButton.disabled = true;
        try {
          await client.knowledge.deleteSource(source.sourceId);
          notifier.show({ message: 'Source deleted.', variant: 'success' });
          await loadSources();
        } catch (error) {
          notifier.show({ message: mapApiError(error).message, variant: 'danger' });
          deleteButton.disabled = false;
        }
      });
      actionCell.appendChild(deleteButton);
      if (source.failureReason) {
        statusCell.title = source.failureReason;
      }
      tr.append(nameCell, typeCell, statusCell, errorCell, chunkCountCell, freshnessCell, indexedCell, updatedCell, actionCell);
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

  reindexStaleButton.addEventListener('click', async () => {
    if (!state.siteId) {
      notifier.show({ message: 'Select a site first.', variant: 'warning' });
      return;
    }

    const staleSources = state.sources.filter((source) => {
      const freshness = getFreshness(source);
      return freshness.label !== 'Fresh' && String(source.status || '').toUpperCase() !== 'PROCESSING';
    });

    if (!staleSources.length) {
      notifier.show({ message: 'No stale sources to reindex.', variant: 'success' });
      return;
    }

    reindexStaleButton.disabled = true;
    reindexStaleButton.textContent = 'Reindexing...';
    let succeeded = 0;
    let failed = 0;
    try {
      for (const source of staleSources) {
        try {
          const response = await client.knowledge.indexSource(source.sourceId);
          if (String(response.status || '').toUpperCase() === 'FAILED') {
            failed += 1;
          } else {
            succeeded += 1;
          }
        } catch (error) {
          failed += 1;
        }
      }

      await loadSources();
      notifier.show({
        message: `Reindex complete. Success: ${succeeded}, Failed: ${failed}.`,
        variant: failed > 0 ? 'warning' : 'success',
      });
    } finally {
      reindexStaleButton.disabled = false;
      reindexStaleButton.textContent = 'Reindex stale';
    }
  });

  siteSelect.addEventListener('change', async () => {
    state.siteId = siteSelect.value;
    await Promise.all([loadSources(), loadQuickFacts()]);
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
      await Promise.all([loadSources(), loadQuickFacts()]);
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
    createCard({ title: 'Quick Facts', body: quickFactsBody }),
    workspace,
    createCard({ title: 'Retrieve', body: retrieveBody })
  );

  container.appendChild(page);

  renderForm();
  renderSources();
  renderRetrieveResults();
  renderQuickFacts([]);
  void loadSites();
};
