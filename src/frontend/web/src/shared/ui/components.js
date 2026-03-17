const STYLE_ID = 'intentify-ui-styles';

export const ensureUiStyles = () => {
  if (typeof document === 'undefined') {
    return;
  }

  if (document.getElementById(STYLE_ID)) {
    return;
  }

  const link = document.createElement('link');
  link.id = STYLE_ID;
  link.rel = 'stylesheet';
  link.href = new URL('./styles.css', import.meta.url).toString();
  document.head.appendChild(link);
};

export const createCard = ({ title, body, footer } = {}) => {
  ensureUiStyles();
  const card = document.createElement('div');
  card.className = 'ui-card';

  if (title) {
    const heading = document.createElement('h3');
    heading.className = 'ui-card__title';
    heading.textContent = title;
    card.appendChild(heading);
  }

  if (body) {
    const content = document.createElement('div');
    content.append(body);
    card.appendChild(content);
  }

  if (footer) {
    const footerNode = document.createElement('div');
    footerNode.append(footer);
    card.appendChild(footerNode);
  }

  return card;
};

export const createInput = ({ label, type = 'text', value = '', placeholder, onChange } = {}) => {
  ensureUiStyles();
  const wrapper = document.createElement('label');
  wrapper.className = 'ui-input';

  if (label) {
    const labelText = document.createElement('span');
    labelText.textContent = label;
    wrapper.appendChild(labelText);
  }

  const input = document.createElement('input');
  input.className = 'ui-input__field';
  input.type = type;
  input.value = value;
  if (placeholder) {
    input.placeholder = placeholder;
  }

  if (onChange) {
    input.addEventListener('input', (event) => onChange(event.target.value, event));
  }

  wrapper.appendChild(input);
  return { wrapper, input };
};

export const createTable = ({ columns = [], rows = [] } = {}) => {
  ensureUiStyles();
  const table = document.createElement('table');
  table.className = 'ui-table';

  const thead = document.createElement('thead');
  const headRow = document.createElement('tr');

  columns.forEach((column) => {
    const th = document.createElement('th');
    th.textContent = column.label || column.key || '';
    headRow.appendChild(th);
  });

  thead.appendChild(headRow);
  table.appendChild(thead);

  const tbody = document.createElement('tbody');

  rows.forEach((row) => {
    const tr = document.createElement('tr');

    columns.forEach((column) => {
      const td = document.createElement('td');
      const value = row[column.key];
      td.textContent = value === undefined || value === null ? '' : String(value);
      tr.appendChild(td);
    });

    tbody.appendChild(tr);
  });

  table.appendChild(tbody);
  return table;
};

export const createBadge = ({ text, variant } = {}) => {
  ensureUiStyles();
  const badge = document.createElement('span');
  badge.className = 'ui-badge';
  if (variant) {
    badge.classList.add(`ui-badge--${variant}`);
  }
  badge.textContent = text || '';
  return badge;
};

export const createButton = ({ label, variant = 'default', type = 'button' } = {}) => {
  ensureUiStyles();
  const button = document.createElement('button');
  button.type = type;
  button.textContent = label;
  button.style.padding = '8px 12px';
  button.style.borderRadius = '6px';
  button.style.border = variant === 'primary' ? 'none' : '1px solid #e2e8f0';
  button.style.background = variant === 'primary' ? '#2563eb' : '#ffffff';
  button.style.color = variant === 'primary' ? '#ffffff' : '#1e293b';
  button.style.cursor = 'pointer';
  button.style.fontSize = '13px';
  return button;
};

export const createToastManager = () => {
  ensureUiStyles();

  if (typeof document === 'undefined') {
    return {
      show: () => null,
    };
  }

  const container = document.createElement('div');
  container.className = 'ui-toast-container';
  document.body.appendChild(container);

  const show = ({ message, variant, duration = 4000 } = {}) => {
    const toast = document.createElement('div');
    toast.className = 'ui-toast';
    if (variant) {
      toast.classList.add(`ui-toast--${variant}`);
    }
    toast.textContent = message || '';
    container.appendChild(toast);

    window.setTimeout(() => {
      toast.remove();
      if (!container.childElementCount) {
        container.remove();
      }
    }, duration);

    return toast;
  };

  return { show };
};
