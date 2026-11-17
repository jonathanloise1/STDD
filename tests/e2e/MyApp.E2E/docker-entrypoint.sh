#!/usr/bin/env bash
# Entry point of the E2E runner image (compose.yaml, profile "e2e"). Runs the tests against the compose services,
# which are already up (AutoStartApps is off), and copies the results and the failure screenshots to /results,
# a volume that outlives the container. Arguments are passed to `dotnet test`, for example:
#   docker compose --profile e2e run --rm e2e --filter "FullyQualifiedName~TaskCreate005"
set -u
cd "$(dirname "$0")" 2> /dev/null || cd /src/tests/e2e/MyApp.E2E

echo "e2e: base url ${E2E_BASE_URL:-?}, database ${E2E_DATABASE_NAME:-MyApp_E2E} (reset by backend-e2e when it started)"
dotnet test --no-build --configuration Debug --settings .runsettings.headless \
  --results-directory /results --logger "trx;LogFileName=e2e.trx" "$@"
code=$?

if [ -d bin/Debug/net10.0/screenshots ]; then
  mkdir -p /results/screenshots
  cp -R bin/Debug/net10.0/screenshots/. /results/screenshots/
fi
exit "$code"
