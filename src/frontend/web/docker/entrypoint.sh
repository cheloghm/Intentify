#!/bin/sh
set -eu

API_BASE="${INTENTIFY_API_BASE_URL:-}"
cat > /usr/share/nginx/html/env-config.js <<EOT
window.__INTENTIFY_API_BASE__ = "${API_BASE}";
window.NEXT_PUBLIC_API_BASE_URL = "${API_BASE}";
EOT

BACKEND_HOST="${INTENTIFY_BACKEND_URL:-backend:5000}"
sed -i "s|BACKEND_PLACEHOLDER|${BACKEND_HOST}|g" /etc/nginx/conf.d/default.conf
