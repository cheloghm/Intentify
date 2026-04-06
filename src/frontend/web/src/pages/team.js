/**
 * team.js — Intentify Team Management
 * Revamped: dark hero, panel layout, same design language.
 * All original: invite member, list users, change role, remove user,
 * list invites, revoke invite. Passes apiClient/capabilities through.
 */

import { mapApiError } from '../shared/apiClient.js';

const ROLE_LABELS = { admin:'Admin', manager:'Manager', user:'User', super_admin:'Super Admin' };

const getInviteStatus = invite => {
  if (invite.acceptedAtUtc) return 'Accepted';
  if (invite.revokedAtUtc)  return 'Revoked';
  const exp = Date.parse(invite.expiresAtUtc||'');
  if (!isNaN(exp) && exp <= Date.now()) return 'Expired';
  return 'Pending';
};

const normalizeRole = (roles=[]) => {
  if (!Array.isArray(roles)) return 'user';
  if (roles.includes('super_admin'))    return 'super_admin';
  if (roles.includes('platform_admin')) return 'admin';
  if (roles.includes('admin'))          return 'admin';
  if (roles.includes('manager'))        return 'manager';
  return 'user';
};

// ─── el() ─────────────────────────────────────────────────────────────────────

const el = (tag, attrs = {}, ...kids) => {
  const e = document.createElement(tag);
  Object.entries(attrs).forEach(([k, v]) => {
    if (k === 'class')          e.className = v;
    else if (k === 'style')     typeof v === 'string' ? (e.style.cssText = v) : Object.assign(e.style, v);
    else if (k.startsWith('@')) e.addEventListener(k.slice(1), v);
    else e.setAttribute(k, v);
  });
  kids.flat(Infinity).forEach(c => c != null && e.append(typeof c === 'string' ? document.createTextNode(c) : c));
  return e;
};

// ─── Styles ───────────────────────────────────────────────────────────────────

const injectStyles = () => {
  if (document.getElementById('_tm_css')) return;
  const s = document.createElement('style');
  s.id = '_tm_css';
  s.textContent = `
@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap');
.tm-root{font-family:'Plus Jakarta Sans',system-ui,sans-serif;display:flex;flex-direction:column;gap:20px;width:100%;max-width:960px;padding-bottom:60px}
.tm-hero{background:linear-gradient(135deg,#0f172a 0%,#1e293b 100%);border-radius:16px;padding:28px 36px;position:relative;overflow:hidden}
.tm-hero::before{content:'';position:absolute;top:-30px;right:-30px;width:180px;height:180px;background:radial-gradient(circle,rgba(16,185,129,.15) 0%,transparent 70%);pointer-events:none}
.tm-hero-title{font-size:24px;font-weight:700;color:#f8fafc;letter-spacing:-.02em;margin-bottom:6px}
.tm-hero-sub{font-size:13px;color:#94a3b8;margin-bottom:18px}
.tm-hero-stats{display:flex;gap:28px;flex-wrap:wrap}
.tm-stat{display:flex;flex-direction:column;gap:2px}
.tm-stat-val{font-family:'JetBrains Mono',monospace;font-size:22px;font-weight:700;color:#f1f5f9}
.tm-stat-lbl{font-size:10px;color:#64748b;text-transform:uppercase;letter-spacing:.07em}
.tm-btn{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;font-weight:600;padding:7px 16px;border-radius:8px;border:none;cursor:pointer;transition:all .14s;display:inline-flex;align-items:center;gap:5px;white-space:nowrap}
.tm-btn-primary{background:#6366f1;color:#fff}.tm-btn-primary:hover:not(:disabled){background:#4f46e5;transform:translateY(-1px)}
.tm-btn-primary:disabled{opacity:.5;cursor:not-allowed}
.tm-btn-outline{background:#fff;color:#64748b;border:1px solid #e2e8f0}.tm-btn-outline:hover{background:#f8fafc;color:#1e293b}
.tm-btn-danger{background:#fee2e2;color:#dc2626;border:none}.tm-btn-danger:hover{background:#fecaca}
.tm-btn-sm{padding:5px 12px;font-size:12px}
.tm-panel{background:#fff;border:1px solid #e2e8f0;border-radius:14px;overflow:hidden}
.tm-panel-hd{display:flex;align-items:center;justify-content:space-between;padding:14px 20px;border-bottom:1px solid #f1f5f9}
.tm-panel-title{font-size:13px;font-weight:700;color:#0f172a;display:flex;align-items:center;gap:7px}
.tm-panel-body{padding:18px 20px;display:flex;flex-direction:column;gap:12px}
.tm-invite-row{display:grid;grid-template-columns:1fr 160px auto;gap:12px;align-items:end}
@media(max-width:600px){.tm-invite-row{grid-template-columns:1fr}}
.tm-field{display:flex;flex-direction:column;gap:5px}
.tm-field-lbl{font-size:10.5px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:#94a3b8}
.tm-input{font-family:'Plus Jakarta Sans',system-ui,sans-serif;font-size:13px;color:#1e293b;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:8px 11px;outline:none;width:100%;box-sizing:border-box}
.tm-input:focus{border-color:#6366f1;background:#fff;box-shadow:0 0 0 3px rgba(99,102,241,.1)}
.tm-err{font-size:11.5px;color:#dc2626;margin-top:4px}
/* Table */
.tm-table-wrap{border-radius:10px;overflow:hidden;border:1px solid #e2e8f0}
.tm-table{width:100%;border-collapse:collapse;font-size:12.5px}
.tm-table thead th{background:#f8fafc;padding:9px 16px;text-align:left;font-size:9.5px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:#94a3b8;border-bottom:1px solid #e2e8f0;white-space:nowrap}
.tm-table tbody td{padding:12px 16px;border-bottom:1px solid #f1f5f9;color:#334155;vertical-align:middle}
.tm-table tbody tr:last-child td{border-bottom:none}
.tm-table tbody tr:hover{background:#fafbff}
/* Pill */
.tm-pill{display:inline-flex;padding:2px 8px;border-radius:999px;font-size:10px;font-weight:700}
.tm-pill-green{background:#d1fae5;color:#065f46}
.tm-pill-amber{background:#fef3c7;color:#92400e}
.tm-pill-gray{background:#f1f5f9;color:#475569}
.tm-pill-blue{background:#dbeafe;color:#1e40af}
.tm-pill-red{background:#fee2e2;color:#dc2626}
/* Avatar */
.tm-avatar{width:34px;height:34px;border-radius:50%;background:linear-gradient(135deg,#6366f1,#8b5cf6);color:#fff;display:flex;align-items:center;justify-content:center;font-size:13px;font-weight:700;flex-shrink:0}
.tm-user-cell{display:flex;align-items:center;gap:10px}
.tm-user-name{font-weight:600;color:#1e293b;font-size:12.5px}
.tm-user-email{font-size:11px;color:#94a3b8;font-family:'JetBrains Mono',monospace}
/* Empty */
.tm-empty{text-align:center;padding:32px 16px;color:#94a3b8;font-size:12.5px}
.tm-loading{color:#94a3b8;font-size:12.5px;padding:16px 0}
  `;
  document.head.appendChild(s);
};

