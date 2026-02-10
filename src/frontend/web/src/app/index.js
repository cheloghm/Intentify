import { createCard, createInput, createTable, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';
import { clearToken, getToken, setToken } from '../shared/auth.js';
import { renderSitesView } from '../pages/sites.js';
import { renderInstallView } from '../pages/install.js';

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

const createNavbar = ({ isAuthenticated }) => {
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

  links.appendChild(createNavLink({ label: 'Home', href: '#/' }));

  if (!isAuthenticated) {
    links.appendChild(createNavLink({ label: 'Login', href: '#/login' }));
    links.appendChild(createNavLink({ label: 'Register', href: '#/register' }));
  } else {
    links.appendChild(createNavLink({ label: 'Sites', href: '#/sites' }));
    links.appendChild(createNavLink({ label: 'Install', href: '#/install' }));
    links.appendChild(createNavLink({ label: 'Dashboard', href: '#/dashboard' }));
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
};

const getRouteFromHash = () => {
  const hash = window.location.hash.replace(/^#/, '');
  if (!hash || hash === '/') {
    return { path: '/', query: {} };
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

  return { path, query };
};

const renderApp = () => {
  if (!app) {
    return;
  }

  setAppLayout();

  const { path: route, query } = getRouteFromHash();
  const isAuthenticated = Boolean(getToken());

  if ((route === '/dashboard' || route === '/sites' || route === '/install') && !isAuthenticated) {
    window.location.hash = '#/login';
    return;
  }

  if ((route === '/login' || route === '/register') && isAuthenticated) {
    window.location.hash = '#/dashboard';
    return;
  }

  const view = routes[route] || routes['/'];
  app.innerHTML = '';
  const navbar = createNavbar({ isAuthenticated });
  const main = createMain();
  app.append(navbar, main);
  view(main, { apiClient, toast, query });
};

window.addEventListener('hashchange', renderApp);
renderApp();
