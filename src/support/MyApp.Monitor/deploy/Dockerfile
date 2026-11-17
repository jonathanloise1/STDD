# Container image of the monitor (docs/guidelines/operations.md 14.3, Container): `run` is the entry point and the store is on a volume.
#
# Build from the repository root (the project embeds docs/support/examples/incident-report.example.json and
# training/investigator/protocol.json):
#   docker build -f src/support/MyApp.Monitor/deploy/Dockerfile -t myapp-monitor .
#
# The image carries an operations pack built from this repository for the on-premises profile (/pack), so that
# `--profile onprem` works at once. For another profile or a newer pack, mount a pack at /pack, which hides it.
# Run (compose.yaml does this with the `monitor` profile; docs/guidelines/operations.md 14.3):
#   docker run -d --name myapp-monitor \
#     -v myapp-monitor-data:/data \
#     -v /path/to/ops-pack:/pack:ro \
#     -e Monitor__Masking__HashKey=... \
#     -e Monitor__Azure__Credential=managed_identity \
#     -e Monitor__Settings__DashboardAuth=entra -e Monitor__Dashboard__Urls=http://+:8080 \
#     -e Monitor__Dashboard__AzureAd__TenantId=... -e Monitor__Dashboard__AzureAd__ClientId=... -e Monitor__Settings__DashboardGroupId=... \
#     -p 8080:8080 myapp-monitor --profile cloud
#
# dashboard.auth "local" binds to loopback inside the container and cannot be reached from outside: serve the dashboard with "entra".
# (compose.yaml reaches a local one through a forwarder that shares the container's network.)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/support/MyApp.Monitor src/support/MyApp.Monitor
COPY docs/support docs/support
COPY training/investigator/protocol.json training/investigator/protocol.json
RUN dotnet publish src/support/MyApp.Monitor/MyApp.Monitor.csproj -c Release -o /app --no-self-contained

# The pack reads the documentation, the support agent's protocol and the backend's log call sites (operations.md 13.2)
FROM build AS pack
COPY docs docs
COPY .github/agents/support.agent.md .github/agents/support.agent.md
COPY src/backend src/backend
RUN dotnet /app/MyApp.Monitor.dll pack build --profile onprem --repo /src --out /pack

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
COPY --from=pack /pack/onprem /pack
COPY --chmod=755 docker/http-check.sh /usr/local/bin/http-check
ENV Monitor__ContentRoot=/data \
    Monitor__Pack__Path=/pack \
    Monitor__Store__Path=/data/monitor.db
VOLUME ["/data"]
EXPOSE 8080
ENTRYPOINT ["dotnet", "MyApp.Monitor.dll", "run"]
