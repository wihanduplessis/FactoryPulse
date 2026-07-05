# ADR-0004: Use the repository pattern for data access

- **Status:** Accepted
- **Date:** 2026-07-04
- **Deciders:** Project owner

## Context

FactoryPulse follows a layered (Clean) architecture: Controller → Service →
data access → EF Core. A decision is needed on how the service layer reaches the
database. EF Core's `DbContext` is itself a Unit of Work and its `DbSet<T>`s are
already repositories, so one option is to use `DbContext` directly from services.
The project's goals include demonstrating clean, idiomatic architecture, and
keeping business logic testable without a live database.

## Decision

Data access will sit behind **repository interfaces** defined in the Application
layer (e.g. `IMachineRepository`) and implemented in the Infrastructure layer
(e.g. `MachineRepository`, wrapping `FactoryPulseDbContext`). Services depend on
the interfaces, not on `DbContext` directly.

## Alternatives considered

- **Use `DbContext` directly in services** — less code, and EF Core already
  provides Unit-of-Work/repository semantics. But it couples business logic to EF,
  makes services harder to unit-test without a database or heavy mocking, and
  leaks persistence concerns upward.
- **Repository (+ later Unit of Work) abstraction** — chosen: keeps the service
  layer persistence-agnostic, gives a clean seam for unit testing, and matches
  the layered architecture the project sets out to demonstrate.

## Consequences

### Positive

- Services depend on intent-revealing interfaces, not EF specifics.
- Business logic is unit-testable by substituting a fake/mock repository.
- Data-access implementation (EF Core, or something else later) can change
  without touching services.

### Negative / trade-offs

- Additional interfaces and classes — some boilerplate over calling `DbContext`
  directly.
- Risk of a leaky/generic repository that just re-exposes `IQueryable`; avoided
  by keeping repository methods intent-based (e.g. `GetByIdAsync`,
  `AddAsync`) rather than exposing raw queryables.

### Follow-ups

- Implement `IMachineRepository` / `MachineRepository`.
- Introduce a Unit of Work / explicit `SaveChangesAsync` boundary if/when a
  single operation must span multiple repositories atomically.

## Amendment (2026-07-04)

Repositories return plain nullable entities (e.g. `Task<Machine?>`), **not**
`Result<T>`. `Result<T>` encodes *business outcomes* and is an Application-layer
concern (see [ADR-0006](0006-result-for-expected-outcomes.md)); a repository is
infrastructure and must stay unaware of it. A repository returning `null` is a
data fact; the *service* decides that `null` means `Errors.Machine.NotFound` and
returns the corresponding `Result` failure.
