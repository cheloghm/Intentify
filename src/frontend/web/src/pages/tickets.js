import { createApiClient, mapApiError } from '../shared/apiClient.js';
import { createToastManager } from '../shared/ui/index.js';

const getSiteId = (site) => site?.siteId || site?.id || '';

const getTicketId = (ticket) => {
  const raw = ticket?.id || ticket?.ticketId;
  if (typeof raw === 'string') return raw;
  if (raw && typeof raw === 'object') return raw.value || raw.id || raw._id || '';
  return typeof ticket === 'string' ? ticket : '';
};

const formatDate = (value) => {
  if (!value) return '—';
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
};

const truncate = (str, max) => {
  if (!str) return '—';
  return str.length > max ? `${str.slice(0, max)}…` : str;
};

const statusVariant = (status) => {
  const s = String(status || '').toLowerCase().replace(/[^a-z]/g, '');
  if (s === 'open') return 'warning';
  if (s === 'inprogress') return 'info';
  if (s === 'closed' || s === 'resolved') return 'success';
  return 'neutral';
};

const statusLabel = (status) => {
  const s = String(status || '').toLowerCase().replace(/[^a-z]/g, '');
  if (s === 'inprogress') return 'In Progress';
  if (s === 'open') return 'Open';
  if (s === 'closed') return 'Closed';
  if (s === 'resolved') return 'Resolved';
  return status || '—';
};

const opportunityVariant = (label) => {
  if (!label) return 'neutral';
  const l = label.toLowerCase();
  if (l.includes('evaluating')) return 'warning';
  if (l.includes('deciding')) return 'info';
  if (l.includes('won')) return 'success';
  if (l.includes('lost')) return 'danger';
  return 'neutral';
};

const SECTION_LABELS = [
  'Visitor Overview',
  'What They Need',
  'Key Details',
  'What They Have Not Considered',
  'Their Concerns',
  'Recommended Next Step',
  'Conversation Tone',
];

const parseDescription = (description) => {
  if (!description) return [];
  const parts = description.split(/\n?\d+\.\s+/).filter(Boolean);
  return parts.map((content, i) => ({
    label: SECTION_LABELS[i] || `Section ${i + 1}`,
    content: content.trim(),
  }));
};

const PAGE_SIZE = 10;

