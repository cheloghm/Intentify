import { createCard, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';

const DEFAULT_FILTERS = {
  category: 'general',
  location: 'US',
  timeWindow: '7d',
};

export const renderIntelligenceView = (container, { apiClient, toast } = {}) => {
  const client = apiClient || createApiClient();
  const notifier = toast || createToastManager();

  const page = document.createElement('div');
  page.style.display = 'flex';
  page.style.flexDirection = 'column';
  page.style.gap = '16px';
  page.style.width = '100%';
  page.style.maxWidth = '1000px';

  const summary = document.createElement('div');
  summary.style.color = '#334155';
  summary.style.lineHeight = '1.5';

  const statusPre = document.createElement('pre');
  statusPre.style.margin = '0';
  statusPre.style.whiteSpace = 'pre-wrap';
  statusPre.style.wordBreak = 'break-word';
  statusPre.style.color = '#0f172a';

  const trendsPre = document.createElement('pre');
  trendsPre.style.margin = '0';
  trendsPre.style.whiteSpace = 'pre-wrap';
  trendsPre.style.wordBreak = 'break-word';
  trendsPre.style.color = '#0f172a';

  const setLoading = () => {
    summary.textContent = 'Loading intelligence data...';
    statusPre.textContent = '';
    trendsPre.textContent = '';
  };

  const setAuthError = () => {
    summary.textContent = 'You are not authorized to view intelligence data. Please sign in again.';
    statusPre.textContent = '';
    trendsPre.textContent = '';
  };

  const load = async () => {
    setLoading();

    try {
      const sites = await client.sites.list();
      const siteId = sites?.[0]?.siteId || sites?.[0]?.id;

      if (!siteId) {
        summary.textContent = 'No site found. Create a site to load intelligence data.';
        return;
      }

      const params = {
        siteId,
        category: DEFAULT_FILTERS.category,
        location: DEFAULT_FILTERS.location,
        timeWindow: DEFAULT_FILTERS.timeWindow,
      };

      const [status, trends] = await Promise.all([
        client.intelligence.status(params),
        client.intelligence.trends(params),
      ]);

      summary.textContent = `Showing intelligence for site ${siteId} (${params.category}, ${params.location}, ${params.timeWindow}).`;
      statusPre.textContent = JSON.stringify(status, null, 2);
      trendsPre.textContent = JSON.stringify(trends, null, 2);
    } catch (error) {
      const uiError = mapApiError(error);
      if (uiError.status === 401 || uiError.status === 403) {
        setAuthError();
        return;
      }

      summary.textContent = 'Unable to load intelligence data.';
      statusPre.textContent = '';
      trendsPre.textContent = '';
      notifier.show({ message: uiError.message, variant: 'danger' });
    }
  };

  page.append(
    createCard({ title: 'Intelligence', body: summary }),
    createCard({ title: 'Status', body: statusPre }),
    createCard({ title: 'Trends', body: trendsPre }),
  );

  container.appendChild(page);
  load();
};
