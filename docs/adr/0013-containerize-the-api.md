# ADR-0013: Containerize the API with a multi-stage build and Compose

- **Status:** Accepted
- **Date:** 2026-07-11
- **Deciders:** Project owner

## Context

SQL Server already ran in Docker ([ADR-0001](0001-use-docker-for-local-sql-server.md)),
but the API only ran on the host. To reach the cloud milestones (CI/CD, Azure) the
API must be a container image, and the whole stack should start with one command —
so a stranger can clone the repository and run it without installing .NET or SQL
Server.

## Decision

- **Multi-stage Dockerfile** (`backend/docker/Dockerfile.api`): `sdk:10.0` builds
  and publishes; `aspnet:10.0` is the runtime image. The final image contains no
  SDK and no source. Build context is `backend/` (Central Package Management needs
  `Directory.Packages.props` in the context).
- **Layered restore**: copy `Directory.Packages.props` and the `.csproj` files,
  `dotnet restore`, *then* copy sources. Editing a `.cs` file must not invalidate
  the NuGet restore layer.
- **`.dockerignore`** excludes `bin/`, `obj/`, `.git/`, `logs/`, `.vs/`. This is a
  **correctness** requirement, not an optimisation: copying the host's Windows
  `obj/` into the Linux build makes `dotnet publish --no-restore` read a
  Windows-generated `project.assets.json` and fail on absolute Windows paths.
- **Container serves plain HTTP on :8080**; TLS is terminated upstream (proxy /
  Azure).
- **Behaviour that differs by environment is configuration-driven, never inferred
  from "am I in a container"**, with deliberately opposite fail-safe defaults:

  | Flag | Default | Rationale |
  |------|---------|-----------|
  | `UseHttpsRedirection` | `true` | A missing key must never silently disable a security control (fails *closed*). |
  | `ApplyMigrationsOnStartup` | `false` | A missing key must never migrate or seed a production database by accident (fails *safe*). |

  Migrations **and identity seeding** are both gated by the same opt-in flag.
- **Compose** runs API + SQL together. The API reaches the database by the
  **service name** (`Server=sqlserver,1433`), not `localhost`. A **SQL healthcheck**
  plus `depends_on: condition: service_healthy` ensures the API starts only once
  SQL accepts connections (`depends_on` alone waits for *start*, not readiness).
- **Secrets via environment variables** (`__` separator) sourced from `.env`;
  `.env.example` is committed and documents the constraint for each value.
- **Fail fast on a bad JWT key**: startup throws if `JwtSettings:Key` is missing or
  under 32 characters, rather than serving confusing 500s on every authenticated
  request.

### Rejected: an API healthcheck in Compose

Compose healthchecks run **inside** the container, so they need a tool in the
image. `mcr.microsoft.com/dotnet/aspnet` is minimal — no `curl`, no `wget` — so a
`curl`-based healthcheck would fail immediately and mark the container permanently
unhealthy. Nothing `depends_on` the API, so its health status would be cosmetic.
Kubernetes / Azure Container Apps / App Service probe **from outside** the
container, so the existing `/api/health` endpoint already serves that purpose with
no image changes. Revisit only if a service ever depends on the API's readiness.

## Alternatives considered

- **Single-stage build** (ship the SDK image) — simple, but a ~800 MB image
  containing compilers and source. Rejected.
- **Dockerfile at the repository root** — would make the build context the whole
  repo. Rejected: a `backend/docker/` folder keeps the context tight and scales to
  future Dockerfiles (worker, frontend).
- **Detecting `DOTNET_RUNNING_IN_CONTAINER`** to disable HTTPS redirection —
  rejected: it conflates *containerization* with *environment*. "Production in
  Docker" and "Development in Docker" are different things; configuration keeps
  them independent.
- **Always migrating on startup** — convenient, but in production multiple
  instances can race and a bad migration takes the app down. Hence opt-in.

## Consequences

### Positive

- `docker compose up -d --build` yields a working API + database with no host
  tooling beyond Docker — a strong developer-experience story.
- Small runtime image (~104 MB content) with no SDK or source.
- Fast rebuilds: source edits don't re-run NuGet restore.
- The image is exactly what CI/CD will build and Azure will run — each milestone
  reuses the last.
- Configuration-driven behaviour with fail-safe defaults; failures are loud
  (fail-fast key check, logged seeding errors) rather than silent.

### Negative / trade-offs

- Migrate-on-startup remains unsuitable for production; CI/CD will add an explicit
  migration step.
- The minimal runtime image has no shell tooling for in-container healthchecks — an
  accepted trade for a smaller attack surface.
- The `sa` account and a symmetric JWT key are used for local convenience; both
  should be replaced (least-privilege login, Key Vault) in the cloud.

### Follow-ups

- CI/CD (v0.6): build and push this image; run tests; explicit migration step.
- Azure (v0.7): container registry, App Service, Key Vault, `/api/health` as the
  platform probe.
