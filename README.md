# FactoryPulse

[![CI](https://github.com/wihanduplessis/FactoryPulse/actions/workflows/ci.yml/badge.svg)](https://github.com/wihanduplessis/FactoryPulse/actions/workflows/ci.yml)

A cloud-native manufacturing management API built with **.NET 10**, demonstrating
Clean Architecture, domain-driven tactical patterns, and modern backend
engineering practices.

FactoryPulse lets production managers and factory supervisors manage machines and
production orders — tracking machine state, order lifecycles, and the business
rules that govern them.

> **Status:** Active development. Machine Management, Production Orders,
> Authentication, containerization, and CI/CD are complete; Azure deployment is
> next. See the [Roadmap](docs/Roadmap.md).

## Continuous integration

Every push and pull request runs, on GitHub Actions:

```
restore → build → 40 unit tests → 6 integration tests (real SQL Server) → build container image
```

Integration tests use **Testcontainers** to start a genuine SQL Server container
and **`WebApplicationFactory`** to host the API in-process against it — no mocks,
no in-memory database. `main` is protected: a red build blocks the merge.

---

## Features

- **Machine Management** — full CRUD over factory machines and their status
  (`Idle`, `Running`, `Maintenance`, `Down`, `Retired`).
- **Production Orders** — a richer domain with a real lifecycle:
  - state machine: `Planned → Running → Completed`, with `Cancelled` as a terminal state
  - transition endpoints (`start` / `complete` / `cancel`) rather than a mutable status field
  - business rules: unique order numbers, quantity > 0, no orders on retired machines, valid end dates, no restarting cancelled orders
  - **pagination and filtering** (by status, machine, product)
- **JWT authentication & role-based authorization** — ASP.NET Core Identity, JWT
  bearer tokens, three roles (`Admin` / `Manager` / `Viewer`) enforced by policies,
  secure-by-default endpoints.
- **Consistent error handling** — RFC-standard `ProblemDetails`, correct status
  codes (400 / 401 / 403 / 404 / 409 / 500), all validation errors returned at once.
- **Structured logging** — Serilog to console and rolling file.
- **Fully containerized** — `docker compose up` runs the API and SQL Server together.
- 🔜 GitHub Actions CI/CD · Azure deployment

## Architecture

FactoryPulse is a **modular monolith** using **Clean Architecture** — all
dependencies point inward toward a framework-free domain.

```
API  →  Application  →  Domain  ←  Infrastructure
        (business)     (core)      (EF Core / SQL)
Controller → Service → Repository → EF Core → SQL Server
```

- **Domain** — entities, enums, domain exceptions. No dependencies (not even EF).
- **Application** — services, DTOs, repository interfaces, mapping, validation,
  the `Result` pattern. Depends only on Domain.
- **Infrastructure** — EF Core `DbContext`, repositories, configurations. Implements
  the Application's interfaces.
- **API** — thin controllers translating `Result`s into HTTP responses.

📄 A detailed visual overview is in **[docs/architecture-overview.pdf](docs/architecture-overview.pdf)**,
and every significant decision is recorded as an **[ADR](docs/adr/)**.

## Engineering practices

The decisions that make this more than CRUD:

- **Clean Architecture** with a project-per-layer split (boundaries enforced at compile time)
- **Rich domain model** for aggregates with a lifecycle (private setters, factories, encapsulated behaviour, invariant guards) — see [ADR-0009](docs/adr/0009-rich-domain-model-for-aggregates.md)
- **`Result<T>` pattern** for expected outcomes; exceptions only for the unexpected — [ADR-0006](docs/adr/0006-result-for-expected-outcomes.md)
- **Repository pattern** behind interfaces owned by the business layer — [ADR-0004](docs/adr/0004-use-repository-pattern.md)
- **FluentValidation**, centralized audit fields via `SaveChanges`, and manual mapping (no runtime reflection)
- **Central Package Management** (one place for all NuGet versions)
- **Unit tests** (xUnit · NSubstitute · Shouldly) covering the domain rules and service logic
- **Architecture Decision Records** documenting the *why*

## Technology

| Area | Tech |
|------|------|
| Language / runtime | C# / .NET 10 |
| Web | ASP.NET Core Web API |
| Data | Entity Framework Core, SQL Server 2022 |
| Local infra | Docker (SQL Server in a container) |
| Validation | FluentValidation |
| Logging | Serilog (console + file) |
| API docs | OpenAPI + Swagger UI |
| Auth | ASP.NET Core Identity, JWT bearer, role-based policies |
| Containers | Docker (multi-stage build), Docker Compose |
| Testing | xUnit, NSubstitute, Shouldly |
| Planned | GitHub Actions, Azure App Service, Azure SQL, Application Insights |

## Running with Docker (quickstart)

**The whole stack — API + SQL Server — in two commands.** The only prerequisite is
[Docker Desktop](https://www.docker.com/products/docker-desktop/): the .NET SDK
builds *inside* the image, and SQL Server runs in a container. You do not need
.NET, SQL Server, or EF tools installed.

```bash
cd infrastructure/docker
cp .env.example .env      # then edit .env — it documents the rules for each value
docker compose up -d --build
```

Open **http://localhost:8080/swagger** and log in via `POST /api/auth/login` with
the **email and password you set in `.env`** (`SEED_ADMIN_EMAIL` /
`SEED_ADMIN_PASSWORD`). The API applies its own migrations and seeds the admin
user and roles on startup.

Useful commands:

```bash
docker compose logs -f api    # follow the API logs
docker compose down           # stop (data is preserved in a volume)
docker compose down -v        # stop and wipe the database
```

## Running locally (for development)

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker
(for SQL Server only).

### 1. Start SQL Server

```bash
cd infrastructure/docker
cp .env.example .env          # set MSSQL_SA_PASSWORD
docker compose up -d sqlserver
```

### 2. Configure secrets

The API reads secrets from .NET user-secrets (never committed). From `backend/`:

```bash
dotnet user-secrets set "ConnectionStrings:FactoryPulseDatabase" \
  "Server=localhost,1433;Database=FactoryPulseDb;User Id=sa;Password=<your .env password>;TrustServerCertificate=True;" \
  --project src/FactoryPulse.API

dotnet user-secrets set "JwtSettings:Key" "<at least 32 characters>" --project src/FactoryPulse.API
dotnet user-secrets set "SeedAdmin:Password" "<e.g. Admin123!>" --project src/FactoryPulse.API
```

### 3. Run the API

```bash
dotnet run --project src/FactoryPulse.API
```

Migrations are applied and the admin user seeded automatically in Development.
Open **https://localhost:7135/swagger**.

### Run the tests

```bash
dotnet test
```

## Project structure

```
FactoryPulse/
├── backend/
│   ├── src/
│   │   ├── FactoryPulse.Domain/          # entities, enums, exceptions (no dependencies)
│   │   ├── FactoryPulse.Application/     # services, DTOs, Result, interfaces, validation
│   │   ├── FactoryPulse.Infrastructure/  # EF Core, repositories, DbContext
│   │   └── FactoryPulse.API/             # controllers, middleware, Program.cs
│   └── tests/
│       └── FactoryPulse.Tests/           # xUnit unit tests
├── infrastructure/docker/                # docker-compose for SQL Server
└── docs/                                 # architecture overview, ADRs, roadmap
```

## Documentation

- [Architecture overview (PDF)](docs/architecture-overview.pdf)
- [Architecture Decision Records](docs/adr/)
- [Roadmap](docs/Roadmap.md)
- [Production Orders design](docs/design/production-orders.md)

## License

See [LICENSE](LICENSE).
