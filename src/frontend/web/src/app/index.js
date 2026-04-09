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
import { renderDashboardView } from '../pages/dashboard.js';
import { renderPlatformAdminTenantDetailView, renderPlatformAdminView } from '../pages/platformAdmin.js';
import { renderMultiSiteAnalyticsView } from '../pages/multiSiteAnalytics.js';
import { showBotWizard } from '../pages/botWizard.js';

const app = document.getElementById('app');
const toast = createToastManager();
const apiClient = createApiClient();

const setAppLayout = () => {
  if (!app) return;
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
  if (!value) return 'Email is required.';
  const atIndex = value.indexOf('@');
  const dotIndex = value.lastIndexOf('.');
  if (atIndex < 1 || dotIndex < atIndex + 2 || dotIndex === value.length - 1)
    return 'Enter a valid email address.';
  return '';
};

const validatePassword = (value) => {
  if (!value) return 'Password is required.';
  if (value.length < 10) return 'Password must be at least 10 characters.';
  if (!/[A-Za-z]/.test(value) || !/[0-9]/.test(value))
    return 'Password must include at least one letter and one number.';
  return '';
};

const normalizeFieldKey = (key) => key.toLowerCase().replace(/\s+/g, '');

const applyFieldErrors = (errors, fields) => {
  if (!errors || typeof errors !== 'object') return false;
  let applied = false;
  Object.entries(errors).forEach(([key, messages]) => {
    const normalizedKey = normalizeFieldKey(key);
    const match = Object.keys(fields).find((fieldKey) => normalizeFieldKey(fieldKey) === normalizedKey);
    if (match) {
      const message = Array.isArray(messages) ? messages.join(' ') : String(messages);
      fields[match].error.textContent = message;
      applied = true;
    }
  });
  return applied;
};

// ── Unauthenticated navbar (login/register pages) ─────────────────────────────

const createNavLink = ({ label, href }) => {
  const link = document.createElement('a');
  link.textContent = label;
  link.href = href;
  link.style.textDecoration = 'none';
  link.style.color = '#1e293b';
  link.style.fontWeight = '500';
  link.style.padding = '6px 10px';
  link.style.borderRadius = '6px';
  link.addEventListener('mouseover', () => { link.style.background = '#e2e8f0'; });
  link.addEventListener('mouseout',  () => { link.style.background = 'transparent'; });
  return link;
};

const createNavbar = ({ isAuthenticated, canAccessPlatformAdmin, canAccessTeam }) => {
  const nav = document.createElement('nav');
  nav.style.cssText = 'display:flex;align-items:center;justify-content:space-between;padding:16px 24px;background:#ffffff;border-bottom:1px solid #e2e8f0';

  const brandWrap = document.createElement('div');
  brandWrap.style.cssText = 'display:flex;align-items:center';
  const brandText = document.createElement('div');
  brandText.textContent = 'Hven';
  brandText.style.cssText = 'font-weight:700;font-size:18px;color:#6366f1';
  brandWrap.appendChild(brandText);

  const links = document.createElement('div');
  links.style.cssText = 'display:flex;gap:8px;flex-wrap:wrap;justify-content:flex-end';
  links.appendChild(createNavLink({ label: 'Home', href: '#/' }));

  if (!isAuthenticated) {
    links.appendChild(createNavLink({ label: 'Login', href: '#/login' }));
    links.appendChild(createNavLink({ label: 'Register', href: '#/register' }));
  } else {
    if (canAccessPlatformAdmin) links.appendChild(createNavLink({ label: 'Platform Admin', href: '#/platform-admin' }));
    const logoutButton = document.createElement('button');
    logoutButton.type = 'button';
    logoutButton.textContent = 'Logout';
    logoutButton.style.cssText = 'padding:6px 10px;border-radius:6px;border:1px solid #e2e8f0;background:#fff;cursor:pointer';
    logoutButton.addEventListener('click', () => { clearToken(); window.location.hash = '#/login'; });
    links.appendChild(logoutButton);
  }

  nav.append(brandWrap, links);
  return nav;
};

// ── Sidebar nav link ───────────────────────────────────────────────────────────

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
    link.addEventListener('mouseover', () => { link.style.background = 'rgba(255,255,255,0.06)'; link.style.color = 'rgba(255,255,255,0.85)'; });
    link.addEventListener('mouseout',  () => { link.style.background = 'transparent'; link.style.color = 'rgba(255,255,255,0.55)'; });
  }
  return link;
};

const getFirstName = (displayName) => {
  const trimmed = (displayName || '').trim();
  if (!trimmed) return 'Account';
  const [first] = trimmed.split(/\s+/);
  return first || 'Account';
};

// ── Profile modal (revamped) ───────────────────────────────────────────────────

