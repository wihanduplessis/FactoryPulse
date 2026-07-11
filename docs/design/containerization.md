# Design: Containerize the API (v0.5)

Design-first note for packaging the API as a Docker image and running the whole
stack (API + SQL Server) with a single `docker compose up`.

> Status: Approved &bull; Target: v0.5 &bull; Depends on: v0.4 (Authentication)

---

## 1. Goals

- A production-shaped **multi-stage Dockerfile** for `FactoryPulse.API`.
- `docker compose up` brings up **API + SQL Server together**, wired by container
  networking, with no host-side setup beyond `.env`.
- Config and secrets supplied by **environment variables**, not baked into the image.
- Behaviour that differs per environment is driven by **configuration**, never
  inferred from "am I in a container".
- Sets up the next milestones: CI/CD builds this image; Azure runs it.

---

## 2. Locked decisions

| # | Decision | Rationale |
|---|----------|-----------|
| D1 | Dockerfile at **`backend/docker/Dockerfile.api`**, build context = `backend/` | CPM needs `Directory.Packages.props` in the context; a `docker/` folder scales to future Dockerfiles (worker, frontend) without cluttering the solution root. |
| D2 | **Multi-stage build**: `sdk:10.0` → publish → `aspnet:10.0` runtime | Small final image (no SDK, no source), standard .NET pattern. |
| D3 | **Layered restore** — copy `Directory.Packages.props` + `.csproj` files, `dotnet restore`, *then* copy sources | Docker layer caching: editing a `.cs` file must not re-download NuGet packages. |
| D4 | Container serves **HTTP on :8080**; TLS terminated outside (reverse proxy / Azure). HTTPS redirection is **configuration-driven**, `UseHttpsRedirection` defaulting to **`true`** | Containerization and environment are orthogonal concerns. Defaulting to `true` means a missing key never silently disables a security control (fails *closed*). |
| D5 | Config via **environment variables** with the `__` separator (e.g. `ConnectionStrings__FactoryPulseDatabase`) | .NET's standard env-var config binding; keeps secrets out of the image. |
| D6 | Inside compose, the DB host is the **service name `sqlserver`**, not `localhost` | Compose creates a network where services resolve each other by name. |
| D7 | Migrations applied on startup only when **`ApplyMigrationsOnStartup`** is `true`; default **`false`** | Convenient for dev/compose, deliberately opt-in. CI/CD and Azure disable it with configuration, not a code change. Defaulting to `false` means a production database is never migrated by accident. |
| D8 | **SQL healthcheck + `depends_on: service_healthy`** | `depends_on` alone waits for container *start*, not readiness; SQL Server takes ~20s. Without this the API fails its first connection. |
| D9 | **`.dockerignore`** excluding `bin/`, `obj/`, `.git/`, `logs/`, `.vs/` | Smaller build context, faster builds, prevents host build artifacts polluting the image. |
| D10 | Identity **seeding is gated by the same opt-in flag** as migrations | The seeder currently runs on every startup. Creating users should be as deliberate as changing schema. |

### On the API healthcheck (considered, not adopted)

A compose `healthcheck` for the API was considered and **rejected for now**:

- Compose healthchecks run **inside** the container, so they need a tool in the
  image. `mcr.microsoft.com/dotnet/aspnet` is minimal — it has **no `curl` and no
  `wget`** — so `test: ["CMD","curl",...]` would fail immediately and mark the
  container permanently unhealthy.
- Nothing `depends_on` the API, so its health status is cosmetic (`docker compose ps`).
- Kubernetes / Azure Container Apps / App Service probe the endpoint **from
  outside** the container, so the existing **`/api/health`** endpoint already
  serves that purpose with **no image changes**.

Adding `curl` via `apt-get` would work, at the cost of a larger, less minimal
runtime image. Revisit if another service ever depends on the API's readiness.

---

## 3. Dockerfile shape

```
# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Packages.props ./
COPY src/<each project>/<project>.csproj src/<each project>/
RUN dotnet restore src/FactoryPulse.API/FactoryPulse.API.csproj
COPY src/ src/
RUN dotnet publish src/FactoryPulse.API/FactoryPulse.API.csproj -c Release -o /app/publish --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "FactoryPulse.API.dll"]
```

