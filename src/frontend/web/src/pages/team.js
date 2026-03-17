import { createCard, createInput } from '../shared/ui/index.js';
import { mapApiError } from '../shared/apiClient.js';

const ROLE_LABELS = {
  admin: 'Admin',
  manager: 'Manager',
  user: 'User',
  super_admin: 'Super Admin',
};

const getInviteStatus = (invite) => {
  if (invite.acceptedAtUtc) {
    return 'Accepted';
  }

  if (invite.revokedAtUtc) {
    return 'Revoked';
  }

  const expiresAt = Date.parse(invite.expiresAtUtc || '');
  if (!Number.isNaN(expiresAt) && expiresAt <= Date.now()) {
    return 'Expired';
  }

  return 'Pending';
};

const createSelectField = ({ label, options }) => {
  const wrapper = document.createElement('label');
  wrapper.className = 'ui-field';

  const title = document.createElement('span');
  title.className = 'ui-label';
  title.textContent = label;

  const select = document.createElement('select');
  select.className = 'ui-input';

  options.forEach((option) => {
    const element = document.createElement('option');
    element.value = option.value;
    element.textContent = option.label;
    select.appendChild(element);
  });

  const error = document.createElement('div');
  error.className = 'ui-field-error';

  wrapper.append(title, select, error);
  return { wrapper, select, error };
};