const createProfileModal = ({
  profile, currentRole, canManageTeam, isAdministrator,
  inviteRoles, onInvite, onManageTeam, onClose, onSave,
}) => {
  // Inject styles once
  if (!document.getElementById('_pm_css')) {
    const s = document.createElement('style');
    s.id = '_pm_css';
    s.textContent = `
.pm-overlay{position:fixed;inset:0;background:rgba(15,23,42,.55);z-index:100;display:flex;align-items:center;justify-content:center;padding:16px;backdrop-filter:blur(4px)}
.pm-card{background:#fff;border-radius:16px;width:100%;max-width:480px;max-height:90vh;overflow-y:auto;box-shadow:0 24px 64px rgba(15,23,42,.22);display:flex;flex-direction:column}
.pm-hd{background:linear-gradient(135deg,#0f172a,#1e293b);padding:22px 24px 18px;border-radius:16px 16px 0 0;position:relative;overflow:hidden;flex-shrink:0}
.pm-hd::after{content:'';position:absolute;top:-20px;right:-20px;width:120px;height:120px;background:radial-gradient(circle,rgba(99,102,241,.25) 0%,transparent 70%);pointer-events:none}
.pm-hd-name{font-size:17px;font-weight:700;color:#f8fafc;margin-bottom:3px}
.pm-hd-email{font-size:12px;color:#64748b;font-family:monospace}
.pm-hd-role{display:inline-flex;align-items:center;gap:5px;margin-top:10px;padding:4px 10px;border-radius:999px;font-size:11px;font-weight:700;background:rgba(99,102,241,.2);color:#a5b4fc;border:1px solid rgba(99,102,241,.3)}
.pm-x{position:absolute;top:14px;right:14px;width:28px;height:28px;border-radius:50%;border:1px solid rgba(255,255,255,.15);background:rgba(255,255,255,.08);color:rgba(255,255,255,.6);cursor:pointer;display:flex;align-items:center;justify-content:center;font-size:14px;transition:background .12s;z-index:1}
.pm-x:hover{background:rgba(255,255,255,.18);color:#fff}
.pm-body{padding:20px 24px;display:flex;flex-direction:column;gap:14px}
.pm-sec{border:1px solid #e2e8f0;border-radius:10px;padding:14px;display:flex;flex-direction:column;gap:10px}
.pm-sec-title{font-size:10.5px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:#94a3b8;margin-bottom:2px}
.pm-lbl{font-size:10.5px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:#94a3b8;margin-bottom:3px}
.pm-inp{font-family:inherit;font-size:13px;color:#1e293b;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:8px 11px;outline:none;width:100%;box-sizing:border-box;transition:border .14s}
.pm-inp:focus{border-color:#6366f1;background:#fff;box-shadow:0 0 0 3px rgba(99,102,241,.1)}
.pm-inp:disabled{opacity:.55;cursor:not-allowed}
.pm-err{font-size:11.5px;color:#dc2626;min-height:16px}
.pm-sel{font-family:inherit;font-size:13px;color:#1e293b;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:8px 11px;outline:none;width:100%;box-sizing:border-box}
.pm-row{display:flex;gap:8px;justify-content:flex-end;padding-top:4px;flex-wrap:wrap}
.pm-btn{font-family:inherit;font-size:13px;font-weight:600;padding:8px 18px;border-radius:8px;border:none;cursor:pointer;transition:all .14s;white-space:nowrap}
.pm-btn-p{background:#6366f1;color:#fff}.pm-btn-p:hover:not(:disabled){background:#4f46e5}
.pm-btn-p:disabled{opacity:.5;cursor:not-allowed}
.pm-btn-o{background:#fff;color:#64748b;border:1px solid #e2e8f0}.pm-btn-o:hover{background:#f8fafc;color:#1e293b}
.pm-btn-s{background:#eef2ff;color:#6366f1;border:none}.pm-btn-s:hover{background:#e0e7ff}
    `;
    document.head.appendChild(s);
  }

  const overlay = document.createElement('div');
  overlay.className = 'pm-overlay';
  overlay.setAttribute('role', 'dialog');
  overlay.setAttribute('aria-modal', 'true');

  const card = document.createElement('div');
  card.className = 'pm-card';

  // Header
  const hd = document.createElement('div');
  hd.className = 'pm-hd';

  const xBtn = document.createElement('button');
  xBtn.type = 'button';
  xBtn.className = 'pm-x';
  xBtn.textContent = '✕';
  xBtn.addEventListener('click', onClose);

  const hdName = document.createElement('div');
  hdName.className = 'pm-hd-name';
  hdName.textContent = profile?.displayName || 'Account';

  const hdEmail = document.createElement('div');
  hdEmail.className = 'pm-hd-email';
  hdEmail.textContent = profile?.email || '';

  const roleIcon = currentRole === 'super_admin' || currentRole === 'admin' ? '🛡' : currentRole === 'manager' ? '📋' : '👤';
  const roleLabel = currentRole === 'super_admin' ? 'Super Admin' : currentRole === 'admin' ? 'Admin' : currentRole === 'manager' ? 'Manager' : 'Viewer';
  const hdRole = document.createElement('div');
  hdRole.className = 'pm-hd-role';
  hdRole.textContent = `${roleIcon} ${roleLabel}`;

  hd.append(xBtn, hdName, hdEmail, hdRole);
  card.appendChild(hd);

  // Body
  const body = document.createElement('div');
  body.className = 'pm-body';

  // Helper: create a labeled input field
  const mkField = (labelText, type, value, disabled = false) => {
    const wrap = document.createElement('div');
    const lbl = document.createElement('div');
    lbl.className = 'pm-lbl';
    lbl.textContent = labelText;
    const inp = document.createElement('input');
    inp.type = type;
    inp.className = 'pm-inp';
    inp.value = value || '';
    if (disabled) inp.disabled = true;
    const err = document.createElement('div');
    err.className = 'pm-err';
    wrap.append(lbl, inp, err);
    return { wrap, input: inp, error: err };
  };

  // Profile section
  const profileSec = document.createElement('div');
  profileSec.className = 'pm-sec';
  const profileTitle = document.createElement('div');
  profileTitle.className = 'pm-sec-title';
  profileTitle.textContent = 'Profile Information';
  const displayNameField    = mkField('Display Name', 'text', profile?.displayName);
  const emailField          = mkField('Email', 'email', profile?.email, true);
  const organizationField   = mkField('Organization', 'text', profile?.organizationName, !isAdministrator);
  profileSec.append(profileTitle, displayNameField.wrap, emailField.wrap, organizationField.wrap);
  body.appendChild(profileSec);

  // Invite section (managers and above)
  if (canManageTeam) {
    const availableInviteRoles = Array.isArray(inviteRoles) ? inviteRoles : [];
    const defaultInviteRole = availableInviteRoles[0] || 'user';

    const inviteSec = document.createElement('div');
    inviteSec.className = 'pm-sec';
    const inviteTitle = document.createElement('div');
    inviteTitle.className = 'pm-sec-title';
    inviteTitle.textContent = 'Invite Team Member';

    const inviteEmailField = mkField('Email Address', 'email', '');

    const roleLbl = document.createElement('div');
    roleLbl.className = 'pm-lbl';
    roleLbl.textContent = 'Role';
    const roleSelect = document.createElement('select');
    roleSelect.className = 'pm-sel';
    availableInviteRoles.forEach((role) => {
      const opt = document.createElement('option');
      opt.value = role;
      opt.textContent = role === 'admin' ? 'Admin' : role === 'manager' ? 'Manager' : 'Viewer';
      roleSelect.appendChild(opt);
    });
    roleSelect.value = defaultInviteRole;
    const roleErr = document.createElement('div');
    roleErr.className = 'pm-err';

    const inviteBtn = document.createElement('button');
    inviteBtn.type = 'button';
    inviteBtn.className = 'pm-btn pm-btn-s';
    inviteBtn.style.alignSelf = 'flex-start';
    inviteBtn.textContent = '✉ Send Invite';
    if (!availableInviteRoles.length) { inviteBtn.disabled = true; roleSelect.disabled = true; }

    inviteBtn.addEventListener('click', async () => {
      inviteEmailField.error.textContent = '';
      roleErr.textContent = '';
      const email = inviteEmailField.input.value.trim();
      const role  = roleSelect.value;
      const emailError = validateEmail(email);
      if (emailError) { inviteEmailField.error.textContent = emailError; return; }
      if (!availableInviteRoles.includes(role)) { roleErr.textContent = 'You cannot invite this role.'; return; }
      inviteBtn.disabled = true; inviteBtn.textContent = '⏳ Sending…';
      try {
        await onInvite({ email, role });
        inviteEmailField.input.value = '';
        roleSelect.value = defaultInviteRole;
        toast.show({ message: 'Invitation sent.', variant: 'success' });
      } catch (error) {
        const uiError = mapApiError(error);
        const applied = applyFieldErrors(uiError.details?.errors, { email: inviteEmailField, role: { error: roleErr } });
        toast.show({ message: applied ? 'Please review the highlighted errors.' : uiError.message, variant: 'danger' });
      } finally {
        inviteBtn.disabled = false; inviteBtn.textContent = '✉ Send Invite';
      }
    });

    const manageTeamBtn = document.createElement('button');
    manageTeamBtn.type = 'button';
    manageTeamBtn.className = 'pm-btn pm-btn-o';
    manageTeamBtn.style.alignSelf = 'flex-start';
    manageTeamBtn.textContent = '👥 Manage Team →';
    manageTeamBtn.addEventListener('click', onManageTeam);

    inviteSec.append(inviteTitle, inviteEmailField.wrap, roleLbl, roleSelect, roleErr, inviteBtn, manageTeamBtn);
    body.appendChild(inviteSec);
  }

  // Action row
  const actionRow = document.createElement('div');
  actionRow.className = 'pm-row';

  const cancelBtn = document.createElement('button');
  cancelBtn.type = 'button';
  cancelBtn.className = 'pm-btn pm-btn-o';
  cancelBtn.textContent = 'Cancel';
  cancelBtn.addEventListener('click', onClose);

  const saveBtn = document.createElement('button');
  saveBtn.type = 'button';
  saveBtn.className = 'pm-btn pm-btn-p';
  saveBtn.textContent = 'Save Changes';
  saveBtn.addEventListener('click', async () => {
    displayNameField.error.textContent = '';
    organizationField.error.textContent = '';
    const displayName = displayNameField.input.value.trim();
    const organizationName = organizationField.input.value.trim();
    if (!displayName) { displayNameField.error.textContent = 'Display name is required.'; return; }
    saveBtn.disabled = true; saveBtn.textContent = '⏳ Saving…';
    try {
      await onSave({ displayName, organizationName: isAdministrator ? organizationName : undefined });
      onClose();
    } catch (error) {
      const uiError = mapApiError(error);
      const applied = applyFieldErrors(uiError.details?.errors, { displayName: displayNameField, organizationName: organizationField });
      toast.show({ message: applied ? 'Please review the highlighted errors.' : uiError.message, variant: 'danger' });
    } finally {
      saveBtn.disabled = false; saveBtn.textContent = 'Save Changes';
    }
  });

  actionRow.append(cancelBtn, saveBtn);
  body.appendChild(actionRow);
  card.appendChild(body);
  overlay.appendChild(card);

  overlay.addEventListener('click', (e) => { if (e.target === overlay) onClose(); });
  card.addEventListener('click', (e) => e.stopPropagation());
  const onKeyDown = (e) => { if (e.key === 'Escape') onClose(); };
  window.addEventListener('keydown', onKeyDown);

  return { overlay, cleanup: () => window.removeEventListener('keydown', onKeyDown) };
};

