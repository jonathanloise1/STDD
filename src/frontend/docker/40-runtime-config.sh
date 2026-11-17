#!/bin/sh
# Writes /config.js from the container's environment when the container starts (runs from the nginx image's
# /docker-entrypoint.d). Vite settings are baked in at build time; these are the ones that differ per
# environment, so they are read at run time instead (src/config/runtimeConfig.ts). A key is written only when
# the variable is set, even to an empty value; an unset one keeps the build-time value.
set -eu

out=/usr/share/nginx/html/config.js

escape() {
  printf '%s' "$1" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g'
}

{
  echo "// Written at container start by 40-runtime-config.sh."
  echo "window.__MYAPP_CONFIG__ = {"
  for key in \
    VITE_API_BASE_URL \
    VITE_API_SCOPE \
    VITE_API_AUDIENCE \
    VITE_AZURE_AD_B2C_CLIENT_ID \
    VITE_AZURE_AD_B2C_AUTHORITY \
    VITE_AZURE_AD_B2C_KNOWN_AUTHORITIES \
    VITE_DEV_MOCK_USER_ID \
    VITE_DEV_MOCK_USER_EMAIL; do
    if eval "[ \"\${$key+x}\" = x ]"; then
      eval "value=\${$key}"
      printf '  "%s": "%s",\n' "$key" "$(escape "$value")"
    fi
  done
  echo "};"
} > "$out"

echo "runtime-config: wrote $out"