const normalizeRole = (roles = []) => {
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

export const renderTeamView = async (container, { apiClient, toast, currentUser, capabilities }) => {
  container.innerHTML = '';

  const body = document.createElement('div');
  body.style.display = 'flex';
  body.style.flexDirection = 'column';
  body.style.gap = '16px';

  const card = createCard({
    title: 'Team Management',
    body,
  });

  card.style.maxWidth = '1000px';
  card.style.width = '100%';
  container.appendChild(card);

  const inviteSection = document.createElement('div');
  inviteSection.style.display = 'flex';
  inviteSection.style.gap = '8px';
  inviteSection.style.alignItems = 'end';
  inviteSection.style.flexWrap = 'wrap';

  const { wrapper: emailWrapper, input: emailInput } = createInput({
    label: 'Invite email',
    type: 'email',
    placeholder: 'you@example.com',
  });
  const emailError = document.createElement('div');
  emailError.className = 'ui-field-error';
  emailWrapper.appendChild(emailError);

  const roleField = createSelectField({
    label: 'Role',
    options: [
      { value: 'admin', label: 'Admin' },
      { value: 'manager', label: 'Manager' },
      { value: 'user', label: 'User' },
    ],
  });

  const inviteButton = document.createElement('button');
  inviteButton.type = 'button';
  inviteButton.textContent = 'Send invite';
  inviteButton.className = 'ui-button ui-button-primary';

  inviteSection.append(emailWrapper, roleField.wrapper, inviteButton);

  const usersSection = document.createElement('div');
  const usersLoading = document.createElement('div');
  usersLoading.textContent = 'Loading team...';
  usersLoading.style.color = '#475569';
  usersSection.appendChild(usersLoading);

  const invitesSection = document.createElement('div');
  const invitesLoading = document.createElement('div');
  invitesLoading.textContent = 'Loading invites...';
  invitesLoading.style.color = '#475569';
  invitesSection.appendChild(invitesLoading);

  body.append(inviteSection, usersSection, invitesSection);

  const reload = async () => {
    usersLoading.textContent = 'Loading team...';
    usersSection.replaceChildren(usersLoading);
    invitesLoading.textContent = 'Loading invites...';
    invitesSection.replaceChildren(invitesLoading);

    try {
      const users = await apiClient.auth.listUsers();
      const actorRole = normalizeRole(currentUser?.roles || []);

      const table = document.createElement('table');
      table.className = 'ui-table';

      const head = document.createElement('thead');
      const headRow = document.createElement('tr');
      ['Display Name', 'Email', 'Role', 'Status', 'Actions'].forEach((label) => {
        const cell = document.createElement('th');
        cell.textContent = label;
        headRow.appendChild(cell);
      });
      head.appendChild(headRow);
      table.appendChild(head);

      const bodyRows = document.createElement('tbody');

      (Array.isArray(users) ? users : []).forEach((user) => {
        const row = document.createElement('tr');
        const roleText = ROLE_LABELS[normalizeRole(user.roles)] || normalizeRole(user.roles);

        const actions = document.createElement('div');
        actions.style.display = 'flex';
        actions.style.gap = '8px';
        actions.style.flexWrap = 'wrap';

        const targetRole = normalizeRole(user.roles);
        const isSelf = (user.userId || '').toLowerCase() === (currentUser?.userId || '').toLowerCase();

        if (capabilities.canChangeRole(actorRole, targetRole) && !isSelf && user.isActive) {
          const selector = document.createElement('select');
          selector.className = 'ui-input';
          selector.style.maxWidth = '140px';

          ['admin', 'manager', 'user'].forEach((role) => {
            if (!capabilities.canInviteRole(actorRole, role)) {
              return;
            }

            const option = document.createElement('option');
            option.value = role;
            option.textContent = ROLE_LABELS[role] || role;
            if (role === targetRole) {
              option.selected = true;
            }
            selector.appendChild(option);
          });

          selector.addEventListener('change', async () => {
            try {
              await apiClient.auth.updateUserRole(user.userId, selector.value);
              toast.show({ message: 'Role updated.', variant: 'success' });
              await reload();
            } catch (error) {
              const uiError = mapApiError(error);
              toast.show({ message: uiError.message, variant: 'danger' });
              await reload();
            }
          });

          actions.appendChild(selector);
        }

        if (capabilities.canRemoveRole(actorRole, targetRole) && !isSelf && user.isActive) {
          const removeButton = document.createElement('button');
          removeButton.type = 'button';
          removeButton.textContent = 'Remove';
          removeButton.className = 'ui-button';
          removeButton.addEventListener('click', async () => {
            const confirmed = window.confirm(`Remove ${user.email}?`);
            if (!confirmed) {
              return;
            }

            try {
              await apiClient.auth.removeUser(user.userId);
              toast.show({ message: 'User removed.', variant: 'success' });
              await reload();
            } catch (error) {
              const uiError = mapApiError(error);
              toast.show({ message: uiError.message, variant: 'danger' });
            }
          });
          actions.appendChild(removeButton);
        }

        [
          user.displayName || '',
          user.email || '',
          roleText,
          user.isActive ? 'Active' : 'Inactive',
        ].forEach((value) => {
          const cell = document.createElement('td');
          cell.textContent = value;
          row.appendChild(cell);
        });

        const actionsCell = document.createElement('td');
        actionsCell.appendChild(actions);
        row.appendChild(actionsCell);
        bodyRows.appendChild(row);
      });

      table.appendChild(bodyRows);

      usersSection.replaceChildren(table);

      try {
        const invites = await apiClient.auth.listInvites();
        const invitesTable = document.createElement('table');
        invitesTable.className = 'ui-table';

        const invitesHead = document.createElement('thead');
        const invitesHeadRow = document.createElement('tr');
        ['Pending Invites', 'Role', 'Status', 'Actions'].forEach((label) => {
          const cell = document.createElement('th');
          cell.textContent = label;
          invitesHeadRow.appendChild(cell);
        });
        invitesHead.appendChild(invitesHeadRow);
        invitesTable.appendChild(invitesHead);

        const invitesBody = document.createElement('tbody');
        const actorRoleForInvites = normalizeRole(currentUser?.roles || []);
        (Array.isArray(invites) ? invites : []).forEach((invite) => {
          const row = document.createElement('tr');
          const status = getInviteStatus(invite);
          const role = (invite.role || 'user').toLowerCase();
          const roleText = ROLE_LABELS[role] || role;

          [invite.email || '', roleText, status].forEach((value) => {
            const cell = document.createElement('td');
            cell.textContent = value;
            row.appendChild(cell);
          });

          const actionsCell = document.createElement('td');
          if (status === 'Pending' && capabilities.canInviteRole(actorRoleForInvites, role)) {
            const revokeButton = document.createElement('button');
            revokeButton.type = 'button';
            revokeButton.textContent = 'Revoke';
            revokeButton.className = 'ui-button';
            revokeButton.addEventListener('click', async () => {
              try {
                await apiClient.auth.revokeInvite(invite.inviteId);
                toast.show({ message: 'Invitation revoked.', variant: 'success' });
                await reload();
              } catch (error) {
                const uiError = mapApiError(error);
                toast.show({ message: uiError.message, variant: 'danger' });
              }
            });
            actionsCell.appendChild(revokeButton);
          }

          row.appendChild(actionsCell);
          invitesBody.appendChild(row);
        });

        invitesTable.appendChild(invitesBody);
        invitesSection.replaceChildren(invitesTable);
      } catch (error) {
        const uiError = mapApiError(error);
        invitesSection.replaceChildren();
        const message = document.createElement('div');
        message.textContent = uiError.message;
        message.style.color = '#b91c1c';
        invitesSection.appendChild(message);
      }
    } catch (error) {
      const uiError = mapApiError(error);
      usersSection.replaceChildren();
      const message = document.createElement('div');
      message.textContent = uiError.message;
      message.style.color = '#b91c1c';
      usersSection.appendChild(message);
    }
  };

  inviteButton.addEventListener('click', async () => {
    emailError.textContent = '';
    roleField.error.textContent = '';

    const email = emailInput.value.trim();
    const role = roleField.select.value;
    const actorRole = normalizeRole(currentUser?.roles || []);

    if (!email) {
      emailError.textContent = 'Email is required.';
      return;
    }

    if (!capabilities.canInviteRole(actorRole, role)) {
      roleField.error.textContent = 'You cannot invite this role.';
      return;
    }

    inviteButton.disabled = true;
    inviteButton.textContent = 'Sending...';

    try {
      await apiClient.auth.createInvite({ email, role });
      emailInput.value = '';
      toast.show({ message: 'Invitation sent.', variant: 'success' });
      await reload();
    } catch (error) {
      const uiError = mapApiError(error);
      toast.show({ message: uiError.message, variant: 'danger' });
    } finally {
      inviteButton.disabled = false;
      inviteButton.textContent = 'Send invite';
    }
  });

  await reload();
};
