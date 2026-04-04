import { createBadge, createCard, createInput, createTable, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';
import { clearToken, getToken, setToken } from '../shared/auth.js';
import { renderSitesView } from '../pages/sites.js';
import { renderInstallView } from '../pages/install.js';
import { renderVisitorsView } from '../pages/visitors.js';
import { renderVisitorProfileView } from '../pages/visitorProfile.js';
import { renderKnowledgeView } from '../pages/knowledge.js';
import { renderFlowsView } from '../pages/flows.js';
import { renderEngageView } from '../pages/engage.js';
import { renderPromosView } from '../pages/promos.js';
import { renderLeadsView } from '../pages/leads.js';
import { renderTicketsView } from '../pages/tickets.js';
import { renderIntelligenceView } from '../pages/intelligence.js';
import { renderAdsView } from '../pages/ads.js';
import { renderTeamView } from '../pages/team.js';
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
  { label: 'Team', href: '#/team' },
  { label: 'Visitors', href: '#/visitors' },
  { label: 'Knowledge', href: '#/knowledge' },
  { label: 'Flows', href: '#/flows' },
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

const createNavbar = ({ isAuthenticated, canAccessPlatformAdmin, canAccessTeam }) => {
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
    AUTH_NAV_ITEMS.filter((item) => item.href !== '#/team' || canAccessTeam).forEach((item) => {
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
  link.style.display = 'block';
  link.style.textDecoration = 'none';
  link.style.padding = '8px 12px 8px 14px';
  link.style.borderRadius = '6px';
  link.style.fontSize = '13px';
  link.style.fontWeight = active ? '500' : '400';
  link.style.color = active ? '#ffffff' : 'rgba(255,255,255,0.55)';
  link.style.background = active ? 'rgba(99,102,241,0.18)' : 'transparent';
  link.style.borderLeft = active ? '3px solid #6366f1' : '3px solid transparent';
  link.style.transition = 'all 150ms ease';
  link.style.marginBottom = '2px';

  if (!active) {
    link.addEventListener('mouseover', () => {
      link.style.background = 'rgba(255,255,255,0.06)';
      link.style.color = 'rgba(255,255,255,0.85)';
    });
    link.addEventListener('mouseout', () => {
      link.style.background = 'transparent';
      link.style.color = 'rgba(255,255,255,0.55)';
    });
  }
  return link;
};


const getFirstName = (displayName) => {
  const trimmed = (displayName || '').trim();
  if (!trimmed) {
    return 'Account';
  }

  const [first] = trimmed.split(/\s+/);
  return first || 'Account';
};

const createUserMenuTrigger = ({ firstName, onProfile, onLogout }) => {
  const container = document.createElement('div');
  container.style.position = 'relative';

  const trigger = document.createElement('button');
  trigger.type = 'button';
  trigger.textContent = firstName;
  trigger.style.padding = '8px 12px';
  trigger.style.borderRadius = '8px';
  trigger.style.border = '1px solid #e2e8f0';
  trigger.style.background = '#ffffff';
  trigger.style.cursor = 'pointer';
  trigger.style.fontWeight = '600';
  trigger.style.color = '#0f172a';

  const menu = document.createElement('div');
  menu.style.position = 'absolute';
  menu.style.top = 'calc(100% + 8px)';
  menu.style.right = '0';
  menu.style.minWidth = '160px';
  menu.style.background = '#ffffff';
  menu.style.border = '1px solid #e2e8f0';
  menu.style.borderRadius = '10px';
  menu.style.boxShadow = '0 12px 28px rgba(15, 23, 42, 0.12)';
  menu.style.padding = '6px';
  menu.style.display = 'none';
  menu.style.zIndex = '60';

  const createMenuButton = (label, onClick) => {
    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = label;
    button.style.width = '100%';
    button.style.textAlign = 'left';
    button.style.padding = '8px 10px';
    button.style.border = '0';
    button.style.borderRadius = '8px';
    button.style.background = 'transparent';
    button.style.cursor = 'pointer';
    button.style.color = '#1e293b';
    button.addEventListener('click', () => {
      menu.style.display = 'none';
      onClick();
    });
    button.addEventListener('mouseover', () => {
      button.style.background = '#f1f5f9';
    });
    button.addEventListener('mouseout', () => {
      button.style.background = 'transparent';
    });
    return button;
  };

  menu.append(
    createMenuButton('Profile', onProfile),
    createMenuButton('Logout', onLogout)
  );

  trigger.addEventListener('click', (event) => {
    event.stopPropagation();
    menu.style.display = menu.style.display === 'none' ? 'block' : 'none';
  });

  container.append(trigger, menu);

  const close = () => {
    menu.style.display = 'none';
  };

  return { container, close };
};

const createProfileModal = ({
  profile,
  currentRole,
  canManageTeam,
  isAdministrator,
  inviteRoles,
  onInvite,
  onManageTeam,
  onClose,
  onSave,
}) => {
  const overlay = document.createElement('div');
  overlay.setAttribute('role', 'dialog');
  overlay.setAttribute('aria-modal', 'true');
  overlay.style.position = 'fixed';
  overlay.style.inset = '0';
  overlay.style.background = 'rgba(15, 23, 42, 0.42)';
  overlay.style.border = '0';
  overlay.style.display = 'flex';
  overlay.style.alignItems = 'center';
  overlay.style.justifyContent = 'center';
  overlay.style.zIndex = '100';

  const card = document.createElement('div');
  card.style.width = '100%';
  card.style.maxWidth = '460px';
  card.style.background = '#ffffff';
  card.style.border = '1px solid #e2e8f0';
  card.style.borderRadius = '14px';
  card.style.boxShadow = '0 16px 36px rgba(15, 23, 42, 0.18)';
  card.style.padding = '18px';
  card.style.display = 'flex';
  card.style.flexDirection = 'column';
  card.style.gap = '12px';

  const titleRow = document.createElement('div');
  titleRow.style.display = 'flex';
  titleRow.style.alignItems = 'center';
  titleRow.style.justifyContent = 'space-between';

  const title = document.createElement('h3');
  title.textContent = 'Profile';
  title.style.margin = '0 0 4px';
  title.style.color = '#0f172a';

  const roleWrapper = document.createElement('div');
  roleWrapper.style.display = 'flex';
  roleWrapper.style.alignItems = 'center';
  roleWrapper.style.gap = '8px';

  const roleLabel = document.createElement('span');
  roleLabel.textContent = 'Role';
  roleLabel.style.fontSize = '13px';
  roleLabel.style.color = '#475569';

  const roleText = currentRole === 'super_admin'
    ? 'Super Admin'
    : currentRole === 'admin'
      ? 'Admin'
      : currentRole === 'manager'
        ? 'Manager'
        : 'User';
  const roleBadge = createBadge({ text: roleText, variant: 'neutral' });
  roleWrapper.append(roleLabel, roleBadge);

  const displayNameField = createField({
    label: 'Display name',
    type: 'text',
    placeholder: 'Jane Doe',
  });
  displayNameField.input.value = profile?.displayName || '';

  const emailField = createField({
    label: 'Email',
    type: 'email',
    placeholder: '',
  });
  emailField.input.value = profile?.email || '';
  emailField.input.disabled = true;
  emailField.input.style.background = '#f8fafc';
  emailField.input.style.cursor = 'not-allowed';

  const organizationField = createField({
    label: 'Organization',
    type: 'text',
    placeholder: 'Organization',
  });
  organizationField.input.value = profile?.organizationName || '';

  if (isAdministrator) {
    titleRow.appendChild(createBadge({ text: 'Administrator', variant: 'info' }));
  }

  titleRow.prepend(title);

  if (!isAdministrator) {
    organizationField.input.disabled = true;
    organizationField.input.style.background = '#f8fafc';
    organizationField.input.style.cursor = 'not-allowed';
  }

  let manageTeamButton;
  let inviteSection;
  if (canManageTeam) {
    const availableInviteRoles = Array.isArray(inviteRoles) ? inviteRoles : [];
    const defaultInviteRole = availableInviteRoles[0] || 'user';

    inviteSection = document.createElement('div');
    inviteSection.style.display = 'flex';
    inviteSection.style.flexDirection = 'column';
    inviteSection.style.gap = '10px';
    inviteSection.style.border = '1px solid #e2e8f0';
    inviteSection.style.borderRadius = '10px';
    inviteSection.style.padding = '12px';

    const inviteTitle = document.createElement('div');
    inviteTitle.textContent = 'Invite team member';
    inviteTitle.style.fontSize = '14px';
    inviteTitle.style.fontWeight = '600';
    inviteTitle.style.color = '#0f172a';

    const inviteEmailField = createField({
      label: 'Invite email',
      type: 'email',
      placeholder: 'you@example.com',
    });

    const inviteRoleField = document.createElement('label');
    inviteRoleField.className = 'ui-input';

    const inviteRoleLabel = document.createElement('span');
    inviteRoleLabel.textContent = 'Role';

    const inviteRoleSelect = document.createElement('select');
    inviteRoleSelect.className = 'ui-input__field';
    availableInviteRoles.forEach((role) => {
      const option = document.createElement('option');
      option.value = role;
      option.textContent = role === 'admin'
        ? 'Admin'
        : role === 'manager'
          ? 'Manager'
          : 'User';
      inviteRoleSelect.appendChild(option);
    });
    inviteRoleSelect.value = defaultInviteRole;

    const inviteRoleError = document.createElement('div');
    inviteRoleError.className = 'ui-field-error';
    inviteRoleField.append(inviteRoleLabel, inviteRoleSelect, inviteRoleError);

    const inviteButton = document.createElement('button');
    inviteButton.type = 'button';
    inviteButton.textContent = 'Send invite';
    inviteButton.style.alignSelf = 'flex-start';
    inviteButton.style.padding = '8px 12px';
    inviteButton.style.border = '0';
    inviteButton.style.borderRadius = '8px';
    inviteButton.style.background = '#2563eb';
    inviteButton.style.color = '#ffffff';
    inviteButton.style.cursor = 'pointer';

    if (!availableInviteRoles.length) {
      inviteRoleSelect.disabled = true;
      inviteButton.disabled = true;
      inviteButton.style.opacity = '0.6';
      inviteButton.style.cursor = 'not-allowed';
    }

    inviteButton.addEventListener('click', async () => {
      inviteEmailField.error.textContent = '';
      inviteRoleError.textContent = '';

      const email = inviteEmailField.input.value.trim();
      const role = inviteRoleSelect.value;

      const emailError = validateEmail(email);
      if (emailError) {
        inviteEmailField.error.textContent = emailError;
        return;
      }

      if (!availableInviteRoles.includes(role)) {
        inviteRoleError.textContent = 'You cannot invite this role.';
        return;
      }

      inviteButton.disabled = true;
      inviteButton.textContent = 'Sending...';

      try {
        await onInvite({ email, role });
        inviteEmailField.input.value = '';
        inviteRoleSelect.value = defaultInviteRole;
        toast.show({ message: 'Invitation sent.', variant: 'success' });
      } catch (error) {
        const uiError = mapApiError(error);
        const applied = applyFieldErrors(uiError.details?.errors, {
          email: inviteEmailField,
          role: { error: inviteRoleError },
        });
        toast.show({
          message: applied ? 'Please review the highlighted errors.' : uiError.message,
          variant: 'danger',
        });
      } finally {
        inviteButton.disabled = false;
        inviteButton.textContent = 'Send invite';
      }
    });

    inviteSection.append(inviteTitle, inviteEmailField.wrapper, inviteRoleField, inviteButton);

    manageTeamButton = document.createElement('button');
    manageTeamButton.type = 'button';
    manageTeamButton.textContent = 'Manage team';
    manageTeamButton.style.alignSelf = 'flex-start';
    manageTeamButton.style.padding = '8px 12px';
    manageTeamButton.style.border = '1px solid #cbd5e1';
    manageTeamButton.style.borderRadius = '8px';
    manageTeamButton.style.background = '#ffffff';
    manageTeamButton.style.cursor = 'pointer';
    manageTeamButton.addEventListener('click', onManageTeam);
  }

  const buttons = document.createElement('div');
  buttons.style.display = 'flex';
  buttons.style.justifyContent = 'flex-end';
  buttons.style.gap = '8px';
  buttons.style.marginTop = '4px';

  const cancel = document.createElement('button');
  cancel.type = 'button';
  cancel.textContent = 'Cancel';
  cancel.style.padding = '9px 12px';
  cancel.style.border = '1px solid #cbd5e1';
  cancel.style.borderRadius = '8px';
  cancel.style.background = '#ffffff';
  cancel.style.cursor = 'pointer';
  cancel.addEventListener('click', onClose);

  const save = document.createElement('button');
  save.type = 'button';
  save.textContent = 'Save';
  save.style.padding = '9px 12px';
  save.style.border = '0';
  save.style.borderRadius = '8px';
  save.style.background = '#2563eb';
  save.style.color = '#ffffff';
  save.style.cursor = 'pointer';

  save.addEventListener('click', async () => {
    displayNameField.error.textContent = '';
    organizationField.error.textContent = '';

    const displayName = displayNameField.input.value.trim();
    const organizationName = organizationField.input.value.trim();

    if (!displayName) {
      displayNameField.error.textContent = 'Display name is required.';
      return;
    }

    save.disabled = true;
    save.textContent = 'Saving...';

    try {
      await onSave({
        displayName,
        organizationName: isAdministrator ? organizationName : undefined,
      });
      onClose();
    } catch (error) {
      const uiError = mapApiError(error);
      const applied = applyFieldErrors(uiError.details?.errors, {
        displayName: displayNameField,
        organizationName: organizationField,
      });
      toast.show({
        message: applied ? 'Please review the highlighted errors.' : uiError.message,
        variant: 'danger',
      });
    } finally {
      save.disabled = false;
      save.textContent = 'Save';
    }
  });

  buttons.append(cancel, save);
  card.append(titleRow, displayNameField.wrapper, emailField.wrapper, roleWrapper, organizationField.wrapper);
  if (inviteSection) {
    card.append(inviteSection);
  }
  if (manageTeamButton) {
    card.append(manageTeamButton);
  }
  card.append(buttons);

  overlay.addEventListener('click', (event) => {
    if (event.target === overlay) {
      onClose();
    }
  });

  card.addEventListener('click', (event) => {
    event.stopPropagation();
  });

  overlay.appendChild(card);

  const onKeyDown = (event) => {
    if (event.key === 'Escape') {
      onClose();
    }
  };
  window.addEventListener('keydown', onKeyDown);

  return {
    overlay,
    cleanup: () => {
      window.removeEventListener('keydown', onKeyDown);
    },
  };
};

const createAuthenticatedShell = ({ route, canAccessPlatformAdmin, canAccessTeam, onLogout, onOpenProfile, firstName }) => {
  const shell = document.createElement('div');
  shell.style.display = 'flex';
  shell.style.minHeight = '100vh';
  shell.style.width = '100%';

  const sidebar = document.createElement('aside');
  sidebar.style.width = '240px';
  sidebar.style.background = '#0f172a';
  sidebar.style.boxSizing = 'border-box';
  sidebar.style.display = 'flex';
  sidebar.style.flexDirection = 'column';
  sidebar.style.flexShrink = '0';

  const brand = document.createElement('div');
  brand.style.padding = '20px 16px 16px';
  brand.style.borderBottom = '1px solid rgba(255,255,255,0.06)';
  const brandText = document.createElement('div');
  brandText.textContent = 'Intentify';
  brandText.style.fontWeight = '700';
  brandText.style.fontSize = '18px';
  brandText.style.color = '#6366f1';
  brand.appendChild(brandText);

  const nav = document.createElement('nav');
  nav.style.flex = '1';
  nav.style.padding = '8px 12px';
  nav.style.overflowY = 'auto';
  nav.style.display = 'flex';
  nav.style.flexDirection = 'column';

  const addNavSection = (sectionLabel, items) => {
    const label = document.createElement('div');
    label.className = 'sidebar-section-label';
    label.textContent = sectionLabel;
    nav.appendChild(label);
    items.forEach((item) => {
      const isActive = route === item.href.replace('#', '')
        || (item.href === '#/platform-admin' && (route === '/platform-admin' || route === '/platform-admin/tenant/:tenantId'));
      nav.appendChild(createSidebarNavLink({ ...item, active: isActive }));
    });
  };

  addNavSection('MAIN', [
    { label: 'Dashboard', href: '#/dashboard' },
    { label: 'Sites',     href: '#/sites' },
    { label: 'Install',   href: '#/install' },
  ]);

  addNavSection('INTELLIGENCE', [
    { label: 'Visitors',     href: '#/visitors' },
    { label: 'Intelligence', href: '#/intelligence' },
  ]);

  addNavSection('ENGAGE', [
    { label: 'Knowledge', href: '#/knowledge' },
    { label: 'Flows',     href: '#/flows' },
    { label: 'Engage',    href: '#/engage' },
    { label: 'Leads',     href: '#/leads' },
    { label: 'Tickets',   href: '#/tickets' },
  ]);

  addNavSection('MARKETING', [
    { label: 'Ads',    href: '#/ads' },
    { label: 'Promos', href: '#/promos' },
  ]);

  if (canAccessTeam || canAccessPlatformAdmin) {
    const adminItems = [];
    if (canAccessTeam)          adminItems.push({ label: 'Team',           href: '#/team' });
    if (canAccessPlatformAdmin) adminItems.push({ label: 'Platform Admin', href: '#/platform-admin' });
    addNavSection('ADMIN', adminItems);
  }

  const userArea = document.createElement('div');
  userArea.style.padding = '12px 16px';
  userArea.style.borderTop = '1px solid rgba(255,255,255,0.06)';
  userArea.style.display = 'flex';
  userArea.style.alignItems = 'center';
  userArea.style.gap = '10px';

  const avatar = document.createElement('div');
  avatar.style.width = '32px';
  avatar.style.height = '32px';
  avatar.style.borderRadius = '50%';
  avatar.style.background = '#6366f1';
  avatar.style.color = '#ffffff';
  avatar.style.display = 'flex';
  avatar.style.alignItems = 'center';
  avatar.style.justifyContent = 'center';
  avatar.style.fontSize = '13px';
  avatar.style.fontWeight = '600';
  avatar.style.flexShrink = '0';
  avatar.textContent = (firstName || '?')[0].toUpperCase();

  const userInfo = document.createElement('div');
  userInfo.style.flex = '1';
  userInfo.style.minWidth = '0';

  const userNameEl = document.createElement('div');
  userNameEl.textContent = firstName;
  userNameEl.style.fontSize = '13px';
  userNameEl.style.fontWeight = '500';
  userNameEl.style.color = 'rgba(255,255,255,0.85)';
  userNameEl.style.overflow = 'hidden';
  userNameEl.style.textOverflow = 'ellipsis';
  userNameEl.style.whiteSpace = 'nowrap';

  const logoutBtn = document.createElement('button');
  logoutBtn.type = 'button';
  logoutBtn.textContent = 'Logout';
  logoutBtn.style.background = 'none';
  logoutBtn.style.border = 'none';
  logoutBtn.style.padding = '0';
  logoutBtn.style.fontSize = '11px';
  logoutBtn.style.color = 'rgba(255,255,255,0.35)';
  logoutBtn.style.cursor = 'pointer';
  logoutBtn.addEventListener('click', onLogout);

  userInfo.append(userNameEl, logoutBtn);
  userArea.append(avatar, userInfo);

  sidebar.append(brand, nav, userArea);

  const contentWrap = document.createElement('div');
  contentWrap.style.display = 'flex';
  contentWrap.style.flexDirection = 'column';
  contentWrap.style.flex = '1';
  contentWrap.style.minWidth = '0';

  const topbar = document.createElement('div');
  topbar.style.display = 'flex';
  topbar.style.alignItems = 'center';
  topbar.style.justifyContent = 'space-between';
  topbar.style.padding = '12px 16px';
  topbar.style.borderBottom = '1px solid #e2e8f0';
  topbar.style.background = '#ffffff';

  const topbarLeft = document.createElement('div');
  topbarLeft.style.display = 'flex';
  topbarLeft.style.alignItems = 'center';
  topbarLeft.style.gap = '10px';

  const toggleButton = document.createElement('button');
  toggleButton.type = 'button';
  toggleButton.textContent = '☰';
  toggleButton.setAttribute('aria-label', 'Toggle navigation');
  toggleButton.style.border = '1px solid #e2e8f0';
  toggleButton.style.background = '#ffffff';
  toggleButton.style.borderRadius = '8px';
  toggleButton.style.padding = '6px 10px';
  toggleButton.style.cursor = 'pointer';

  const topbarBrand = document.createElement('div');
  topbarBrand.textContent = 'Intentify';
  topbarBrand.style.fontWeight = '700';
  topbarBrand.style.color = '#0f172a';

  topbarLeft.append(toggleButton, topbarBrand);

  const userMenu = createUserMenuTrigger({
    firstName,
    onProfile: onOpenProfile,
    onLogout,
  });

  topbar.append(topbarLeft, userMenu.container);

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
      toggleButton.style.display = 'inline-flex';
      sidebar.style.position = 'fixed';
      sidebar.style.left = '0';
      sidebar.style.top = '0';
      sidebar.style.bottom = '0';
      sidebar.style.height = '100vh';
      sidebar.style.zIndex = '40';
      sidebar.style.transform = 'translateX(-105%)';
      sidebar.style.transition = 'transform 160ms ease-out';
    } else {
      toggleButton.style.display = 'none';
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
  shell.addEventListener('click', (event) => {
    if (!userMenu.container.contains(event.target)) {
      userMenu.close();
    }
  });

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
  const organizationNameField = createField({
    label: 'Organization name',
    type: 'text',
    placeholder: 'Acme Inc',
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
    organizationNameField.wrapper,
    emailField.wrapper,
    passwordField.wrapper,
    submitButton,
    switchLink
  );

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    displayNameField.error.textContent = '';
    organizationNameField.error.textContent = '';
    emailField.error.textContent = '';
    passwordField.error.textContent = '';

    const displayName = displayNameField.input.value.trim();
    const organizationName = organizationNameField.input.value.trim();
    const email = emailField.input.value.trim();
    const password = passwordField.input.value;

    let hasError = false;

    if (!displayName) {
      displayNameField.error.textContent = 'Display name is required.';
      hasError = true;
    }

    if (!organizationName) {
      organizationNameField.error.textContent = 'Organization name is required.';
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
        body: JSON.stringify({ displayName, organizationName, email, password }),
      });

      setToken(response.accessToken);
      window.location.hash = '#/dashboard';
    } catch (error) {
      const uiError = mapApiError(error);
      const applied = applyFieldErrors(uiError.details?.errors, {
        displayName: displayNameField,
        organizationName: organizationNameField,
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


const renderAcceptInviteView = (container, { query } = {}) => {
  const tokenField = createField({
    label: 'Invitation token',
    type: 'text',
    placeholder: 'Paste invitation token',
  });
  tokenField.input.value = query?.token || '';

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
  submitButton.textContent = 'Accept invite';
  submitButton.style.marginTop = '12px';
  submitButton.style.padding = '10px 14px';
  submitButton.style.borderRadius = '6px';
  submitButton.style.border = 'none';
  submitButton.style.background = '#2563eb';
  submitButton.style.color = '#fff';
  submitButton.style.cursor = 'pointer';

  const form = document.createElement('form');
  form.style.display = 'flex';
  form.style.flexDirection = 'column';
  form.style.gap = '12px';
  form.append(tokenField.wrapper, displayNameField.wrapper, emailField.wrapper, passwordField.wrapper, submitButton);

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    tokenField.error.textContent = '';
    displayNameField.error.textContent = '';
    emailField.error.textContent = '';
    passwordField.error.textContent = '';

    const token = tokenField.input.value.trim();
    const displayName = displayNameField.input.value.trim();
    const email = emailField.input.value.trim();
    const password = passwordField.input.value;

    let hasError = false;
    if (!token) {
      tokenField.error.textContent = 'Invitation token is required.';
      hasError = true;
    }
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
    submitButton.textContent = 'Accepting...';

    try {
      const response = await apiClient.auth.acceptInvite({ token, displayName, email, password });
      setToken(response.accessToken);
      window.location.hash = '#/dashboard';
    } catch (error) {
      const uiError = mapApiError(error);
      const applied = applyFieldErrors(uiError.details?.errors, {
        token: tokenField,
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
      submitButton.textContent = 'Accept invite';
    }
  });

  const card = createCard({
    title: 'Accept invitation',
    body: form,
  });
  card.style.width = '100%';
  card.style.maxWidth = '460px';

  container.appendChild(card);
};

const renderDashboardView = async (container) => {
  const page = document.createElement('div');
  page.style.display = 'flex';
  page.style.flexDirection = 'column';
  page.style.gap = '20px';
  page.style.width = '100%';

  // Header
  const header = document.createElement('div');
  header.className = 'page-header';
  const titleWrap = document.createElement('div');
  const title = document.createElement('h2');
  title.className = 'page-title';
  title.textContent = 'Dashboard';
  const subtitle = document.createElement('div');
  subtitle.className = 'page-subtitle';
  subtitle.textContent = new Date().toLocaleDateString(undefined, { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' });
  titleWrap.append(title, subtitle);
  header.appendChild(titleWrap);

  // Metrics grid
  const metricsGrid = document.createElement('div');
  metricsGrid.className = 'grid-4';

  const makeMetricCard = (label, value, icon) => {
    const card = document.createElement('div');
    card.className = 'metric-card';
    card.innerHTML = `
      <div class="metric-icon" style="background:var(--brand-primary-light);color:var(--brand-primary)">${icon}</div>
      <div class="metric-label">${label}</div>
      <div class="metric-value">${value}</div>
    `;
    return card;
  };

  const metricPlaceholders = [
    ['Visitors (30d)', '—', '👥'],
    ['Active Leads', '—', '🎯'],
    ['Open Tickets', '—', '🎫'],
    ['Knowledge Sources', '—', '📚'],
  ].map(([label, value, icon]) => makeMetricCard(label, value, icon));
  metricPlaceholders.forEach((c) => metricsGrid.appendChild(c));

  // Charts row
  const chartsRow = document.createElement('div');
  chartsRow.className = 'grid-2';

  const convChartCard = document.createElement('div');
  convChartCard.className = 'card';
  convChartCard.innerHTML = '<div class="card-header"><div class="card-title">Conversations (last 7 days)</div></div>';
  const convCanvas = document.createElement('canvas');
  convCanvas.height = 200;
  convChartCard.appendChild(convCanvas);

  const pipelineChartCard = document.createElement('div');
  pipelineChartCard.className = 'card';
  pipelineChartCard.innerHTML = '<div class="card-header"><div class="card-title">Lead Pipeline</div></div>';
  const pipelineCanvas = document.createElement('canvas');
  pipelineCanvas.height = 200;
  pipelineChartCard.appendChild(pipelineCanvas);

  chartsRow.append(convChartCard, pipelineChartCard);

  // Recent activity table
  const activityCard = document.createElement('div');
  activityCard.className = 'card';
  const activityHeader = document.createElement('div');
  activityHeader.className = 'card-header';
  activityHeader.innerHTML = '<div class="card-title">Recent Leads</div>';
  const activityTableWrap = document.createElement('div');
  activityTableWrap.className = 'table-wrapper';
  activityTableWrap.style.marginTop = '12px';
  const activityTable = document.createElement('table');
  activityTable.className = 'data-table';
  activityTable.innerHTML = `
    <thead><tr><th>Name</th><th>Email</th><th>Opportunity</th><th>Date</th></tr></thead>
    <tbody id="dash-activity-tbody"><tr><td colspan="4" style="text-align:center;padding:24px;color:var(--color-text-muted)">Loading…</td></tr></tbody>
  `;
  activityTableWrap.appendChild(activityTable);
  activityCard.append(activityHeader, activityTableWrap);

  // Quick links row
  const quickLinks = document.createElement('div');
  quickLinks.className = 'grid-3';
  [
    { label: 'Add Knowledge', desc: 'Upload docs & URLs for your AI bot', href: '#/knowledge', icon: '📚' },
    { label: 'View Conversations', desc: 'Review live chat sessions', href: '#/engage', icon: '💬' },
    { label: 'View Leads', desc: 'Browse and manage captured leads', href: '#/leads', icon: '🎯' },
  ].forEach(({ label, desc, href, icon }) => {
    const link = document.createElement('a');
    link.href = href;
    link.style.textDecoration = 'none';
    const card = document.createElement('div');
    card.className = 'card';
    card.style.cursor = 'pointer';
    card.style.transition = 'box-shadow var(--transition)';
    card.innerHTML = `
      <div style="font-size:24px;margin-bottom:8px">${icon}</div>
      <div style="font-weight:600;color:var(--color-text);margin-bottom:4px">${label}</div>
      <div style="font-size:12px;color:var(--color-text-muted)">${desc}</div>
    `;
    card.addEventListener('mouseenter', () => { card.style.boxShadow = 'var(--shadow-md)'; });
    card.addEventListener('mouseleave', () => { card.style.boxShadow = ''; });
    link.appendChild(card);
    quickLinks.appendChild(link);
  });

  page.append(header, metricsGrid, chartsRow, activityCard, quickLinks);
  container.appendChild(page);

  // Load live metrics and draw charts with real data
  try {
    const sites = await apiClient.sites.list().catch(() => []);
    const siteId = Array.isArray(sites) && sites[0] ? (sites[0].siteId || sites[0].id || '') : '';

    const [visitCounts, leads, tickets, knowledgeSources, engageAnalytics] = await Promise.allSettled([
      siteId ? apiClient.visitors.visitCounts(siteId) : Promise.resolve(null),
      siteId ? apiClient.leads.list(siteId, 1, 100) : Promise.resolve([]),
      siteId ? apiClient.tickets.listTickets({ siteId, page: 1, pageSize: 100 }) : Promise.resolve([]),
      siteId ? apiClient.knowledge.listSources(siteId) : Promise.resolve([]),
      siteId ? apiClient.engage.getOpportunityAnalytics(siteId) : Promise.resolve(null),
    ]);

    const visitData = visitCounts.status === 'fulfilled' ? visitCounts.value : null;
    const leadsData = leads.status === 'fulfilled' && Array.isArray(leads.value) ? leads.value : [];
    const ticketsData = tickets.status === 'fulfilled' && Array.isArray(tickets.value) ? tickets.value : [];
    const knowledgeData = knowledgeSources.status === 'fulfilled' && Array.isArray(knowledgeSources.value) ? knowledgeSources.value : [];
    const analyticsData = engageAnalytics.status === 'fulfilled' ? engageAnalytics.value : null;

    const openTickets = ticketsData.filter((t) => {
      const s = (t.status || '').toLowerCase();
      return s === 'open' || s === 'in-progress' || s === 'inprogress';
    }).length;

    metricPlaceholders.forEach((card, i) => {
      const values = [visitData?.last30 ?? '—', leadsData.length, openTickets, knowledgeData.length];
      card.querySelector('.metric-value').textContent = values[i];
    });

    // Recent activity table
    const tbody = activityTable.querySelector('#dash-activity-tbody');
    if (leadsData.length === 0) {
      tbody.innerHTML = '<tr><td colspan="4" style="text-align:center;padding:24px;color:var(--color-text-muted)">No leads yet</td></tr>';
    } else {
      tbody.innerHTML = '';
      leadsData.slice(0, 5).forEach((lead) => {
        const tr = document.createElement('tr');
        const name = lead.fullName || lead.name || lead.displayName || '—';
        const email = lead.email || '—';
        const opp = lead.opportunityLabel || lead.opportunity || '—';
        const date = lead.createdAtUtc || lead.createdAt
          ? new Date(lead.createdAtUtc || lead.createdAt).toLocaleDateString()
          : '—';
        tr.innerHTML = `<td class="text-primary">${name}</td><td>${email}</td><td>${opp}</td><td>${date}</td>`;
        tbody.appendChild(tr);
      });
    }

    // Chart 1 — Conversations (last 7 days) from opportunitiesOverTime
    const dailyPoints = Array.isArray(analyticsData?.opportunitiesOverTime) ? analyticsData.opportunitiesOverTime : [];
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const last7 = Array.from({ length: 7 }, (_, i) => {
      const d = new Date(today);
      d.setDate(d.getDate() - (6 - i));
      return d;
    });
    const convLabels = last7.map((d) => d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' }));
    const convData = last7.map((d) => {
      const iso = d.toISOString().slice(0, 10);
      const match = dailyPoints.find((p) => p.dateUtc && String(p.dateUtc).slice(0, 10) === iso);
      return match ? match.count : 0;
    });

    // Chart 2 — Lead Pipeline grouped by opportunityLabel
    const PIPELINE_COLORS = { evaluating: '#f59e0b', deciding: '#3b82f6', won: '#10b981', lost: '#ef4444' };
    const pipeline = {};
    leadsData.forEach((lead) => {
      const label = lead.opportunityLabel || lead.opportunity || 'Unknown';
      pipeline[label] = (pipeline[label] || 0) + 1;
    });
    const pipelineEntries = Object.entries(pipeline);
    const pipelineLabels = pipelineEntries.map(([l]) => l);
    const pipelineData = pipelineEntries.map(([, c]) => c);
    const pipelineBg = pipelineLabels.map((l) => PIPELINE_COLORS[l.toLowerCase().replace(/[^a-z]/g, '')] || '#94a3b8');

    if (window.Chart) {
      requestAnimationFrame(() => {
        new window.Chart(convCanvas, {
          type: 'bar',
          data: {
            labels: convLabels,
            datasets: [{ label: 'Conversations', data: convData, backgroundColor: 'rgba(99,102,241,0.7)', borderRadius: 4 }],
          },
          options: { responsive: true, plugins: { legend: { display: false } }, scales: { y: { beginAtZero: true, ticks: { precision: 0 } } } },
        });

        if (pipelineLabels.length === 0) {
          const empty = document.createElement('div');
          empty.style.cssText = 'text-align:center;padding:32px;color:var(--color-text-muted);font-size:13px;';
          empty.textContent = 'No leads yet';
          pipelineChartCard.appendChild(empty);
        } else {
          new window.Chart(pipelineCanvas, {
            type: 'doughnut',
            data: { labels: pipelineLabels, datasets: [{ data: pipelineData, backgroundColor: pipelineBg, borderWidth: 0 }] },
            options: { responsive: true, plugins: { legend: { position: 'bottom' } } },
          });
        }
      });
    }
  } catch (error) {
    const uiError = mapApiError(error);
    if (uiError.status !== 401) {
      toast.show({ message: uiError.message, variant: 'warning' });
    }
  }
};

const routes = {
  '/': renderHomeView,
  '/login': renderLoginView,
  '/register': renderRegisterView,
  '/accept-invite': renderAcceptInviteView,
  '/dashboard': renderDashboardView,
  '/team': renderTeamView,
  '/sites': renderSitesView,
  '/install': renderInstallView,
  '/visitors': renderVisitorsView,
  '/knowledge': renderKnowledgeView,
  '/flows': renderFlowsView,
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
  profile: null,
  profileModalOpen: false,
};

const getPrimaryRole = (roles) => {
  if (!Array.isArray(roles)) {
    return 'user';
  }

  if (roles.includes('super_admin')) {
    return 'super_admin';
  }

  if (roles.includes('platform_admin')) {
    return 'admin';
  }

  if (roles.includes('admin')) {
    return 'admin';
  }

  if (roles.includes('manager')) {
    return 'manager';
  }

  return 'user';
};

const canManageUsers = (role) => role === 'super_admin' || role === 'admin' || role === 'manager';

const isAdministratorRole = (role) => role === 'super_admin' || role === 'admin';

const canInviteRole = (actorRole, targetRole) => {
  if (actorRole === 'super_admin') {
    return targetRole === 'admin' || targetRole === 'manager' || targetRole === 'user';
  }

  if (actorRole === 'admin') {
    return targetRole === 'manager' || targetRole === 'user';
  }

  if (actorRole === 'manager') {
    return targetRole === 'user';
  }

  return false;
};

const canChangeRole = (actorRole, targetRole) => {
  if (targetRole === 'super_admin') {
    return false;
  }

  if (actorRole === 'super_admin') {
    return targetRole === 'admin' || targetRole === 'manager' || targetRole === 'user';
  }

  if (actorRole === 'admin') {
    return targetRole === 'manager' || targetRole === 'user';
  }

  if (actorRole === 'manager') {
    return targetRole === 'user';
  }

  return false;
};

const canRemoveRole = (actorRole, targetRole) => canChangeRole(actorRole, targetRole);

const hasPlatformAccess = () =>
  Array.isArray(authState.roles)
  && (authState.roles.includes('super_admin') || authState.roles.includes('platform_admin'));

const loadAuthProfile = async () => {
  if (authState.loading) {
    return;
  }

  authState.loading = true;
  try {
    const me = await apiClient.auth.me();
    authState.roles = Array.isArray(me.roles) ? me.roles : [];
    authState.profile = me;
  } catch (error) {
    authState.roles = [];
    authState.profile = null;
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
    '/team',
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

  if ((route === '/login' || route === '/register' || route === '/accept-invite') && isAuthenticated) {
    window.location.hash = '#/dashboard';
    return;
  }

  if (isAuthenticated && !authState.loaded) {
    loadAuthProfile();
  }

  if (!isAuthenticated) {
    authState.loaded = false;
    authState.roles = [];
    authState.profile = null;
    authState.profileModalOpen = false;
  }

  const isPlatformRoute = route === '/platform-admin' || route === '/platform-admin/tenant/:tenantId';
  const isTeamRoute = route === '/team';
  if (isAuthenticated && isPlatformRoute && !authState.loaded) {
    app.innerHTML = '';
    const authenticatedShell = createAuthenticatedShell({
      route,
      canAccessPlatformAdmin: false,
      canAccessTeam: false,
      firstName: getFirstName(authState.profile?.displayName),
      onOpenProfile: () => {
        authState.profileModalOpen = true;
        renderApp();
      },
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

  if (isAuthenticated && isTeamRoute && authState.loaded && !canManageUsers(getPrimaryRole(authState.roles))) {
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
      canAccessTeam: canManageUsers(getPrimaryRole(authState.roles)),
      firstName: getFirstName(authState.profile?.displayName),
      onOpenProfile: () => {
        authState.profileModalOpen = true;
        renderApp();
      },
      onLogout: () => {
        clearToken();
        window.location.hash = '#/login';
      },
    });
    main = authenticatedShell.main;
    app.append(authenticatedShell.shell, authenticatedShell.overlay);
  } else {
    const navbar = createNavbar({ isAuthenticated, canAccessPlatformAdmin: false, canAccessTeam: false });
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

  view(main, {
    apiClient,
    toast,
    query,
    params,
    currentUser: authState.profile,
    capabilities: {
      canManageUsers,
      canInviteRole,
      canChangeRole,
      canRemoveRole,
    },
  });

  if (isAuthenticated && authState.profileModalOpen && authState.profile) {
    const primaryRole = getPrimaryRole(authState.roles);
    const canManageTeam = canManageUsers(primaryRole);
    const isAdministrator = isAdministratorRole(primaryRole);
    const inviteRoles = ['admin', 'manager', 'user'].filter((role) => canInviteRole(primaryRole, role));
    let modal;
    const closeModal = () => {
      if (modal) {
        modal.cleanup();
      }
      authState.profileModalOpen = false;
      renderApp();
    };

    modal = createProfileModal({
      profile: authState.profile,
      currentRole: primaryRole,
      canManageTeam,
      isAdministrator,
      inviteRoles,
      onInvite: async ({ email, role }) => {
        await apiClient.auth.createInvite({ email, role });
      },
      onManageTeam: () => {
        authState.profileModalOpen = false;
        window.location.hash = '#/team';
      },
      onClose: closeModal,
      onSave: async ({ displayName, organizationName }) => {
        await apiClient.auth.updateProfile({ displayName, organizationName });
        const refreshed = await apiClient.auth.me();
        authState.profile = refreshed;
        authState.roles = Array.isArray(refreshed.roles) ? refreshed.roles : [];
      },
    });

    app.appendChild(modal.overlay);
  }
};

window.addEventListener('hashchange', renderApp);
renderApp();
