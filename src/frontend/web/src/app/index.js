import { createCard, createInput, createTable, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';
import { clearToken, getToken, setToken } from '../shared/auth.js';
import { renderSitesView } from '../pages/sites.js';
import { renderInstallView } from '../pages/install.js';
import { renderVisitorsView } from '../pages/visitors.js';
import { renderVisitorProfileView } from '../pages/visitorProfile.js';
import { renderKnowledgeView } from '../pages/knowledge.js';
import { renderEngageView } from '../pages/engage.js';
import { renderPromosView } from '../pages/promos.js';
import { renderLeadsView } from '../pages/leads.js';
import { renderTicketsView } from '../pages/tickets.js';
import { renderIntelligenceView } from '../pages/intelligence.js';
import { renderAdsView } from '../pages/ads.js';
import { renderPlatformAdminTenantDetailView, renderPlatformAdminView } from '../pages/platformAdmin.js';

const app = document.getElementById('app');
const toast = createToastManager();
const apiClient = createApiClient();

const setAppLayout = () => {
  if (!app) {
    return;
  }

  document.body.style.margin = '0';
  document.body.style.background = '#f8fafc';
  document.body.style.fontFamily = 'system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';
  app.style.minHeight = '100vh';
  app.style.display = 'flex';
  app.style.flexDirection = 'column';
};

const createField = ({ label, type, placeholder }) => {
  const { wrapper, input } = createInput({ label, type, placeholder });
  const error = document.createElement('div');
  error.className = 'ui-field-error';
  wrapper.appendChild(error);
  return { wrapper, input, error };
};

const validateEmail = (value) => {
  if (!value) {
    return 'Email is required.';
  }

  const atIndex = value.indexOf('@');
  const dotIndex = value.lastIndexOf('.');

  if (atIndex < 1 || dotIndex < atIndex + 2 || dotIndex === value.length - 1) {
    return 'Enter a valid email address.';
  }

  return '';
};

const validatePassword = (value) => {
  if (!value) {
    return 'Password is required.';
  }

  if (value.length < 10) {
    return 'Password must be at least 10 characters.';
  }

  if (!/[A-Za-z]/.test(value) || !/[0-9]/.test(value)) {
    return 'Password must include at least one letter and one number.';
  }

  return '';
};

const normalizeFieldKey = (key) => key.toLowerCase().replace(/\s+/g, '');

const AUTH_NAV_ITEMS = [
  { label: 'Sites', href: '#/sites' },
  { label: 'Dashboard', href: '#/dashboard' },
  { label: 'Visitors', href: '#/visitors' },
  { label: 'Knowledge', href: '#/knowledge' },
  { label: 'Engage', href: '#/engage' },
  { label: 'Promos', href: '#/promos' },
  { label: 'Intelligence', href: '#/intelligence' },
  { label: 'Ads', href: '#/ads' },
  { label: 'Leads', href: '#/leads' },
  { label: 'Tickets', href: '#/tickets' },
];

const applyFieldErrors = (errors, fields) => {
  if (!errors || typeof errors !== 'object') {
    return false;
  }

  let applied = false;
  Object.entries(errors).forEach(([key, messages]) => {
    const normalizedKey = normalizeFieldKey(key);
    const match = Object.keys(fields).find(
      (fieldKey) => normalizeFieldKey(fieldKey) === normalizedKey
    );
    if (match) {
      const message = Array.isArray(messages) ? messages.join(' ') : String(messages);
      fields[match].error.textContent = message;
      applied = true;
    }
  });

  return applied;
};

const createNavLink = ({ label, href }) => {
  const link = document.createElement('a');
  link.textContent = label;
  link.href = href;
  link.style.textDecoration = 'none';
  link.style.color = '#1e293b';
  link.style.fontWeight = '500';
  link.style.padding = '6px 10px';
  link.style.borderRadius = '6px';
  link.addEventListener('mouseover', () => {
    link.style.background = '#e2e8f0';
  });
  link.addEventListener('mouseout', () => {
    link.style.background = 'transparent';
  });
  return link;
};

const createNavbar = ({ isAuthenticated, canAccessPlatformAdmin }) => {
  const nav = document.createElement('nav');
  nav.style.display = 'flex';
  nav.style.alignItems = 'center';
  nav.style.justifyContent = 'space-between';
  nav.style.padding = '16px 24px';
  nav.style.background = '#ffffff';
  nav.style.borderBottom = '1px solid #e2e8f0';

  const brand = document.createElement('div');
  brand.textContent = 'Intentify';
  brand.style.fontWeight = '600';
  brand.style.fontSize = '18px';

  const links = document.createElement('div');
  links.style.display = 'flex';
  links.style.gap = '8px';
  links.style.flexWrap = 'wrap';
  links.style.justifyContent = 'flex-end';

  links.appendChild(createNavLink({ label: 'Home', href: '#/' }));

  if (!isAuthenticated) {
    links.appendChild(createNavLink({ label: 'Login', href: '#/login' }));
    links.appendChild(createNavLink({ label: 'Register', href: '#/register' }));
  } else {
    AUTH_NAV_ITEMS.forEach((item) => {
      links.appendChild(createNavLink(item));
    });
    if (canAccessPlatformAdmin) {
      links.appendChild(createNavLink({ label: 'Platform Admin', href: '#/platform-admin' }));
    }
    const logoutButton = document.createElement('button');
    logoutButton.type = 'button';
    logoutButton.textContent = 'Logout';
    logoutButton.style.padding = '6px 10px';
    logoutButton.style.borderRadius = '6px';
    logoutButton.style.border = '1px solid #e2e8f0';
    logoutButton.style.background = '#ffffff';
    logoutButton.style.cursor = 'pointer';
    logoutButton.addEventListener('click', () => {
      clearToken();
      window.location.hash = '#/login';
    });
    links.appendChild(logoutButton);
  }

  nav.append(brand, links);
  return nav;
};

const createSidebarNavLink = ({ label, href, active }) => {
  const link = document.createElement('a');
  link.textContent = label;
  link.href = href;
  link.style.textDecoration = 'none';
  link.style.color = active ? '#0f172a' : '#334155';
  link.style.fontWeight = active ? '600' : '500';
  link.style.padding = '10px 12px';
  link.style.borderRadius = '8px';
  link.style.background = active ? '#e2e8f0' : 'transparent';
  link.style.transition = 'background-color 120ms ease';
  link.addEventListener('mouseover', () => {
    if (!active) {
      link.style.background = '#f1f5f9';
    }
  });
  link.addEventListener('mouseout', () => {
    if (!active) {
      link.style.background = 'transparent';
    }
  });
  return link;
};

const createAuthenticatedShell = ({ route, canAccessPlatformAdmin, onLogout }) => {
  const shell = document.createElement('div');
  shell.style.display = 'flex';
  shell.style.minHeight = '100vh';
  shell.style.width = '100%';

  const sidebar = document.createElement('aside');
  sidebar.style.width = '250px';
  sidebar.style.background = '#ffffff';
  sidebar.style.borderRight = '1px solid #e2e8f0';
  sidebar.style.padding = '16px 12px';
  sidebar.style.boxSizing = 'border-box';
  sidebar.style.display = 'flex';
  sidebar.style.flexDirection = 'column';
  sidebar.style.gap = '12px';

  const brand = document.createElement('div');
  brand.textContent = 'Intentify';
  brand.style.fontWeight = '700';
  brand.style.fontSize = '18px';
  brand.style.color = '#0f172a';
  brand.style.padding = '4px 8px 10px';

  const nav = document.createElement('nav');
  nav.style.display = 'flex';
  nav.style.flexDirection = 'column';
  nav.style.gap = '4px';

  AUTH_NAV_ITEMS.forEach((item) => {
    nav.appendChild(createSidebarNavLink({ ...item, active: route === item.href.replace('#', '') }));
  });

  if (canAccessPlatformAdmin) {
    nav.appendChild(
      createSidebarNavLink({
        label: 'Platform Admin',
        href: '#/platform-admin',
        active: route === '/platform-admin' || route === '/platform-admin/tenant/:tenantId',
      })
    );
  }

  const spacer = document.createElement('div');
  spacer.style.flex = '1';

  const logoutButton = document.createElement('button');
  logoutButton.type = 'button';
  logoutButton.textContent = 'Logout';
  logoutButton.style.padding = '10px 12px';
  logoutButton.style.borderRadius = '8px';
  logoutButton.style.border = '1px solid #e2e8f0';
  logoutButton.style.background = '#ffffff';
  logoutButton.style.cursor = 'pointer';
  logoutButton.style.textAlign = 'left';
  logoutButton.addEventListener('click', onLogout);

  sidebar.append(brand, nav, spacer, logoutButton);

  const contentWrap = document.createElement('div');
  contentWrap.style.display = 'flex';
  contentWrap.style.flexDirection = 'column';
  contentWrap.style.flex = '1';
  contentWrap.style.minWidth = '0';

  const topbar = document.createElement('div');
  topbar.style.display = 'none';
  topbar.style.alignItems = 'center';
  topbar.style.justifyContent = 'space-between';
  topbar.style.padding = '12px 16px';
  topbar.style.borderBottom = '1px solid #e2e8f0';
  topbar.style.background = '#ffffff';

  const topbarBrand = document.createElement('div');
  topbarBrand.textContent = 'Intentify';
  topbarBrand.style.fontWeight = '700';
  topbarBrand.style.color = '#0f172a';

  const toggleButton = document.createElement('button');
  toggleButton.type = 'button';
  toggleButton.textContent = '☰';
  toggleButton.setAttribute('aria-label', 'Toggle navigation');
  toggleButton.style.border = '1px solid #e2e8f0';
  toggleButton.style.background = '#ffffff';
  toggleButton.style.borderRadius = '8px';
  toggleButton.style.padding = '6px 10px';
  toggleButton.style.cursor = 'pointer';

  topbar.append(topbarBrand, toggleButton);

  const main = createMain();
  main.style.padding = '24px';

  const overlay = document.createElement('button');
  overlay.type = 'button';
  overlay.setAttribute('aria-label', 'Close navigation');
  overlay.style.position = 'fixed';
  overlay.style.inset = '0';
  overlay.style.background = 'rgba(15, 23, 42, 0.35)';
  overlay.style.border = '0';
  overlay.style.display = 'none';
  overlay.style.zIndex = '39';

  const mobileQuery = window.matchMedia('(max-width: 1024px)');
  const applySidebarMode = () => {
    if (mobileQuery.matches) {
      topbar.style.display = 'flex';
      sidebar.style.position = 'fixed';
      sidebar.style.left = '0';
      sidebar.style.top = '0';
      sidebar.style.bottom = '0';
      sidebar.style.height = '100vh';
      sidebar.style.zIndex = '40';
      sidebar.style.transform = 'translateX(-105%)';
      sidebar.style.transition = 'transform 160ms ease-out';
    } else {
      topbar.style.display = 'none';
      sidebar.style.position = 'static';
      sidebar.style.height = 'auto';
      sidebar.style.zIndex = 'auto';
      sidebar.style.transform = 'translateX(0)';
      sidebar.style.transition = 'none';
      overlay.style.display = 'none';
    }
  };

  const closeSidebar = () => {
    if (!mobileQuery.matches) {
      return;
    }
    sidebar.style.transform = 'translateX(-105%)';
    overlay.style.display = 'none';
  };

  const openSidebar = () => {
    if (!mobileQuery.matches) {
      return;
    }
    sidebar.style.transform = 'translateX(0)';
    overlay.style.display = 'block';
  };

  toggleButton.addEventListener('click', () => {
    const isOpen = sidebar.style.transform === 'translateX(0)';
    if (isOpen) {
      closeSidebar();
    } else {
      openSidebar();
    }
  });
  overlay.addEventListener('click', closeSidebar);

  applySidebarMode();
  if (typeof mobileQuery.addEventListener === 'function') {
    mobileQuery.addEventListener('change', applySidebarMode);
  }

  contentWrap.append(topbar, main);
  shell.append(sidebar, contentWrap);

  return { shell, main, overlay };
};

const createMain = () => {
  const main = document.createElement('main');
  main.style.flex = '1';
  main.style.padding = '32px 24px';
  main.style.display = 'flex';
  main.style.justifyContent = 'center';
  main.style.alignItems = 'flex-start';
  return main;
};

const renderHomeView = (container) => {
  const body = document.createElement('div');
  body.style.color = '#475569';
  body.style.lineHeight = '1.6';
  body.textContent =
    'Welcome to Intentify. Use the navigation above to sign in, register, or view your dashboard.';

  const card = createCard({
    title: 'Home',
    body,
  });
  card.style.maxWidth = '640px';
  card.style.width = '100%';
  container.appendChild(card);
};

const renderLoginView = (container) => {
  const emailField = createField({
    label: 'Email',
    type: 'email',
    placeholder: 'you@example.com',
  });
  const passwordField = createField({
    label: 'Password',
    type: 'password',
    placeholder: 'At least 10 characters',
  });

  const submitButton = document.createElement('button');
  submitButton.type = 'submit';
  submitButton.textContent = 'Login';
  submitButton.style.marginTop = '12px';
  submitButton.style.padding = '10px 14px';
  submitButton.style.borderRadius = '6px';
  submitButton.style.border = 'none';
  submitButton.style.background = '#2563eb';
  submitButton.style.color = '#fff';
  submitButton.style.cursor = 'pointer';

  const switchLink = document.createElement('a');
  switchLink.href = '#/register';
  switchLink.textContent = 'Need an account? Register';
  switchLink.style.display = 'inline-block';
  switchLink.style.marginTop = '12px';
  switchLink.style.color = '#2563eb';

  const form = document.createElement('form');
  form.style.display = 'flex';
  form.style.flexDirection = 'column';
  form.style.gap = '12px';
  form.append(emailField.wrapper, passwordField.wrapper, submitButton, switchLink);

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    emailField.error.textContent = '';
    passwordField.error.textContent = '';

    const email = emailField.input.value.trim();
    const password = passwordField.input.value;

    const emailError = validateEmail(email);
    const passwordError = validatePassword(password);

    if (emailError) {
      emailField.error.textContent = emailError;
    }
    if (passwordError) {
      passwordField.error.textContent = passwordError;
    }

    if (emailError || passwordError) {
      toast.show({ message: 'Please fix the highlighted fields.', variant: 'warning' });
      return;
    }

    submitButton.disabled = true;
    submitButton.textContent = 'Signing in...';

    try {
      const response = await apiClient.request('/auth/login', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ email, password }),
      });

      setToken(response.accessToken);
      window.location.hash = '#/dashboard';
    } catch (error) {
      const uiError = mapApiError(error);
      const applied = applyFieldErrors(uiError.details?.errors, {
        email: emailField,
        password: passwordField,
      });
      toast.show({
        message: applied ? 'Please review the highlighted errors.' : uiError.message,
        variant: 'danger',
      });
    } finally {
      submitButton.disabled = false;
      submitButton.textContent = 'Login';
    }
  });

  const card = createCard({
    title: 'Login',
    body: form,
  });
  card.style.width = '100%';
  card.style.maxWidth = '420px';

  container.appendChild(card);
};

