import { createApiClient, mapApiError } from '../shared/apiClient.js';
import { createToastManager } from '../shared/ui/index.js';

const client = createApiClient();
const toast = createToastManager();

const TRIGGER_TYPES = [
  { value: 'CollectorPageView', label: 'Page View' },
  { value: 'IntelligenceTrendsUpdated', label: 'Intelligence Trends Updated' },
  { value: 'engage_lead_captured', label: 'Lead Captured' },
  { value: 'engage_ticket_created', label: 'Ticket Created' },
  { value: 'engage_conversation_completed', label: 'Conversation Completed' },
  { value: 'visitor_return', label: 'Return Visitor' },
  { value: 'exit_intent', label: 'Exit Intent' },
];

const OPERATOR_OPTIONS = [
  { value: 1, label: 'Equals' },
  { value: 2, label: 'Contains' },
];

const ACTION_TYPES = [
  { value: 'LogRun',                  label: 'Log Event' },
  { value: 'SendWebhook',             label: 'Send Webhook' },
  { value: 'SendSlackNotification',   label: 'Send Slack Notification' },
  { value: 'SendEmail',               label: 'Send Email' },
  { value: 'CreateTicket',            label: 'Create Ticket' },
  { value: 'UpdateLeadStage',         label: 'Update Lead Stage' },
  { value: 'TagLead',                 label: 'Tag Lead' },
  { value: 'AddNote',                 label: 'Add Note to Ticket' },
  { value: 'NotifyTeam',              label: 'Notify Team' },
];

const TAG_LEAD_LABELS = [
  { value: 'evaluating', label: 'Evaluating' },
  { value: 'deciding',   label: 'Deciding' },
  { value: 'won',        label: 'Won' },
  { value: 'lost',       label: 'Lost' },
];

// ── Modal helpers ────────────────────────────────────────────────────────────

function createModal(title) {
  const overlay = document.createElement('div');
  overlay.style.cssText =
    'position:fixed;inset:0;background:rgba(0,0,0,0.45);z-index:1000;display:flex;align-items:center;justify-content:center;padding:24px;';

  const dialog = document.createElement('div');
  dialog.className = 'card';
  dialog.style.cssText = 'width:100%;max-width:620px;max-height:90vh;overflow-y:auto;padding:28px;';

  const hdr = document.createElement('div');
  hdr.style.cssText = 'display:flex;align-items:center;justify-content:space-between;margin-bottom:20px;';
  const h = document.createElement('h3');
  h.style.cssText = 'margin:0;font-size:16px;font-weight:600;color:var(--color-text);';
  h.textContent = title;
  const closeBtn = document.createElement('button');
  closeBtn.className = 'btn btn-secondary btn-sm';
  closeBtn.textContent = '✕';
  hdr.append(h, closeBtn);
  dialog.appendChild(hdr);

  overlay.appendChild(dialog);

  const close = () => overlay.remove();
  closeBtn.addEventListener('click', close);
  overlay.addEventListener('click', (e) => { if (e.target === overlay) close(); });

  document.body.appendChild(overlay);
  return { overlay, dialog, close };
}

function formGroup(labelText, el) {
  const g = document.createElement('div');
  g.className = 'form-group';
  const lbl = document.createElement('label');
  lbl.className = 'form-label';
  lbl.textContent = labelText;
  g.append(lbl, el);
  return g;
}

function makeInput(placeholder = '') {
  const el = document.createElement('input');
  el.className = 'form-input';
  el.placeholder = placeholder;
  return el;
}

function makeSelect(options) {
  const el = document.createElement('select');
  el.className = 'form-select';
  options.forEach(({ value, label }) => {
    const opt = document.createElement('option');
    opt.value = value;
    opt.textContent = label;
    el.appendChild(opt);
  });
  return el;
}

// ── Conditions / Actions dynamic lists ───────────────────────────────────────

