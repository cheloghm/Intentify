#!/bin/sh
set -eu

API_BASE="${INTENTIFY_API_BASE_URL:-}"
cat > /usr/share/nginx/html/env-config.js <<EOT
window.__INTENTIFY_API_BASE__ = "${API_BASE}";
window.NEXT_PUBLIC_API_BASE_URL = "${API_BASE}";
EOT

BACKEND_HOST="${INTENTIFY_BACKEND_URL:-intentify-production.up.railway.app}"
sed -i "s|BACKEND_PLACEHOLDER|${BACKEND_HOST}|g" /etc/nginx/conf.d/default.conf

BUILD_TS=$(date +%s)
sed -i "s|login\.js?v=[0-9]*|login.js?v=${BUILD_TS}|g" /usr/share/nginx/html/public/login.html
sed -i "s|register\.js?v=[0-9]*|register.js?v=${BUILD_TS}|g" /usr/share/nginx/html/public/register.html
sed -i "s|src/app/index\.js|src/app/index.js?v=${BUILD_TS}|g" /usr/share/nginx/html/index.html

exec nginx -g 'daemon off;'
