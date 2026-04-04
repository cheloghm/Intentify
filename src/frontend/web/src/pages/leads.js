import { createApiClient, mapApiError } from '../shared/apiClient.js';
import { createToastManager } from '../shared/ui/index.js';

const getSiteId = (site) => site?.siteId || site?.id || '';

const formatDate = (value) => {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
};

const opportunityVariant = (label) => {
  if (!label) return 'neutral';
  const l = label.toLowerCase();
  if (l.includes('evaluating')) return 'warning';
  if (l.includes('deciding'))   return 'info';
  if (l.includes('won'))        return 'success';
  if (l.includes('lost'))       return 'danger';
  return 'neutral';
};

const isWithinLastWeek = (dateStr) => {
  if (!dateStr) return false;
  const date = new Date(dateStr);
  if (Number.isNaN(date.getTime())) return false;
  const weekAgo = new Date();
  weekAgo.setDate(weekAgo.getDate() - 7);
  return date >= weekAgo;
};

const PAGE_SIZE = 10;

export const renderLeadsView = (container, { apiClient, toast } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();

  const state = {
    sites: [],
    siteId: '',
    leads: [],
    filtered: [],
    selected: null,
    currentPage: 1,
    search: '',
  };

  // ── Root ──────────────────────────────────────────────────────────────
  const page = document.createElement('div');
  page.className = 'stack';
  page.style.maxWidth = '1100px';
  page.style.width = '100%';

  // ── Page header ───────────────────────────────────────────────────────
  const pageHeader = document.createElement('div');
  pageHeader.className = 'page-header';
  const titleGroup = document.createElement('div');
  const titleEl = document.createElement('h1');
  titleEl.className = 'page-title';
  titleEl.textContent = 'Leads';
  const subtitleEl = document.createElement('div');
  subtitleEl.className = 'page-subtitle';
  subtitleEl.textContent = 'Captured leads from your website conversations';
  titleGroup.append(titleEl, subtitleEl);
  pageHeader.append(titleGroup);

  // ── Filters row ───────────────────────────────────────────────────────
  const filtersRow = document.createElement('div');
  filtersRow.style.display = 'flex';
  filtersRow.style.gap = '10px';
  filtersRow.style.alignItems = 'center';
  filtersRow.style.flexWrap = 'wrap';

  const siteSelect = document.createElement('select');
  siteSelect.className = 'form-select';
  siteSelect.style.minWidth = '220px';

  const searchInput = document.createElement('input');
  searchInput.className = 'form-input';
  searchInput.type = 'text';
  searchInput.placeholder = 'Search by name or email…';
  searchInput.style.maxWidth = '260px';

  filtersRow.append(siteSelect, searchInput);

  // ── Metric cards ──────────────────────────────────────────────────────
  const metricsGrid = document.createElement('div');
  metricsGrid.className = 'grid-3';

  const makeMetricCard = (icon, label, iconBg) => {
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

  const mTotal   = makeMetricCard('🧑‍💼', 'Total Leads',     'var(--brand-primary-light)');
  const mWeek    = makeMetricCard('📅',   'New This Week',   'var(--color-success-light)');
  const mConvert = makeMetricCard('🎯',   'Conversion Rate', 'var(--color-info-light)');
  metricsGrid.append(mTotal.card, mWeek.card, mConvert.card);

  // ── Table card ────────────────────────────────────────────────────────
  const tableCard = document.createElement('div');
  tableCard.className = 'card';

  const tableCardHeader = document.createElement('div');
  tableCardHeader.className = 'card-header';
  const tableTitleGroup = document.createElement('div');
  const tableTitleEl = document.createElement('div');
  tableTitleEl.className = 'card-title';
  tableTitleEl.textContent = 'All Leads';
  const tableCountEl = document.createElement('div');
  tableCountEl.className = 'card-subtitle';
  tableCountEl.textContent = 'Loading…';
  tableTitleGroup.append(tableTitleEl, tableCountEl);
  tableCardHeader.append(tableTitleGroup);

  const tableWrapper = document.createElement('div');
  tableWrapper.className = 'table-wrapper';

  const paginationEl = document.createElement('div');
  paginationEl.className = 'pagination';
  const paginationInfo = document.createElement('div');
  const paginationControls = document.createElement('div');
  paginationControls.className = 'pagination-controls';
  paginationEl.append(paginationInfo, paginationControls);

  tableCard.append(tableCardHeader, tableWrapper, paginationEl);

  // ── Lead detail modal ─────────────────────────────────────────────────
  const modalOverlay = document.createElement('div');
  modalOverlay.style.cssText = 'position:fixed;inset:0;background:rgba(15,23,42,0.5);display:none;align-items:center;justify-content:center;z-index:1000;padding:20px;';

  const modalCard = document.createElement('div');
  modalCard.className = 'card';
  modalCard.style.cssText = 'width:100%;max-width:580px;max-height:85vh;overflow-y:auto;position:relative;';

  modalOverlay.appendChild(modalCard);
  document.body.appendChild(modalOverlay);

  // ── Helpers ───────────────────────────────────────────────────────────
  const makeOpportunityBadge = (label) => {
    const badge = document.createElement('span');
    badge.className = `badge badge-${opportunityVariant(label)}`;
    badge.textContent = label || 'Unknown';
    return badge;
  };

  const makeDetailRow = (label, value) => {
    const row = document.createElement('div');
    const lbl = document.createElement('div');
    lbl.style.fontSize = '11px';
    lbl.style.color = 'var(--color-text-muted)';
    lbl.style.fontWeight = '500';
    lbl.style.textTransform = 'uppercase';
    lbl.style.letterSpacing = '0.04em';
    lbl.style.marginBottom = '2px';
    lbl.textContent = label;
    const val = document.createElement('div');
    val.style.fontSize = '14px';
    val.style.color = value ? 'var(--color-text)' : 'var(--color-text-muted)';
    val.textContent = value || '—';
    row.append(lbl, val);
    return row;
  };

  const applyFilter = () => {
    const q = state.search.toLowerCase();
    state.filtered = state.leads.filter((lead) => {
      if (!q) return true;
      return (lead.displayName || '').toLowerCase().includes(q)
          || (lead.primaryEmail || '').toLowerCase().includes(q);
    });
    state.currentPage = 1;
    updateMetrics();
    renderTable();
  };

  const updateMetrics = () => {
    const total = state.leads.length;
    const newThisWeek = state.leads.filter((l) => isWithinLastWeek(l.createdAtUtc)).length;
    mTotal.valueEl.textContent   = total || '—';
    mWeek.valueEl.textContent    = total > 0 ? newThisWeek : '—';
    mConvert.valueEl.textContent = '—';
  };

  const closeDetail = () => {
    state.selected = null;
    modalOverlay.style.display = 'none';
    document.removeEventListener('keydown', handleModalKeydown);
  };

  const handleModalKeydown = (e) => {
    if (e.key === 'Escape') closeDetail();
  };

  modalOverlay.addEventListener('click', (e) => {
    if (e.target === modalOverlay) closeDetail();
  });

  const renderDetail = (lead) => {
    modalCard.innerHTML = '';

    // Header
    const detailHeader = document.createElement('div');
    detailHeader.className = 'card-header';
    detailHeader.style.marginBottom = '20px';

    const detailTitleGroup = document.createElement('div');
    const detailName = document.createElement('div');
    detailName.className = 'card-title';
    detailName.textContent = lead.displayName || '(no name)';
    const detailEmail = document.createElement('div');
    detailEmail.className = 'card-subtitle';
    detailEmail.textContent = lead.primaryEmail || '(no email)';
    detailTitleGroup.append(detailName, detailEmail);

    const closeBtn = document.createElement('button');
    closeBtn.className = 'btn btn-secondary btn-sm';
    closeBtn.textContent = '×';
    closeBtn.setAttribute('aria-label', 'Close');
    closeBtn.addEventListener('click', closeDetail);

    detailHeader.append(detailTitleGroup, closeBtn);

    // Two-column body
    const detailBody = document.createElement('div');
    detailBody.className = 'grid-2';
    detailBody.style.gap = '24px';

    // Left: Contact details
    const leftCol = document.createElement('div');
    leftCol.className = 'stack';
    leftCol.style.gap = '12px';

    const leftTitle = document.createElement('div');
    leftTitle.style.fontWeight = '600';
    leftTitle.style.fontSize = '11px';
    leftTitle.style.color = 'var(--color-text-muted)';
    leftTitle.style.textTransform = 'uppercase';
    leftTitle.style.letterSpacing = '0.06em';
    leftTitle.textContent = 'Contact Details';

    leftCol.append(
      leftTitle,
      makeDetailRow('Name',              lead.displayName),
      makeDetailRow('Email',             lead.primaryEmail),
      makeDetailRow('Phone',             lead.phone),
      makeDetailRow('Location',          lead.location),
      makeDetailRow('Preferred Contact', lead.preferredContactMethod),
    );

    // Right: Lead intelligence
    const rightCol = document.createElement('div');
    rightCol.className = 'stack';
    rightCol.style.gap = '12px';

    const rightTitle = document.createElement('div');
    rightTitle.style.fontWeight = '600';
    rightTitle.style.fontSize = '11px';
    rightTitle.style.color = 'var(--color-text-muted)';
    rightTitle.style.textTransform = 'uppercase';
    rightTitle.style.letterSpacing = '0.06em';
    rightTitle.textContent = 'Lead Intelligence';

    const opportunityRow = document.createElement('div');
    const oppLbl = document.createElement('div');
    oppLbl.style.fontSize = '11px';
    oppLbl.style.color = 'var(--color-text-muted)';
    oppLbl.style.fontWeight = '500';
    oppLbl.style.textTransform = 'uppercase';
    oppLbl.style.letterSpacing = '0.04em';
    oppLbl.style.marginBottom = '4px';
    oppLbl.textContent = 'Opportunity';
    opportunityRow.append(oppLbl, makeOpportunityBadge(lead.opportunityLabel));

    rightCol.append(
      rightTitle,
      opportunityRow,
      makeDetailRow('Intent Score',         lead.intentScore != null ? String(lead.intentScore) : null),
      makeDetailRow('Conversation Summary', lead.conversationSummary),
    );

    detailBody.append(leftCol, rightCol);
    modalCard.append(detailHeader, detailBody);

    // Suggested follow-up (full-width, bottom)
    if (lead.suggestedFollowUp) {
      const followUpSection = document.createElement('div');
      followUpSection.style.marginTop = '20px';
      followUpSection.style.paddingTop = '20px';
      followUpSection.style.borderTop = '1px solid var(--color-border)';

      const followUpLabel = document.createElement('div');
      followUpLabel.style.fontWeight = '600';
      followUpLabel.style.fontSize = '13px';
      followUpLabel.style.marginBottom = '10px';
      followUpLabel.textContent = 'Suggested Follow-Up Message';

      const followUpBox = document.createElement('div');
      followUpBox.style.borderLeft = '3px solid var(--brand-primary)';
      followUpBox.style.background = 'var(--brand-primary-light)';
      followUpBox.style.padding = '12px 16px';
      followUpBox.style.borderRadius = '0 var(--radius-sm) var(--radius-sm) 0';
      followUpBox.style.fontSize = '14px';
      followUpBox.style.color = 'var(--color-text-secondary)';
      followUpBox.style.lineHeight = '1.6';
      followUpBox.textContent = lead.suggestedFollowUp;

      followUpSection.append(followUpLabel, followUpBox);
      modalCard.appendChild(followUpSection);
    }

    // Linked visitor
    if (lead.linkedVisitorId) {
      const visitorLinkRow = document.createElement('div');
      visitorLinkRow.style.marginTop = '16px';
      const vLink = document.createElement('a');
      vLink.href = `#/visitors/${lead.linkedVisitorId}?siteId=${encodeURIComponent(lead.siteId || '')}`;
      vLink.style.color = 'var(--brand-primary)';
      vLink.style.fontSize = '13px';
      vLink.style.textDecoration = 'none';
      vLink.textContent = '→ Open visitor profile';
      visitorLinkRow.appendChild(vLink);
      modalCard.appendChild(visitorLinkRow);
    }
  };

  const openDetail = (lead) => {
    state.selected = lead;
    // Show loading state
    modalCard.innerHTML = '<div style="padding:32px;text-align:center;color:var(--color-text-muted)">Loading…</div>';
    modalOverlay.style.display = 'flex';
    document.addEventListener('keydown', handleModalKeydown);
    renderDetail(lead);
  };

  const renderTable = () => {
    tableWrapper.innerHTML = '';
    paginationInfo.textContent = '';
    paginationControls.innerHTML = '';

    const total = state.filtered.length;
    tableCountEl.textContent = total
      ? `${total} lead${total !== 1 ? 's' : ''}`
      : 'No leads found';

    if (!total) {
      const empty = document.createElement('div');
      empty.className = 'empty-state';
      const icon = document.createElement('div');
      icon.className = 'empty-state-icon';
      icon.textContent = '🧑‍💼';
      const emptyTitle = document.createElement('div');
      emptyTitle.className = 'empty-state-title';
      emptyTitle.textContent = state.siteId ? 'No leads yet' : 'Select a site';
      const emptyDesc = document.createElement('div');
      emptyDesc.className = 'empty-state-desc';
      emptyDesc.textContent = state.siteId
        ? 'Leads are captured from chat conversations on your site.'
        : 'Choose a site from the dropdown above.';
      empty.append(icon, emptyTitle, emptyDesc);
      tableWrapper.appendChild(empty);
      return;
    }

    const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
    const pg = Math.min(state.currentPage, totalPages);
    const start = (pg - 1) * PAGE_SIZE;
    const end = Math.min(start + PAGE_SIZE, total);
    const slice = state.filtered.slice(start, end);

    const table = document.createElement('table');
    table.className = 'data-table';

    const thead = document.createElement('thead');
    const headRow = document.createElement('tr');
    ['Name', 'Email', 'Location', 'Opportunity', 'Intent Score', 'Created', 'Actions'].forEach((label) => {
      const th = document.createElement('th');
      th.textContent = label;
      headRow.appendChild(th);
    });
    thead.appendChild(headRow);
    table.appendChild(thead);

    const tbody = document.createElement('tbody');
    slice.forEach((lead) => {
      const tr = document.createElement('tr');
      tr.style.cursor = 'pointer';
      tr.addEventListener('click', () => openDetail(lead));

      const nameTd = document.createElement('td');
      const nameSpan = document.createElement('span');
      if (lead.displayName) {
        nameSpan.className = 'text-primary';
        nameSpan.textContent = lead.displayName;
      } else {
        nameSpan.style.color = 'var(--color-text-muted)';
        nameSpan.textContent = '(no name)';
      }
      nameTd.appendChild(nameSpan);

      const emailTd = document.createElement('td');
      const emailSpan = document.createElement('span');
      emailSpan.style.color = lead.primaryEmail ? 'var(--color-text-secondary)' : 'var(--color-text-muted)';
      emailSpan.textContent = lead.primaryEmail || '(no email)';
      emailTd.appendChild(emailSpan);

      const locationTd = document.createElement('td');
      locationTd.textContent = lead.location || '—';

      const oppTd = document.createElement('td');
      if (lead.opportunityLabel) {
        oppTd.appendChild(makeOpportunityBadge(lead.opportunityLabel));
      } else {
        oppTd.textContent = '—';
      }

      const scoreTd = document.createElement('td');
      scoreTd.textContent = lead.intentScore != null ? String(lead.intentScore) : '—';

      const createdTd = document.createElement('td');
      createdTd.textContent = formatDate(lead.createdAtUtc);

      const actionTd = document.createElement('td');
      const viewBtn = document.createElement('button');
      viewBtn.className = 'btn btn-sm btn-secondary';
      viewBtn.textContent = 'View';
      viewBtn.addEventListener('click', (e) => { e.stopPropagation(); openDetail(lead); });
      actionTd.appendChild(viewBtn);

      tr.append(nameTd, emailTd, locationTd, oppTd, scoreTd, createdTd, actionTd);
      tbody.appendChild(tr);
    });

    table.appendChild(tbody);
    tableWrapper.appendChild(table);

    // Pagination
    paginationInfo.textContent = `Showing ${start + 1}–${end} of ${total} lead${total !== 1 ? 's' : ''}`;

    if (totalPages > 1) {
      const prevBtn = document.createElement('button');
      prevBtn.className = 'page-btn';
      prevBtn.textContent = '← Prev';
      prevBtn.disabled = pg <= 1;
      prevBtn.addEventListener('click', () => { state.currentPage = pg - 1; renderTable(); });

      const pageInfoSpan = document.createElement('span');
      pageInfoSpan.style.padding = '0 6px';
      pageInfoSpan.style.fontSize = '12px';
      pageInfoSpan.style.color = 'var(--color-text-muted)';
      pageInfoSpan.textContent = `${pg} / ${totalPages}`;

      const nextBtn = document.createElement('button');
      nextBtn.className = 'page-btn';
      nextBtn.textContent = 'Next →';
      nextBtn.disabled = pg >= totalPages;
      nextBtn.addEventListener('click', () => { state.currentPage = pg + 1; renderTable(); });

      paginationControls.append(prevBtn, pageInfoSpan, nextBtn);
    }
  };

  const setSiteOptions = () => {
    siteSelect.innerHTML = '';
    const allOpt = document.createElement('option');
    allOpt.value = '';
    allOpt.textContent = 'All sites';
    siteSelect.appendChild(allOpt);
    state.sites.forEach((site) => {
      const option = document.createElement('option');
      option.value = getSiteId(site);
      option.textContent = site.domain || option.value;
      siteSelect.appendChild(option);
    });
    siteSelect.value = state.siteId;
  };

  const loadLeads = async () => {
    try {
      state.leads = await client.leads.list(state.siteId || undefined, 1, 100);
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
      state.leads = [];
    }
    applyFilter();
  };

  siteSelect.addEventListener('change', async () => {
    state.siteId = siteSelect.value;
    closeDetail();
    await loadLeads();
  });

  searchInput.addEventListener('input', () => {
    state.search = searchInput.value;
    applyFilter();
  });

  // ── Assemble ──────────────────────────────────────────────────────────
  page.append(pageHeader, filtersRow, metricsGrid, tableCard);
  container.appendChild(page);

  updateMetrics();
  renderTable();

  const init = async () => {
    try {
      state.sites = await client.sites.list();
      if (state.sites.length) state.siteId = getSiteId(state.sites[0]);
      setSiteOptions();
      await loadLeads();
    } catch (error) {
      notifier.show({ message: mapApiError(error).message, variant: 'danger' });
    }
  };

  init();
};