function makeConditionsSection(initial = []) {
  const wrap = document.createElement('div');
  const listEl = document.createElement('div');
  listEl.style.cssText = 'display:flex;flex-direction:column;gap:8px;margin-bottom:8px;';

  const addBtn = document.createElement('button');
  addBtn.className = 'btn btn-secondary btn-sm';
  addBtn.textContent = '+ Add Condition';
  wrap.append(listEl, addBtn);

  const items = [];

  const addCondition = (cond = null) => {
    const row = document.createElement('div');
    row.style.cssText = 'display:flex;gap:8px;align-items:center;';

    const fieldInput = makeInput('Field (e.g. url, keyword)');
    fieldInput.className = 'form-input';
    fieldInput.style.flex = '1';
    if (cond?.field) fieldInput.value = cond.field;

    const opSelect = makeSelect(OPERATOR_OPTIONS);
    if (cond?.operator) opSelect.value = cond.operator;

    const valInput = makeInput('Value');
    valInput.className = 'form-input';
    valInput.style.flex = '1';
    if (cond?.value) valInput.value = cond.value;

    const del = document.createElement('button');
    del.className = 'btn btn-danger btn-sm';
    del.textContent = '✕';

    row.append(fieldInput, opSelect, valInput, del);
    listEl.appendChild(row);

    const entry = { row, fieldInput, opSelect, valInput };
    items.push(entry);
    del.addEventListener('click', () => {
      row.remove();
      items.splice(items.indexOf(entry), 1);
    });
  };

  initial.forEach(addCondition);
  addBtn.addEventListener('click', () => addCondition());

  const getValues = () =>
    items.map((item) => ({
      field: item.fieldInput.value.trim(),
      operator: parseInt(item.opSelect.value, 10),
      value: item.valInput.value.trim(),
    })).filter((c) => c.field && c.value);

  return { wrap, getValues };
}

