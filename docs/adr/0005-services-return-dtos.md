# ADR-0005: Services return DTOs, not domain entities

- **Status:** Accepted
- **Date:** 2026-07-04
- **Deciders:** Project owner

## Context

FactoryPulse uses a layered (Clean) architecture: Controller → Service →
Repository → EF Core. A decision is needed on what the **service layer** exposes
to callers (controllers today; possibly Minimal APIs, background jobs, or gRPC
later). The two common choices are for services to return **domain entities**
(and let the caller map to DTOs) or to return **DTOs** directly.

This decision is foundational because the service contract sits at the centre of
the application: once its return shape is fixed, DTOs, mapping, and controller
code all follow from it.

## Decision

Services return **DTOs** (wrapped in `Result<T>` — see
[ADR-0006](0006-result-for-expected-outcomes.md)), e.g.
`Task<Result<MachineDto>>`. The service performs entity → DTO mapping internally.
Domain entities never leave the Application layer. Controllers only ever see DTOs.

## Alternatives considered

- **Option B — services return domain entities**, controllers map to DTOs at the
  edge (`return Ok(machine.ToDto())`). Keeps services working purely with domain
  objects, but every entry point must repeat the mapping, controllers grow
  fatter over time, and any new consumer must re-implement mapping.
- **Option A — services return DTOs** (chosen). The Application layer owns the
  DTOs and the mapping, so it fully defines "what leaves the application."
  Controllers stay near-trivial and any consumer gets ready-to-use contracts.

## Consequences

### Positive

- Controllers are extremely thin — translate a `Result<DTO>` to an HTTP response
  and nothing more.
- Domain entities never leak past the Application boundary.
- Services present a stable, application-facing contract reusable from any entry
  point (HTTP, jobs, gRPC) without duplicated mapping.

### Negative / trade-offs

- Services reference DTO types. This is acceptable — and correct — because DTOs
  live *in* the Application layer; a service using them is the layer doing its
  job, not a leak.

### Follow-ups

- Define DTOs (`MachineDto`, `CreateMachineRequest`, `UpdateMachineRequest`).
- Provide manual mapping (see [ADR-0007](0007-manual-mapping.md)).