const renderRegisterView = (container) => {
  const displayNameField = createField({
    label: 'Display name',
    type: 'text',
    placeholder: 'Jane Doe',
  });
  const emailField = createField({
    label: 'Email',
    type: 'email',
    placeholder: 'you@example.com',
  });
  const passwordField = createField({
    label: 'Password',
    type: 'password',
    placeholder: 'At least 10 characters',
  });

  const submitButton = document.createElement('button');
  submitButton.type = 'submit';
  submitButton.textContent = 'Create account';
  submitButton.style.marginTop = '12px';
  submitButton.style.padding = '10px 14px';
  submitButton.style.borderRadius = '6px';
  submitButton.style.border = 'none';
  submitButton.style.background = '#2563eb';
  submitButton.style.color = '#fff';
  submitButton.style.cursor = 'pointer';

  const switchLink = document.createElement('a');
  switchLink.href = '#/login';
  switchLink.textContent = 'Already have an account? Login';
  switchLink.style.display = 'inline-block';
  switchLink.style.marginTop = '12px';
  switchLink.style.color = '#2563eb';

  const form = document.createElement('form');
  form.style.display = 'flex';
  form.style.flexDirection = 'column';
  form.style.gap = '12px';
  form.append(
    displayNameField.wrapper,
    emailField.wrapper,
    passwordField.wrapper,
    submitButton,
    switchLink
  );

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    displayNameField.error.textContent = '';
    emailField.error.textContent = '';
    passwordField.error.textContent = '';

    const displayName = displayNameField.input.value.trim();
    const email = emailField.input.value.trim();
    const password = passwordField.input.value;

    let hasError = false;

    if (!displayName) {
      displayNameField.error.textContent = 'Display name is required.';
      hasError = true;
    }

    const emailError = validateEmail(email);
    if (emailError) {
      emailField.error.textContent = emailError;
      hasError = true;
    }

    const passwordError = validatePassword(password);
    if (passwordError) {
      passwordField.error.textContent = passwordError;
      hasError = true;
    }

    if (hasError) {
      toast.show({ message: 'Please fix the highlighted fields.', variant: 'warning' });
      return;
    }

    submitButton.disabled = true;
    submitButton.textContent = 'Creating account...';

    try {
      const response = await apiClient.request('/auth/register', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ displayName, email, password }),
      });

      setToken(response.accessToken);
      window.location.hash = '#/dashboard';
    } catch (error) {
      const uiError = mapApiError(error);
      const applied = applyFieldErrors(uiError.details?.errors, {
        displayName: displayNameField,
        email: emailField,
        password: passwordField,
      });
      toast.show({
        message: applied ? 'Please review the highlighted errors.' : uiError.message,
        variant: 'danger',
      });
    } finally {
      submitButton.disabled = false;
      submitButton.textContent = 'Create account';
    }
  });

  const card = createCard({
    title: 'Register',
    body: form,
  });
  card.style.width = '100%';
  card.style.maxWidth = '460px';

  container.appendChild(card);
};