export const renderTicketsView = async (container, { apiClient, toast } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();

  const state = {
    sites: [],
    siteId: '',
    statusFilter: '',
    tickets: [],
    currentPage: 1,
  };

  // ── Root ──────────────────────────────────────────────────────────────
  const page = document.createElement('div');
  page.className = 'stack';
  page.style.maxWidth = '1100px';
  page.style.width = '100%';

  // ── Header ────────────────────────────────────────────────────────────
  const pageHeader = document.createElement('div');
  pageHeader.className = 'page-header';
  const titleGroup = document.createElement('div');
  const titleEl = document.createElement('h2');
  titleEl.className = 'page-title';
  titleEl.textContent = 'Tickets';
  const subtitleEl = document.createElement('div');
  subtitleEl.className = 'page-subtitle';
  subtitleEl.textContent = 'Support escalations from your Engage conversations';
  titleGroup.append(titleEl, subtitleEl);
  pageHeader.append(titleGroup);

  // ── Filters ───────────────────────────────────────────────────────────
  const filtersRow = document.createElement('div');
  filtersRow.style.display = 'flex';
  filtersRow.style.gap = '10px';
  filtersRow.style.alignItems = 'center';
  filtersRow.style.flexWrap = 'wrap';

  const siteSelect = document.createElement('select');
  siteSelect.className = 'form-select';
  siteSelect.style.minWidth = '220px';

  const statusFilterSelect = document.createElement('select');
  statusFilterSelect.className = 'form-select';
  statusFilterSelect.style.minWidth = '160px';
  [['', 'All Statuses'], ['Open', 'Open'], ['InProgress', 'In Progress'], ['Closed', 'Closed']].forEach(([value, label]) => {
    const opt = document.createElement('option');
    opt.value = value;
    opt.textContent = label;
    statusFilterSelect.appendChild(opt);
  });

  filtersRow.append(siteSelect, statusFilterSelect);

  // ── Metrics ───────────────────────────────────────────────────────────
  const metricsGrid = document.createElement('div');
  metricsGrid.className = 'grid-3';

  const makeMetric = (icon, label, iconBg) => {
    const card = document.createElement('div');
    card.className = 'metric-card';
    const iconEl = document.createElement('div');
    iconEl.className = 'metric-icon';
    iconEl.textContent = icon;
    iconEl.style.background = iconBg;
    const labelEl = document.createElement('div');
    labelEl.className = 'metric-label';
    labelEl.textContent = label;
    const valueEl = document.createElement('div');
    valueEl.className = 'metric-value';
    valueEl.textContent = '—';
    card.append(iconEl, labelEl, valueEl);
    return { card, valueEl };
  };

  const mTotal    = makeMetric('🎫', 'Total Tickets', 'var(--brand-primary-light)');
  const mOpen     = makeMetric('🔓', 'Open',          'var(--color-warning-light)');
  const mResolved = makeMetric('✅', 'Resolved',      'var(--color-success-light)');
  metricsGrid.append(mTotal.card, mOpen.card, mResolved.card);

  // ── Table card ────────────────────────────────────────────────────────
  const tableCard = document.createElement('div');
  tableCard.className = 'card';

  const tableCardHeader = document.createElement('div');
  tableCardHeader.className = 'card-header';
  const tableTitle = document.createElement('div');
  tableTitle.className = 'card-title';
  tableTitle.textContent = 'All Tickets';
  tableCardHeader.append(tableTitle);

  const tableWrapper = document.createElement('div');
  tableWrapper.className = 'table-wrapper';

  const paginationEl = document.createElement('div');
  paginationEl.className = 'pagination';
  const paginationInfo = document.createElement('div');
  const paginationControls = document.createElement('div');
  paginationControls.className = 'pagination-controls';
  paginationEl.append(paginationInfo, paginationControls);

  tableCard.append(tableCardHeader, tableWrapper, paginationEl);

  // ── Modal ─────────────────────────────────────────────────────────────
  const existingOverlay = document.getElementById('tickets-modal-overlay');
  if (existingOverlay) existingOverlay.remove();

  const modalOverlay = document.createElement('div');
  modalOverlay.id = 'tickets-modal-overlay';
  modalOverlay.style.cssText = 'position:fixed;inset:0;background:rgba(15,23,42,0.5);display:none;align-items:center;justify-content:center;z-index:1000;padding:20px;';

  const modalCard = document.createElement('div');
  modalCard.className = 'card';
  modalCard.style.cssText = 'width:100%;max-width:680px;max-height:90vh;overflow-y:auto;';

  modalOverlay.appendChild(modalCard);
  document.body.appendChild(modalOverlay);

  const handleModalKey = (e) => { if (e.key === 'Escape') closeModal(); };

  const closeModal = () => {
    modalOverlay.style.display = 'none';
    document.removeEventListener('keydown', handleModalKey);
  };

  modalOverlay.addEventListener('click', (e) => { if (e.target === modalOverlay) closeModal(); });

  const renderModal = (ticket) => {
    modalCard.innerHTML = '';

    // Header
    const mHeader = document.createElement('div');
    mHeader.className = 'card-header';
    mHeader.style.marginBottom = '20px';

    const mTitleGroup = document.createElement('div');
    const mSubject = document.createElement('div');
    mSubject.className = 'card-title';
    mSubject.textContent = ticket.subject || '(no subject)';
    const mStatusBadge = document.createElement('span');
    mStatusBadge.className = `badge badge-${statusVariant(ticket.status)}`;
    mStatusBadge.textContent = statusLabel(ticket.status);
    mStatusBadge.style.marginTop = '4px';
    mStatusBadge.style.display = 'inline-block';
    mTitleGroup.append(mSubject, mStatusBadge);

    const closeBtn = document.createElement('button');
    closeBtn.className = 'btn btn-secondary btn-sm';
    closeBtn.textContent = '×';
    closeBtn.setAttribute('aria-label', 'Close');
    closeBtn.addEventListener('click', closeModal);

    mHeader.append(mTitleGroup, closeBtn);
    modalCard.appendChild(mHeader);

    // 7-section description
    const sections = parseDescription(ticket.description);
    if (sections.length > 0) {
      const sectionsWrap = document.createElement('div');

      sections.forEach((section, i) => {
        const sectionEl = document.createElement('div');
        if (i > 0) {
          sectionEl.style.borderTop = '1px solid var(--color-border)';
          sectionEl.style.paddingTop = '14px';
          sectionEl.style.marginTop = '14px';
        }

        const labelEl = document.createElement('div');
        labelEl.style.cssText = 'font-size:11px;font-weight:600;text-transform:uppercase;letter-spacing:0.06em;color:var(--brand-primary);margin-bottom:6px;';
        labelEl.textContent = section.label;

        const contentEl = document.createElement('div');
        contentEl.style.cssText = 'font-size:14px;color:var(--color-text-secondary);line-height:1.6;white-space:pre-wrap;';
        contentEl.textContent = section.content;

        sectionEl.append(labelEl, contentEl);
        sectionsWrap.appendChild(sectionEl);
      });

      modalCard.appendChild(sectionsWrap);
    } else if (ticket.description) {
      // Fallback: render description as plain text
      const descEl = document.createElement('div');
      descEl.style.cssText = 'font-size:14px;color:var(--color-text-secondary);line-height:1.6;white-space:pre-wrap;';
      descEl.textContent = ticket.description;
      modalCard.appendChild(descEl);
    }

    // Contact details row
    const contactSection = document.createElement('div');
    contactSection.style.cssText = 'margin-top:20px;padding-top:20px;border-top:1px solid var(--color-border);display:grid;grid-template-columns:repeat(3,1fr);gap:12px;';

    const makeContactField = (label, value) => {
      const wrap = document.createElement('div');
      const lbl = document.createElement('div');
      lbl.style.cssText = 'font-size:11px;font-weight:500;color:var(--color-text-muted);text-transform:uppercase;letter-spacing:0.04em;margin-bottom:2px;';
      lbl.textContent = label;
      const val = document.createElement('div');
      val.style.cssText = 'font-size:13px;color:var(--color-text);';
      val.textContent = value || '—';
      wrap.append(lbl, val);
      return wrap;
    };

    contactSection.append(
      makeContactField('Contact Name', ticket.contactName),
      makeContactField('Preferred Contact', ticket.preferredContactMethod),
      makeContactField('Contact Detail', ticket.preferredContactDetail),
    );
    modalCard.appendChild(contactSection);

    // Suggested follow-up / conversation summary
    const followUpText = ticket.suggestedFollowUp || ticket.conversationSummary;
    if (followUpText) {
      const followUpSection = document.createElement('div');
      followUpSection.style.cssText = 'margin-top:20px;padding-top:20px;border-top:1px solid var(--color-border);';

      const followUpLabel = document.createElement('div');
      followUpLabel.style.cssText = 'font-weight:600;font-size:13px;margin-bottom:10px;color:var(--color-text);';
      followUpLabel.textContent = ticket.suggestedFollowUp ? 'Suggested Follow-Up' : 'Conversation Summary';

      const followUpBox = document.createElement('div');
      followUpBox.style.cssText = 'border-left:3px solid var(--brand-primary);background:var(--brand-primary-light);padding:12px 16px;border-radius:0 var(--radius-sm) var(--radius-sm) 0;font-size:14px;color:var(--color-text-secondary);line-height:1.6;';
      followUpBox.textContent = followUpText;

      followUpSection.append(followUpLabel, followUpBox);
      modalCard.appendChild(followUpSection);
    }

    // Status transition buttons
    const ticketId = getTicketId(ticket);
    const s = String(ticket.status || '').toLowerCase().replace(/[^a-z]/g, '');
    const statusActions = [];
    if (s === 'open') {
      statusActions.push({ label: 'Start Progress', targetStatus: 'InProgress', variant: 'btn-primary' });
    } else if (s === 'inprogress') {
      statusActions.push({ label: 'Mark Resolved', targetStatus: 'Resolved', variant: 'btn-primary' });
    } else if (s === 'resolved') {
      statusActions.push({ label: 'Mark Closed', targetStatus: 'Closed', variant: 'btn-primary' });
      statusActions.push({ label: 'Reopen', targetStatus: 'Open', variant: 'btn-secondary' });
    } else if (s === 'closed') {
      statusActions.push({ label: 'Reopen', targetStatus: 'Open', variant: 'btn-secondary' });
    }

    const actionsRow = document.createElement('div');
    actionsRow.style.cssText = 'margin-top:20px;padding-top:20px;border-top:1px solid var(--color-border);display:flex;gap:8px;align-items:center;';

    if (statusActions.length === 0) {
      const closedText = document.createElement('div');
      closedText.style.cssText = 'font-size:13px;color:var(--color-text-muted);';
      closedText.textContent = 'Ticket closed';
      actionsRow.appendChild(closedText);
    } else {
      statusActions.forEach(({ label, targetStatus, variant }) => {
        const btn = document.createElement('button');
        btn.className = `btn ${variant} btn-sm`;
        btn.textContent = label;
        btn.addEventListener('click', async () => {
          btn.disabled = true;
          btn.textContent = 'Saving…';
          try {
            await client.tickets.transitionTicketStatus(ticketId, targetStatus);
            // Update the ticket in state so the table re-renders with the new badge
            const idx = state.tickets.findIndex((t) => getTicketId(t) === ticketId);
            if (idx !== -1) state.tickets[idx] = { ...state.tickets[idx], status: targetStatus };
            updateMetrics();
            renderTable();
            // Re-render the modal in-place with the updated status
            renderModal({ ...ticket, status: targetStatus });
            notifier.show({ message: `Ticket marked as ${statusLabel(targetStatus)}.`, variant: 'success' });
          } catch (error) {
            notifier.show({ message: mapApiError(error).message, variant: 'danger' });
            btn.disabled = false;
            btn.textContent = label;
          }
        });
        actionsRow.appendChild(btn);
      });
    }

    modalCard.appendChild(actionsRow);
  };

  const openModal = async (ticketId) => {
    modalCard.innerHTML = '<div style="padding:32px;text-align:center;color:var(--color-text-muted)">Loading…</div>';
    modalOverlay.style.display = 'flex';
    document.addEventListener('keydown', handleModalKey);

    try {
      const ticket = await client.tickets.getTicket(ticketId);
      renderModal(ticket);
    } catch (error) {
      modalCard.innerHTML = `<div style="padding:24px;color:var(--color-danger)">${mapApiError(error).message}</div>`;
    }
  };

  // ── Table render ──────────────────────────────────────────────────────
  const updateMetrics = () => {
    const total = state.tickets.length;
    mTotal.valueEl.textContent = total || '—';
    mOpen.valueEl.textContent = state.tickets.filter((t) =>
      String(t.status || '').toLowerCase().replace(/[^a-z]/g, '') === 'open'
    ).length || '—';
    mResolved.valueEl.textContent = state.tickets.filter((t) => {
      const sv = String(t.status || '').toLowerCase().replace(/[^a-z]/g, '');
      return sv === 'closed' || sv === 'resolved';
    }).length || '—';
  };

  const renderTable = () => {
    tableWrapper.innerHTML = '';
    paginationInfo.textContent = '';
    paginationControls.innerHTML = '';

    const filtered = state.tickets.filter((t) => {
      if (!state.statusFilter) return true;
      const sv = String(t.status || '').toLowerCase().replace(/[^a-z]/g, '');
      return sv === state.statusFilter.toLowerCase().replace(/[^a-z]/g, '');
    });

    const total = filtered.length;

    if (!total) {
      const empty = document.createElement('div');
      empty.className = 'empty-state';
      const icon = document.createElement('div');
      icon.className = 'empty-state-icon';
      icon.textContent = '🎫';
      const emptyTitle = document.createElement('div');
      emptyTitle.className = 'empty-state-title';
      emptyTitle.textContent = state.siteId ? 'No tickets found' : 'Select a site';
      const emptyDesc = document.createElement('div');
      emptyDesc.className = 'empty-state-desc';
      emptyDesc.textContent = state.siteId
        ? 'Tickets are created when AI conversations are escalated.'
        : 'Choose a site from the dropdown above.';
      empty.append(icon, emptyTitle, emptyDesc);
      tableWrapper.appendChild(empty);
      return;
    }

    const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
    const pg = Math.min(state.currentPage, totalPages);
    const start = (pg - 1) * PAGE_SIZE;
    const end = Math.min(start + PAGE_SIZE, total);
    const slice = filtered.slice(start, end);

    const table = document.createElement('table');
    table.className = 'data-table';

    const thead = document.createElement('thead');
    const headRow = document.createElement('tr');
    ['Subject', 'Contact', 'Status', 'Opportunity', 'Created', 'Actions'].forEach((label) => {
      const th = document.createElement('th');
      th.textContent = label;
      headRow.appendChild(th);
    });
    thead.appendChild(headRow);
    table.appendChild(thead);

    const tbody = document.createElement('tbody');
    slice.forEach((ticket) => {
      const tr = document.createElement('tr');
      tr.style.cursor = 'pointer';
      const ticketId = getTicketId(ticket);
      tr.addEventListener('click', () => openModal(ticketId));

      const subjectTd = document.createElement('td');
      const subjectSpan = document.createElement('span');
      subjectSpan.className = 'text-primary';
      subjectSpan.textContent = truncate(ticket.subject, 60);
      subjectTd.appendChild(subjectSpan);

      const contactTd = document.createElement('td');
      contactTd.textContent = ticket.contactName || ticket.preferredContactDetail || '—';

      const statusTd = document.createElement('td');
      const statusBadge = document.createElement('span');
      statusBadge.className = `badge badge-${statusVariant(ticket.status)}`;
      statusBadge.textContent = statusLabel(ticket.status);
      statusTd.appendChild(statusBadge);

      const oppTd = document.createElement('td');
      if (ticket.opportunityLabel) {
        const oppBadge = document.createElement('span');
        oppBadge.className = `badge badge-${opportunityVariant(ticket.opportunityLabel)}`;
        oppBadge.textContent = ticket.opportunityLabel;
        oppTd.appendChild(oppBadge);
      } else {
        oppTd.textContent = '—';
      }

      const createdTd = document.createElement('td');
      createdTd.textContent = formatDate(ticket.createdAtUtc);

      const actionTd = document.createElement('td');
      const viewBtn = document.createElement('button');
      viewBtn.className = 'btn btn-sm btn-secondary';
      viewBtn.textContent = 'View';
      viewBtn.addEventListener('click', (e) => { e.stopPropagation(); openModal(ticketId); });
      actionTd.appendChild(viewBtn);

      tr.append(subjectTd, contactTd, statusTd, oppTd, createdTd, actionTd);
      tbody.appendChild(tr);
    });

    table.appendChild(tbody);
    tableWrapper.appendChild(table);

    paginationInfo.textContent = `Showing ${start + 1}–${end} of ${total} ticket${total !== 1 ? 's' : ''}`;

    if (totalPages > 1) {
      const prevBtn = document.createElement('button');
      prevBtn.className = 'page-btn';
      prevBtn.textContent = '← Prev';
      prevBtn.disabled = pg <= 1;
      prevBtn.addEventListener('click', () => { state.currentPage = pg - 1; renderTable(); });

      const pageSpan = document.createElement('span');
      pageSpan.style.cssText = 'padding:0 6px;font-size:12px;color:var(--color-text-muted);';
      pageSpan.textContent = `${pg} / ${totalPages}`;

      const nextBtn = document.createElement('button');
      nextBtn.className = 'page-btn';
      nextBtn.textContent = 'Next →';
      nextBtn.disabled = pg >= totalPages;
      nextBtn.addEventListener('click', () => { state.currentPage = pg + 1; renderTable(); });

      paginationControls.append(prevBtn, pageSpan, nextBtn);
    }
  };

  const loadTickets = async () => {
    if (!state.siteId) {
      state.tickets = [];
      updateMetrics();
      renderTable();
      return;
    }

    tableWrapper.innerHTML = '<div style="padding:32px;text-align:center;color:var(--color-text-muted)">Loading…</div>';
    try {
      const result = await client.tickets.listTickets({ siteId: state.siteId, page: 1, pageSize: 200 });
      state.tickets = Array.isArray(result) ? result : [];
    } catch (error) {
      state.tickets = [];
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
    state.currentPage = 1;
    updateMetrics();
    renderTable();
  };

  const setSiteOptions = () => {
    siteSelect.innerHTML = '';
    const allOpt = document.createElement('option');
    allOpt.value = '';
    allOpt.textContent = 'All sites';
    siteSelect.appendChild(allOpt);
    state.sites.forEach((site) => {
      const opt = document.createElement('option');
      opt.value = getSiteId(site);
      opt.textContent = site.domain || opt.value;
      siteSelect.appendChild(opt);
    });
    siteSelect.value = state.siteId;
  };

  siteSelect.addEventListener('change', async () => {
    state.siteId = siteSelect.value;
    await loadTickets();
  });

  statusFilterSelect.addEventListener('change', () => {
    state.statusFilter = statusFilterSelect.value;
    state.currentPage = 1;
    renderTable();
  });

  // ── Assemble ──────────────────────────────────────────────────────────
  page.append(pageHeader, filtersRow, metricsGrid, tableCard);
  container.appendChild(page);

  updateMetrics();
  renderTable();

  try {
    state.sites = await client.sites.list();
    if (state.sites.length) state.siteId = getSiteId(state.sites[0]);
    setSiteOptions();
    await loadTickets();
  } catch (error) {
    notifier.show({ message: mapApiError(error).message, variant: 'danger' });
  }
};