// ── Theme helpers ──────────────────────────────────────────────────────────────
const getTheme = () => localStorage.getItem('hven_theme') || 'light';
const applyTheme = (theme) => {
  document.documentElement.setAttribute('data-theme', theme);
  localStorage.setItem('hven_theme', theme);
};

// ── Authenticated shell (sidebar + topbar) ─────────────────────────────────────

const createAuthenticatedShell = ({ route, canAccessPlatformAdmin, canAccessTeam, onLogout, onOpenProfile, firstName }) => {
  const shell = document.createElement('div');
  shell.style.cssText = 'display:flex;min-height:100vh;width:100%';

  const sidebar = document.createElement('aside');
  sidebar.style.cssText = 'width:240px;background:#0f172a;box-sizing:border-box;display:flex;flex-direction:column;flex-shrink:0';

  // ── Brand: logo + tagline ────────────────────────────────────────────────────
  const brand = document.createElement('div');
  brand.style.cssText = 'padding:16px 16px 14px;border-bottom:1px solid rgba(255,255,255,0.06);display:flex;flex-direction:column;gap:5px';

  const sidebarBrandText = document.createElement('div');
  sidebarBrandText.textContent = 'Hven';
  sidebarBrandText.style.cssText = 'font-weight:700;font-size:17px;color:#6366f1';

  const tagline = document.createElement('div');
  tagline.textContent = "Know who\u2019s visiting. Engage what matters.";
  tagline.style.cssText = 'font-size:9.5px;color:rgba(255,255,255,0.28);line-height:1.45;letter-spacing:0.01em';

  brand.append(sidebarBrandText, tagline);

  // ── Nav sections ─────────────────────────────────────────────────────────────
  const nav = document.createElement('nav');
  nav.style.cssText = 'flex:1;padding:8px 12px;overflow-y:auto;display:flex;flex-direction:column';

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

  addNavSection('START', [
    { label: 'Getting Started', href: '#/' },
  ]);

  addNavSection('MAIN', [
    { label: 'Dashboard', href: '#/dashboard' },
    { label: 'Sites',     href: '#/sites' },
  ]);

  addNavSection('INTELLIGENCE', [
    { label: 'Analytics',    href: '#/analytics' },
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

  // ── User area — replaces old avatar + logout with a popup menu ───────────────
  const userArea = document.createElement('div');
  userArea.style.cssText = 'padding:10px 12px;border-top:1px solid rgba(255,255,255,0.06);position:relative';

  // Popup (appears above the user row)
  const popup = document.createElement('div');
  popup.style.cssText = `
    position:absolute;bottom:calc(100% + 6px);left:10px;right:10px;
    background:#1e293b;border:1px solid rgba(255,255,255,0.1);
    border-radius:10px;padding:6px;box-shadow:0 -8px 24px rgba(0,0,0,.35);
    display:none;z-index:50;
  `;

  const mkPopupBtn = (icon, label, onClick) => {
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.style.cssText = `
      width:100%;display:flex;align-items:center;gap:9px;padding:8px 10px;
      border:none;border-radius:7px;background:transparent;cursor:pointer;
      color:rgba(255,255,255,0.75);font-size:13px;font-weight:500;text-align:left;
      transition:background 110ms;
    `;
    const iconEl = document.createElement('span');
    iconEl.textContent = icon;
    iconEl.style.fontSize = '15px';
    const labelEl = document.createElement('span');
    labelEl.textContent = label;
    btn.append(iconEl, labelEl);
    btn.addEventListener('mouseover', () => { btn.style.background = 'rgba(255,255,255,0.08)'; });
    btn.addEventListener('mouseout',  () => { btn.style.background = 'transparent'; });
    btn.addEventListener('click', () => {
      popup.style.display = 'none';
      chevron.style.transform = 'rotate(180deg)';
      onClick();
    });
    return btn;
  };

  popup.append(
    mkPopupBtn('👤', 'Profile', onOpenProfile),
    mkPopupBtn('🚪', 'Sign out', onLogout),
  );

  // User row trigger
  const userRow = document.createElement('button');
  userRow.type = 'button';
  userRow.style.cssText = `
    width:100%;display:flex;align-items:center;gap:10px;padding:8px 10px;
    border:none;border-radius:8px;background:transparent;cursor:pointer;
    transition:background 120ms;
  `;
  userRow.addEventListener('mouseover', () => { userRow.style.background = 'rgba(255,255,255,0.06)'; });
  userRow.addEventListener('mouseout',  () => { userRow.style.background = 'transparent'; });

  const avatar = document.createElement('div');
  avatar.style.cssText = `
    width:30px;height:30px;border-radius:50%;background:#6366f1;color:#fff;
    display:flex;align-items:center;justify-content:center;
    font-size:12px;font-weight:700;flex-shrink:0;
  `;
  avatar.textContent = (firstName || '?')[0].toUpperCase();

  const userLabel = document.createElement('div');
  userLabel.style.cssText = `
    flex:1;min-width:0;font-size:13px;font-weight:500;
    color:rgba(255,255,255,0.8);overflow:hidden;text-overflow:ellipsis;white-space:nowrap;text-align:left;
  `;
  userLabel.textContent = firstName;

  const chevron = document.createElement('div');
  chevron.textContent = '⌃';
  chevron.style.cssText = 'font-size:11px;color:rgba(255,255,255,0.3);transform:rotate(180deg);transition:transform 160ms;flex-shrink:0';

  userRow.append(avatar, userLabel, chevron);

  let popupOpen = false;
  userRow.addEventListener('click', (e) => {
    e.stopPropagation();
    popupOpen = !popupOpen;
    popup.style.display = popupOpen ? 'block' : 'none';
    chevron.style.transform = popupOpen ? 'rotate(0deg)' : 'rotate(180deg)';
  });

  document.addEventListener('click', () => {
    if (popupOpen) {
      popupOpen = false;
      popup.style.display = 'none';
      chevron.style.transform = 'rotate(180deg)';
    }
  });

  const themeBtn = document.createElement('button');
  themeBtn.type = 'button';
  themeBtn.style.cssText = 'width:100%;display:flex;align-items:center;gap:8px;padding:8px 12px;border-radius:8px;font-size:12px;color:#64748b;background:none;border:1px solid rgba(255,255,255,0.1);cursor:pointer;margin-bottom:8px;font-family:inherit';
  themeBtn.textContent = getTheme() === 'dark' ? '☀ Light mode' : '☽ Dark mode';
  themeBtn.addEventListener('click', (e) => {
    e.stopPropagation();
    const next = getTheme() === 'dark' ? 'light' : 'dark';
    applyTheme(next);
    themeBtn.textContent = next === 'dark' ? '☀ Light mode' : '☽ Dark mode';
  });

  userArea.append(popup, themeBtn, userRow);
  sidebar.append(brand, nav, userArea);

  // ── Content area ──────────────────────────────────────────────────────────────
  const contentWrap = document.createElement('div');
  contentWrap.style.cssText = 'display:flex;flex-direction:column;flex:1;min-width:0';

  const topbar = document.createElement('div');
  topbar.style.cssText = 'display:flex;align-items:center;padding:11px 16px;border-bottom:1px solid #e2e8f0;background:#ffffff';

  const toggleButton = document.createElement('button');
  toggleButton.type = 'button';
  toggleButton.textContent = '☰';
  toggleButton.setAttribute('aria-label', 'Toggle navigation');
  toggleButton.style.cssText = 'border:1px solid #e2e8f0;background:#fff;border-radius:8px;padding:6px 10px;cursor:pointer;margin-right:10px';

  const topbarBrand = document.createElement('div');
  topbarBrand.textContent = 'Hven';
  topbarBrand.style.cssText = 'font-weight:700;color:#0f172a;font-size:15px';

  topbar.append(toggleButton, topbarBrand);

  const main = createMain();

  // Mobile sidebar overlay
  const overlay = document.createElement('button');
  overlay.type = 'button';
  overlay.setAttribute('aria-label', 'Close navigation');
  overlay.style.cssText = 'position:fixed;inset:0;background:rgba(15,23,42,.35);border:0;display:none;z-index:39';

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
    if (!mobileQuery.matches) return;
    sidebar.style.transform = 'translateX(-105%)';
    overlay.style.display = 'none';
  };
  const openSidebar = () => {
    if (!mobileQuery.matches) return;
    sidebar.style.transform = 'translateX(0)';
    overlay.style.display = 'block';
  };

  toggleButton.addEventListener('click', () => {
    sidebar.style.transform === 'translateX(0)' ? closeSidebar() : openSidebar();
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
  main.style.cssText = 'flex:1;padding:32px 24px;display:flex;justify-content:center;align-items:flex-start';
  return main;
};

// ── Page views ─────────────────────────────────────────────────────────────────

const renderHomeView = (container) => {
  // ── Layout ──────────────────────────────────────────────────────────────────
  const layout = document.createElement('div');
  layout.style.cssText = 'display:flex;gap:32px;align-items:flex-start;width:100%;max-width:1000px';

  // ── Progress sidebar ─────────────────────────────────────────────────────────
  const sidebar = document.createElement('div');
  sidebar.style.cssText = [
    'width:220px;flex-shrink:0;position:sticky;top:80px',
    'background:#f8fafc;border:1px solid #e4e7f0;border-radius:12px',
    'padding:20px 16px',
  ].join(';');

  const sidebarTitle = document.createElement('div');
  sidebarTitle.textContent = 'Getting Started';
  sidebarTitle.style.cssText = 'font-size:11px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;color:#94a3b8;margin-bottom:14px;padding:0 4px';
  sidebar.appendChild(sidebarTitle);

  const stepMeta = [
    { id: 'step-1', label: 'Welcome to Hven' },
    { id: 'step-2', label: 'Create your account' },
    { id: 'step-3', label: 'Add your first site' },
    { id: 'step-4', label: 'Install the tracking snippet' },
    { id: 'step-5', label: 'Index your Knowledge Base' },
    { id: 'step-6', label: 'Configure your Engage bot' },
    { id: 'step-7', label: 'Set up Flows automation' },
    { id: 'step-8', label: 'Monitor Visitors & Leads' },
  ];

  let completedSteps = new Set(['step-2']);
  let lastActiveIndex = 0;

  const navItems = [];
  stepMeta.forEach((s, i) => {
    const item = document.createElement('div');
    item.dataset.target = s.id;
    item.style.cssText = 'display:flex;align-items:center;gap:8px;padding:8px 12px;border-radius:8px;font-size:13px;cursor:pointer;color:#64748b;border-left:3px solid transparent;transition:all .15s;margin-bottom:2px';
    item.innerHTML = `<span style="display:inline-flex;align-items:center;justify-content:center;width:18px;height:18px;border-radius:50%;background:#e2e8f0;color:#64748b;font-size:10px;font-weight:700;flex-shrink:0">${i + 1}</span><span style="line-height:1.35">${s.label}</span>`;
    item.addEventListener('click', () => {
      const target = document.getElementById(s.id);
      if (target) target.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
    sidebar.appendChild(item);
    navItems.push(item);
  });

  function setActiveNav(index) {
    lastActiveIndex = index;
    navItems.forEach((item, i) => {
      const isDone = completedSteps.has(stepMeta[i].id);
      const circle = item.querySelector('span');
      if (i === index) {
        item.style.cssText = 'display:flex;align-items:center;gap:8px;padding:8px 12px;border-radius:8px;font-size:13px;cursor:pointer;color:#6366f1;font-weight:600;background:#eef2ff;border-left:3px solid #6366f1;margin-bottom:2px';
        if (circle) {
          if (isDone) {
            circle.textContent = '✓';
            circle.style.cssText = 'display:inline-flex;align-items:center;justify-content:center;width:18px;height:18px;border-radius:50%;background:#dcfce7;color:#16a34a;font-size:10px;font-weight:700;flex-shrink:0';
          } else {
            circle.textContent = String(i + 1);
            circle.style.cssText = 'display:inline-flex;align-items:center;justify-content:center;width:18px;height:18px;border-radius:50%;background:#c7d2fe;color:#4338ca;font-size:10px;font-weight:700;flex-shrink:0';
          }
        }
      } else if (isDone) {
        item.style.cssText = 'display:flex;align-items:center;gap:8px;padding:8px 12px;border-radius:8px;font-size:13px;cursor:pointer;color:#16a34a;border-left:3px solid transparent;margin-bottom:2px';
        if (circle) { circle.textContent = '✓'; circle.style.cssText = 'display:inline-flex;align-items:center;justify-content:center;width:18px;height:18px;border-radius:50%;background:#dcfce7;color:#16a34a;font-size:10px;font-weight:700;flex-shrink:0'; }
      } else {
        item.style.cssText = 'display:flex;align-items:center;gap:8px;padding:8px 12px;border-radius:8px;font-size:13px;cursor:pointer;color:#64748b;border-left:3px solid transparent;margin-bottom:2px';
        if (circle) { circle.textContent = String(i + 1); circle.style.cssText = 'display:inline-flex;align-items:center;justify-content:center;width:18px;height:18px;border-radius:50%;background:#e2e8f0;color:#64748b;font-size:10px;font-weight:700;flex-shrink:0'; }
      }
    });
  }
  setActiveNav(0);

  const refreshProgressBtn = document.createElement('button');
  refreshProgressBtn.textContent = '↻ Refresh progress';
  refreshProgressBtn.style.cssText = 'margin-top:14px;width:100%;background:none;border:1px solid #e2e8f0;border-radius:7px;padding:7px 10px;font-size:11.5px;color:#64748b;cursor:pointer;transition:all .15s;font-family:inherit';
  refreshProgressBtn.addEventListener('mouseenter', () => { refreshProgressBtn.style.background = '#f1f5f9'; });
  refreshProgressBtn.addEventListener('mouseleave', () => { refreshProgressBtn.style.background = 'none'; });
  refreshProgressBtn.addEventListener('click', () => {
    refreshProgressBtn.textContent = '⏳ Checking…';
    refreshProgressBtn.disabled = true;
    runProgressCheck(true).finally(() => { refreshProgressBtn.textContent = '↻ Refresh progress'; refreshProgressBtn.disabled = false; });
  });
  sidebar.appendChild(refreshProgressBtn);

  // ── Content area ─────────────────────────────────────────────────────────────
  const content = document.createElement('div');
  content.style.cssText = 'flex:1;min-width:0;display:flex;flex-direction:column;gap:24px';

  // Helper: action button
  const mkBtn = (label, href) => {
    const a = document.createElement('a');
    a.href = href; a.textContent = label;
    a.style.cssText = 'display:inline-block;margin-top:8px;padding:8px 16px;background:#0f172a;color:#fff;border-radius:6px;font-size:13px;font-weight:500;text-decoration:none;width:fit-content';
    return a;
  };

  // Helper: code block
  const mkCode = (text) => {
    const pre = document.createElement('pre');
    pre.style.cssText = 'background:#0f172a;border-radius:8px;padding:12px 14px;font-size:12px;color:#a5f3fc;overflow-x:auto;margin:0;white-space:pre-wrap;word-break:break-all;font-family:ui-monospace,Consolas,monospace';
    pre.textContent = text;
    return pre;
  };

  // Helper: section card
  const mkSection = (id, stepNum, title, buildBody) => {
    const card = document.createElement('div');
    card.id = id;
    card.style.cssText = 'background:#fff;border:1px solid #e4e7f0;border-radius:12px;padding:24px;scroll-margin-top:80px';

    const header = document.createElement('div');
    header.style.cssText = 'display:flex;align-items:center;gap:10px;margin-bottom:14px';
    const badge = document.createElement('span');
    badge.textContent = stepNum;
    badge.style.cssText = 'display:inline-flex;align-items:center;justify-content:center;width:24px;height:24px;border-radius:50%;background:#0f172a;color:#fff;font-size:11px;font-weight:700;flex-shrink:0';
    const titleEl = document.createElement('div');
    titleEl.textContent = title;
    titleEl.style.cssText = 'font-size:15px;font-weight:600;color:#0f172a';
    header.appendChild(badge); header.appendChild(titleEl);
    card.appendChild(header);

    const body = document.createElement('div');
    body.style.cssText = 'display:flex;flex-direction:column;gap:10px';
    buildBody(body);
    card.appendChild(body);

    return card;
  };

  const mkDesc = (text) => {
    const d = document.createElement('div');
    d.style.cssText = 'font-size:13.5px;color:#475569;line-height:1.65';
    d.textContent = text;
    return d;
  };

  const mkNote = (text) => {
    const n = document.createElement('div');
    n.style.cssText = 'font-size:12px;color:#94a3b8;line-height:1.5;margin-top:4px';
    n.textContent = text;
    return n;
  };

  // ── SECTION 1: Welcome ────────────────────────────────────────────────────────
  content.appendChild(mkSection('step-1', '1', 'Welcome to Hven', (body) => {
    body.appendChild(mkDesc('Hven is a visitor intelligence and AI engagement platform. It identifies anonymous website visitors, enriches them with company and intent data, and deploys an AI bot trained on your content — so you convert traffic while it\'s hot.'));
  }));

  // ── SECTION 2: Create account ─────────────────────────────────────────────────
  content.appendChild(mkSection('step-2', '2', 'Create your account', (body) => {
    body.appendChild(mkDesc('You\'re already here. Your account gives you access to one workspace where you can manage sites, knowledge, engage bots, leads, and automation flows.'));
    const pill = document.createElement('span');
    pill.textContent = '✓  Account created';
    pill.style.cssText = 'display:inline-flex;align-items:center;gap:6px;background:#dcfce7;color:#16a34a;font-size:12px;font-weight:600;padding:5px 12px;border-radius:100px;margin-top:6px;width:fit-content';
    body.appendChild(pill);
  }));

  // ── SECTION 3: Add site ───────────────────────────────────────────────────────
  content.appendChild(mkSection('step-3', '3', 'Add your first site', (body) => {
    body.appendChild(mkDesc('A site represents one website you want to track. Go to Sites in the sidebar, click Add Site, and enter your domain.'));
    body.appendChild(mkBtn('Go to Sites →', '#/sites'));
    body.appendChild(mkNote('Each site gets a unique tracker snippet. You can add multiple sites if you are on Growth plan or above.'));
  }));

  // ── SECTION 4: Install snippet ────────────────────────────────────────────────
  content.appendChild(mkSection('step-4', '4', 'Install the tracking snippet', (body) => {
    body.appendChild(mkDesc('Once you have a site, go to Sites → click the Keys button on your site → find the Tracker Snippet section. Copy the snippet for your platform (HTML, WordPress, Shopify, Webflow, or Next.js) and paste it into your website\'s <head>.'));
    const _tutorialBackendBase = (window.__INTENTIFY_API_BASE__ || '/api').startsWith('http')
      ? window.__INTENTIFY_API_BASE__.replace(/\/api\/?$/, '')
      : window.location.origin;
    body.appendChild(mkCode(`<script src="${_tutorialBackendBase}/api/collector/sdk.js" data-site-id="YOUR_SITE_ID"></script>`));
    body.appendChild(mkNote('Visitors will start appearing in your dashboard within minutes of installation.'));
  }));

  // ── SECTION 5: Knowledge base ─────────────────────────────────────────────────
  content.appendChild(mkSection('step-5', '5', 'Index your Knowledge Base', (body) => {
    body.appendChild(mkDesc('After adding your site, Hven automatically creates two knowledge sources — your website URL and a Quick Facts template. You need to go to Knowledge in the sidebar and click Index on each source to activate them. The URL source crawls your website; the Quick Facts source is a template you should edit first with your real business details.'));
    const steps = [
      '→ Go to Knowledge in the sidebar',
      '→ Your site URL and Quick Facts are already created — click Index to start',
      '→ Edit the Quick Facts source first with your real business details',
      '→ Add more sources: paste a URL, upload a PDF, or enter text',
      '→ Watch the status badge — green Indexed ✓ means your bot can use it',
      '→ If a source fails, the error is shown below it — click Retry to try again',
    ];
    const stepList = document.createElement('div');
    stepList.style.cssText = 'display:flex;flex-direction:column;gap:5px;margin:4px 0';
    steps.forEach(s => {
      const line = document.createElement('div');
      line.textContent = s;
      line.style.cssText = 'font-size:13px;color:#475569';
      stepList.appendChild(line);
    });
    body.appendChild(stepList);
    body.appendChild(mkNote('Indexing usually takes 30–60 seconds. If it fails, check that your website has a valid SSL certificate.'));
    body.appendChild(mkBtn('Go to Knowledge →', '#/knowledge'));
  }));

  // ── SECTION 6: Engage bot ─────────────────────────────────────────────────────
  content.appendChild(mkSection('step-6', '6', 'Configure your Engage bot', (body) => {
    body.appendChild(mkDesc('The Engage bot is your AI sales assistant. It lives on your website, greets visitors, answers questions, qualifies leads, and escalates hot prospects to your team.'));
    const items = [
      'Set the bot\'s name and tone (friendly, professional, or direct)',
      'Define its primary goal (book a demo, capture email, answer questions)',
      'Set escalation rules — when to notify a human',
      'Connect it to a site',
    ];
    const list = document.createElement('div');
    list.style.cssText = 'display:flex;flex-direction:column;gap:6px;margin:4px 0';
    items.forEach(label => {
      const pill = document.createElement('div');
      pill.style.cssText = 'display:inline-flex;align-items:center;gap:8px;background:#f1f5f9;border:1px solid #e2e8f0;border-radius:6px;padding:6px 10px;font-size:12.5px;color:#334155;width:fit-content';
      const dot = document.createElement('span');
      dot.style.cssText = 'width:6px;height:6px;border-radius:50%;background:#6366f1;flex-shrink:0';
      pill.appendChild(dot);
      pill.appendChild(document.createTextNode(label));
      list.appendChild(pill);
    });
    body.appendChild(list);
    body.appendChild(mkBtn('Go to Engage →', '#/engage'));
  }));

  // ── SECTION 7: Flows ──────────────────────────────────────────────────────────
  content.appendChild(mkSection('step-7', '7', 'Set up Flows automation', (body) => {
    body.appendChild(mkDesc('Flows let you automate actions based on visitor behaviour. When a visitor matches a trigger — like viewing your pricing page three times — a Flow can send an alert, create a lead, or trigger the Engage bot.'));
    const row = document.createElement('div');
    row.style.cssText = 'display:flex;align-items:center;gap:12px;margin:8px 0;flex-wrap:wrap';
    const triggerCard = document.createElement('div');
    triggerCard.style.cssText = 'background:#fff;border:1px solid #e4e7f0;border-radius:8px;padding:10px 14px;font-size:12.5px;color:#334155;line-height:1.45;max-width:200px';
    triggerCard.innerHTML = '<div style="font-size:10px;font-weight:700;letter-spacing:.07em;text-transform:uppercase;color:#94a3b8;margin-bottom:4px">Trigger</div>Visitor views /pricing more than 2 times';
    const arrow = document.createElement('span');
    arrow.textContent = '→';
    arrow.style.cssText = 'font-size:18px;color:#6366f1;font-weight:700;flex-shrink:0';
    const actionCard = document.createElement('div');
    actionCard.style.cssText = 'background:#eef2ff;border:1px solid rgba(99,102,241,.2);border-radius:8px;padding:10px 14px;font-size:12.5px;color:#3730a3;line-height:1.45;max-width:200px';
    actionCard.innerHTML = '<div style="font-size:10px;font-weight:700;letter-spacing:.07em;text-transform:uppercase;color:#818cf8;margin-bottom:4px">Action</div>Create lead + notify Slack';
    row.appendChild(triggerCard); row.appendChild(arrow); row.appendChild(actionCard);
    body.appendChild(row);
    body.appendChild(mkBtn('Go to Flows →', '#/flows'));
  }));

  // ── SECTION 8: Visitors & Leads ───────────────────────────────────────────────
  content.appendChild(mkSection('step-8', '8', 'Monitor Visitors and Leads', (body) => {
    body.appendChild(mkDesc('Once your snippet is installed, Visitors shows everyone on your site right now — identified by company, location, and intent score. Leads shows enriched profiles of visitors who have been qualified.'));
    const row = document.createElement('div');
    row.style.cssText = 'display:flex;gap:12px;margin:8px 0;flex-wrap:wrap';
    const mkStatCard = (bg, title, sub) => {
      const c = document.createElement('div');
      c.style.cssText = `background:${bg};border-radius:10px;padding:14px 18px;flex:1;min-width:140px`;
      const t = document.createElement('div');
      t.textContent = title;
      t.style.cssText = 'font-size:13px;font-weight:700;color:#fff;margin-bottom:4px';
      const s = document.createElement('div');
      s.textContent = sub;
      s.style.cssText = 'font-size:12px;color:rgba(255,255,255,.65)';
      c.appendChild(t); c.appendChild(s);
      return c;
    };
    row.appendChild(mkStatCard('#0f1729', 'Visitors', 'Live activity on your site'));
    row.appendChild(mkStatCard('#6366f1', 'Leads', 'Identified and scored prospects'));
    body.appendChild(row);
    const btnRow = document.createElement('div');
    btnRow.style.cssText = 'display:flex;gap:10px;flex-wrap:wrap';
    btnRow.appendChild(mkBtn('Go to Visitors →', '#/visitors'));
    btnRow.appendChild(mkBtn('Go to Leads →', '#/leads'));
    body.appendChild(btnRow);
  }));

  // ── Tutorial completion tracking ──────────────────────────────────────────────
  const applyCompletionToCards = (done) => {
    done.forEach(stepId => {
      const card = content.querySelector('#' + stepId);
      if (!card) return;
      const header = card.firstElementChild;
      if (!header || header.querySelector('.tut-done-pill')) return;
      const pill = document.createElement('span');
      pill.className = 'tut-done-pill';
      pill.textContent = '✓ Complete';
      pill.style.cssText = 'display:inline-flex;align-items:center;background:#dcfce7;color:#16a34a;font-size:11px;font-weight:600;padding:3px 10px;border-radius:100px;margin-left:auto;flex-shrink:0;white-space:nowrap';
      header.appendChild(pill);
    });
  };

  const _tutUserId = authState.profile?.userId || authState.profile?.email || 'default';
  const _tutCacheKey = `hven_tutorial_${_tutUserId}`;
  const _tutCacheTtl = 30_000;

  const runProgressCheck = async (bust) => {
    if (bust) { try { localStorage.removeItem(_tutCacheKey); } catch {} }
    const completed = new Set(['step-2']);
    try {
      if (!bust) {
        const cached = JSON.parse(localStorage.getItem(_tutCacheKey) || 'null');
        if (cached && Date.now() - cached.ts < _tutCacheTtl) {
          completedSteps = new Set(cached.completed);
          setActiveNav(lastActiveIndex);
          applyCompletionToCards(completedSteps);
          return;
        }
      }
      const sites = await apiClient.sites.list();
      const siteList = Array.isArray(sites) ? sites : [];
      if (siteList.length > 0) {
        completed.add('step-3');
        if (siteList.some(s => s.firstEventReceivedAtUtc)) completed.add('step-4');
        const firstId = siteList[0].siteId || siteList[0].id || '';
        const [knR, botR, flR, vsR] = await Promise.allSettled([
          apiClient.knowledge.listSources(firstId),
          apiClient.engage.getBot(firstId),
          apiClient.flows.list(firstId),
          apiClient.visitors.list(firstId, 1, 1),
        ]);
        if (knR.status === 'fulfilled') {
          const srcs = Array.isArray(knR.value) ? knR.value : [];
          if (srcs.some(s => String(s.status || '').toUpperCase() === 'INDEXED')) completed.add('step-5');
        }
        if (botR.status === 'fulfilled' && botR.value?.businessDescription) completed.add('step-6');
        if (flR.status === 'fulfilled') {
          const fl = Array.isArray(flR.value) ? flR.value : (flR.value?.items ?? []);
          if (fl.length > 0) completed.add('step-7');
        }
        if (vsR.status === 'fulfilled') {
          const vs = Array.isArray(vsR.value) ? vsR.value : (vsR.value?.items ?? []);
          if (vs.length > 0) completed.add('step-8');
        }
      }
      try { localStorage.setItem(_tutCacheKey, JSON.stringify({ ts: Date.now(), completed: [...completed] })); } catch {}
    } catch { /* non-critical */ }
    completedSteps = completed;
    setActiveNav(lastActiveIndex);
    applyCompletionToCards(completedSteps);
  };

  // ── IntersectionObserver for sidebar ─────────────────────────────────────────
  const sectionEls = stepMeta.map(s => document.getElementById(s.id)).filter(Boolean);
  // observe after DOM is appended (requestAnimationFrame ensures IDs exist)
  requestAnimationFrame(() => {
    const sectionNodes = stepMeta.map(s => content.querySelector('#' + s.id)).filter(Boolean);
    const io = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting) {
          const idx = sectionNodes.indexOf(entry.target);
          if (idx !== -1) setActiveNav(idx);
        }
      });
    }, { threshold: 0.35 });
    sectionNodes.forEach(n => io.observe(n));
  });

  layout.appendChild(sidebar);
  layout.appendChild(content);
  container.appendChild(layout);

  requestAnimationFrame(() => runProgressCheck(false));
};