function makeActionRow(action = null, onRemove) {
  const row = document.createElement('div');
  row.style.cssText = 'display:flex;flex-direction:column;gap:8px;padding:12px;border:1px solid var(--color-border);border-radius:6px;background:var(--color-surface,#fff);';

  const top = document.createElement('div');
  top.style.cssText = 'display:flex;align-items:center;gap:8px;';

  const typeSelect = makeSelect(ACTION_TYPES);
  typeSelect.style.flex = '1';
  if (action?.actionType) typeSelect.value = action.actionType;

  const delBtn = document.createElement('button');
  delBtn.className = 'btn btn-danger btn-sm';
  delBtn.textContent = '✕';
  delBtn.addEventListener('click', onRemove);

  top.append(typeSelect, delBtn);
  row.appendChild(top);

  // Extra fields container — shown/hidden depending on type
  const extras = document.createElement('div');
  extras.style.cssText = 'display:flex;flex-direction:column;gap:8px;';
  row.appendChild(extras);

  // Build extra fields for the current type
  let webhookUrlInput = null;
  let slackWebhookUrlInput = null;
  let slackMessageTextarea = null;
  let emailToInput = null;
  let emailSubjectInput = null;
  let emailBodyTextarea = null;
  let ticketSubjectInput = null;
  let ticketDescTextarea = null;
  let updateLeadEmailInput = null;
  let updateLeadLabelSelect = null;
  let leadIdInput = null;
  let leadLabelSelect = null;
  let noteTicketIdInput = null;
  let noteTextarea = null;
  let notifyMessageTextarea = null;
  let notifyRecipientsInput = null;

  const renderExtras = (type) => {
    extras.innerHTML = '';
    webhookUrlInput = slackWebhookUrlInput = slackMessageTextarea = null;
    emailToInput = emailSubjectInput = emailBodyTextarea = null;
    ticketSubjectInput = ticketDescTextarea = null;
    updateLeadEmailInput = updateLeadLabelSelect = null;
    leadIdInput = leadLabelSelect = null;
    noteTicketIdInput = noteTextarea = null;
    notifyMessageTextarea = notifyRecipientsInput = null;

    if (type === 'SendWebhook') {
      webhookUrlInput = makeInput('Webhook URL (https://…)');
      if (action?.params?.url) webhookUrlInput.value = action.params.url;
      extras.appendChild(formGroup('URL', webhookUrlInput));
    } else if (type === 'SendSlackNotification') {
      slackWebhookUrlInput = makeInput('Slack Webhook URL (https://hooks.slack.com/…)');
      if (action?.params?.webhookUrl) slackWebhookUrlInput.value = action.params.webhookUrl;
      extras.appendChild(formGroup('Webhook URL', slackWebhookUrlInput));
      slackMessageTextarea = document.createElement('textarea');
      slackMessageTextarea.className = 'form-input';
      slackMessageTextarea.placeholder = 'Message text';
      slackMessageTextarea.rows = 2;
      slackMessageTextarea.style.resize = 'vertical';
      if (action?.params?.message) slackMessageTextarea.value = action.params.message;
      extras.appendChild(formGroup('Message', slackMessageTextarea));
    } else if (type === 'SendEmail') {
      emailToInput = makeInput('To (email address)');
      if (action?.params?.to) emailToInput.value = action.params.to;
      extras.appendChild(formGroup('To', emailToInput));
      emailSubjectInput = makeInput('Subject');
      if (action?.params?.subject) emailSubjectInput.value = action.params.subject;
      extras.appendChild(formGroup('Subject', emailSubjectInput));
      emailBodyTextarea = document.createElement('textarea');
      emailBodyTextarea.className = 'form-input';
      emailBodyTextarea.placeholder = 'Email body (optional)';
      emailBodyTextarea.rows = 3;
      emailBodyTextarea.style.resize = 'vertical';
      if (action?.params?.body) emailBodyTextarea.value = action.params.body;
      extras.appendChild(formGroup('Body', emailBodyTextarea));
    } else if (type === 'CreateTicket') {
      ticketSubjectInput = makeInput('Ticket subject');
      if (action?.params?.subject) ticketSubjectInput.value = action.params.subject;
      extras.appendChild(formGroup('Subject', ticketSubjectInput));
      ticketDescTextarea = document.createElement('textarea');
      ticketDescTextarea.className = 'form-input';
      ticketDescTextarea.placeholder = 'Description (optional)';
      ticketDescTextarea.rows = 2;
      ticketDescTextarea.style.resize = 'vertical';
      if (action?.params?.description) ticketDescTextarea.value = action.params.description;
      extras.appendChild(formGroup('Description', ticketDescTextarea));
    } else if (type === 'UpdateLeadStage') {
      updateLeadEmailInput = makeInput('Lead email address');
      if (action?.params?.email) updateLeadEmailInput.value = action.params.email;
      extras.appendChild(formGroup('Email', updateLeadEmailInput));
      updateLeadLabelSelect = makeSelect(TAG_LEAD_LABELS);
      if (action?.params?.label) updateLeadLabelSelect.value = action.params.label;
      extras.appendChild(formGroup('Stage', updateLeadLabelSelect));
    } else if (type === 'TagLead') {
      leadIdInput = makeInput('Lead ID (GUID)');
      if (action?.params?.leadId) leadIdInput.value = action.params.leadId;
      extras.appendChild(formGroup('Lead ID', leadIdInput));
      leadLabelSelect = makeSelect(TAG_LEAD_LABELS);
      if (action?.params?.label) leadLabelSelect.value = action.params.label;
      extras.appendChild(formGroup('Label', leadLabelSelect));
    } else if (type === 'AddNote') {
      noteTicketIdInput = makeInput('Ticket ID (GUID)');
      if (action?.params?.ticketId) noteTicketIdInput.value = action.params.ticketId;
      extras.appendChild(formGroup('Ticket ID', noteTicketIdInput));
      noteTextarea = document.createElement('textarea');
      noteTextarea.className = 'form-input';
      noteTextarea.placeholder = 'Note content';
      noteTextarea.rows = 2;
      noteTextarea.style.resize = 'vertical';
      if (action?.params?.note) noteTextarea.value = action.params.note;
      extras.appendChild(formGroup('Note', noteTextarea));
    } else if (type === 'NotifyTeam') {
      notifyMessageTextarea = document.createElement('textarea');
      notifyMessageTextarea.className = 'form-input';
      notifyMessageTextarea.placeholder = 'Message to send to team';
      notifyMessageTextarea.rows = 2;
      notifyMessageTextarea.style.resize = 'vertical';
      if (action?.params?.message) notifyMessageTextarea.value = action.params.message;
      extras.appendChild(formGroup('Message', notifyMessageTextarea));
      notifyRecipientsInput = makeInput('Recipient emails (comma-separated, optional)');
      if (action?.params?.recipientEmails) notifyRecipientsInput.value = action.params.recipientEmails;
      extras.appendChild(formGroup('Recipients', notifyRecipientsInput));
    }
  };

  renderExtras(typeSelect.value);
  typeSelect.addEventListener('change', () => renderExtras(typeSelect.value));

  const getValues = () => {
    const type = typeSelect.value;
    if (type === 'LogRun') return { actionType: 'LogRun', params: null };
    if (type === 'SendWebhook') return { actionType: 'SendWebhook', params: { url: webhookUrlInput?.value.trim() || '' } };
    if (type === 'SendSlackNotification') return {
      actionType: 'SendSlackNotification',
      params: {
        webhookUrl: slackWebhookUrlInput?.value.trim() || '',
        message: slackMessageTextarea?.value.trim() || '',
      },
    };
    if (type === 'SendEmail') return {
      actionType: 'SendEmail',
      params: {
        to: emailToInput?.value.trim() || '',
        subject: emailSubjectInput?.value.trim() || '',
        body: emailBodyTextarea?.value.trim() || '',
      },
    };
    if (type === 'CreateTicket') return {
      actionType: 'CreateTicket',
      params: {
        subject: ticketSubjectInput?.value.trim() || '',
        description: ticketDescTextarea?.value.trim() || '',
      },
    };
    if (type === 'UpdateLeadStage') return {
      actionType: 'UpdateLeadStage',
      params: {
        email: updateLeadEmailInput?.value.trim() || '',
        label: updateLeadLabelSelect?.value || 'evaluating',
      },
    };
    if (type === 'TagLead') return {
      actionType: 'TagLead',
      params: {
        leadId: leadIdInput?.value.trim() || '',
        label: leadLabelSelect?.value || 'evaluating',
      },
    };
    if (type === 'AddNote') return {
      actionType: 'AddNote',
      params: {
        ticketId: noteTicketIdInput?.value.trim() || '',
        note: noteTextarea?.value.trim() || '',
      },
    };
    if (type === 'NotifyTeam') return {
      actionType: 'NotifyTeam',
      params: {
        message: notifyMessageTextarea?.value.trim() || '',
        recipientEmails: notifyRecipientsInput?.value.trim() || '',
      },
    };
    return { actionType: type, params: null };
  };

  return { row, getValues };
}

