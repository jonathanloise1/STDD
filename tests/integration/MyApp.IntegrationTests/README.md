# MyApp integration tests

The backend over HTTP, hosted in the test process, against a real SQL Server (`development.md` 10.1).

## The database

A run has its own database, `MyApp_Integration_Test`, on the server you give. At start the backend (Development
environment, `MyApp:Testing:ResetDatabaseOnStart=true`) drops that database, migrates it and loads the seed data,
so every run starts from the same data. The guard is the one of the E2E run (`TestDatabaseReset`): it refuses any
database whose name does not end in `_Test` or `_E2E`. No database a person uses is ever touched.

Tests never change the seed data: a test that writes makes its own users and organizations (`TestWorld`), so the
tests do not depend on their order.

## Prerequisites

- A SQL Server the account can create and drop databases on.
- `IntegrationTests__ConnectionString`: a connection string to that server. Its database part is ignored. It can
  also be set in `appsettings.integration.local.json` (git-ignored). If it is missing the run stops and names it.
- Optional: `IntegrationTests__DatabaseName`, another name ending in `_Test` or `_E2E`.

## Run

```
IntegrationTests__ConnectionString="Server=localhost,1433;User Id=...;Password=...;TrustServerCertificate=True" \
  dotnet test tests/integration/MyApp.IntegrationTests
```

With the compose SQL Server, which publishes no port unless `compose.sqlserver-port.yaml` is added (host port 14330), use
`localhost,14330` from the host, or run the tests inside the compose network:

```
docker compose --profile integration run --rm integration                  # SQL Server of the compose project, database MyApp_Integration_Test
docker compose -f compose.yaml -f compose.sqlserver-port.yaml up -d sqlserver   # only to reach it from the host: Server=localhost,14330
```

The `integration` profile starts the SQL Server if needed, copies the repository (without `bin`, `obj`, `node_modules`)
into a .NET 10 SDK container and runs this project against it. Arguments after `integration` go to `dotnet test`.

A run needs the owner's approval every time (`CLAUDE.md`): it creates and drops a database.
