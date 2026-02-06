const env = typeof process !== 'undefined' && process.env ? process.env : {};
const windowEnv = typeof window !== 'undefined' ? window : undefined;

const resolveApiBaseUrl = () =>
  env.NEXT_PUBLIC_API_BASE_URL || windowEnv?.NEXT_PUBLIC_API_BASE_URL;

export const getApiBaseUrl = () => {
  const apiBaseUrl = resolveApiBaseUrl();
  const nodeEnv = env.NODE_ENV || 'development';
  const isDev = nodeEnv !== 'production';

  if (!apiBaseUrl && isDev) {
    throw new Error(
      'Missing NEXT_PUBLIC_API_BASE_URL. Define it in your environment for local development.'
    );
  }

  return apiBaseUrl || '';
};

export const API_BASE_URL = getApiBaseUrl();