const renderLoginView = (container) => {
  const emailField    = createField({ label: 'Email',    type: 'email',    placeholder: 'you@example.com' });
  const passwordField = createField({ label: 'Password', type: 'password', placeholder: 'At least 10 characters' });

  const submitButton = document.createElement('button');
  submitButton.type = 'submit';
  submitButton.textContent = 'Login';
  submitButton.style.cssText = 'margin-top:12px;padding:10px 14px;border-radius:6px;border:none;background:#2563eb;color:#fff;cursor:pointer';

  const switchLink = document.createElement('a');
  switchLink.href = '#/register';
  switchLink.textContent = 'Need an account? Register';
  switchLink.style.cssText = 'display:inline-block;margin-top:12px;color:#2563eb';

  const form = document.createElement('form');
  form.style.cssText = 'display:flex;flex-direction:column;gap:12px';
  form.append(emailField.wrapper, passwordField.wrapper, submitButton, switchLink);

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    emailField.error.textContent = '';
    passwordField.error.textContent = '';
    const email    = emailField.input.value.trim();
    const password = passwordField.input.value;
    const emailError    = validateEmail(email);
    const passwordError = validatePassword(password);
    if (emailError)    emailField.error.textContent    = emailError;
    if (passwordError) passwordField.error.textContent = passwordError;
    if (emailError || passwordError) { toast.show({ message: 'Please fix the highlighted fields.', variant: 'warning' }); return; }

    submitButton.disabled = true; submitButton.textContent = 'Signing in...';
    try {
      const response = await apiClient.request('/auth/login', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password }),
      });
      setToken(response.accessToken);
      window.location.hash = '#/dashboard';
    } catch (error) {
      const uiError = mapApiError(error);
      const applied = applyFieldErrors(uiError.details?.errors, { email: emailField, password: passwordField });
      toast.show({ message: applied ? 'Please review the highlighted errors.' : uiError.message, variant: 'danger' });
    } finally {
      submitButton.disabled = false; submitButton.textContent = 'Login';
    }
  });

  const card = createCard({ title: 'Login', body: form });
  card.style.cssText = 'width:100%;max-width:420px';
  container.appendChild(card);
};

