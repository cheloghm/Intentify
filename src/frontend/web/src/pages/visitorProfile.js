import { createCard, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const SELECTED_SITE_STORAGE_KEY = 'intentify.selectedSiteId';

const formatDate = (value) => {
  if (!value) return '—';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString();
};

const toShortId = (value) => {
  if (!value || value.length < 8) {
    return value || '—';
  }

  return `${value.slice(0, 8)}…`;
};

const createSummaryValue = (label, value) => {
  const block = document.createElement('div');
  block.style.padding = '10px 12px';
  block.style.border = '1px solid #e2e8f0';
  block.style.borderRadius = '8px';
  block.style.background = '#ffffff';

  const labelNode = document.createElement('div');
  labelNode.textContent = label;
  labelNode.style.fontSize = '12px';
  labelNode.style.color = '#64748b';

  const valueNode = document.createElement('div');
  valueNode.textContent = value ?? '—';
  valueNode.style.fontSize = '15px';
  valueNode.style.fontWeight = '600';
  valueNode.style.color = '#0f172a';

  block.append(labelNode, valueNode);
  return block;
};

const mapPath = (timelineItem) => {
  const url = timelineItem?.url;
  if (!url) return '—';
  try {
    const parsedUrl = new URL(url);
    return `${parsedUrl.pathname || '/'}${parsedUrl.search || ''}${parsedUrl.hash || ''}` || url;
  } catch {
    return url;
  }
};

const mapReferrer = (timelineItem) => {
  const referrer = timelineItem?.referrer;
  if (!referrer) return 'Direct / None';
  try {
    return new URL(referrer).hostname || referrer;
  } catch {
    return referrer;
  }
};

const sortByOccurredDesc = (items) =>
  [...items].sort((left, right) => new Date(right?.occurredAtUtc || 0).getTime() - new Date(left?.occurredAtUtc || 0).getTime());

const normalizeSessionId = (value) =>
  typeof value === 'string' && value.trim() ? value.trim() : 'sessionless';

const getEventData = (timelineItem) => {
  const raw = timelineItem?.metadataSummary ?? timelineItem?.data;
  if (!raw) return null;
  if (typeof raw === 'string') {
    try {
      const parsed = JSON.parse(raw);
      return parsed && typeof parsed === 'object' ? parsed : null;
    } catch {
      return null;
    }
  }
  return typeof raw === 'object' ? raw : null;
};

const getCollectorSessionId = (timelineItem) => {
  return normalizeSessionId(timelineItem?.sessionId ?? timelineItem?.SessionId);
};

const parseNumber = (value) => {
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value === 'string') {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }
  return null;
};

const formatDuration = (secondsValue) => {
  const seconds = parseNumber(secondsValue);
  if (seconds === null) return '—';
  const totalSeconds = Math.max(0, Math.round(seconds));
  return `${Math.floor(totalSeconds / 60)}m ${totalSeconds % 60}s`;
};

const formatIdentificationSource = (value) => {
  if (!value || value === 'unknown') return 'Unknown';
  if (value === 'lead_capture') return 'Lead capture';
  return value;
};

const formatKnownTraits = (traits) => {
  if (!Array.isArray(traits) || !traits.length) return '—';
  return traits.join(', ');
};

const getSessionNarrative = (events, sessionId) => {
  if (!Array.isArray(events) || !events.length) {
    return {
      firstSeenAtUtc: null,
      lastSeenAtUtc: null,
      eventCount: 0,
      firstPath: '—',
      lastPath: '—',
      sessionId,
    };
  }

  const ordered = [...events].sort((left, right) => new Date(left?.occurredAtUtc || 0).getTime() - new Date(right?.occurredAtUtc || 0).getTime());
  return {
    firstSeenAtUtc: ordered[0]?.occurredAtUtc || null,
    lastSeenAtUtc: ordered[ordered.length - 1]?.occurredAtUtc || null,
    eventCount: ordered.length,
    firstPath: mapPath(ordered[0]),
    lastPath: mapPath(ordered[ordered.length - 1]),
    sessionId,
  };
};

