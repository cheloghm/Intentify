import { createCard, createInput, createToastManager } from '../shared/ui/index.js';
import { createApiClient, mapApiError } from '../shared/apiClient.js';
import { setToken } from '../shared/auth.js';

const app = document.getElementById('app');

const toast = createToastManager();
const apiClient = createApiClient();

const setAppLayout = () => {
  if (!app) {
    return;
  }

  app.style.minHeight = '100vh';
  app.style.display = 'flex';
  app.style.alignItems = 'center';
  app.style.justifyContent = 'center';
  app.style.padding = '24px';
  app.style.background = '#f8fafc';
  app.style.fontFamily = 'system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';
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

const render = () => {
  if (!app) {
    return;
  }

  setAppLayout();

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
  switchLink.href = '/public/login.html';
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
      window.location.href = '/public/index.html';
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

  app.innerHTML = '';
  app.appendChild(card);
};

render();