const renderRegisterView = (container) => {
  const displayNameField      = createField({ label: 'Display name',      type: 'text',     placeholder: 'Jane Doe' });
  const organizationNameField = createField({ label: 'Organization name', type: 'text',     placeholder: 'Acme Inc' });
  const emailField            = createField({ label: 'Email',             type: 'email',    placeholder: 'you@example.com' });
  const passwordField         = createField({ label: 'Password',          type: 'password', placeholder: 'At least 10 characters' });

  const submitButton = document.createElement('button');
  submitButton.type = 'submit';
  submitButton.textContent = 'Create account';
  submitButton.style.cssText = 'margin-top:12px;padding:10px 14px;border-radius:6px;border:none;background:#2563eb;color:#fff;cursor:pointer';

  const switchLink = document.createElement('a');
  switchLink.href = '#/login';
  switchLink.textContent = 'Already have an account? Login';
  switchLink.style.cssText = 'display:inline-block;margin-top:12px;color:#2563eb';

  const form = document.createElement('form');
  form.style.cssText = 'display:flex;flex-direction:column;gap:12px';
  form.append(displayNameField.wrapper, organizationNameField.wrapper, emailField.wrapper, passwordField.wrapper, submitButton, switchLink);

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    displayNameField.error.textContent = '';
    organizationNameField.error.textContent = '';
    emailField.error.textContent = '';
    passwordField.error.textContent = '';

    const displayName    = displayNameField.input.value.trim();
    const organizationName = organizationNameField.input.value.trim();
    const email          = emailField.input.value.trim();
    const password       = passwordField.input.value;

    let hasError = false;
    if (!displayName)      { displayNameField.error.textContent = 'Display name is required.';      hasError = true; }
    if (!organizationName) { organizationNameField.error.textContent = 'Organization name is required.'; hasError = true; }
    const emailError = validateEmail(email);
    if (emailError)        { emailField.error.textContent = emailError;                              hasError = true; }
    const passwordError = validatePassword(password);
    if (passwordError)     { passwordField.error.textContent = passwordError;                        hasError = true; }
    if (hasError)          { toast.show({ message: 'Please fix the highlighted fields.', variant: 'warning' }); return; }

    submitButton.disabled = true; submitButton.textContent = 'Creating account...';
    try {
      const response = await apiClient.request('/auth/register', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ displayName, organizationName, email, password }),
      });
      setToken(response.accessToken);
      window.location.hash = '#/dashboard';
    } catch (error) {
      const uiError = mapApiError(error);
      const applied = applyFieldErrors(uiError.details?.errors, {
        displayName: displayNameField, organizationName: organizationNameField,
        email: emailField, password: passwordField,
      });
      toast.show({ message: applied ? 'Please review the highlighted errors.' : uiError.message, variant: 'danger' });
    } finally {
      submitButton.disabled = false; submitButton.textContent = 'Create account';
    }
  });

  const card = createCard({ title: 'Register', body: form });
  card.style.cssText = 'width:100%;max-width:460px';
  container.appendChild(card);
};

