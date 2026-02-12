import { createCard, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

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
  if (!url) {
    return '—';
  }

  try {
    return new URL(url).pathname || url;
  } catch (error) {
    return url;
  }
};

const sortByOccurredDesc = (items) => {
  return [...items].sort((left, right) => {
    const leftMs = new Date(left?.occurredAtUtc || 0).getTime();
    const rightMs = new Date(right?.occurredAtUtc || 0).getTime();
    return rightMs - leftMs;
  });
};

export const renderVisitorProfileView = async (
  container,
  { apiClient, toast, params = {}, query = {} } = {}
) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();
  const visitorId = params.visitorId;
  const siteId = query.siteId || '';

  const page = document.createElement('div');
  page.style.display = 'flex';
  page.style.flexDirection = 'column';
  page.style.gap = '20px';
  page.style.width = '100%';
  page.style.maxWidth = '980px';

  const backLink = document.createElement('a');
  backLink.textContent = '← Back to Visitors';
  backLink.href = siteId ? `#/visitors?siteId=${encodeURIComponent(siteId)}` : '#/visitors';
  backLink.style.color = '#2563eb';
  backLink.style.textDecoration = 'none';

  const header = document.createElement('div');
  const title = document.createElement('h2');
  title.textContent = 'Visitor profile';
  title.style.margin = '0';
  const subtitle = document.createElement('p');
  subtitle.textContent = visitorId || '—';
  subtitle.style.margin = '6px 0 0';
  subtitle.style.color = '#64748b';
  header.append(title, subtitle);

  const summaryGrid = document.createElement('div');
  summaryGrid.style.display = 'grid';
  summaryGrid.style.gridTemplateColumns = 'repeat(3, minmax(0, 1fr))';
  summaryGrid.style.gap = '10px';

  const timelineBody = document.createElement('div');
  timelineBody.style.display = 'flex';
  timelineBody.style.flexDirection = 'column';
  timelineBody.style.gap = '10px';

  const fillSummary = (events = []) => {
    const sessionsFromTimeline = new Set(
      events.map((item) => item?.metadataSummary?.sessionId).filter(Boolean)
    ).size;

    const pagesVisited = query.totalPagesVisited || events.length || '—';

    summaryGrid.innerHTML = '';
    summaryGrid.append(
      createSummaryValue('Last seen', formatDate(query.lastSeenAtUtc || events[0]?.occurredAtUtc)),
      createSummaryValue('Sessions count', query.sessionsCount || (sessionsFromTimeline || '—')),
      createSummaryValue('Pages visited', String(pagesVisited)),
      createSummaryValue('Time on site', '—'),
      createSummaryValue('Engagement score', query.lastSessionEngagementScore || '—'),
      createSummaryValue('Recent events', String(events.length))
    );
  };

  const renderTimeline = (items = []) => {
    timelineBody.innerHTML = '';

    if (!items.length) {
      const empty = document.createElement('div');
      empty.textContent = 'No events found for this visitor.';
      empty.style.color = '#64748b';
      timelineBody.appendChild(empty);
      return;
    }

    const table = document.createElement('table');
    table.className = 'ui-table';

    const thead = document.createElement('thead');
    const row = document.createElement('tr');
    ['Timestamp', 'Event', 'Path / URL', 'Referrer'].forEach((label) => {
      const th = document.createElement('th');
      th.textContent = label;
      row.appendChild(th);
    });
    thead.appendChild(row);
    table.appendChild(thead);

    const tbody = document.createElement('tbody');
    items.forEach((item) => {
      const tr = document.createElement('tr');
      const values = [
        formatDate(item.occurredAtUtc),
        item.type || '—',
        mapPath(item),
        item.referrer || '—',
      ];

      values.forEach((value) => {
        const td = document.createElement('td');
        td.textContent = value;
        tr.appendChild(td);
      });

      tbody.appendChild(tr);
    });

    table.appendChild(tbody);
    timelineBody.appendChild(table);
  };

  const summaryCard = createCard({
    title: 'Summary',
    body: summaryGrid,
  });

  const timelineCard = createCard({
    title: 'Timeline',
    body: timelineBody,
  });

  page.append(backLink, header, summaryCard, timelineCard);
  container.appendChild(page);

  if (!visitorId || !siteId) {
    fillSummary();
    renderTimeline();
    return;
  }

  timelineBody.textContent = 'Loading timeline...';

  try {
    const timeline = client.visitors?.timeline
      ? await client.visitors.timeline(visitorId, 200, siteId)
      : await client.request(
          `/visitors/${visitorId}/timeline?siteId=${encodeURIComponent(siteId)}&limit=200`
        );

    const sortedTimeline = Array.isArray(timeline) ? sortByOccurredDesc(timeline) : [];
    fillSummary(sortedTimeline);
    renderTimeline(sortedTimeline);
  } catch (error) {
    const uiError = mapApiError(error);
    notifier.show({ message: uiError.message, variant: 'danger' });
    fillSummary();
    renderTimeline();
  }
};
