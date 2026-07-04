# ADR-0001: Use Docker for the local SQL Server environment

- **Status:** Accepted
- **Date:** 2026-07-04
- **Deciders:** Project owner

## Context

FactoryPulse needs a SQL Server instance for local development. The project's
stated goals include learning Docker and building toward an Azure-hosted,
cloud-native deployment. The local database should behave as closely as possible
to the eventual cloud target (Azure SQL / SQL Server on Linux), and setup should
be reproducible for anyone cloning the repo.

Options for a local SQL Server on a Windows dev machine include SQL Server
Developer Edition installed on the host, LocalDB, SQL Server Express, or SQL
Server running in a Docker container.

## Decision

We will run SQL Server 2022 (Developer edition) in a **Docker container**,
defined in `infrastructure/docker/docker-compose.yml`, and connect to it over
`localhost,1433` using SQL authentication.

## Alternatives considered

- **LocalDB** — zero setup and bundled with Visual Studio, but Windows-only and
  cannot run inside a Linux container, so it diverges from the cloud target and
  can't be reused in CI.
- **SQL Server Developer/Express installed on the host** — production-like, but a
  heavyweight machine-wide install, not reproducible from the repo, and harder to
  reset to a clean state.
- **Docker SQL Server** — chosen: identical locally, in CI, and close to the
  Linux-based cloud target; disposable and reproducible from a single
  compose file; directly advances the Docker learning goal.

## Consequences

### Positive

- The database is reproducible from one `docker compose up -d`.
- Same engine locally, in CI, and near-identical to Azure SQL / MSSQL-on-Linux.
- Data persists across restarts via a named volume; a full reset is one
  `docker compose down -v`.
- Advances the project's Docker learning objective early.

### Negative / trade-offs

- Requires Docker Desktop + hardware virtualization (WSL2), which added one-time
  setup cost (BIOS SVM had to be enabled).
- Uses SQL authentication (`sa`) rather than Windows/integrated auth, so a
  password must be managed as a secret.

### Follow-ups

- Secret handling for the `sa` password (see the `.env` / user-secrets approach).
- When multiple services are needed (Azurite, Redis, Seq), extend the same
  compose file rather than adding separate tooling.