const renderAcceptInviteView = (container, { query } = {}) => {
  const tokenField       = createField({ label: 'Invitation token', type: 'text',     placeholder: 'Paste invitation token' });
  tokenField.input.value = query?.token || '';
  const displayNameField = createField({ label: 'Display name',     type: 'text',     placeholder: 'Jane Doe' });
  const emailField       = createField({ label: 'Email',            type: 'email',    placeholder: 'you@example.com' });
  const passwordField    = createField({ label: 'Password',         type: 'password', placeholder: 'At least 10 characters' });

  const submitButton = document.createElement('button');
  submitButton.type = 'submit';
  submitButton.textContent = 'Accept invite';
  submitButton.style.cssText = 'margin-top:12px;padding:10px 14px;border-radius:6px;border:none;background:#2563eb;color:#fff;cursor:pointer';

  const form = document.createElement('form');
  form.style.cssText = 'display:flex;flex-direction:column;gap:12px';
  form.append(tokenField.wrapper, displayNameField.wrapper, emailField.wrapper, passwordField.wrapper, submitButton);

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    tokenField.error.textContent = ''; displayNameField.error.textContent = '';
    emailField.error.textContent = ''; passwordField.error.textContent = '';
    const token       = tokenField.input.value.trim();
    const displayName = displayNameField.input.value.trim();
    const email       = emailField.input.value.trim();
    const password    = passwordField.input.value;
    let hasError = false;
    if (!token)       { tokenField.error.textContent = 'Invitation token is required.'; hasError = true; }
    if (!displayName) { displayNameField.error.textContent = 'Display name is required.'; hasError = true; }
    const emailError = validateEmail(email);
    if (emailError)   { emailField.error.textContent = emailError; hasError = true; }
    const passwordError = validatePassword(password);
    if (passwordError){ passwordField.error.textContent = passwordError; hasError = true; }
    if (hasError)     { toast.show({ message: 'Please fix the highlighted fields.', variant: 'warning' }); return; }

    submitButton.disabled = true; submitButton.textContent = 'Accepting...';
    try {
      const response = await apiClient.auth.acceptInvite({ token, displayName, email, password });
      setToken(response.accessToken);
      window.location.hash = '#/dashboard';
    } catch (error) {
      const uiError = mapApiError(error);
      const applied = applyFieldErrors(uiError.details?.errors, {
        token: tokenField, displayName: displayNameField, email: emailField, password: passwordField,
      });
      toast.show({ message: applied ? 'Please review the highlighted errors.' : uiError.message, variant: 'danger' });
    } finally {
      submitButton.disabled = false; submitButton.textContent = 'Accept invite';
    }
  });

  const card = createCard({ title: 'Accept invitation', body: form });
  card.style.cssText = 'width:100%;max-width:460px';
  container.appendChild(card);
};

