import { createCard, createTable, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';
import { clearToken, getToken } from '../shared/auth.js';

const app = document.getElementById('app');

const toast = createToastManager();
const apiClient = createApiClient();

const setAppLayout = () => {
  if (!app) {
    return;
  }

  app.style.minHeight = '100vh';
  app.style.padding = '32px 24px';
  app.style.background = '#f8fafc';
  app.style.fontFamily = 'system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';
};

const redirectToLogin = () => {
  window.location.href = '/public/login.html';
};

const render = () => {
  if (!app) {
    return;
  }

  if (!getToken()) {
    redirectToLogin();
    return;
  }

  setAppLayout();

  const header = document.createElement('div');
  header.style.display = 'flex';
  header.style.justifyContent = 'space-between';
  header.style.alignItems = 'center';
  header.style.marginBottom = '20px';

  const title = document.createElement('h1');
  title.textContent = 'Welcome';
  title.style.margin = '0';
  title.style.fontSize = '22px';

  const logoutButton = document.createElement('button');
  logoutButton.type = 'button';
  logoutButton.textContent = 'Logout';
  logoutButton.style.padding = '8px 12px';
  logoutButton.style.borderRadius = '6px';
  logoutButton.style.border = '1px solid #cbd5f5';
  logoutButton.style.background = '#fff';
  logoutButton.style.cursor = 'pointer';

  logoutButton.addEventListener('click', () => {
    clearToken();
    redirectToLogin();
  });

  header.append(title, logoutButton);

  const loadingText = document.createElement('div');
  loadingText.textContent = 'Loading profile...';
  loadingText.style.color = '#475569';

  const body = document.createElement('div');
  body.appendChild(loadingText);

  const card = createCard({
    title: 'Me',
    body,
  });
  card.style.maxWidth = '720px';

  app.innerHTML = '';
  app.append(header, card);

  const loadProfile = async () => {
    try {
      const me = await apiClient.request('/auth/me');
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

      body.replaceChildren(table);
    } catch (error) {
      const uiError = mapApiError(error);
      toast.show({ message: uiError.message, variant: 'danger' });
      loadingText.textContent = 'Unable to load profile.';
    }
  };

  loadProfile();
};

render();