const summarizePromoAnswers = (answers) => {
  const entries = Object.entries(answers || {});
  if (!entries.length) return '—';
  return entries
    .slice(0, 3)
    .map(([key, value]) => `${key}: ${value}`)
    .join('; ');
};

const getTimelineDetails = (item) => {
  const data = getEventData(item);
  if (!data) return '—';

  const type = String(item?.type || '').toLowerCase();
  if (type === 'time_on_page') {
    const duration = formatDuration(data.seconds);
    if (duration === '—') return '—';
    const reason = typeof data.reason === 'string' ? data.reason.trim() : '';
    return reason ? `${duration} (${reason})` : duration;
  }

  if (type === 'scroll_depth') {
    const percent = parseNumber(data.percent ?? data.value);
    return percent === null ? '—' : `${Math.round(percent)}%`;
  }

  if (type === 'pageview') return data.title || data.pageTitle || '—';

  if (type === 'click' || type === 'outbound_click') {
    const text = typeof data.text === 'string' ? data.text.trim() : '';
    const id = typeof data.id === 'string' ? data.id.trim() : '';
    const tag = typeof data.tag === 'string' ? data.tag.trim() : '';
    if (text && id) return `${text} (#${id})`;
    return text || (id ? `#${id}` : '') || data.href || tag || '—';
  }

  return '—';
};

const formatConfidencePercent = (value) => {
  const numeric = typeof value === 'number' ? value : Number(value);
  if (!Number.isFinite(numeric)) {
    return 'n/a';
  }

  return `${Math.round(numeric * 100)}%`;
};

const buildStage7TargetSummary = (targetRefs) => {
  if (!targetRefs || typeof targetRefs !== 'object') {
    return '';
  }

  return [
    targetRefs.promoId ? `promoId: ${targetRefs.promoId}` : null,
    targetRefs.promoPublicKey ? `promoKey: ${targetRefs.promoPublicKey}` : null,
    targetRefs.knowledgeSourceId ? `knowledgeSourceId: ${targetRefs.knowledgeSourceId}` : null,
    targetRefs.ticketId ? `ticketId: ${targetRefs.ticketId}` : null,
    targetRefs.visitorId ? `visitorId: ${targetRefs.visitorId}` : null,
  ].filter(Boolean).join(' · ');
};

const getReadOnlyStage7Recommendations = (message) => {
  const decision = message?.stage7Decision;
  const recommendations = decision?.recommendations ?? message?.recommendations;

  if (decision && decision.validationStatus && decision.validationStatus !== 'Valid') {
    return [];
  }

  if (!Array.isArray(recommendations)) {
    return [];
  }

  return recommendations.filter((item) => {
    if (!item || typeof item !== 'object') {
      return false;
    }

    if (typeof item.type !== 'string' || !item.type.trim()) {
      return false;
    }

    if (typeof item.rationale !== 'string' || !item.rationale.trim()) {
      return false;
    }

    return true;
  });
};

const getTimeOnSite = (events = []) => {
  const totalSeconds = events.reduce((sum, item) => {
    if (String(item?.type || '').toLowerCase() !== 'time_on_page') return sum;
    const data = getEventData(item);
    const seconds = parseNumber(data?.seconds ?? data?.durationSeconds ?? data?.duration ?? item?.seconds);
    return seconds === null ? sum : sum + seconds;
  }, 0);

  return totalSeconds > 0 ? formatDuration(totalSeconds) : '—';
};

// ── Event type icons ──────────────────────────────────────────────────
const EVENT_ICONS = {
  pageview: '📄',
  time_on_page: '⏱',
  exit_intent: '🚪',
  referral_source: '🔗',
  engage_chat: '💬',
};
const getEventIcon = (type) => EVENT_ICONS[String(type || '').toLowerCase()] || '•';

