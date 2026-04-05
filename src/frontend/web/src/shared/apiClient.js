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

  const createInvite = async (payload) =>
    request('/auth/invites', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(payload),
    });

  const acceptInvite = async (payload) =>
    request('/auth/invites/accept', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(payload),
    });

  const getCurrentUser = async () => request('/auth/me');

  const updateCurrentUserProfile = async (payload) =>
    request('/auth/me', {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(payload),
    });

  const listTenantUsers = async () => request('/auth/users');

  const updateTenantUserRole = async (userId, role) =>
    request(`/auth/users/${encodeURIComponent(userId)}/role`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ role }),
    });

  const removeTenantUser = async (userId) =>
    request(`/auth/users/${encodeURIComponent(userId)}`, {
      method: 'DELETE',
    });

  const listTenantInvites = async () => request('/auth/invites');

  const revokeTenantInvite = async (inviteId) =>
    request(`/auth/invites/${encodeURIComponent(inviteId)}`, {
      method: 'DELETE',
    });

  const listVisitors = async (siteId, page = 1, pageSize = 50) =>
    request(`/visitors${buildQueryString({ siteId, page, pageSize })}`);

  const getVisitorDetail = async (visitorId, siteId) =>
    request(`/visitors/${visitorId}${buildQueryString({ siteId })}`);

  const getVisitorTimeline = async (visitorId, limit = 200, siteId) =>
    request(`/visitors/${visitorId}/timeline${buildQueryString({ siteId, limit })}`);

  const getVisitCounts = async (siteId) =>
    request(`/visitors/visits/counts${buildQueryString({ siteId })}`);

  const getOnlineNow = async (siteId, windowMinutes = 5, limit = 50) =>
    request(`/visitors/online-now${buildQueryString({ siteId, windowMinutes, limit })}`);

  const getPageAnalytics = async (siteId, days = 7, limit = 15) =>
    request(`/visitors/analytics/pages${buildQueryString({ siteId, days, limit })}`);

  const getCountryBreakdown = async (siteId, days = 7, limit = 20) =>
    request(`/visitors/analytics/countries${buildQueryString({ siteId, days, limit })}`);

  const getDashboardAnalytics = async (siteId) =>
    request(`/visitors/analytics/dashboard${buildQueryString({ siteId })}`);

  const listSites = async () => request('/sites');

  const createSite = async (payload) =>
    request('/sites', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(payload),
    });

  const updateSiteProfile = async (siteId, payload) =>
    request(`/sites/${siteId}/profile`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(payload),
    });

  const deleteSite = async (siteId) =>
    request(`/sites/${siteId}`, {
      method: 'DELETE',
    });

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

  const deleteKnowledgeSource = async (sourceId) =>
    request(`/knowledge/sources/${sourceId}`, {
      method: 'DELETE',
    });

  const retrieveKnowledgeChunks = async ({ siteId, query, top = 5 }) =>
    request(`/knowledge/retrieve${buildQueryString({ siteId, query, top })}`);

  const listQuickFacts = async (siteId) =>
    request(`/knowledge/quick-facts${buildQueryString({ siteId })}`);

  const addQuickFact = async (siteId, fact) =>
    request('/knowledge/quick-facts', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ siteId, fact }),
    });

  const deleteQuickFact = async (siteId, factId) =>
    request(`/knowledge/quick-facts/${encodeURIComponent(factId)}${buildQueryString({ siteId })}`, {
      method: 'DELETE',
    });

  const getIntelligenceStatus = async (params) =>
    request(`/intelligence/status${buildQueryString(params)}`);

  const getIntelligenceTrends = async (params) =>
    request(`/intelligence/trends${buildQueryString(params)}`);

  const getIntelligenceDashboard = async (params) =>
    request(`/intelligence/dashboard${buildQueryString({
      siteId: params.siteId,
      category: params.category || undefined,
      location: params.location || undefined,
      timeWindow: params.timeWindow || undefined,
      provider: params.provider || undefined,
      keyword: params.keyword || undefined,
      audienceType: params.audienceType || undefined,
      ageRange: params.ageRange || undefined,
      categoryId: params.categoryId || undefined,
      searchType: params.searchType || undefined,
      comparisonTerms: params.comparisonTerms || undefined,
      subRegion: params.subRegion || undefined,
      limit: params.limit || undefined,
    })}`);

  const getIntelligenceSiteSummary = async (params) =>
    request(`/intelligence/site-summary${buildQueryString({
      siteId: params.siteId,
      category: params.category || undefined,
      location: params.location || undefined,
      timeWindow: params.timeWindow || undefined,
      provider: params.provider || undefined,
      keyword: params.keyword || undefined,
      audienceType: params.audienceType || undefined,
      ageRange: params.ageRange || undefined,
      categoryId: params.categoryId || undefined,
      searchType: params.searchType || undefined,
      comparisonTerms: params.comparisonTerms || undefined,
      subRegion: params.subRegion || undefined,
      limit: params.limit || undefined,
    })}`);

  const refreshIntelligence = async (payload) =>
    request('/intelligence/refresh', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        siteId: payload.siteId,
        category: payload.category || 'general',
        location: payload.location || 'GB',
        timeWindow: payload.timeWindow || '7d',
        limit: payload.limit || 25,
        keyword: payload.keyword || undefined,
        ageRange: payload.ageRange || undefined,
        categoryId: payload.categoryId || undefined,
        searchType: payload.searchType || undefined,
        comparisonTerms: payload.comparisonTerms || undefined,
        subRegion: payload.subRegion || undefined,
      }),
    });

  const getIntelligenceProfile = async (siteId) =>
    request(`/intelligence/profiles/${encodeURIComponent(siteId)}`);

  const upsertIntelligenceProfile = async (siteId, payload) =>
    request(`/intelligence/profiles/${encodeURIComponent(siteId)}`, {
      method: 'PUT',
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

  const listVisitorPromoEntries = async (siteId, visitorId, page = 1, pageSize = 50) =>
    request(`/promos/entries/by-visitor${buildQueryString({ siteId, visitorId, page, pageSize })}`);

  const getPromoDetail = async (promoId) =>
    request(`/promos/${encodeURIComponent(promoId)}`);

  const getPromoFlyerUrl = (promoId) => `${baseUrl.replace(/\/$/, '')}/promos/${encodeURIComponent(promoId)}/flyer`;

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

  const getEngageOpportunityAnalytics = async (siteId) =>
    request(`/engage/opportunities/analytics${buildQueryString({ siteId })}`);

  const getEngageConversationMessages = async (sessionId, siteId) =>
    request(
      `/engage/conversations/${encodeURIComponent(sessionId)}/messages?siteId=${encodeURIComponent(siteId)}`
    );

  const getEngageBot = async (siteId) =>
    request(`/engage/bot${buildQueryString({ siteId })}`);

  const updateEngageBotWithSiteId = async (siteId, payload) =>
    request(`/engage/bot${buildQueryString({ siteId })}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });

  const sendEngageDigest = async (siteId) =>
    request('/engage/digest/send', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ siteId }),
    });

  const listAdsCampaigns = async (siteId) =>
    request(`/ads/campaigns${buildQueryString({ siteId })}`);

  const getAdsCampaign = async (campaignId) =>
    request(`/ads/campaigns/${encodeURIComponent(campaignId)}`);

  const createAdsCampaign = async (payload) =>
    request('/ads/campaigns', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(payload),
    });

  const updateAdsCampaign = async (campaignId, payload) =>
    request(`/ads/campaigns/${encodeURIComponent(campaignId)}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(payload),
    });

  const upsertAdsPlacements = async (campaignId, payload) =>
    request(`/ads/campaigns/${encodeURIComponent(campaignId)}/placements`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(payload),
    });

  const activateAdsCampaign = async (campaignId) =>
    request(`/ads/campaigns/${encodeURIComponent(campaignId)}/activate`, {
      method: 'POST',
    });

  const deactivateAdsCampaign = async (campaignId) =>
    request(`/ads/campaigns/${encodeURIComponent(campaignId)}/deactivate`, {
      method: 'POST',
    });

  const getAdsReport = async (campaignId, fromUtc, toUtc) =>
    request(`/ads/campaigns/${encodeURIComponent(campaignId)}/report${buildQueryString({ fromUtc, toUtc })}`);

  const listLeads = async (siteId, page = 1, pageSize = 50) =>
    request(`/leads${buildQueryString({ siteId, page, pageSize })}`);

  const getLead = async (leadId) =>
    request(`/leads/${encodeURIComponent(leadId)}`);

  const getLeadByVisitor = async (siteId, visitorId) =>
    request(`/leads/by-visitor${buildQueryString({ siteId, visitorId })}`);

  const tagLeadStage = async (leadId, stage) =>
    request(`/flows/execute`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        triggerType: 'manual_stage_update',
        payload: { leadId, stage },
      }),
    });

  const tagLeadStageViaFlow = async (leadId, stageName) =>
    request(`/leads/${encodeURIComponent(leadId)}/stage`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ stage: stageName }),
    });

  const listTickets = async ({ siteId, visitorId, engageSessionId, page = 1, pageSize = 50 } = {}) =>
    request(`/tickets${buildQueryString({ siteId, visitorId, engageSessionId, page, pageSize })}`);

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

  const listFlows = async (siteId) =>
    request(`/flows${buildQueryString({ siteId })}`);

  const getFlow = async (flowId) =>
    request(`/flows/${encodeURIComponent(flowId)}`);

  const createFlow = async (payload) =>
    request('/flows', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });

  const updateFlow = async (flowId, payload) =>
    request(`/flows/${encodeURIComponent(flowId)}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });

  const enableFlow = async (flowId) =>
    request(`/flows/${encodeURIComponent(flowId)}/enable`, { method: 'POST' });

  const disableFlow = async (flowId) =>
    request(`/flows/${encodeURIComponent(flowId)}/disable`, { method: 'POST' });

  const listFlowRuns = async (flowId, limit = 100) =>
    request(`/flows/${encodeURIComponent(flowId)}/runs${buildQueryString({ limit })}`);

  const getFlowTemplates = async () => request('/flows/templates');

  const getPlatformSummary = async () => request('/platform-admin/summary');

  const listPlatformTenants = async ({ page = 1, pageSize = 25, search } = {}) =>
    request(`/platform-admin/tenants${buildQueryString({ page, pageSize, search })}`);

  const getPlatformTenantDetail = async (tenantId) =>
    request(`/platform-admin/tenants/${encodeURIComponent(tenantId)}`);

  const getPlatformOperationalSummary = async () => request('/platform-admin/operations/summary');

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
      create: createSite,
      updateProfile: updateSiteProfile,
      delete: deleteSite,
    },
    visitors: {
      list: listVisitors,
      detail: getVisitorDetail,
      timeline: getVisitorTimeline,
      visitCounts: getVisitCounts,
      onlineNow: getOnlineNow,
      pageAnalytics: getPageAnalytics,
      countryBreakdown: getCountryBreakdown,
      dashboardAnalytics: getDashboardAnalytics,
    },
    knowledge: {
      createSource: createKnowledgeSource,
      uploadPdf: uploadKnowledgePdf,
      indexSource: indexKnowledgeSource,
      listSources: listKnowledgeSources,
      deleteSource: deleteKnowledgeSource,
      retrieve: retrieveKnowledgeChunks,
      listQuickFacts,
      addQuickFact,
      deleteQuickFact,
    },
    intelligence: {
      status: getIntelligenceStatus,
      trends: getIntelligenceTrends,
      dashboard: getIntelligenceDashboard,
      siteSummary: getIntelligenceSiteSummary,
      refresh: refreshIntelligence,
      getProfile: getIntelligenceProfile,
      upsertProfile: upsertIntelligenceProfile,
    },
    ads: {
      listCampaigns: listAdsCampaigns,
      getCampaign: getAdsCampaign,
      createCampaign: createAdsCampaign,
      updateCampaign: updateAdsCampaign,
      upsertPlacements: upsertAdsPlacements,
      activateCampaign: activateAdsCampaign,
      deactivateCampaign: deactivateAdsCampaign,
      getReport: getAdsReport,
    },
    promos: {
      create: createPromo,
      list: listPromos,
      listEntries: listPromoEntries,
      listVisitorEntries: listVisitorPromoEntries,
      getDetail: getPromoDetail,
      flyerUrl: getPromoFlyerUrl,
      downloadCsv: downloadPromoCsv,
      downloadFlyer: downloadPromoFlyer,
    },
    engage: {
      sendChat: sendEngageChat,
      getConversations: listEngageConversations,
      listConversations: listEngageConversations,
      getOpportunityAnalytics: getEngageOpportunityAnalytics,
      getConversationMessages: getEngageConversationMessages,
      getBot: getEngageBot,
      updateBot: updateEngageBotWithSiteId,
      sendDigest: sendEngageDigest,
    },
    tickets: {
      listTickets,
      getTicket,
      getTicketNotes,
      addTicketNote,
      transitionTicketStatus,
    },
    flows: {
      list: listFlows,
      get: getFlow,
      create: createFlow,
      update: updateFlow,
      enable: enableFlow,
      disable: disableFlow,
      listRuns: listFlowRuns,
      getTemplates: getFlowTemplates,
    },
    leads: {
      list: listLeads,
      get: getLead,
      getByVisitor: getLeadByVisitor,
      tagStage: tagLeadStageViaFlow,
    },
    auth: {
      createInvite,
      acceptInvite,
      me: getCurrentUser,
      updateProfile: updateCurrentUserProfile,
      listUsers: listTenantUsers,
      updateUserRole: updateTenantUserRole,
      removeUser: removeTenantUser,
      listInvites: listTenantInvites,
      revokeInvite: revokeTenantInvite,
    },
    platformAdmin: {
      getSummary: getPlatformSummary,
      listTenants: listPlatformTenants,
      getTenantDetail: getPlatformTenantDetail,
      getOperationalSummary: getPlatformOperationalSummary,
    },
  };
};
