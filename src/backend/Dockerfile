# Backend image (docs/guidelines/architecture.md 8.5). Build from the repository root:
#   docker build -f src/backend/Dockerfile -t myapp-backend .
#
# The container listens on plain http, port 8080: TLS is the job of whatever sits in front (the frontend's nginx,
# a reverse proxy). The environment is Production unless ASPNETCORE_ENVIRONMENT says otherwise; the seed data, Swagger
# and the impersonation of the E2E run exist only in Development. Migrations (and, in Development, the seed) run at
# start, as they do outside Docker. The JSON lines log file goes to /logs when MyApp__Logging__File__Enabled is true.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/backend/MyApp.sln src/backend/
COPY src/backend/MyApp.Domain/MyApp.Domain.csproj src/backend/MyApp.Domain/
COPY src/backend/MyApp.Application/MyApp.Application.csproj src/backend/MyApp.Application/
COPY src/backend/MyApp.Infrastructure/MyApp.Infrastructure.csproj src/backend/MyApp.Infrastructure/
COPY src/backend/MyApp.WebApi/MyApp.WebApi.csproj src/backend/MyApp.WebApi/
RUN dotnet restore src/backend/MyApp.WebApi/MyApp.WebApi.csproj
COPY src/backend src/backend
RUN dotnet publish src/backend/MyApp.WebApi/MyApp.WebApi.csproj -c Release -o /app --no-restore
# Files keep the mode they have on the host (an appsettings file may be 600): the app user must be able to read them
RUN chmod -R a+rX /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
COPY --chmod=755 docker/http-check.sh /usr/local/bin/http-check
# The log folder belongs to the app user, so that a new named volume mounted here is writable
RUN mkdir /logs && chown app:app /logs
ENV ASPNETCORE_URLS=http://+:8080 \
    MyApp__Logging__File__Directory=/logs
USER app
VOLUME ["/logs"]
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=5s --start-period=90s --retries=12 CMD ["http-check", "8080", "/health"]
ENTRYPOINT ["dotnet", "MyApp.WebApi.dll"]