The `aspnet` image already runs as a **non-root** user and defaults to port
**8080** (`ASPNETCORE_HTTP_PORTS`).

## 4. Application changes required

1. **Configuration-driven HTTPS redirection** (D4) — default `true`:
   ```csharp
   if (builder.Configuration.GetValue("UseHttpsRedirection", defaultValue: true))
   {
       app.UseHttpsRedirection();
   }
   ```
2. **Opt-in migrations + seeding on startup** (D7, D10) — default `false`:
   ```csharp
   if (builder.Configuration.GetValue("ApplyMigrationsOnStartup", defaultValue: false))
   {
       await dbContext.Database.MigrateAsync();
       logger.LogInformation("Database migration complete.");
       await seeder.SeedAsync();
   }
   ```
   `appsettings.Development.json` sets `ApplyMigrationsOnStartup: true` so local
   `dotnet run` keeps working; compose sets it via an environment variable.

## 5. Compose changes

```yaml
services:
  sqlserver:
    # ...existing...
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"$$MSSQL_SA_PASSWORD\" -C -Q 'SELECT 1' || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 10

  api:
    build:
      context: ../../backend
      dockerfile: docker/Dockerfile.api
    container_name: factorypulse-api
    depends_on:
      sqlserver:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: "Development"
      UseHttpsRedirection: "false"
      ApplyMigrationsOnStartup: "true"
      ConnectionStrings__FactoryPulseDatabase: "Server=sqlserver,1433;Database=FactoryPulseDb;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True;"
      JwtSettings__Key: "${JWT_KEY}"
      SeedAdmin__Password: "${SEED_ADMIN_PASSWORD}"
    ports:
      - "8080:8080"
    restart: unless-stopped
```

`.env` gains `JWT_KEY` and `SEED_ADMIN_PASSWORD`; `.env.example` documents them.

## 6. Trade-offs

- **Migrate-on-startup** is convenient but not ideal in production (instances can
  race; a bad migration takes the app down). Hence opt-in, default off — CI/CD will
  introduce an explicit migration step for real deployments.
- **Symmetric JWT key via env var** — fine for local compose; Azure will use Key
  Vault / App Service configuration. Never reuse the dev key.
- The **`sa` account** is used for simplicity; a least-privilege SQL login would be
  the production choice.
- A **minimal runtime image** means no shell tooling (`curl`) for in-container
  healthchecks — an accepted trade for a smaller attack surface.

## 7. Issue breakdown (milestone "v0.5 — Containerization")

| # | Issue | Acceptance |
|---|-------|------------|
| 1 | `feature/dockerfile` | `docker/Dockerfile.api` + `.dockerignore`; `docker build` succeeds; image starts (failing to reach a DB is expected here) |
| 2 | `feature/container-runtime` | `UseHttpsRedirection` + `ApplyMigrationsOnStartup` config flags with correct defaults; migration + seeding gated and logged |
| 3 | `feature/compose-api` | `api` service + SQL healthcheck; `.env`/`.env.example` updated; `docker compose up -d --build` brings both up; Swagger on `http://localhost:8080/swagger`; login works |
| 4 | `feature/docker-docs` | README "Running with Docker" section; **ADR-0013** recording D1–D10 |

## 8. Acceptance walkthrough

1. `docker compose up -d --build` from `infrastructure/docker`.
2. `docker ps` shows `factorypulse-sql` (healthy) and `factorypulse-api` (up).
3. `docker compose logs api` contains `Now listening on: http://[::]:8080`,
   `Application started.`, and `Database migration complete.`
4. `http://localhost:8080/swagger` loads.
5. `POST /api/auth/login` as the seeded admin → **200** + JWT (proves DB reachable,
   migrations applied, seeding ran).
6. `GET /api/machines` without a token → **401**; with the token → **200**.
7. `docker compose down` then `up -d` → data persists (named volume).
8. A fresh clone can run the API with: copy `.env.example` → `.env`, `docker compose up -d --build`.
