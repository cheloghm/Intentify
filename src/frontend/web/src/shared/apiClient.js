import { API_BASE } from './config.js';
import { getToken, handleSessionExpired } from './auth.js';

const buildUrl = (path, baseUrl) => {
  if (path.startsWith('http')) {
    return path;
  }

  const normalizedBase = baseUrl.replace(/\/$/, '');
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  return `${normalizedBase}${normalizedPath}`;
};

export const mapApiError = (error) => {
  if (!error) {
    return { message: 'Unexpected error occurred.', status: 0, details: null };
  }

  if (error.uiError) {
    return error.uiError;
  }

  return {
    message: error.message || 'Unexpected error occurred.',
    status: error.status || 0,
    details: error.details || null,
  };
};

const parseErrorResponse = async (response) => {
  let details = null;

  try {
    details = await response.json();
  } catch (error) {
    try {
      details = await response.text();
    } catch (textError) {
      details = null;
    }
  }

  return {
    message: (details && details.message) || response.statusText || 'Request failed.',
    status: response.status,
    details,
  };
};

export const createApiClient = ({ baseUrl = API_BASE } = {}) => {
  const request = async (path, options = {}) => {
    const url = path.startsWith('http') ? path : buildUrl(path, baseUrl);
    const headers = new Headers(options.headers || {});
    const token = getToken();

    if (token && !headers.has('Authorization')) {
      headers.set('Authorization', `Bearer ${token}`);
    }

    const response = await fetch(url, { ...options, headers });

    if (response.status === 401) {
      handleSessionExpired();
    }

    if (!response.ok) {
      const uiError = await parseErrorResponse(response);
      const error = new Error(uiError.message);
      error.uiError = uiError;
      throw error;
    }

    const contentType = response.headers.get('content-type') || '';
    if (contentType.includes('application/json')) {
      return response.json();
    }

    return response.text();
  };

  const regenerateSiteKeys = async (siteId) =>
    request(`/sites/${siteId}/keys/regenerate`, { method: 'POST' });

  const buildQueryString = (params = {}) => {
    const search = new URLSearchParams();
    Object.entries(params).forEach(([key, value]) => {
      if (value === undefined || value === null || value === '') {
        return;
      }
      search.set(key, String(value));
    });
    const queryString = search.toString();
    return queryString ? `?${queryString}` : '';
  };

  const listVisitors = async (siteId, page = 1, pageSize = 50) =>
    request(`/visitors${buildQueryString({ siteId, page, pageSize })}`);

  const getVisitorTimeline = async (visitorId, limit = 200, siteId) =>
    request(`/visitors/${visitorId}/timeline${buildQueryString({ siteId, limit })}`);

  const getVisitCounts = async (siteId) =>
    request(`/visitors/visits/counts${buildQueryString({ siteId })}`);

  return {
    request,
    sites: {
      regenerateKeys: regenerateSiteKeys,
    },
    visitors: {
      list: listVisitors,
      timeline: getVisitorTimeline,
      visitCounts: getVisitCounts,
    },
  };
};
