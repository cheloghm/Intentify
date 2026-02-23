import { createCard, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const getSiteId = (site) => site?.siteId || site?.id || '';
const SELECTED_SITE_STORAGE_KEY = 'intentify.selectedSiteId';
const DEFAULT_PAGE_SIZE = 20;
const TICKET_STATUSES = ['Open', 'InProgress', 'Resolved', 'Closed'];

const getTicketIdValue = (ticketOrId) => {
  if (typeof ticketOrId === 'string') {
    return ticketOrId;
  }

  if (ticketOrId && typeof ticketOrId === 'object') {
    return ticketOrId.value || ticketOrId.id || ticketOrId.ticketId || ticketOrId._id || '';
  }

  return '';
};

const formatDate = (value) => {
  if (!value) {
    return '—';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '—';
  }

  return date.toLocaleString();
};

const toShortId = (value) => {
  if (!value || value.length < 8) {
    return value || '—';
  }

  return `${value.slice(0, 8)}…`;
};

const loadSelectedSiteId = () => {
  try {
    return localStorage.getItem(SELECTED_SITE_STORAGE_KEY) || '';
  } catch (error) {
    return '';
  }
};

const saveSelectedSiteId = (siteId) => {
  try {
    if (!siteId) {
      localStorage.removeItem(SELECTED_SITE_STORAGE_KEY);
      return;
    }
    localStorage.setItem(SELECTED_SITE_STORAGE_KEY, siteId);
  } catch (error) {
    // ignore storage errors
  }
};

export const renderTicketsView = async (container, { apiClient, toast, query } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();

  const initialPage = Number.parseInt(query?.page || '1', 10);
  const state = {
    sites: [],
    siteId: query?.siteId || loadSelectedSiteId(),
    tickets: [],
    page: Number.isFinite(initialPage) && initialPage > 0 ? initialPage : 1,
    pageSize: DEFAULT_PAGE_SIZE,
    loadingSites: true,
    loadingTickets: false,
  };

  const page = document.createElement('div');
  page.style.display = 'flex';
  page.style.flexDirection = 'column';
  page.style.gap = '16px';
  page.style.width = '100%';
  page.style.maxWidth = '1000px';

  const controls = document.createElement('div');
  controls.style.display = 'flex';
  controls.style.flexWrap = 'wrap';
  controls.style.gap = '10px';
  controls.style.alignItems = 'flex-end';

  const siteField = document.createElement('label');
  siteField.style.display = 'flex';
  siteField.style.flexDirection = 'column';
  siteField.style.gap = '6px';

  const siteLabel = document.createElement('span');
  siteLabel.textContent = 'Site';
  siteLabel.style.fontSize = '13px';
  siteLabel.style.color = '#334155';

  const siteSelect = document.createElement('select');
  siteSelect.style.minWidth = '280px';
  siteSelect.style.padding = '8px 10px';
  siteSelect.style.borderRadius = '6px';
  siteSelect.style.border = '1px solid #cbd5e1';

  siteField.append(siteLabel, siteSelect);
  controls.append(siteField);

  const listBody = document.createElement('div');
  listBody.style.display = 'flex';
  listBody.style.flexDirection = 'column';
  listBody.style.gap = '10px';

  const pagination = document.createElement('div');
  pagination.style.display = 'flex';
  pagination.style.justifyContent = 'space-between';
  pagination.style.alignItems = 'center';

  const prevButton = document.createElement('button');
  prevButton.type = 'button';
  prevButton.textContent = 'Previous';
  prevButton.style.padding = '8px 12px';
  prevButton.style.border = '1px solid #cbd5e1';
  prevButton.style.borderRadius = '6px';
  prevButton.style.background = '#fff';

  const nextButton = document.createElement('button');
  nextButton.type = 'button';
  nextButton.textContent = 'Next';
  nextButton.style.padding = '8px 12px';
  nextButton.style.border = '1px solid #cbd5e1';
  nextButton.style.borderRadius = '6px';
  nextButton.style.background = '#fff';

  const pageInfo = document.createElement('span');
  pageInfo.style.color = '#475569';
  pageInfo.style.fontSize = '13px';

  pagination.append(prevButton, pageInfo, nextButton);

  const existingModalOverlay = document.getElementById('tickets-modal-overlay');
  if (existingModalOverlay) {
    existingModalOverlay.remove();
  }

  const modalState = {
    ticketId: '',
    ticket: null,
    notes: [],
    loading: false,
  };
  const getNextStatusOptions = (status) => {
    if (status === 'Open') {
      return ['Open', 'InProgress'];
    }

    if (status === 'InProgress') {
      return ['InProgress', 'Resolved'];
    }

    if (status === 'Resolved') {
      return ['Resolved', 'Closed'];
    }

    return ['Closed'];
  };

  const modalOverlay = document.createElement('div');
  modalOverlay.id = 'tickets-modal-overlay';
  modalOverlay.style.position = 'fixed';
  modalOverlay.style.inset = '0';
  modalOverlay.style.background = 'rgba(15, 23, 42, 0.55)';
  modalOverlay.style.display = 'none';
  modalOverlay.style.alignItems = 'center';
  modalOverlay.style.justifyContent = 'center';
  modalOverlay.style.zIndex = '999999';
  modalOverlay.style.padding = '20px';

  const modal = document.createElement('div');
  modal.style.width = '100%';
  modal.style.maxWidth = '760px';
  modal.style.maxHeight = '85vh';
  modal.style.background = '#ffffff';
  modal.style.borderRadius = '12px';
  modal.style.border = '1px solid #e2e8f0';
  modal.style.display = 'flex';
  modal.style.flexDirection = 'column';
  modal.style.overflow = 'hidden';

  const modalHeader = document.createElement('div');
  modalHeader.style.display = 'flex';
  modalHeader.style.alignItems = 'center';
  modalHeader.style.justifyContent = 'space-between';
  modalHeader.style.gap = '8px';
  modalHeader.style.padding = '12px 14px';
  modalHeader.style.borderBottom = '1px solid #e2e8f0';

  const modalTitle = document.createElement('div');
  modalTitle.style.fontWeight = '600';
  modalTitle.style.color = '#0f172a';
  modalTitle.textContent = 'Ticket details';

  const closeModalButton = document.createElement('button');
  closeModalButton.type = 'button';
  closeModalButton.textContent = 'Close';

  const modalBody = document.createElement('div');
  modalBody.style.padding = '12px 14px';
  modalBody.style.overflowY = 'auto';
  modalBody.style.display = 'flex';
  modalBody.style.flexDirection = 'column';
  modalBody.style.gap = '12px';

  const modalInlineError = document.createElement('div');
  modalInlineError.style.display = 'none';
  modalInlineError.style.color = '#b91c1c';
  modalInlineError.style.fontSize = '13px';

  const ticketDetails = document.createElement('div');
  ticketDetails.style.display = 'grid';
  ticketDetails.style.gridTemplateColumns = 'minmax(160px, 220px) 1fr';
  ticketDetails.style.gap = '6px 10px';

  const statusControls = document.createElement('div');
  statusControls.style.display = 'flex';
  statusControls.style.gap = '8px';
  statusControls.style.alignItems = 'center';
  statusControls.style.flexWrap = 'wrap';

  const statusSelect = document.createElement('select');
  statusSelect.style.padding = '8px 10px';
  statusSelect.style.borderRadius = '6px';
  statusSelect.style.border = '1px solid #cbd5e1';

  const updateStatusButton = document.createElement('button');
  updateStatusButton.type = 'button';
  updateStatusButton.textContent = 'Update status';
  updateStatusButton.style.padding = '8px 12px';
  updateStatusButton.style.border = '1px solid #cbd5e1';
  updateStatusButton.style.borderRadius = '6px';
  updateStatusButton.style.background = '#fff';

  statusControls.append(statusSelect, updateStatusButton);

  const notesTitle = document.createElement('div');
  notesTitle.textContent = 'Notes';
  notesTitle.style.fontWeight = '600';

  const notesList = document.createElement('div');
  notesList.style.display = 'flex';
  notesList.style.flexDirection = 'column';
  notesList.style.gap = '8px';

  const addNoteControls = document.createElement('div');
  addNoteControls.style.display = 'flex';
  addNoteControls.style.flexDirection = 'column';
  addNoteControls.style.gap = '8px';

  const noteTextarea = document.createElement('textarea');
  noteTextarea.rows = 3;
  noteTextarea.placeholder = 'Add a note';
  noteTextarea.style.padding = '8px 10px';
  noteTextarea.style.borderRadius = '6px';
  noteTextarea.style.border = '1px solid #cbd5e1';

  const addNoteButton = document.createElement('button');
  addNoteButton.type = 'button';
  addNoteButton.textContent = 'Add note';
  addNoteButton.style.padding = '8px 12px';
  addNoteButton.style.border = '1px solid #cbd5e1';
  addNoteButton.style.borderRadius = '6px';
  addNoteButton.style.background = '#fff';
  addNoteButton.style.alignSelf = 'flex-start';

  addNoteControls.append(noteTextarea, addNoteButton);

  modalHeader.append(modalTitle, closeModalButton);
  modalBody.append(modalInlineError, ticketDetails, statusControls, notesTitle, notesList, addNoteControls);
  modal.append(modalHeader, modalBody);
  modalOverlay.appendChild(modal);
  document.body.appendChild(modalOverlay);

  const updateHash = () => {
    const params = new URLSearchParams();
    if (state.siteId) {
      params.set('siteId', state.siteId);
    }
    if (state.page > 1) {
      params.set('page', String(state.page));
    }

    const queryString = params.toString();
    const nextHash = `#/tickets${queryString ? `?${queryString}` : ''}`;
    if (window.location.hash !== nextHash) {
      window.location.hash = nextHash;
    }
  };

  const setSiteOptions = () => {
    siteSelect.innerHTML = '';

    const placeholder = document.createElement('option');
    placeholder.value = '';
    placeholder.textContent = state.loadingSites ? 'Loading sites...' : 'Select a site';
    siteSelect.appendChild(placeholder);

    state.sites.forEach((site) => {
      const option = document.createElement('option');
      option.value = getSiteId(site);
      option.textContent = site.domain || option.value;
      siteSelect.appendChild(option);
    });

    siteSelect.value = state.siteId || '';
  };

  const setModalError = (message) => {
    modalInlineError.textContent = message || '';
    modalInlineError.style.display = message ? 'block' : 'none';
  };

  const renderTicketDetails = () => {
    ticketDetails.innerHTML = '';

    if (modalState.loading) {
      const loading = document.createElement('div');
      loading.textContent = 'Loading ticket details...';
      loading.style.color = '#64748b';
      ticketDetails.appendChild(loading);
      statusSelect.disabled = true;
      updateStatusButton.disabled = true;
      addNoteButton.disabled = true;
      noteTextarea.disabled = true;
      notesList.innerHTML = '';
      return;
    }

    const ticket = modalState.ticket;
    if (!ticket) {
      const empty = document.createElement('div');
      empty.textContent = 'Ticket details unavailable.';
      empty.style.color = '#64748b';
      ticketDetails.appendChild(empty);
      return;
    }

    const currentStatus = TICKET_STATUSES.includes(ticket.status) ? ticket.status : 'Open';
    const statusOptions = getNextStatusOptions(currentStatus);
    statusSelect.innerHTML = '';
    statusOptions.forEach((status) => {
      const option = document.createElement('option');
      option.value = status;
      option.textContent = status;
      statusSelect.appendChild(option);
    });

    statusSelect.disabled = currentStatus === 'Closed';
    updateStatusButton.disabled = currentStatus === 'Closed';
    addNoteButton.disabled = false;
    noteTextarea.disabled = false;
    statusSelect.value = currentStatus;

    const fields = [
      ['Subject', ticket.subject || '—'],
      ['Status', ticket.status || '—'],
      ['Description', ticket.description || '—'],
      ['Created', formatDate(ticket.createdAtUtc)],
      ['Updated', formatDate(ticket.updatedAtUtc)],
      ['Site ID', ticket.siteId || '—'],
      ['Visitor ID', ticket.visitorId || '—'],
      ['Engage Session ID', ticket.engageSessionId || '—'],
      ['Assigned To User ID', ticket.assignedToUserId || '—'],
    ];

    fields.forEach(([label, value]) => {
      const key = document.createElement('div');
      key.textContent = label;
      key.style.fontWeight = '600';
      key.style.color = '#334155';

      const val = document.createElement('div');
      val.textContent = String(value);
      val.style.whiteSpace = 'pre-wrap';

      ticketDetails.append(key, val);
    });

    notesList.innerHTML = '';
    if (!modalState.notes.length) {
      const empty = document.createElement('div');
      empty.textContent = 'No notes yet.';
      empty.style.color = '#64748b';
      notesList.appendChild(empty);
      return;
    }

    modalState.notes.forEach((note) => {
      const row = document.createElement('div');
      row.style.border = '1px solid #e2e8f0';
      row.style.borderRadius = '8px';
      row.style.padding = '8px 10px';

      const meta = document.createElement('div');
      meta.textContent = `${formatDate(note.createdAtUtc)} • ${note.authorUserId || '—'}`;
      meta.style.fontSize = '12px';
      meta.style.color = '#64748b';

      const content = document.createElement('div');
      content.textContent = note.content || '—';
      content.style.marginTop = '4px';
      content.style.whiteSpace = 'pre-wrap';

      row.append(meta, content);
      notesList.appendChild(row);
    });
  };

  const closeModal = () => {
    modalOverlay.style.display = 'none';
    modalState.ticketId = '';
    setModalError('');
  };

  const loadTicketModalData = async (ticketId) => {
    try {
      const ticketPromise = client.tickets?.getTicket
        ? client.tickets.getTicket(ticketId)
        : client.request(`/tickets/${encodeURIComponent(ticketId)}`);
      const notesPromise = client.tickets?.getTicketNotes
        ? client.tickets.getTicketNotes(ticketId)
        : client.request(`/tickets/${encodeURIComponent(ticketId)}/notes`);

      const [ticket, notes] = await Promise.all([ticketPromise, notesPromise]);
      modalState.ticket = ticket;
      modalState.notes = Array.isArray(notes) ? notes : [];
    } catch (error) {
      modalState.ticket = null;
      modalState.notes = [];
      const apiError = mapApiError(error);
      setModalError(apiError.message);
      notifier.show({ message: apiError.message, variant: 'danger' });
    }
  };

  const openTicketModal = async (ticketId) => {
    const normalizedTicketId = getTicketIdValue(ticketId);
    if (!normalizedTicketId) {
      notifier.show({ message: 'Invalid ticket id', variant: 'danger' });
      return;
    }

    modalState.ticketId = normalizedTicketId;
    modalState.ticket = null;
    modalState.notes = [];
    modalState.loading = true;
    setModalError('');
    renderTicketDetails();
    modalOverlay.style.display = 'flex';

    try {
      await loadTicketModalData(normalizedTicketId);
    } catch (error) {
      setModalError(mapApiError(error).message);
    } finally {
      modalState.loading = false;
      renderTicketDetails();
    }
  };

  closeModalButton.addEventListener('click', closeModal);
  modalOverlay.addEventListener('click', (event) => {
    if (event.target === modalOverlay) {
      closeModal();
    }
  });

  addNoteButton.addEventListener('click', async () => {
    const content = noteTextarea.value.trim();
    if (!content || !modalState.ticketId) {
      return;
    }

    setModalError('');
    addNoteButton.disabled = true;
    try {
      if (client.tickets?.addTicketNote) {
        await client.tickets.addTicketNote(modalState.ticketId, content);
      } else {
        await client.request(`/tickets/${encodeURIComponent(modalState.ticketId)}/notes`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({ content }),
        });
      }
      noteTextarea.value = '';
      await loadTicketModalData(modalState.ticketId);
      notifier.show({ message: 'Note added.', variant: 'success' });
      renderTicketDetails();
    } catch (error) {
      setModalError(mapApiError(error).message);
    } finally {
      addNoteButton.disabled = false;
    }
  });

  updateStatusButton.addEventListener('click', async () => {
    if (!modalState.ticketId) {
      return;
    }

    setModalError('');
    updateStatusButton.disabled = true;
    try {
      if (client.tickets?.transitionTicketStatus) {
        await client.tickets.transitionTicketStatus(modalState.ticketId, statusSelect.value);
      } else {
        await client.request(`/tickets/${encodeURIComponent(modalState.ticketId)}/status`, {
          method: 'PUT',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({ status: statusSelect.value }),
        });
      }
      await loadTicketModalData(modalState.ticketId);
      notifier.show({ message: 'Ticket status updated.', variant: 'success' });
      renderTicketDetails();
      await loadTickets();
    } catch (error) {
      setModalError(mapApiError(error).message);
    } finally {
      updateStatusButton.disabled = false;
    }
  });

  const renderTickets = () => {
    listBody.innerHTML = '';

    if (state.loadingTickets) {
      listBody.textContent = 'Loading tickets...';
      listBody.style.color = '#64748b';
      pageInfo.textContent = `Page ${state.page}`;
      prevButton.disabled = true;
      nextButton.disabled = true;
      return;
    }

    if (!state.siteId) {
      listBody.textContent = 'Select a site to view tickets.';
      listBody.style.color = '#64748b';
      pageInfo.textContent = 'Page 1';
      prevButton.disabled = true;
      nextButton.disabled = true;
      return;
    }

    if (!state.tickets.length) {
      listBody.textContent = 'No tickets found for this site.';
      listBody.style.color = '#64748b';
      pageInfo.textContent = `Page ${state.page}`;
      prevButton.disabled = state.page <= 1;
      nextButton.disabled = true;
      return;
    }

    const table = document.createElement('table');
    table.className = 'ui-table';

    const headerRow = document.createElement('tr');
    ['ID', 'Subject', 'Status', 'Updated'].forEach((label) => {
      const th = document.createElement('th');
      th.textContent = label;
      headerRow.appendChild(th);
    });

    const thead = document.createElement('thead');
    thead.appendChild(headerRow);
    table.appendChild(thead);

    const tbody = document.createElement('tbody');
    state.tickets.forEach((ticket) => {
      const tr = document.createElement('tr');
      tr.style.cursor = 'pointer';
      [
        toShortId(ticket.id),
        ticket.subject || '—',
        ticket.status || '—',
        formatDate(ticket.updatedAtUtc),
      ].forEach((value) => {
        const td = document.createElement('td');
        td.textContent = String(value);
        tr.appendChild(td);
      });
      tr.addEventListener('click', () => {
        void openTicketModal(getTicketIdValue(ticket.id) || getTicketIdValue(ticket));
      });
      tbody.appendChild(tr);
    });

    table.appendChild(tbody);
    listBody.appendChild(table);

    pageInfo.textContent = `Page ${state.page}`;
    prevButton.disabled = state.page <= 1;
    nextButton.disabled = state.tickets.length < state.pageSize;
  };

  const loadTickets = async () => {
    renderTickets();

    if (!state.siteId) {
      state.tickets = [];
      renderTickets();
      return;
    }

    state.loadingTickets = true;
    renderTickets();

    try {
      state.tickets = client.tickets?.listTickets
        ? await client.tickets.listTickets({ siteId: state.siteId, page: state.page, pageSize: state.pageSize })
        : await client.request(`/tickets?siteId=${encodeURIComponent(state.siteId)}&page=${state.page}&pageSize=${state.pageSize}`);
    } catch (error) {
      state.tickets = [];
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    } finally {
      state.loadingTickets = false;
      renderTickets();
    }
  };

  siteSelect.addEventListener('change', async () => {
    state.siteId = siteSelect.value;
    state.page = 1;
    saveSelectedSiteId(state.siteId);
    updateHash();
    await loadTickets();
  });

  prevButton.addEventListener('click', async () => {
    if (state.page <= 1 || state.loadingTickets) {
      return;
    }

    state.page -= 1;
    updateHash();
    await loadTickets();
  });

  nextButton.addEventListener('click', async () => {
    if (state.loadingTickets || state.tickets.length < state.pageSize) {
      return;
    }

    state.page += 1;
    updateHash();
    await loadTickets();
  });

  page.append(
    createCard({ title: 'Tickets filters', body: controls }),
    createCard({ title: 'Tickets', body: listBody }),
    pagination
  );

  container.appendChild(page);

  try {
    state.sites = await client.sites.list();
    setSiteOptions();
    if (!state.siteId && state.sites.length > 0) {
      state.siteId = getSiteId(state.sites[0]);
      siteSelect.value = state.siteId;
      saveSelectedSiteId(state.siteId);
      updateHash();
    }
    state.loadingSites = false;
    setSiteOptions();
  } catch (error) {
    state.loadingSites = false;
    setSiteOptions();
    notifier.show({ message: mapApiError(error).message, variant: 'danger' });
  }

  await loadTickets();
};
