#!/bin/sh
set -eu

API_BASE="${INTENTIFY_API_BASE_URL:-}"
cat > /usr/share/nginx/html/env-config.js <<EOT
window.__INTENTIFY_API_BASE__ = "${API_BASE}";
window.NEXT_PUBLIC_API_BASE_URL = "${API_BASE}";
EOT