function makeActionsSection(initial = []) {
  const wrap = document.createElement('div');
  wrap.style.cssText = 'display:flex;flex-direction:column;gap:8px;';

  const listEl = document.createElement('div');
  listEl.style.cssText = 'display:flex;flex-direction:column;gap:8px;';
  wrap.appendChild(listEl);

  const addBtn = document.createElement('button');
  addBtn.className = 'btn btn-secondary btn-sm';
  addBtn.textContent = '+ Add Action';
  addBtn.style.alignSelf = 'flex-start';
  wrap.appendChild(addBtn);

  const rows = [];

  const addActionRow = (action = null) => {
    const remove = () => {
      entry.row.remove();
      rows.splice(rows.indexOf(entry), 1);
    };
    const entry = makeActionRow(action, remove);
    listEl.appendChild(entry.row);
    rows.push(entry);
  };

  const seed = initial.length ? initial : [null];
  seed.forEach((a) => addActionRow(a));

  addBtn.addEventListener('click', () => addActionRow());

  const getValues = () => rows.map((r) => r.getValues());

  return { wrap, getValues };
}

// ── Flow form modal ───────────────────────────────────────────────────────────

async function openFlowModal(siteId, existing = null, onSaved) {
  const isEdit = Boolean(existing?.id);
  const { dialog, close } = createModal(isEdit ? 'Edit Flow' : 'Create Flow');

  const body = document.createElement('div');
  body.style.cssText = 'display:flex;flex-direction:column;gap:16px;';

  // Name
  const nameInput = makeInput('Flow name');
  if (existing?.name) nameInput.value = existing.name;
  body.appendChild(formGroup('Name', nameInput));

  // Enabled (edit only)
  let enabledCheck = null;
  if (isEdit) {
    const enabledRow = document.createElement('div');
    enabledRow.style.cssText = 'display:flex;align-items:center;gap:8px;';
    enabledCheck = document.createElement('input');
    enabledCheck.type = 'checkbox';
    enabledCheck.id = 'flow-enabled';
    enabledCheck.checked = existing.enabled !== false;
    const enabledLabel = document.createElement('label');
    enabledLabel.htmlFor = 'flow-enabled';
    enabledLabel.textContent = 'Enabled';
    enabledLabel.style.fontSize = '13px';
    enabledRow.append(enabledCheck, enabledLabel);
    body.appendChild(formGroup('Status', enabledRow));
  }

  // Trigger
  const triggerSelect = makeSelect(TRIGGER_TYPES);
  if (existing?.trigger?.triggerType) triggerSelect.value = existing.trigger.triggerType;
  body.appendChild(formGroup('Trigger', triggerSelect));

  // Priority
  const priorityInput = document.createElement('input');
  priorityInput.type = 'number';
  priorityInput.className = 'form-input';
  priorityInput.placeholder = '0';
  priorityInput.min = '0';
  priorityInput.max = '100';
  priorityInput.value = existing?.priority ?? 0;
  priorityInput.style.width = '120px';
  body.appendChild(formGroup('Priority (0–100, higher runs first)', priorityInput));

  // Max Runs Per Hour
  const maxRunsInput = document.createElement('input');
  maxRunsInput.type = 'number';
  maxRunsInput.className = 'form-input';
  maxRunsInput.placeholder = 'Unlimited';
  maxRunsInput.min = '1';
  maxRunsInput.value = existing?.maxRunsPerHour ?? '';
  maxRunsInput.style.width = '120px';
  body.appendChild(formGroup('Max runs per hour (blank = unlimited)', maxRunsInput));

  // Conditions
  const condSection = document.createElement('div');
  condSection.innerHTML = '<div class="form-label" style="margin-bottom:6px;">Conditions <span style="font-weight:400;font-size:11px;color:var(--color-text-muted)">(all must match)</span></div>';
  const { wrap: condWrap, getValues: getConditions } = makeConditionsSection(existing?.conditions || []);
  condSection.appendChild(condWrap);
  body.appendChild(condSection);

  // Actions
  const actSection = document.createElement('div');
  actSection.innerHTML = '<div class="form-label" style="margin-bottom:6px;">Actions</div>';
  const { wrap: actWrap, getValues: getActions } = makeActionsSection(existing?.actions || []);
  actSection.appendChild(actWrap);
  body.appendChild(actSection);

  // Error
  const errEl = document.createElement('div');
  errEl.style.cssText = 'font-size:13px;color:var(--color-danger);display:none;';
  body.appendChild(errEl);

  // Buttons
  const footer = document.createElement('div');
  footer.style.cssText = 'display:flex;gap:8px;justify-content:flex-end;margin-top:8px;';
  const cancelBtn = document.createElement('button');
  cancelBtn.className = 'btn btn-secondary';
  cancelBtn.textContent = 'Cancel';
  cancelBtn.addEventListener('click', close);
  const saveBtn = document.createElement('button');
  saveBtn.className = 'btn btn-primary';
  saveBtn.textContent = isEdit ? 'Save Changes' : 'Create Flow';
  footer.append(cancelBtn, saveBtn);

  dialog.append(body, footer);

  saveBtn.addEventListener('click', async () => {
    const name = nameInput.value.trim();
    if (!name) { errEl.textContent = 'Name is required.'; errEl.style.display = ''; return; }

    const conditions = getConditions();
    const actions = getActions();
    if (!actions.length) { errEl.textContent = 'At least one action is required.'; errEl.style.display = ''; return; }

    const priority = parseInt(priorityInput.value, 10) || 0;
    const maxRunsRaw = parseInt(maxRunsInput.value, 10);
    const maxRunsPerHour = maxRunsRaw > 0 ? maxRunsRaw : null;

    saveBtn.disabled = true;
    saveBtn.textContent = 'Saving…';
    errEl.style.display = 'none';

    try {
      if (isEdit) {
        await client.flows.update(existing.id, {
          name,
          enabled: enabledCheck ? enabledCheck.checked : existing.enabled,
          trigger: { triggerType: triggerSelect.value, filters: null },
          conditions,
          actions,
          priority,
          maxRunsPerHour,
        });
      } else {
        await client.flows.create({
          siteId,
          name,
          trigger: { triggerType: triggerSelect.value, filters: null },
          conditions,
          actions,
          priority,
          maxRunsPerHour,
        });
      }
      close();
      onSaved();
    } catch (err) {
      const uiError = mapApiError(err);
      errEl.textContent = uiError.message;
      errEl.style.display = '';
      saveBtn.disabled = false;
      saveBtn.textContent = isEdit ? 'Save Changes' : 'Create Flow';
    }
  });
}

