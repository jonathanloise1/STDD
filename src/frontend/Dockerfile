# Frontend image: Node builds the application, nginx serves it and forwards the API (docs/guidelines/architecture.md 8.5).
#
# Build from the repository root:
#   docker build -f src/frontend/Dockerfile -t myapp-frontend .
#
# The context is the repository root, because the help pack (the manual and the in-page assistant's retrieval index, manual.md
# 5 and 6) is built from docs/manual in the build stage: `npm run help:build -- --strict`, lexical only, no model needed.
# A tree whose docs/manual has no pages gets no pack. The context is filtered by src/frontend/Dockerfile.dockerignore.
#
# The build bakes in the VITE_* settings of .env.production (theme, layout). The settings that differ per environment
# (API address, sign-in, mock user) are not baked in: the container reads them from its environment at start
# (docker/40-runtime-config.sh). Variables: VITE_API_BASE_URL (empty = same origin), VITE_API_SCOPE, VITE_API_AUDIENCE,
# VITE_AZURE_AD_B2C_CLIENT_ID, VITE_AZURE_AD_B2C_AUTHORITY, VITE_AZURE_AD_B2C_KNOWN_AUTHORITIES, VITE_DEV_MOCK_USER_ID,
# VITE_DEV_MOCK_USER_EMAIL, and BACKEND_UPSTREAM (where nginx forwards /api, default http://backend:8080).
FROM node:22-alpine AS build
# help:build finds the repository root three folders above src/frontend (manual.md 1.2): /repo
WORKDIR /repo/src/frontend
COPY src/frontend/package.json src/frontend/package-lock.json ./
RUN --mount=type=cache,target=/root/.npm npm ci
COPY src/frontend ./
COPY docs /repo/docs
COPY training/assistant /repo/training/assistant
COPY models/model.lock* /repo/models/
RUN if find /repo/docs/manual -type f -name '*.md' ! -name 'bench.md' | grep -q .; then \
      npm run help:build -- --strict; \
    else \
      echo "docs/manual has no pages: no help pack"; \
    fi
RUN npm run build

FROM nginx:1.28-alpine
ENV BACKEND_UPSTREAM=http://backend:8080
COPY src/frontend/docker/nginx.conf.template /etc/nginx/templates/default.conf.template
COPY src/frontend/docker/proxy_params.conf /etc/nginx/proxy_params.conf
COPY --chmod=755 src/frontend/docker/40-runtime-config.sh /docker-entrypoint.d/40-runtime-config.sh
COPY --from=build /repo/src/frontend/build /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --retries=5 CMD wget -q --spider http://127.0.0.1:8080/nginx-health || exit 1