const renderDashboardView = async (container) => {
  const loadingText = document.createElement('div');
  loadingText.textContent = 'Loading profile...';
  loadingText.style.color = '#475569';

  const body = document.createElement('div');
  body.appendChild(loadingText);

  const card = createCard({
    title: 'Dashboard',
    body,
  });
  card.style.maxWidth = '720px';
  card.style.width = '100%';

  container.appendChild(card);

  try {
    const me = await apiClient.request('/auth/me');
    let sites = [];
    try {
      sites = await apiClient.request('/sites');
    } catch (error) {
      const uiError = mapApiError(error);
      toast.show({ message: uiError.message, variant: 'warning' });
    }
    const rows = [
      { field: 'Display name', value: me.displayName || '' },
      { field: 'User ID', value: me.userId || '' },
      { field: 'Tenant ID', value: me.tenantId || '' },
      {
        field: 'Roles',
        value: Array.isArray(me.roles) ? me.roles.join(', ') : me.roles || '',
      },
    ];

    const table = createTable({
      columns: [
        { key: 'field', label: 'Field' },
        { key: 'value', label: 'Value' },
      ],
      rows,
    });

    const sitesSection = document.createElement('div');
    sitesSection.style.marginTop = '24px';

    const sitesHeader = document.createElement('div');
    sitesHeader.style.display = 'flex';
    sitesHeader.style.alignItems = 'center';
    sitesHeader.style.justifyContent = 'space-between';

    const sitesTitle = document.createElement('h3');
    sitesTitle.textContent = 'Sites';
    sitesTitle.style.margin = '0';
    sitesTitle.style.fontSize = '18px';
    sitesTitle.style.color = '#0f172a';

    const manageSitesLink = document.createElement('a');
    manageSitesLink.textContent = 'Manage Sites';
    manageSitesLink.href = '#/sites';
    manageSitesLink.style.color = '#2563eb';
    manageSitesLink.style.textDecoration = 'none';
    manageSitesLink.style.fontWeight = '500';

    sitesHeader.append(sitesTitle, manageSitesLink);
    sitesSection.appendChild(sitesHeader);

    if (Array.isArray(sites) && sites.length > 0) {
      const sitesTable = createTable({
        columns: [
          { key: 'domain', label: 'Domain' },
          { key: 'siteId', label: 'Site ID' },
          { key: 'configured', label: 'Configured' },
          { key: 'allowedOrigins', label: 'Allowed Origins (count)' },
        ],
        rows: sites.map((site) => ({
          domain: site.domain || '',
          siteId: site.siteId || '',
          configured: site.installationStatus?.isConfigured ? 'Yes' : 'No',
          allowedOrigins:
            site.installationStatus?.allowedOriginsCount !== undefined
              ? String(site.installationStatus.allowedOriginsCount)
              : '0',
        })),
      });
      sitesTable.style.marginTop = '12px';
      sitesSection.appendChild(sitesTable);
    } else {
      const emptyText = document.createElement('div');
      emptyText.textContent = 'No sites yet. Create one in the Sites page.';
      emptyText.style.marginTop = '12px';
      emptyText.style.color = '#475569';
      sitesSection.appendChild(emptyText);
    }

    body.replaceChildren(table, sitesSection);
  } catch (error) {
    const uiError = mapApiError(error);
    if (uiError.status === 401) {
      return;
    }
    toast.show({ message: uiError.message, variant: 'danger' });
    loadingText.textContent = 'Unable to load profile.';
  }
};