// ── Run History modal ─────────────────────────────────────────────────────────

async function openRunHistoryModal(flowId, flowName) {
  const { dialog } = createModal(`Run History — ${flowName}`);

  const loading = document.createElement('div');
  loading.style.cssText = 'text-align:center;padding:24px;color:var(--color-text-muted);';
  loading.textContent = 'Loading…';
  dialog.appendChild(loading);

  try {
    const runs = await client.flows.listRuns(flowId, 100);
    loading.remove();

    if (!runs || runs.length === 0) {
      const empty = document.createElement('div');
      empty.className = 'empty-state';
      empty.innerHTML = '<div class="empty-state-icon">📋</div><div class="empty-state-title">No runs yet</div><div class="empty-state-desc">This flow has not fired yet.</div>';
      dialog.appendChild(empty);
      return;
    }

    // Filter controls
    const filterBar = document.createElement('div');
    filterBar.style.cssText = 'display:flex;gap:12px;margin-bottom:12px;align-items:center;flex-wrap:wrap;';

    const statusFilter = makeSelect([
      { value: '', label: 'All statuses' },
      { value: 'Succeeded', label: 'Succeeded' },
      { value: 'Failed', label: 'Failed' },
    ]);
    statusFilter.style.maxWidth = '160px';

    const dateFilter = makeSelect([
      { value: '', label: 'All time' },
      { value: '24h', label: 'Last 24 hours' },
      { value: '7d', label: 'Last 7 days' },
      { value: '30d', label: 'Last 30 days' },
    ]);
    dateFilter.style.maxWidth = '160px';

    filterBar.append(statusFilter, dateFilter);
    dialog.appendChild(filterBar);

    const wrap = document.createElement('div');
    wrap.className = 'table-wrapper';
    dialog.appendChild(wrap);

    const renderTable = () => {
      const statusVal = statusFilter.value;
      const dateVal = dateFilter.value;
      const now = Date.now();
      const cutoffMs = dateVal === '24h' ? 24 * 60 * 60 * 1000
        : dateVal === '7d' ? 7 * 24 * 60 * 60 * 1000
        : dateVal === '30d' ? 30 * 24 * 60 * 60 * 1000
        : 0;

      const filtered = runs.filter((run) => {
        if (statusVal && run.status !== statusVal) return false;
        if (cutoffMs > 0 && run.executedAtUtc) {
          const runMs = new Date(run.executedAtUtc).getTime();
          if (now - runMs > cutoffMs) return false;
        }
        return true;
      });

      wrap.innerHTML = '';
      if (filtered.length === 0) {
        wrap.innerHTML = '<div style="padding:16px;color:var(--color-text-muted);text-align:center;">No runs match the current filters.</div>';
        return;
      }

      const table = document.createElement('table');
      table.className = 'data-table';
      table.innerHTML = '<thead><tr><th>Executed</th><th>Trigger</th><th>Status</th><th>Summary</th></tr></thead>';
      const tbody = document.createElement('tbody');
      filtered.forEach((run) => {
        const tr = document.createElement('tr');
        const statusClass = run.status === 'Succeeded' ? 'badge-success' : 'badge-danger';
        const date = run.executedAtUtc ? new Date(run.executedAtUtc).toLocaleString() : '—';
        tr.innerHTML = `
          <td style="white-space:nowrap;">${date}</td>
          <td><span class="badge badge-info">${run.triggerType || '—'}</span></td>
          <td><span class="badge ${statusClass}">${run.status || '—'}</span></td>
          <td style="font-size:12px;color:var(--color-text-muted);max-width:200px;word-break:break-word;">${run.triggerSummary || run.errorMessage || '—'}</td>
        `;
        tbody.appendChild(tr);
      });
      table.appendChild(tbody);
      wrap.appendChild(table);
    };

    statusFilter.addEventListener('change', renderTable);
    dateFilter.addEventListener('change', renderTable);
    renderTable();
  } catch (err) {
    loading.textContent = 'Failed to load run history.';
  }
}