// ── Routes ─────────────────────────────────────────────────────────────────────

const routes = {
  '/':                                    renderHomeView,
  '/login':                               renderLoginView,
  '/register':                            renderRegisterView,
  '/accept-invite':                       renderAcceptInviteView,
  '/dashboard':                           renderDashboardView,
  '/team':                                renderTeamView,
  '/sites':                               renderSitesView,
  '/install':                             renderInstallView,
  '/visitors':                            renderVisitorsView,
  '/knowledge':                           renderKnowledgeView,
  '/flows':                               renderFlowsView,
  '/engage':                              renderEngageView,
  '/promos':                              renderPromosView,
  '/intelligence':                        renderIntelligenceView,
  '/ads':                                 renderAdsView,
  '/leads':                               renderLeadsView,
  '/tickets':                             renderTicketsView,
  '/analytics':                           renderMultiSiteAnalyticsView,
  '/platform-admin':                      renderPlatformAdminView,
  '/platform-admin/tenant/:tenantId':     renderPlatformAdminTenantDetailView,
};

const getRouteFromHash = () => {
  const hash = window.location.hash.replace(/^#/, '');
  if (!hash || hash === '/') return { path: '/', query: {}, params: {} };

  const [pathSegment, queryString] = hash.split('?');
  const path = pathSegment.startsWith('/') ? pathSegment : `/${pathSegment}`;
  const query = {};
  if (queryString) {
    const params = new URLSearchParams(queryString);
    params.forEach((value, key) => { query[key] = value; });
  }

  const visitorMatch = path.match(/^\/visitors\/([^/]+)$/);
  if (visitorMatch) return { path: '/visitors/:visitorId', query, params: { visitorId: visitorMatch[1] } };

  const platformTenantMatch = path.match(/^\/platform-admin\/tenant\/([^/]+)$/);
  if (platformTenantMatch) return { path: '/platform-admin/tenant/:tenantId', query, params: { tenantId: platformTenantMatch[1] } };

  return { path, query, params: {} };
};

// ── Auth state ─────────────────────────────────────────────────────────────────

const authState = { loaded: false, loading: false, roles: [], profile: null, profileModalOpen: false, wizardChecked: false };

const getPrimaryRole = (roles) => {
  if (!Array.isArray(roles)) return 'user';
  if (roles.includes('super_admin'))    return 'super_admin';
  if (roles.includes('platform_admin')) return 'admin';
  if (roles.includes('admin'))          return 'admin';
  if (roles.includes('manager'))        return 'manager';
  return 'user';
};

const canManageUsers    = (role) => role === 'super_admin' || role === 'admin' || role === 'manager';
const isAdministratorRole = (role) => role === 'super_admin' || role === 'admin';
const canAccessWrite    = (role) => role === 'super_admin' || role === 'admin' || role === 'manager';

const canInviteRole = (actorRole, targetRole) => {
  if (actorRole === 'super_admin') return ['admin', 'manager', 'user'].includes(targetRole);
  if (actorRole === 'admin')       return ['manager', 'user'].includes(targetRole);
  if (actorRole === 'manager')     return targetRole === 'user';
  return false;
};

const canChangeRole = (actorRole, targetRole) => {
  if (targetRole === 'super_admin') return false;
  if (actorRole === 'super_admin')  return ['admin', 'manager', 'user'].includes(targetRole);
  if (actorRole === 'admin')        return ['manager', 'user'].includes(targetRole);
  if (actorRole === 'manager')      return targetRole === 'user';
  return false;
};

const canRemoveRole = (actorRole, targetRole) => canChangeRole(actorRole, targetRole);

const hasPlatformAccess = () =>
  Array.isArray(authState.roles) &&
  (authState.roles.includes('super_admin') || authState.roles.includes('platform_admin'));

const loadAuthProfile = async () => {
  if (authState.loading) return;
  authState.loading = true;
  try {
    const me = await apiClient.auth.me();
    authState.roles   = Array.isArray(me.roles) ? me.roles : [];
    authState.profile = me;
  } catch {
    authState.roles   = [];
    authState.profile = null;
  } finally {
    authState.loaded  = true;
    authState.loading = false;
    renderApp();
  }
};

// ── Main render loop ───────────────────────────────────────────────────────────

const renderApp = () => {
  if (!app) return;
  setAppLayout();

  const { path: route, query, params } = getRouteFromHash();
  const isAuthenticated = Boolean(getToken());

  const protectedRoutes = [
    '/dashboard', '/team', '/sites', '/visitors', '/visitors/:visitorId',
    '/knowledge', '/engage', '/promos', '/intelligence', '/ads', '/leads', '/tickets',
    '/analytics', '/platform-admin', '/platform-admin/tenant/:tenantId',
  ];

  if (protectedRoutes.includes(route) && !isAuthenticated) {
    window.location.hash = '#/login';
    return;
  }

  if ((route === '/login' || route === '/register' || route === '/accept-invite') && isAuthenticated) {
    window.location.hash = '#/dashboard';
    return;
  }

  if (isAuthenticated && !authState.loaded) loadAuthProfile();

  if (!isAuthenticated) {
    authState.loaded = false; authState.roles = []; authState.profile = null; authState.profileModalOpen = false;
  }

  const isPlatformRoute = route === '/platform-admin' || route === '/platform-admin/tenant/:tenantId';
  const isTeamRoute     = route === '/team';

  // Show loading shell while auth profile is loading for privileged routes
  if (isAuthenticated && isPlatformRoute && !authState.loaded) {
    app.innerHTML = '';
    const { shell, main, overlay } = createAuthenticatedShell({
      route, canAccessPlatformAdmin: false, canAccessTeam: false,
      firstName: getFirstName(authState.profile?.displayName),
      onOpenProfile: () => { authState.profileModalOpen = true; renderApp(); },
      onLogout: () => { clearToken(); window.location.hash = '#/login'; },
    });
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

  // Viewer role cannot access write-heavy routes
  const WRITE_ROUTES = ['/sites', '/flows', '/engage', '/knowledge', '/promos', '/ads', '/intelligence'];
  if (isAuthenticated && WRITE_ROUTES.includes(route) && authState.loaded) {
    if (getPrimaryRole(authState.roles) === 'user') {
      window.location.hash = '#/dashboard';
      return;
    }
  }

  const view = routes[route] || routes['/'];
  app.innerHTML = '';
  let main;

  if (isAuthenticated) {
    const { shell, main: shellMain, overlay } = createAuthenticatedShell({
      route,
      canAccessPlatformAdmin: hasPlatformAccess(),
      canAccessTeam: canManageUsers(getPrimaryRole(authState.roles)),
      firstName: getFirstName(authState.profile?.displayName),
      onOpenProfile: () => { authState.profileModalOpen = true; renderApp(); },
      onLogout: () => { clearToken(); window.location.hash = '#/login'; },
    });
    main = shellMain;
    app.append(shell, overlay);
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
    apiClient, toast, query, params,
    currentUser: authState.profile,
    capabilities: {
      canManageUsers,
      canInviteRole,
      canChangeRole,
      canRemoveRole,
      currentRole: getPrimaryRole(authState.roles),
      isAdmin:     isAdministratorRole(getPrimaryRole(authState.roles)),
      canWrite:    canAccessWrite(getPrimaryRole(authState.roles)),
    },
  });

  if (isAuthenticated && authState.loaded && !authState.wizardChecked) {
    authState.wizardChecked = true;
    (async () => {
      try {
        const sites = await apiClient.sites.list();
        const firstSite = Array.isArray(sites) && sites.length > 0 ? sites[0] : null;
        if (!firstSite) return;
        const siteId = firstSite.siteId || firstSite.id || String(firstSite._id || '');
        if (!siteId) return;
        const dismissKey = `hven_wizard_dismissed_${siteId}`;
        if (localStorage.getItem(dismissKey)) return;
        const bot = await apiClient.engage.getBot(siteId);
        if (!bot?.businessDescription) {
          showBotWizard(app, { apiClient, siteId, toast, onComplete: () => {} });
        }
      } catch { /* ignore — wizard is non-critical */ }
    })();
  }

  if (isAuthenticated && authState.profileModalOpen && authState.profile) {
    const primaryRole    = getPrimaryRole(authState.roles);
    const canManageTeam  = canManageUsers(primaryRole);
    const isAdministrator = isAdministratorRole(primaryRole);
    const inviteRoles    = ['admin', 'manager', 'user'].filter((role) => canInviteRole(primaryRole, role));

    let modal;
    const closeModal = () => {
      if (modal) modal.cleanup();
      authState.profileModalOpen = false;
      renderApp();
    };

    modal = createProfileModal({
      profile: authState.profile,
      currentRole: primaryRole,
      canManageTeam,
      isAdministrator,
      inviteRoles,
      onInvite: async ({ email, role }) => { await apiClient.auth.createInvite({ email, role }); },
      onManageTeam: () => { authState.profileModalOpen = false; window.location.hash = '#/team'; },
      onClose: closeModal,
      onSave: async ({ displayName, organizationName }) => {
        await apiClient.auth.updateProfile({ displayName, organizationName });
        const refreshed = await apiClient.auth.me();
        authState.profile = refreshed;
        authState.roles   = Array.isArray(refreshed.roles) ? refreshed.roles : [];
      },
    });

    app.appendChild(modal.overlay);
  }
};

// Pick up Google OAuth token from redirect (?token=...)
(function captureOAuthToken() {
  const urlParams = new URLSearchParams(window.location.search);
  const oauthToken = urlParams.get('token');
  if (oauthToken) {
    setToken(oauthToken);
    window.history.replaceState({}, document.title, window.location.pathname);
  }
})();

window.addEventListener('hashchange', renderApp);
applyTheme(getTheme());
renderApp();