const routes = {
  '/': renderHomeView,
  '/login': renderLoginView,
  '/register': renderRegisterView,
  '/dashboard': renderDashboardView,
  '/sites': renderSitesView,
  '/install': renderInstallView,
  '/visitors': renderVisitorsView,
  '/knowledge': renderKnowledgeView,
  '/engage': renderEngageView,
  '/promos': renderPromosView,
  '/intelligence': renderIntelligenceView,
  '/ads': renderAdsView,
  '/leads': renderLeadsView,
  '/tickets': renderTicketsView,
  '/platform-admin': renderPlatformAdminView,
  '/platform-admin/tenant/:tenantId': renderPlatformAdminTenantDetailView,
};

const getRouteFromHash = () => {
  const hash = window.location.hash.replace(/^#/, '');
  if (!hash || hash === '/') {
    return { path: '/', query: {}, params: {} };
  }

  const [pathSegment, queryString] = hash.split('?');
  const path = pathSegment.startsWith('/') ? pathSegment : `/${pathSegment}`;
  const query = {};

  if (queryString) {
    const params = new URLSearchParams(queryString);
    params.forEach((value, key) => {
      query[key] = value;
    });
  }

  const visitorMatch = path.match(/^\/visitors\/([^/]+)$/);
  if (visitorMatch) {
    return { path: '/visitors/:visitorId', query, params: { visitorId: visitorMatch[1] } };
  }

  const platformTenantMatch = path.match(/^\/platform-admin\/tenant\/([^/]+)$/);
  if (platformTenantMatch) {
    return {
      path: '/platform-admin/tenant/:tenantId',
      query,
      params: { tenantId: platformTenantMatch[1] },
    };
  }

  return { path, query, params: {} };
};


const authState = {
  loaded: false,
  loading: false,
  roles: [],
};

const hasPlatformAccess = () =>
  Array.isArray(authState.roles)
  && (authState.roles.includes('super_admin') || authState.roles.includes('platform_admin'));

const loadAuthRoles = async () => {
  if (authState.loading) {
    return;
  }

  authState.loading = true;
  try {
    const me = await apiClient.request('/auth/me');
    authState.roles = Array.isArray(me.roles) ? me.roles : [];
  } catch (error) {
    authState.roles = [];
  } finally {
    authState.loaded = true;
    authState.loading = false;
    renderApp();
  }
};

const renderApp = () => {
  if (!app) {
    return;
  }

  setAppLayout();

  const { path: route, query, params } = getRouteFromHash();
  const isAuthenticated = Boolean(getToken());

  const protectedRoutes = [
    '/dashboard',
    '/sites',
    '/install',
    '/visitors',
    '/visitors/:visitorId',
    '/knowledge',
    '/engage',
    '/promos',
    '/intelligence',
    '/ads',
    '/leads',
    '/tickets',
    '/platform-admin',
    '/platform-admin/tenant/:tenantId',
  ];

  if (protectedRoutes.includes(route) && !isAuthenticated) {
    window.location.hash = '#/login';
    return;
  }

  if ((route === '/login' || route === '/register') && isAuthenticated) {
    window.location.hash = '#/dashboard';
    return;
  }

  if (isAuthenticated && !authState.loaded) {
    loadAuthRoles();
  }

  if (!isAuthenticated) {
    authState.loaded = false;
    authState.roles = [];
  }

  const isPlatformRoute = route === '/platform-admin' || route === '/platform-admin/tenant/:tenantId';
  if (isAuthenticated && isPlatformRoute && !authState.loaded) {
    app.innerHTML = '';
    const authenticatedShell = createAuthenticatedShell({
      route,
      canAccessPlatformAdmin: false,
      onLogout: () => {
        clearToken();
        window.location.hash = '#/login';
      },
    });
    const { shell, main, overlay } = authenticatedShell;
    const loading = document.createElement('div');
    loading.style.color = '#475569';
    loading.textContent = 'Loading access...';
    main.appendChild(loading);
    app.append(shell, overlay);
    return;
  }

  if (isAuthenticated && isPlatformRoute && authState.loaded && !hasPlatformAccess()) {
    window.location.hash = '#/dashboard';
    return;
  }

  const view = routes[route] || routes['/'];
  app.innerHTML = '';
  let main;

  if (isAuthenticated) {
    const authenticatedShell = createAuthenticatedShell({
      route,
      canAccessPlatformAdmin: hasPlatformAccess(),
      onLogout: () => {
        clearToken();
        window.location.hash = '#/login';
      },
    });
    main = authenticatedShell.main;
    app.append(authenticatedShell.shell, authenticatedShell.overlay);
  } else {
    const navbar = createNavbar({ isAuthenticated, canAccessPlatformAdmin: false });
    main = createMain();
    app.append(navbar, main);
  }

  if (route === '/visitors/:visitorId') {
    renderVisitorProfileView(main, { apiClient, toast, query, params });
    return;
  }

  if (route === '/platform-admin/tenant/:tenantId') {
    renderPlatformAdminTenantDetailView(main, { apiClient, toast, query, params });
    return;
  }

  view(main, { apiClient, toast, query, params });
};

window.addEventListener('hashchange', renderApp);
renderApp();