// ─── Main export ──────────────────────────────────────────────────────────────

export const renderTeamView = async (container, { apiClient, toast, currentUser, capabilities } = {}) => {
  injectStyles();
  container.innerHTML = '';

  const root = el('div', { class: 'tm-root' });
  container.appendChild(root);

  // ── Hero ───────────────────────────────────────────────────────────────────
  const hero = el('div',{class:'tm-hero'});
  hero.appendChild(el('div',{class:'tm-hero-title'},'👥 Team'));
  hero.appendChild(el('div',{class:'tm-hero-sub'},'Invite teammates, manage roles, and control access'));
  const heroStats = el('div',{class:'tm-hero-stats'});
  const mkS = lbl => { const w=el('div',{class:'tm-stat'}); const v=el('div',{class:'tm-stat-val'},'—'); w.append(v,el('div',{class:'tm-stat-lbl'},lbl)); heroStats.appendChild(w); return v; };
  const hMembers=mkS('Members'); const hPending=mkS('Pending Invites');
  hero.appendChild(heroStats);
  root.appendChild(hero);

  // ── Invite panel ───────────────────────────────────────────────────────────
  const invitePanel = el('div',{class:'tm-panel'});
  const inviteHd = el('div',{class:'tm-panel-hd'}); inviteHd.appendChild(el('div',{class:'tm-panel-title'},'✉️ Invite a Team Member'));
  const inviteBody = el('div',{class:'tm-panel-body'});

  const inviteRow = el('div',{class:'tm-invite-row'});

  const emailField = el('div',{class:'tm-field'}, el('div',{class:'tm-field-lbl'},'Email Address'));
  const emailInput = el('input',{class:'tm-input',type:'email',placeholder:'colleague@company.com'});
  const emailErr   = el('div',{class:'tm-err'});
  emailField.append(emailInput, emailErr);

  const roleField = el('div',{class:'tm-field'}, el('div',{class:'tm-field-lbl'},'Role'));
  const roleSelect = el('select',{class:'tm-input'});
  [['admin','Admin'],['manager','Manager'],['user','User']].forEach(([v,l])=> roleSelect.appendChild(el('option',{value:v},l)));
  roleField.appendChild(roleSelect);

  const inviteBtn = el('button',{class:'tm-btn tm-btn-primary',style:'align-self:flex-end;min-height:38px'},'📧 Send Invite');
  inviteRow.append(emailField, roleField, inviteBtn);
  inviteBody.appendChild(inviteRow);
  invitePanel.append(inviteHd, inviteBody);
  root.appendChild(invitePanel);

  // ── Members panel ──────────────────────────────────────────────────────────
  const membersPanel = el('div',{class:'tm-panel'});
  const membersHd = el('div',{class:'tm-panel-hd'}); membersHd.appendChild(el('div',{class:'tm-panel-title'},'🧑‍💼 Members'));
  const membersBody = el('div',{class:'tm-panel-body',style:'padding:0'});
  membersPanel.append(membersHd, membersBody);
  root.appendChild(membersPanel);

  // ── Invites panel ──────────────────────────────────────────────────────────
  const invitesPanel = el('div',{class:'tm-panel'});
  const invitesHd = el('div',{class:'tm-panel-hd'}); invitesHd.appendChild(el('div',{class:'tm-panel-title'},'📨 Invitations'));
  const invitesBody = el('div',{class:'tm-panel-body',style:'padding:0'});
  invitesPanel.append(invitesHd, invitesBody);
  root.appendChild(invitesPanel);

  // ── Reload ─────────────────────────────────────────────────────────────────
  const reload = async () => {
    membersBody.replaceChildren(el('div',{class:'tm-loading',style:'padding:16px 20px'},'⏳ Loading members…'));
    invitesBody.replaceChildren(el('div',{class:'tm-loading',style:'padding:16px 20px'},'⏳ Loading invites…'));

    try {
      const users = await apiClient.auth.listUsers();
      const actorRole = normalizeRole(currentUser?.roles||[]);
      const userList = Array.isArray(users) ? users : [];
      hMembers.textContent = String(userList.length);

      if (!userList.length) { membersBody.replaceChildren(el('div',{class:'tm-empty'},'No team members yet.')); }
      else {
        const tableWrap = el('div',{class:'tm-table-wrap'});
        const table = el('table',{class:'tm-table'});
        table.appendChild(el('thead',{},el('tr',{}, ...['Member','Role','Status','Actions'].map(c=>el('th',{},c)))));
        const tbody = el('tbody',{});

        userList.forEach(user => {
          const role     = normalizeRole(user.roles);
          const roleLabel= ROLE_LABELS[role]||role;
          const isSelf   = user.userId === currentUser?.userId;
          const initial  = (user.displayName||user.email||'?')[0].toUpperCase();

          const tr = el('tr',{});
          const userCell = el('td',{});
          const userInner = el('div',{class:'tm-user-cell'});
          userInner.appendChild(el('div',{class:'tm-avatar'},initial));
          const uinfo = el('div',{});
          uinfo.append(el('div',{class:'tm-user-name'},user.displayName||user.email||'—'), el('div',{class:'tm-user-email'},user.email||''));
          userInner.appendChild(uinfo);
          userCell.appendChild(userInner);

          const roleCell = el('td',{});
          const canChangeRole = !isSelf && capabilities?.canInviteRole?.(actorRole, role);
          if (canChangeRole) {
            const roleSel = el('select',{class:'tm-input',style:'padding:4px 8px;font-size:12px'});
            [['admin','Admin'],['manager','Manager'],['user','User']].forEach(([v,l])=>{ const o=el('option',{value:v},l); if(v===role) o.selected=true; roleSel.appendChild(o); });
            roleSel.addEventListener('change', async ()=>{
              try { await apiClient.auth.updateUserRole(user.userId, roleSel.value); notifier?.show({message:'Role updated.',variant:'success'}); await reload(); }
              catch(err){ notifier?.show({message:mapApiError(err).message,variant:'danger'}); }
            });
            roleCell.appendChild(roleSel);
          } else {
            roleCell.appendChild(el('span',{class:'tm-pill tm-pill-blue'},roleLabel));
          }

          const statusCell = el('td',{},el('span',{class:'tm-pill tm-pill-green'},'Active'));

          const actCell = el('td',{});
          if (!isSelf && canChangeRole) {
            const rmBtn = el('button',{class:'tm-btn tm-btn-danger tm-btn-sm'},'Remove');
            rmBtn.addEventListener('click', async ()=>{
              if (!confirm(`Remove ${user.displayName||user.email} from the team?`)) return;
              rmBtn.disabled=true;
              try { await apiClient.auth.removeUser(user.userId); notifier?.show({message:'User removed.',variant:'success'}); await reload(); }
              catch(err){ notifier?.show({message:mapApiError(err).message,variant:'danger'}); rmBtn.disabled=false; }
            });
            actCell.appendChild(rmBtn);
          } else if (isSelf) {
            actCell.appendChild(el('span',{style:'font-size:11.5px;color:#94a3b8'},'You'));
          }

          tr.append(userCell, roleCell, statusCell, actCell);
          tbody.appendChild(tr);
        });
        table.appendChild(tbody);
        tableWrap.appendChild(table);
        membersBody.replaceChildren(tableWrap);
      }
    } catch(err) {
      membersBody.replaceChildren(el('div',{style:'color:#dc2626;font-size:12.5px;padding:16px 20px'},mapApiError(err).message));
    }

    try {
      const invites = await apiClient.auth.listInvites();
      const inviteList = Array.isArray(invites) ? invites : [];
      const pending = inviteList.filter(i=>getInviteStatus(i)==='Pending');
      hPending.textContent = String(pending.length);
      const actorRole = normalizeRole(currentUser?.roles||[]);

      if (!inviteList.length) { invitesBody.replaceChildren(el('div',{class:'tm-empty'},'No invitations yet.')); }
      else {
        const tableWrap = el('div',{class:'tm-table-wrap'});
        const table = el('table',{class:'tm-table'});
        table.appendChild(el('thead',{},el('tr',{}, ...['Email','Role','Status','Invited','Actions'].map(c=>el('th',{},c)))));
        const tbody = el('tbody',{});

        inviteList.forEach(invite => {
          const status = getInviteStatus(invite);
          const role   = invite.role||'user';
          const statusPillCls = status==='Accepted'?'tm-pill-green':status==='Pending'?'tm-pill-amber':status==='Revoked'?'tm-pill-red':'tm-pill-gray';

          const tr = el('tr',{});
          tr.append(
            el('td',{style:'font-family:JetBrains Mono,monospace;font-size:11.5px'},invite.email||'—'),
            el('td',{},el('span',{class:'tm-pill tm-pill-blue'},ROLE_LABELS[role]||role)),
            el('td',{},el('span',{class:`tm-pill ${statusPillCls}`},status)),
            el('td',{style:'font-size:11.5px;color:#94a3b8'},invite.createdAtUtc?new Date(invite.createdAtUtc).toLocaleDateString('en-GB',{day:'numeric',month:'short',year:'numeric'}):'—')
          );

          const actCell = el('td',{});
          if (status==='Pending' && capabilities?.canInviteRole?.(actorRole, role)) {
            const revokeBtn = el('button',{class:'tm-btn tm-btn-outline tm-btn-sm'},'Revoke');
            revokeBtn.addEventListener('click', async ()=>{
              revokeBtn.disabled=true;
              try { await apiClient.auth.revokeInvite(invite.inviteId); notifier?.show({message:'Invite revoked.',variant:'success'}); await reload(); }
              catch(err){ notifier?.show({message:mapApiError(err).message,variant:'danger'}); revokeBtn.disabled=false; }
            });
            actCell.appendChild(revokeBtn);
          }
          tr.appendChild(actCell);
          tbody.appendChild(tr);
        });
        table.appendChild(tbody); tableWrap.appendChild(table);
        invitesBody.replaceChildren(tableWrap);
      }
    } catch(err) {
      invitesBody.replaceChildren(el('div',{style:'color:#dc2626;font-size:12.5px;padding:16px 20px'},mapApiError(err).message));
    }
  };

  // ── Invite button ──────────────────────────────────────────────────────────
  inviteBtn.addEventListener('click', async () => {
    emailErr.textContent='';
    const email = emailInput.value.trim();
    const role  = roleSelect.value;
    const actorRole = normalizeRole(currentUser?.roles||[]);
    if (!email) { emailErr.textContent='Email is required.'; return; }
    if (capabilities && !capabilities.canInviteRole?.(actorRole, role)) { emailErr.textContent='You cannot invite this role.'; return; }

    inviteBtn.disabled=true; inviteBtn.textContent='⏳ Sending…';
    try {
      await apiClient.auth.createInvite({ email, role });
      emailInput.value='';
      toast?.show({message:'Invitation sent.',variant:'success'});
      await reload();
    } catch(err) {
      toast?.show({message:mapApiError(err).message,variant:'danger'});
    } finally { inviteBtn.disabled=false; inviteBtn.textContent='📧 Send Invite'; }
  });

  emailInput.addEventListener('keydown', e=>{ if(e.key==='Enter') inviteBtn.click(); });

  await reload();
};
