const TOKEN_STORAGE_KEY = 'intentify.auth.token';
let memoryToken = null;

const getStorage = () => {
  if (typeof window === 'undefined') {
    return null;
  }

  try {
    return window.localStorage;
  } catch (error) {
    return null;
  }
};

export const getToken = () => {
  const storage = getStorage();
  if (!storage) {
    return memoryToken;
  }

  return storage.getItem(TOKEN_STORAGE_KEY);
};

export const setToken = (token) => {
  const storage = getStorage();
  if (!storage) {
    memoryToken = token;
    return;
  }

  storage.setItem(TOKEN_STORAGE_KEY, token);
};

export const clearToken = () => {
  const storage = getStorage();
  memoryToken = null;

  if (!storage) {
    return;
  }

  storage.removeItem(TOKEN_STORAGE_KEY);
};

export const isLoggedIn = () => Boolean(getToken());

export const handleSessionExpired = () => {
  clearToken();

  if (typeof window !== 'undefined') {
    window.location.href = '/public/login.html';
  }
};