// ── Flow card ────────────────────────────────────────────────────────────────

function buildFlowCard(flow, siteId, onRefresh) {
  const card = document.createElement('div');
  card.className = 'card';
  card.style.cssText = 'display:flex;flex-direction:column;gap:12px;';

  // Header row
  const hdr = document.createElement('div');
  hdr.style.cssText = 'display:flex;align-items:center;justify-content:space-between;gap:12px;';

  const left = document.createElement('div');
  left.style.cssText = 'display:flex;align-items:center;gap:10px;min-width:0;';

  const toggle = document.createElement('input');
  toggle.type = 'checkbox';
  toggle.checked = flow.enabled !== false;
  toggle.title = flow.enabled ? 'Disable flow' : 'Enable flow';
  toggle.addEventListener('change', async () => {
    try {
      if (toggle.checked) {
        await client.flows.enable(flow.id);
      } else {
        await client.flows.disable(flow.id);
      }
      toast.show({ message: `Flow ${toggle.checked ? 'enabled' : 'disabled'}.`, variant: 'success' });
    } catch (err) {
      toggle.checked = !toggle.checked;
      toast.show({ message: mapApiError(err).message, variant: 'error' });
    }
  });

  const nameEl = document.createElement('div');
  nameEl.style.cssText = 'font-weight:600;font-size:14px;color:var(--color-text);white-space:nowrap;overflow:hidden;text-overflow:ellipsis;';
  nameEl.textContent = flow.name;

  const triggerBadge = document.createElement('span');
  triggerBadge.className = 'badge badge-info';
  const triggerLabel = TRIGGER_TYPES.find((t) => t.value === flow.triggerType)?.label || flow.triggerType || '—';
  triggerBadge.textContent = triggerLabel;

  left.append(toggle, nameEl, triggerBadge);

  const actions = document.createElement('div');
  actions.style.cssText = 'display:flex;gap:6px;flex-shrink:0;';

  const runsBtn = document.createElement('button');
  runsBtn.className = 'btn btn-secondary btn-sm';
  runsBtn.textContent = 'Runs';
  runsBtn.addEventListener('click', () => openRunHistoryModal(flow.id, flow.name));

  const editBtn = document.createElement('button');
  editBtn.className = 'btn btn-secondary btn-sm';
  editBtn.textContent = 'Edit';
  editBtn.addEventListener('click', async () => {
    try {
      const detail = await client.flows.get(flow.id);
      openFlowModal(siteId, detail, onRefresh);
    } catch (err) {
      toast.show({ message: mapApiError(err).message, variant: 'error' });
    }
  });

  actions.append(runsBtn, editBtn);
  hdr.append(left, actions);

  // Stats row
  const stats = document.createElement('div');
  stats.style.cssText = 'display:flex;gap:16px;font-size:12px;color:var(--color-text-muted);flex-wrap:wrap;align-items:center;';
  const priorityBadge = flow.priority > 0 ? `<span class="badge badge-info">Priority: ${flow.priority}</span>` : '';
  const rateBadge = flow.maxRunsPerHour ? `<span class="badge badge-neutral">Max ${flow.maxRunsPerHour}/hr</span>` : '';
  stats.innerHTML = `
    <span>${flow.conditionsCount ?? 0} condition${flow.conditionsCount === 1 ? '' : 's'}</span>
    <span>${flow.actionsCount ?? 0} action${flow.actionsCount === 1 ? '' : 's'}</span>
    <span class="badge ${flow.enabled ? 'badge-success' : 'badge-neutral'}">${flow.enabled ? 'Active' : 'Disabled'}</span>
    ${priorityBadge}${rateBadge}
  `;

  card.append(hdr, stats);
  return card;
}

