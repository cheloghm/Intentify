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

  const getSiteKeys = async (siteId) => request(`/sites/${siteId}/keys`);

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

  const listSites = async () => request('/sites');

  const createKnowledgeSource = async (payload) =>
    request('/knowledge/sources', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(payload),
    });

  const uploadKnowledgePdf = async (sourceId, file) => {
    const formData = new FormData();
    formData.append('file', file);
    return request(`/knowledge/sources/${sourceId}/pdf`, {
      method: 'POST',
      body: formData,
    });
  };

  const indexKnowledgeSource = async (sourceId) =>
    request(`/knowledge/sources/${sourceId}/index`, {
      method: 'POST',
    });

  const listKnowledgeSources = async (siteId) =>
    request(`/knowledge/sources${buildQueryString({ siteId })}`);

  const retrieveKnowledgeChunks = async ({ siteId, query, top = 5 }) =>
    request(`/knowledge/retrieve${buildQueryString({ siteId, query, top })}`);

  const getIntelligenceStatus = async (params) =>
    request(`/intelligence/status${buildQueryString(params)}`);

  const getIntelligenceTrends = async (params) =>
    request(`/intelligence/trends${buildQueryString(params)}`);

  const refreshIntelligence = async (payload) =>
    request('/intelligence/refresh', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(payload),
    });

  const sendEngageChat = async (widgetKeyOrPayload, sessionId, message) => {
    const payload =
      typeof widgetKeyOrPayload === 'object' && widgetKeyOrPayload !== null
        ? widgetKeyOrPayload
        : {
            widgetKey: widgetKeyOrPayload,
            sessionId,
            message,
          };

    const resolvedWidgetKey = payload.widgetKey || widgetKeyOrPayload;
    return request(`/engage/chat/send${buildQueryString({ widgetKey: resolvedWidgetKey })}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(payload),
    });
  };


  const createPromo = async (payload) => {
    if (payload instanceof FormData) {
      return request('/promos', {
        method: 'POST',
        body: payload,
      });
    }

    return request('/promos', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(payload),
    });
  };

  const listPromos = async (siteId) =>
    request(`/promos${buildQueryString({ siteId })}`);

  const listPromoEntries = async (promoId, page = 1, pageSize = 50) =>
    request(`/promos/${encodeURIComponent(promoId)}/entries${buildQueryString({ page, pageSize })}`);

  const getPromoDetail = async (promoId) =>
    request(`/promos/${encodeURIComponent(promoId)}`);

  const getPromoFlyerUrl = (promoId) => `${baseUrl.replace(/\/$/, "")}/promos/${encodeURIComponent(promoId)}/flyer`;

  const downloadPromoCsv = async (promoId) => {
    const url = buildUrl(`/promos/${encodeURIComponent(promoId)}/export.csv`, baseUrl);
    const headers = new Headers();
    const token = getToken();
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }

    const response = await fetch(url, { method: 'GET', headers });
    if (!response.ok) {
      const uiError = await parseErrorResponse(response);
      const error = new Error(uiError.message);
      error.uiError = uiError;
      throw error;
    }

    return response.blob();
  };

  const downloadPromoFlyer = async (promoId) => {
    const url = buildUrl(`/promos/${encodeURIComponent(promoId)}/flyer`, baseUrl);
    const headers = new Headers();
    const token = getToken();
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }

    const response = await fetch(url, { method: 'GET', headers });
    if (!response.ok) {
      const uiError = await parseErrorResponse(response);
      const error = new Error(uiError.message);
      error.uiError = uiError;
      throw error;
    }

    return response.blob();
  };

  const listEngageConversations = async (siteId, collectorSessionId) =>
    request(`/engage/conversations${buildQueryString({ siteId, collectorSessionId })}`);

  const getEngageConversationMessages = async (sessionId, siteId) =>
    request(
      `/engage/conversations/${encodeURIComponent(sessionId)}/messages?siteId=${encodeURIComponent(siteId)}`
    );

  const getEngageBot = async (siteId) =>
    request(`/engage/bot${buildQueryString({ siteId })}`);

  const updateEngageBot = async (siteId, name) =>
    request(`/engage/bot${buildQueryString({ siteId })}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ name }),
    });

  const listTickets = async ({ siteId, page = 1, pageSize = 50 } = {}) =>
    request(`/tickets${buildQueryString({ siteId, page, pageSize })}`);

  const getTicket = async (ticketId) =>
    request(`/tickets/${encodeURIComponent(ticketId)}`);

  const getTicketNotes = async (ticketId) =>
    request(`/tickets/${encodeURIComponent(ticketId)}/notes`);

  const addTicketNote = async (ticketId, content) =>
    request(`/tickets/${encodeURIComponent(ticketId)}/notes`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ content }),
    });

  const transitionTicketStatus = async (ticketId, status) =>
    request(`/tickets/${encodeURIComponent(ticketId)}/status`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ status }),
    });

  return {
    request,
    sites: {
      regenerateKeys: regenerateSiteKeys,
      getKeys: getSiteKeys,
      list: listSites,
    },
    visitors: {
      list: listVisitors,
      timeline: getVisitorTimeline,
      visitCounts: getVisitCounts,
    },
    knowledge: {
      createSource: createKnowledgeSource,
      uploadPdf: uploadKnowledgePdf,
      indexSource: indexKnowledgeSource,
      listSources: listKnowledgeSources,
      retrieve: retrieveKnowledgeChunks,
    },

    intelligence: {
      status: getIntelligenceStatus,
      trends: getIntelligenceTrends,
      refresh: refreshIntelligence,
    },

    promos: {
      create: createPromo,
      list: listPromos,
      listEntries: listPromoEntries,
      getDetail: getPromoDetail,
      flyerUrl: getPromoFlyerUrl,
      downloadCsv: downloadPromoCsv,
      downloadFlyer: downloadPromoFlyer,
    },
    engage: {
      sendChat: sendEngageChat,
      getConversations: listEngageConversations,
      listConversations: listEngageConversations,
      getConversationMessages: getEngageConversationMessages,
      getBot: getEngageBot,
      updateBot: updateEngageBot,
    },
    tickets: {
      listTickets,
      getTicket,
      getTicketNotes,
      addTicketNote,
      transitionTicketStatus,
    },
  };
};
