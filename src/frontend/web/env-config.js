// Local dev only. In Docker this file is overwritten by entrypoint.sh
// which reads INTENTIFY_API_BASE_URL from the environment.
window.__INTENTIFY_API_BASE__ = 'http://localhost:5000';
window.NEXT_PUBLIC_API_BASE_URL = 'http://localhost:5000';
