const env = typeof process !== 'undefined' && process.env ? process.env : {};
const windowEnv = typeof window !== 'undefined' ? window : undefined;

const resolveApiBaseUrl = () =>
  env.INTENTIFY_API_BASE ||
  env.NEXT_PUBLIC_API_BASE_URL ||
  windowEnv?.__INTENTIFY_API_BASE__ ||
  windowEnv?.NEXT_PUBLIC_API_BASE_URL;

export const getApiBaseUrl = () => resolveApiBaseUrl() || '';

export const API_BASE = getApiBaseUrl();
export const API_BASE_URL = API_BASE;
