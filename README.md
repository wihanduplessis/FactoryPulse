# FactoryPulse

A cloud-native manufacturing management API built with **.NET 10**, demonstrating
Clean Architecture, domain-driven tactical patterns, and modern backend
engineering practices.

FactoryPulse lets production managers and factory supervisors manage machines and
production orders — tracking machine state, order lifecycles, and the business
rules that govern them.

> **Status:** Active development. Machine Management and Production Orders are
> complete; Authentication, containerization, CI/CD, and Azure deployment are
> next. See the [Roadmap](docs/Roadmap.md).

---

## Features

- **Machine Management** — full CRUD over factory machines and their status
  (`Idle`, `Running`, `Maintenance`, `Down`, `Retired`).
- **Production Orders** — a richer domain with a real lifecycle:
  - state machine: `Planned → Running → Completed`, with `Cancelled` as a terminal state
  - transition endpoints (`start` / `complete` / `cancel`) rather than a mutable status field
  - business rules: unique order numbers, quantity > 0, no orders on retired machines, valid end dates, no restarting cancelled orders
  - **pagination and filtering** (by status, machine, product)
- **Consistent error handling** — RFC-standard `ProblemDetails`, correct status
  codes (400 / 404 / 409 / 500), all validation errors returned at once.
- **Structured logging** — Serilog to console and rolling file.
- 🔜 JWT authentication & authorization · Docker image · GitHub Actions CI/CD · Azure deployment

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
| Testing | xUnit, NSubstitute, Shouldly |
| Planned | JWT auth, Docker image, GitHub Actions, Azure App Service, Azure SQL, Application Insights |

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for local SQL Server)

### 1. Start SQL Server

```bash
cd infrastructure/docker
cp .env.example .env          # then set a strong MSSQL_SA_PASSWORD in .env
docker compose up -d
```

### 2. Configure the connection string

The API reads the connection string from .NET user-secrets (never committed).
From `backend/`:

```bash
dotnet user-secrets set "ConnectionStrings:FactoryPulseDatabase" \
  "Server=localhost,1433;Database=FactoryPulseDb;User Id=sa;Password=<your .env password>;TrustServerCertificate=True;" \
  --project src/FactoryPulse.API
```

### 3. Apply migrations

```bash
dotnet ef database update --project src/FactoryPulse.Infrastructure --startup-project src/FactoryPulse.API
```

### 4. Run the API

```bash
dotnet run --project src/FactoryPulse.API
```

Open **https://localhost:7135/swagger** to explore the API.

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
