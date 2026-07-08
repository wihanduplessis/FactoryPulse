# ADR-0009: Use a rich domain model for aggregates with a lifecycle

- **Status:** Accepted
- **Date:** 2026-07-08
- **Deciders:** Project owner

## Context

`Machine` (v0.2) is an **anemic** entity: public setters, no behaviour — all rules
live in the service. That is fine for a simple entity whose state is just data.

`ProductionOrder` (v0.3) is different: it has a **lifecycle** with legal and
illegal transitions (Planned → Running → Completed; Planned/Running → Cancelled;
terminal states cannot change). Rule 5 ("a cancelled order cannot be restarted")
is a state-machine rule. Modelling this with public setters would let any caller
put the order into an invalid state (`order.Status = Running` on a cancelled
order), and the transition rules would be scattered across services.

This decision also has to fit [ADR-0006](0006-result-for-expected-outcomes.md):
expected outcomes are `Result` failures, not exceptions.

## Decision

Aggregates that have a **lifecycle** use a **rich domain model**:

- **Private setters** — state is never set directly from outside.
- **A static factory** (`Create(...)`) is the only construction path; a new
  instance is always valid (correct initial state, timestamps set).
- **Behaviour methods** (`Start()`, `Complete(endDate)`, `Cancel()`,
  `UpdateDetails(...)`) are the only way to change state. Each enforces its
  transition via a `Can*` guard and throws a **domain exception**
  (`InvalidProductionOrderTransitionException`) if violated.
- A **private parameterless constructor** exists solely for EF Core materialisation.

**Reconciliation with Result (ADR-0006):** the **service** checks the `Can*`
guard first and returns a clean `Result` failure (`InvalidTransition`, Conflict →
409) for the expected user-facing case. The entity's exception is therefore an
**invariant backstop** — it only fires if a service check was bypassed (a bug),
in which case the `GlobalExceptionHandler` turns it into a 500. In normal flow it
never throws.

Simple, lifecycle-free entities (e.g. `Machine`) remain anemic — this pattern is
applied only where a real lifecycle justifies it.

## Alternatives considered

- **Anemic model + service-enforced transitions** (like `Machine`) — less code,
  but the entity can be put into invalid states, and transition rules are not
  co-located with the thing they govern.
- **Rich model where methods return a domain `Result`** — would require a
  `Result` type in the Domain layer; rejected to keep `Result` an Application
  concern and the Domain dependency-free.
- **Rich model, chosen** — invariants enforced by the entity; expected outcomes
  surfaced as `Result` by the service. Best of both.

## Consequences

### Positive

- A `ProductionOrder` cannot exist in an invalid state — the compiler and the
  entity enforce it.
- Transition rules live in exactly one place (the entity), defined once via
  `Can*` and reused by both the guard and the service.
- The entity is unit-testable with zero infrastructure
  (`ProductionOrder.Create(...).Start()`).
- Honours ADR-0006: clean `Result` failures for users, exceptions only as a bug
  backstop.

### Negative / trade-offs

- More ceremony than an anemic entity (factory, private constructor, `= null!`
  for EF + nullable reference types).
- A slight inconsistency with `Machine` (anemic). Acceptable: the pattern is
  applied by need, not uniformly. Both converge when audit fields move into
  `SaveChanges`.

### Follow-ups

- Apply the same pattern to future lifecycle aggregates.
- Consider extracting a small shared base (`Entity`) if several aggregates share
  identity/audit plumbing.

## Amendment (2026-07-08)

Audit timestamps are no longer set by the entity. They are centralized in the
`DbContext`: entities implement the `IAuditableEntity` marker interface, and an
override of `SaveChangesAsync` sets `CreatedAt`/`UpdatedAt` automatically
(`CreatedAt` + `UpdatedAt` on insert, `UpdatedAt` on update). This supersedes the
original decision that the entity manages its own timestamps, and unifies the
approach across the anemic `Machine` and the rich `ProductionOrder` (EF writes
the private setters via its metadata, so encapsulation is preserved). The entity
factory and behaviour methods no longer touch `CreatedAt`/`UpdatedAt`.