// ── Main view ────────────────────────────────────────────────────────────────

export async function renderFlowsView(container, { client: _c, toast: _t } = {}) {
  container.innerHTML = '';

  const page = document.createElement('div');
  page.style.cssText = 'display:flex;flex-direction:column;gap:24px;width:100%;';

  // Page header
  const pageHeader = document.createElement('div');
  pageHeader.className = 'page-header';
  const titleWrap = document.createElement('div');
  const title = document.createElement('h2');
  title.className = 'page-title';
  title.textContent = 'Flows';
  const subtitle = document.createElement('div');
  subtitle.className = 'page-subtitle';
  subtitle.textContent = 'Automate actions triggered by visitor events and intelligence updates.';
  titleWrap.append(title, subtitle);

  const headerBtns = document.createElement('div');
  headerBtns.style.cssText = 'display:flex;gap:8px;';

  const templateBtn = document.createElement('button');
  templateBtn.className = 'btn btn-secondary';
  templateBtn.textContent = '⚡ Use Template';

  const createBtn = document.createElement('button');
  createBtn.className = 'btn btn-primary';
  createBtn.textContent = '+ Create Flow';

  headerBtns.append(templateBtn, createBtn);
  pageHeader.append(titleWrap, headerBtns);
  page.appendChild(pageHeader);

  // Site selector
  let sites = [];
  let selectedSiteId = '';

  const selectorWrap = document.createElement('div');
  selectorWrap.style.cssText = 'display:flex;align-items:center;gap:12px;';
  const siteLabel = document.createElement('label');
  siteLabel.className = 'form-label';
  siteLabel.textContent = 'Site:';
  siteLabel.style.marginBottom = '0';
  const siteSelect = makeSelect([{ value: '', label: 'Loading sites…' }]);
  siteSelect.style.maxWidth = '300px';
  selectorWrap.append(siteLabel, siteSelect);
  page.appendChild(selectorWrap);

  // Flow list area
  const listArea = document.createElement('div');
  listArea.style.cssText = 'display:flex;flex-direction:column;gap:12px;';
  page.appendChild(listArea);

  container.appendChild(page);

  const loadFlows = async () => {
    if (!selectedSiteId) {
      listArea.innerHTML = '';
      return;
    }
    listArea.innerHTML = '<div style="color:var(--color-text-muted);padding:16px;">Loading…</div>';
    try {
      const flows = await client.flows.list(selectedSiteId);
      listArea.innerHTML = '';
      if (!flows || flows.length === 0) {
        const empty = document.createElement('div');
        empty.className = 'empty-state';
        empty.innerHTML = `
          <div class="empty-state-icon">⚡</div>
          <div class="empty-state-title">No flows yet</div>
          <div class="empty-state-desc">Create your first flow to automate actions when visitors arrive or intelligence updates.</div>
        `;
        const emptyBtn = document.createElement('button');
        emptyBtn.className = 'btn btn-primary';
        emptyBtn.style.marginTop = '12px';
        emptyBtn.textContent = '+ Create Flow';
        emptyBtn.addEventListener('click', () => openFlowModal(selectedSiteId, null, loadFlows));
        empty.appendChild(emptyBtn);
        listArea.appendChild(empty);
      } else {
        flows.forEach((flow) => {
          listArea.appendChild(buildFlowCard(flow, selectedSiteId, loadFlows));
        });
      }
    } catch (err) {
      listArea.innerHTML = '';
      const errEl = document.createElement('div');
      errEl.style.cssText = 'color:var(--color-danger);padding:16px;';
      errEl.textContent = mapApiError(err).message;
      listArea.appendChild(errEl);
    }
  };

  // Load sites
  try {
    sites = await client.sites.list();
    if (!Array.isArray(sites) || sites.length === 0) {
      siteSelect.innerHTML = '<option value="">No sites available</option>';
    } else {
      siteSelect.innerHTML = '';
      sites.forEach((site) => {
        const opt = document.createElement('option');
        opt.value = site.siteId || site.id || '';
        opt.textContent = site.name || site.domain || opt.value;
        siteSelect.appendChild(opt);
      });
      selectedSiteId = siteSelect.value;
      await loadFlows();
    }
  } catch (err) {
    siteSelect.innerHTML = '<option value="">Failed to load sites</option>';
  }

  siteSelect.addEventListener('change', () => {
    selectedSiteId = siteSelect.value;
    loadFlows();
  });

  templateBtn.addEventListener('click', async () => {
    if (!selectedSiteId) {
      toast.show({ message: 'Select a site first.', variant: 'warning' });
      return;
    }
    try {
      const templates = await client.flows.getTemplates();
      openTemplateModal(templates, selectedSiteId, loadFlows);
    } catch (err) {
      toast.show({ message: mapApiError(err).message, variant: 'error' });
    }
  });

  createBtn.addEventListener('click', () => {
    if (!selectedSiteId) {
      toast.show({ message: 'Select a site first.', variant: 'warning' });
      return;
    }
    openFlowModal(selectedSiteId, null, loadFlows);
  });
}