export const renderVisitorProfileView = async (
  container,
  { apiClient, toast, params = {}, query = {} } = {}
) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();
  const visitorId = params.visitorId;

  let selectedSiteId = '';
  try {
    selectedSiteId = localStorage.getItem(SELECTED_SITE_STORAGE_KEY) || '';
  } catch {
    selectedSiteId = '';
  }
  const siteId = query.siteId || selectedSiteId || '';

  // ── Root ──────────────────────────────────────────────────────────
  const page = document.createElement('div');
  page.className = 'stack';
  page.style.maxWidth = '1100px';
  page.style.width = '100%';

  // ── Back link ─────────────────────────────────────────────────────
  const backLink = document.createElement('a');
  backLink.textContent = '← Back to Visitors';
  backLink.href = siteId ? `#/visitors?siteId=${encodeURIComponent(siteId)}` : '#/visitors';
  backLink.style.cssText = 'color:var(--brand-primary);text-decoration:none;font-size:13px;font-weight:500;';

  // ── Header ────────────────────────────────────────────────────────
  const pageHeader = document.createElement('div');
  pageHeader.className = 'page-header';
  const titleGroup = document.createElement('div');
  const titleEl = document.createElement('h2');
  titleEl.className = 'page-title';
  titleEl.textContent = 'Visitor Profile';
  const subtitleEl = document.createElement('div');
  subtitleEl.className = 'page-subtitle';
  subtitleEl.textContent = visitorId ? `ID: ${toShortId(visitorId)}` : '—';
  titleGroup.append(titleEl, subtitleEl);
  pageHeader.appendChild(titleGroup);

  // ── Top row (Overview + Signals) ──────────────────────────────────
  const topRow = document.createElement('div');
  topRow.className = 'grid-2';

  const overviewBody = document.createElement('div');
  const signalsBody = document.createElement('div');

  topRow.append(
    createCard({ title: 'Visitor Overview', body: overviewBody }),
    createCard({ title: 'Signals', body: signalsBody }),
  );

  // ── Timeline card ─────────────────────────────────────────────────
  const timelineBody = document.createElement('div');
  timelineBody.className = 'stack';
  const timelineCard = createCard({ title: 'Journey Timeline', body: timelineBody });

  // ── Linked leads & tickets card ───────────────────────────────────
  const linkedBody = document.createElement('div');
  linkedBody.className = 'stack';
  const linkedCard = createCard({ title: 'Linked Leads & Tickets', body: linkedBody });

  // ── Conversations card ────────────────────────────────────────────
  const conversationsBody = document.createElement('div');
  conversationsBody.className = 'stack';
  const conversationsCard = createCard({ title: 'Engage Conversations', body: conversationsBody });

  // ── Conversation modal ────────────────────────────────────────────
  const modalOverlay = document.createElement('div');
  modalOverlay.style.cssText = 'position:fixed;inset:0;background:rgba(15,23,42,0.55);display:none;align-items:center;justify-content:center;z-index:1000;padding:20px;';
  const modalCard = document.createElement('div');
  modalCard.className = 'card';
  modalCard.style.cssText = 'width:100%;max-width:760px;max-height:85vh;overflow-y:auto;';
  modalOverlay.appendChild(modalCard);
  document.body.appendChild(modalOverlay);

  const handleModalKey = (e) => { if (e.key === 'Escape') closeModal(); };
  const closeModal = () => {
    modalOverlay.style.display = 'none';
    document.removeEventListener('keydown', handleModalKey);
  };
  modalOverlay.addEventListener('click', (e) => { if (e.target === modalOverlay) closeModal(); });

  // ── Assemble page ─────────────────────────────────────────────────
  page.append(backLink, pageHeader, topRow, timelineCard, linkedCard, conversationsCard);
  container.appendChild(page);

  // ── Helper: field row ─────────────────────────────────────────────
  const makeField = (label, value) => {
    const row = document.createElement('div');
    row.style.cssText = 'display:flex;justify-content:space-between;align-items:center;padding:7px 0;border-bottom:1px solid var(--color-border);gap:8px;';
    const lbl = document.createElement('span');
    lbl.style.cssText = 'font-size:12px;color:var(--color-text-muted);font-weight:500;white-space:nowrap;';
    lbl.textContent = label;
    const val = document.createElement('span');
    val.style.cssText = 'font-size:13px;color:var(--color-text);font-weight:600;text-align:right;';
    if (value instanceof Element) {
      val.appendChild(value);
    } else {
      val.textContent = (value !== null && value !== undefined && String(value).trim() !== '') ? String(value) : '—';
    }
    row.append(lbl, val);
    return row;
  };

  // ── Render: Overview card ─────────────────────────────────────────
  const renderOverview = (detail) => {
    overviewBody.innerHTML = '';
    const sessions = Array.isArray(detail?.recentSessions) ? detail.recentSessions : [];
    const latestSession = sessions.length
      ? sessions.reduce((a, b) => new Date(a.lastSeenAtUtc || 0) > new Date(b.lastSeenAtUtc || 0) ? a : b)
      : null;

    const lastSeen = detail?.lastSeenAtUtc || null;
    const isOnline = lastSeen && (Date.now() - new Date(lastSeen).getTime()) < 5 * 60 * 1000;
    const statusBadge = document.createElement('span');
    statusBadge.className = `badge badge-${isOnline ? 'success' : 'neutral'}`;
    statusBadge.textContent = isOnline ? 'Online' : 'Offline';

    overviewBody.append(
      makeField('First seen', formatDate(detail?.firstSeenAtUtc)),
      makeField('Last seen', formatDate(detail?.lastSeenAtUtc)),
      makeField('Total sessions', detail?.visitCount ?? '—'),
      makeField('Pages visited', detail?.totalPagesVisited ?? '—'),
      makeField('Engagement score', latestSession?.engagementScore ?? '—'),
      makeField('Status', statusBadge),
    );
  };

  // ── Render: Signals card ──────────────────────────────────────────
  const renderSignals = (detail, events) => {
    signalsBody.innerHTML = '';

    // Referral source — most recent referral_source event
    const referralEvent = [...events]
      .find((e) => String(e?.type || '').toLowerCase() === 'referral_source');
    const referralData = referralEvent ? getEventData(referralEvent) : null;
    let referralSource = '—';
    if (referralData) {
      const parts = [
        referralData.referrer && referralData.referrer !== 'direct' ? referralData.referrer : null,
        referralData.utmSource ? `utm: ${referralData.utmSource}` : null,
      ].filter(Boolean);
      if (parts.length) referralSource = parts.join(' / ');
      else if (referralData.referrer === 'direct') referralSource = 'Direct';
    }

    // Device / browser from userAgent
    const ua = detail?.userAgent || '';
    let deviceInfo = '—';
    if (ua) {
      const isMobile = /Mobile|Android|iPhone|iPad/i.test(ua);
      deviceInfo = isMobile ? 'Mobile' : 'Desktop';
      if (/Edg\//i.test(ua)) deviceInfo += ' / Edge';
      else if (/Chrome/i.test(ua)) deviceInfo += ' / Chrome';
      else if (/Firefox/i.test(ua)) deviceInfo += ' / Firefox';
      else if (/Safari/i.test(ua)) deviceInfo += ' / Safari';
    }

    // Max scroll depth — most recent scroll_depth event
    const scrollEvent = events.find((e) => String(e?.type || '').toLowerCase() === 'scroll_depth');
    const scrollData = scrollEvent ? getEventData(scrollEvent) : null;
    const scrollDepth = scrollData?.percent !== undefined ? `${scrollData.percent}%` : '—';

    // Time on page — most recent time_on_page event
    const topEvent = events.find((e) => String(e?.type || '').toLowerCase() === 'time_on_page');
    const topData = topEvent ? getEventData(topEvent) : null;
    const timeOnPage = topData?.seconds !== undefined ? formatDuration(topData.seconds) : '—';

    // Return visitor
    const returnVisitor = (detail?.visitCount ?? 0) > 1 ? 'Yes' : 'No';

    signalsBody.append(
      makeField('Referral source', referralSource),
      makeField('Device / Browser', deviceInfo),
      makeField('Max scroll depth', scrollDepth),
      makeField('Time on page (last)', timeOnPage),
      makeField('Return visitor', returnVisitor),
    );
  };

  // ── Render: Timeline card ─────────────────────────────────────────
  const renderTimeline = (events) => {
    timelineBody.innerHTML = '';

    if (!events.length) {
      const empty = document.createElement('div');
      empty.className = 'empty-state';
      const icon = document.createElement('div');
      icon.className = 'empty-state-icon';
      icon.textContent = '📋';
      const title = document.createElement('div');
      title.className = 'empty-state-title';
      title.textContent = 'No events recorded';
      empty.append(icon, title);
      timelineBody.appendChild(empty);
      return;
    }

    const grouped = new Map();
    events.forEach((item) => {
      const sid = getCollectorSessionId(item);
      if (!grouped.has(sid)) grouped.set(sid, []);
      grouped.get(sid).push(item);
    });

    const sortedSessions = Array.from(grouped.entries())
      .map(([sid, evts]) => ({ sid, evts: sortByOccurredDesc(evts) }))
      .sort((a, b) => new Date(b.evts[0]?.occurredAtUtc || 0) - new Date(a.evts[0]?.occurredAtUtc || 0));

    sortedSessions.forEach(({ sid, evts }) => {
      const divider = document.createElement('div');
      divider.style.cssText = 'display:flex;align-items:center;gap:8px;padding:8px 0 4px;';
      const pill = document.createElement('span');
      pill.className = 'badge badge-neutral';
      pill.textContent = `Session ${toShortId(sid)}`;
      const timeSpan = document.createElement('span');
      timeSpan.style.cssText = 'font-size:11px;color:var(--color-text-muted);';
      const earliest = evts[evts.length - 1]?.occurredAtUtc;
      const latest = evts[0]?.occurredAtUtc;
      timeSpan.textContent = `${formatDate(earliest)} → ${formatDate(latest)} · ${evts.length} events`;
      divider.append(pill, timeSpan);
      timelineBody.appendChild(divider);

      const table = document.createElement('table');
      table.className = 'data-table';
      const thead = document.createElement('thead');
      const headRow = document.createElement('tr');
      ['', 'Event', 'Page', 'Details', 'Time'].forEach((h) => {
        const th = document.createElement('th');
        th.textContent = h;
        headRow.appendChild(th);
      });
      thead.appendChild(headRow);
      table.appendChild(thead);

      const tbody = document.createElement('tbody');
      evts.forEach((item) => {
        const tr = document.createElement('tr');
        const type = String(item?.type || '').toLowerCase();

        const iconTd = document.createElement('td');
        iconTd.style.cssText = 'width:28px;text-align:center;font-size:14px;';
        iconTd.textContent = getEventIcon(type);

        const typeTd = document.createElement('td');
        typeTd.textContent = item.type || '—';

        const pageTd = document.createElement('td');
        pageTd.style.cssText = 'max-width:240px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;';
        pageTd.textContent = mapPath(item);

        const detailsTd = document.createElement('td');
        detailsTd.textContent = getTimelineDetails(item);

        const timeTd = document.createElement('td');
        timeTd.style.whiteSpace = 'nowrap';
        timeTd.textContent = formatDate(item.occurredAtUtc);

        tr.append(iconTd, typeTd, pageTd, detailsTd, timeTd);
        tbody.appendChild(tr);
      });
      table.appendChild(tbody);
      timelineBody.appendChild(table);
    });
  };

  // ── Render: Linked Leads & Tickets ────────────────────────────────
  const renderLinked = async (detail) => {
    linkedBody.innerHTML = '';
    let hasContent = false;

    // Linked lead — VisitorDetailResult has no linkedLeadId field, so find by linkedVisitorId
    try {
      const allLeads = siteId ? await client.leads.list(siteId, 1, 200) : [];
      if (Array.isArray(allLeads) && visitorId) {
        const normVisitorId = String(visitorId).toLowerCase().replace(/-/g, '');
        const linkedLead = allLeads.find((l) => {
          const lv = String(l.linkedVisitorId || l.LinkedVisitorId || '').toLowerCase().replace(/-/g, '');
          return lv && lv === normVisitorId;
        }) || null;

        if (linkedLead) {
          hasContent = true;
          const leadSection = document.createElement('div');
          const leadTitle = document.createElement('div');
          leadTitle.style.cssText = 'font-weight:600;font-size:13px;margin-bottom:8px;color:var(--color-text);';
          leadTitle.textContent = 'Linked Lead';
          leadSection.appendChild(leadTitle);

          const leadRow = document.createElement('div');
          leadRow.style.cssText = 'border:1px solid var(--color-border);border-radius:var(--radius-md);padding:12px 16px;display:flex;justify-content:space-between;align-items:center;';
          const nameEmail = document.createElement('div');
          const nameEl = document.createElement('div');
          nameEl.style.cssText = 'font-weight:600;font-size:13px;color:var(--color-text);';
          nameEl.textContent = linkedLead.displayName || linkedLead.fullName || linkedLead.name || '—';
          const emailEl = document.createElement('div');
          emailEl.style.cssText = 'font-size:12px;color:var(--color-text-muted);';
          emailEl.textContent = linkedLead.primaryEmail || linkedLead.email || '—';
          nameEmail.append(nameEl, emailEl);
          const rightSide = document.createElement('div');
          rightSide.style.cssText = 'display:flex;align-items:center;gap:8px;';
          if (linkedLead.opportunityLabel) {
            const badge = document.createElement('span');
            badge.className = 'badge badge-info';
            badge.textContent = linkedLead.opportunityLabel;
            rightSide.appendChild(badge);
          }
          const viewLink = document.createElement('a');
          viewLink.href = '#/leads';
          viewLink.className = 'btn btn-sm btn-secondary';
          viewLink.textContent = 'View Leads';
          rightSide.appendChild(viewLink);
          leadRow.append(nameEmail, rightSide);
          leadSection.appendChild(leadRow);
          linkedBody.appendChild(leadSection);
        }
      }
    } catch {
      // fail-soft: lead section skipped on error
    }

    // Linked tickets
    try {
      const ticketResult = visitorId && siteId
        ? await client.tickets.listTickets({ siteId, visitorId, page: 1, pageSize: 50 })
        : [];
      const ticketArr = Array.isArray(ticketResult) ? ticketResult : [];

      if (ticketArr.length > 0) {
        hasContent = true;
        const ticketSection = document.createElement('div');
        const ticketTitle = document.createElement('div');
        ticketTitle.style.cssText = 'font-weight:600;font-size:13px;margin-bottom:8px;color:var(--color-text);';
        ticketTitle.textContent = 'Linked Tickets';
        ticketSection.appendChild(ticketTitle);

        const table = document.createElement('table');
        table.className = 'data-table';
        const thead = document.createElement('thead');
        const headRow = document.createElement('tr');
        ['Subject', 'Status', 'Created'].forEach((h) => {
          const th = document.createElement('th');
          th.textContent = h;
          headRow.appendChild(th);
        });
        thead.appendChild(headRow);
        table.appendChild(thead);

        const tbody = document.createElement('tbody');
        ticketArr.forEach((ticket) => {
          const tr = document.createElement('tr');
          const sv = String(ticket.status || '').toLowerCase().replace(/[^a-z]/g, '');
          const variant = sv === 'open' ? 'warning' : sv === 'inprogress' ? 'info' : (sv === 'resolved' || sv === 'closed') ? 'success' : 'neutral';
          const statusBadge = document.createElement('span');
          statusBadge.className = `badge badge-${variant}`;
          statusBadge.textContent = ticket.status || '—';
          const statusTd = document.createElement('td');
          statusTd.appendChild(statusBadge);

          const subjectTd = document.createElement('td');
          subjectTd.textContent = ticket.subject || '—';
          const dateTd = document.createElement('td');
          dateTd.textContent = formatDate(ticket.createdAtUtc);

          tr.append(subjectTd, statusTd, dateTd);
          tbody.appendChild(tr);
        });
        table.appendChild(tbody);
        ticketSection.appendChild(table);
        linkedBody.appendChild(ticketSection);
      }
    } catch {
      // silently skip tickets section on error
    }

    if (!hasContent) {
      const empty = document.createElement('div');
      empty.className = 'empty-state';
      const icon = document.createElement('div');
      icon.className = 'empty-state-icon';
      icon.textContent = '🔗';
      const title = document.createElement('div');
      title.className = 'empty-state-title';
      title.textContent = 'No linked leads or tickets';
      empty.append(icon, title);
      linkedBody.appendChild(empty);
    }
  };

  // ── Render: Conversations card ────────────────────────────────────
  const renderConversations = (conversations) => {
    conversationsBody.innerHTML = '';

    if (!conversations.length) {
      const empty = document.createElement('div');
      empty.className = 'empty-state';
      const icon = document.createElement('div');
      icon.className = 'empty-state-icon';
      icon.textContent = '💬';
      const title = document.createElement('div');
      title.className = 'empty-state-title';
      title.textContent = 'No Engage conversations';
      empty.append(icon, title);
      conversationsBody.appendChild(empty);
      return;
    }

    const table = document.createElement('table');
    table.className = 'data-table';
    const thead = document.createElement('thead');
    const headRow = document.createElement('tr');
    ['Session ID', 'Started', 'Last active', ''].forEach((h) => {
      const th = document.createElement('th');
      th.textContent = h;
      headRow.appendChild(th);
    });
    thead.appendChild(headRow);
    table.appendChild(thead);

    const tbody = document.createElement('tbody');
    conversations.forEach((conversation) => {
      const tr = document.createElement('tr');

      const sidTd = document.createElement('td');
      sidTd.style.cssText = 'font-family:ui-monospace,SFMono-Regular,monospace;font-size:12px;';
      sidTd.textContent = toShortId(conversation.sessionId);

      const startedTd = document.createElement('td');
      startedTd.textContent = formatDate(conversation.createdAtUtc);

      const lastTd = document.createElement('td');
      lastTd.textContent = formatDate(conversation.updatedAtUtc);

      const actionTd = document.createElement('td');
      const viewBtn = document.createElement('button');
      viewBtn.className = 'btn btn-sm btn-secondary';
      viewBtn.textContent = 'View';
      viewBtn.addEventListener('click', () => loadConversationMessages(conversation.sessionId));
      actionTd.appendChild(viewBtn);

      tr.append(sidTd, startedTd, lastTd, actionTd);
      tbody.appendChild(tr);
    });
    table.appendChild(tbody);
    conversationsBody.appendChild(table);
  };

  // ── Render: Conversation modal ────────────────────────────────────
  const renderConversationModal = (conversationId, messages) => {
    modalCard.innerHTML = '';

    const mHeader = document.createElement('div');
    mHeader.className = 'card-header';
    mHeader.style.marginBottom = '16px';
    const mTitle = document.createElement('div');
    mTitle.className = 'card-title';
    mTitle.textContent = `Conversation ${toShortId(conversationId)}`;
    const closeBtn = document.createElement('button');
    closeBtn.className = 'btn btn-secondary btn-sm';
    closeBtn.textContent = '×';
    closeBtn.setAttribute('aria-label', 'Close');
    closeBtn.addEventListener('click', closeModal);
    mHeader.append(mTitle, closeBtn);
    modalCard.appendChild(mHeader);

    if (!messages || !messages.length) {
      const empty = document.createElement('div');
      empty.style.cssText = 'padding:24px;text-align:center;color:var(--color-text-muted);';
      empty.textContent = 'No messages';
      modalCard.appendChild(empty);
      return;
    }

    const wrap = document.createElement('div');
    wrap.className = 'stack';
    wrap.style.padding = '0 4px 16px';
    messages.forEach((message) => {
      const row = document.createElement('div');
      row.style.cssText = 'border:1px solid var(--color-border);border-radius:var(--radius-md);padding:10px 12px;';
      const meta = document.createElement('div');
      meta.style.cssText = 'font-size:11px;color:var(--color-text-muted);margin-bottom:6px;';
      meta.textContent = `${message.role} · ${formatDate(message.createdAtUtc)}`;
      const content = document.createElement('div');
      content.style.cssText = 'font-size:13px;color:var(--color-text);white-space:pre-wrap;line-height:1.5;';
      content.textContent = message.content || '—';
      row.append(meta, content);
      wrap.appendChild(row);
    });
    modalCard.appendChild(wrap);
  };

  const loadConversationMessages = async (conversationId) => {
    if (!conversationId || !siteId) return;
    modalCard.innerHTML = '<div style="padding:24px;text-align:center;color:var(--color-text-muted)">Loading…</div>';
    modalOverlay.style.display = 'flex';
    document.addEventListener('keydown', handleModalKey);
    try {
      const messages = await client.engage.getConversationMessages(conversationId, siteId);
      renderConversationModal(conversationId, Array.isArray(messages) ? messages : []);
    } catch (error) {
      modalCard.innerHTML = `<div style="padding:24px;color:var(--color-danger)">${mapApiError(error).message}</div>`;
    }
  };

  // ── Early exit if missing params ──────────────────────────────────
  if (!visitorId || !siteId) {
    overviewBody.innerHTML = '<div class="empty-state"><div class="empty-state-title">No visitor selected</div></div>';
    signalsBody.innerHTML = '';
    timelineBody.innerHTML = '<div class="empty-state"><div class="empty-state-title">No visitor selected</div></div>';
    linkedBody.innerHTML = '';
    conversationsBody.innerHTML = '';
    return;
  }

  // ── Loading states ────────────────────────────────────────────────
  overviewBody.innerHTML = '<div style="padding:16px;color:var(--color-text-muted);font-size:13px;">Loading…</div>';
  signalsBody.innerHTML = '<div style="padding:16px;color:var(--color-text-muted);font-size:13px;">Loading…</div>';
  timelineBody.innerHTML = '<div style="padding:16px;color:var(--color-text-muted);font-size:13px;">Loading…</div>';
  linkedBody.innerHTML = '<div style="padding:16px;color:var(--color-text-muted);font-size:13px;">Loading…</div>';
  conversationsBody.innerHTML = '<div style="padding:16px;color:var(--color-text-muted);font-size:13px;">Loading…</div>';

  // ── Fetch visitor data ────────────────────────────────────────────
  const [detailResult, timelineResult] = await Promise.allSettled([
    client.visitors?.detail
      ? client.visitors.detail(visitorId, siteId)
      : client.request(`/visitors/${visitorId}?siteId=${encodeURIComponent(siteId)}`),
    client.visitors?.timeline
      ? client.visitors.timeline(visitorId, 200, siteId)
      : client.request(`/visitors/${visitorId}/timeline?siteId=${encodeURIComponent(siteId)}&limit=200`),
  ]);

  const detail = detailResult.status === 'fulfilled' ? (detailResult.value || null) : null;
  const events = timelineResult.status === 'fulfilled' && Array.isArray(timelineResult.value)
    ? sortByOccurredDesc(timelineResult.value)
    : [];

  if (detailResult.status === 'rejected') {
    notifier.show({ message: mapApiError(detailResult.reason).message, variant: 'danger' });
  }
  if (timelineResult.status === 'rejected') {
    notifier.show({ message: mapApiError(timelineResult.reason).message, variant: 'danger' });
  }

  // ── Render cards ──────────────────────────────────────────────────
  renderOverview(detail);
  renderSignals(detail, events);
  renderTimeline(events);

  // Linked (async — fetches tickets)
  await renderLinked(detail);

  // Conversations — fetch per session ID from visitor detail
  conversationsBody.innerHTML = '';
  const sessions = Array.isArray(detail?.recentSessions) ? detail.recentSessions : [];
  const sessionIds = [...new Set(
    sessions
      .map((s) => normalizeSessionId(s?.sessionId))
      .filter((id) => id && id !== 'sessionless'),
  )];

  if (sessionIds.length > 0) {
    try {
      const convResults = await Promise.allSettled(
        sessionIds.map((sid) => client.engage.getConversations(siteId, sid)),
      );
      const deduped = new Map();
      convResults.forEach((result) => {
        if (result.status !== 'fulfilled' || !Array.isArray(result.value)) return;
        result.value.forEach((c) => {
          const key = c?.sessionId || c?.updatedAtUtc;
          if (key && !deduped.has(key)) deduped.set(key, c);
        });
      });
      const conversations = Array.from(deduped.values()).sort(
        (a, b) => new Date(b.updatedAtUtc || 0) - new Date(a.updatedAtUtc || 0),
      );
      renderConversations(conversations);
    } catch {
      renderConversations([]);
    }
  } else {
    renderConversations([]);
  }
};
