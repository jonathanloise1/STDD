# E2E runner image (compose.yaml, profile "e2e"; tests/e2e/MyApp.E2E/README.md "Run in Docker"). Build from the repository root:
#   docker build -f tests/e2e/MyApp.E2E/Dockerfile -t myapp-e2e .
#
# The official Playwright .NET image brings the browsers and their system libraries, but only the .NET 8 SDK, and the
# test project is net10.0: the .NET 10 SDK is copied in from the official SDK image (same Ubuntu release).
# The tests read the documentation (test case files), the frontend's locale files and the help pack from the
# repository, so those are copied in. They run against the applications of the compose network; this image starts none (AutoStartApps off).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS sdk10

# The help pack, built from docs/manual exactly as the frontend image builds it (src/frontend/Dockerfile), so the help
# tests find the same pack the application serves. Without manual pages there is no pack.
FROM node:22-alpine AS helppack
WORKDIR /repo/src/frontend
COPY src/frontend/package.json src/frontend/package-lock.json ./
RUN --mount=type=cache,target=/root/.npm npm ci
COPY src/frontend ./
COPY docs /repo/docs
COPY training/assistant /repo/training/assistant
COPY models/model.lock* /repo/models/
RUN mkdir -p public/help && \
    if find /repo/docs/manual -type f -name '*.md' ! -name 'bench.md' | grep -q .; then \
      npm run help:build -- --strict; \
    fi

FROM mcr.microsoft.com/playwright/dotnet:v1.58.0-noble
COPY --from=sdk10 /usr/share/dotnet /usr/share/dotnet
ENV DOTNET_NOLOGO=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=true
WORKDIR /src
COPY tests/e2e tests/e2e
COPY docs docs
COPY src/frontend/public/locales src/frontend/public/locales
COPY --from=helppack /repo/src/frontend/public/help src/frontend/public/help
# The tests find the repository root by its src/backend folder (there is no .git in the image)
RUN mkdir -p src/backend
WORKDIR /src/tests/e2e/MyApp.E2E
RUN dotnet build --configuration Debug
RUN chmod +x docker-entrypoint.sh
ENTRYPOINT ["/src/tests/e2e/MyApp.E2E/docker-entrypoint.sh"]