function openTemplateModal(templates, siteId, onSaved) {
  const { dialog, close } = createModal('Flow Templates');

  if (!templates || templates.length === 0) {
    const empty = document.createElement('div');
    empty.className = 'empty-state';
    empty.textContent = 'No templates available.';
    dialog.appendChild(empty);
    return;
  }

  const list = document.createElement('div');
  list.style.cssText = 'display:flex;flex-direction:column;gap:12px;';

  templates.forEach((tmpl) => {
    const card = document.createElement('div');
    card.style.cssText = 'padding:14px;border:1px solid var(--color-border);border-radius:6px;display:flex;align-items:flex-start;justify-content:space-between;gap:12px;';

    const info = document.createElement('div');
    info.style.flex = '1';

    const tname = document.createElement('div');
    tname.style.cssText = 'font-weight:600;font-size:13px;color:var(--color-text);margin-bottom:4px;';
    tname.textContent = tmpl.name;

    const tdesc = document.createElement('div');
    tdesc.style.cssText = 'font-size:12px;color:var(--color-text-muted);';
    tdesc.textContent = tmpl.description;

    const triggerLabel = TRIGGER_TYPES.find((t) => t.value === tmpl.trigger?.triggerType)?.label || tmpl.trigger?.triggerType || '—';
    const tmeta = document.createElement('div');
    tmeta.style.cssText = 'margin-top:6px;font-size:11px;';
    tmeta.innerHTML = `<span class="badge badge-info">${triggerLabel}</span>`;

    info.append(tname, tdesc, tmeta);

    const useBtn = document.createElement('button');
    useBtn.className = 'btn btn-primary btn-sm';
    useBtn.textContent = 'Use This';
    useBtn.addEventListener('click', () => {
      close();
      // Pre-fill the create modal using template data
      const prefilled = {
        name: tmpl.name,
        trigger: tmpl.trigger,
        conditions: tmpl.conditions || [],
        actions: tmpl.actions || [],
        priority: 0,
        maxRunsPerHour: null,
      };
      openFlowModal(siteId, prefilled, onSaved);
    });

    card.append(info, useBtn);
    list.appendChild(card);
  });

  dialog.appendChild(list);
}
