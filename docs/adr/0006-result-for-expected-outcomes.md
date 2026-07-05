# ADR-0006: Use `Result<T>` for expected outcomes, exceptions for unexpected failures

- **Status:** Accepted
- **Date:** 2026-07-04
- **Deciders:** Project owner

## Context

Service methods encounter two very different kinds of "things that didn't
succeed":

1. **Expected business outcomes** — a machine isn't found, input fails
   validation, a uniqueness constraint is violated. These are normal control
   flow the caller is expected to handle.
2. **Unexpected failures** — the database is unreachable, a bug throws. These are
   genuinely exceptional and should not be part of normal flow.

Modelling the first kind with exceptions is a known smell: it's slower, hides
control flow, and makes the "expected" cases look exceptional. The project wants
a clear, honest contract for outcomes and clean, low-noise logging.

## Decision

- **Expected outcomes** are modelled with a `Result` / `Result<T>` type carrying
  one or more `Error` values. Services return `Result<T>` (e.g.
  `Task<Result<MachineDto>>`) and do **not** throw for these cases.
- **Unexpected failures** remain **exceptions**, allowed to bubble up to
  exception-handling middleware, which logs them and returns HTTP 500.

### Supporting design

- `Error` carries a **category** (`ErrorType`: `Failure`, `NotFound`,
  `Validation`, `Conflict`, …) so a single shared helper can map an error to the
  correct HTTP status (`NotFound → 404`, `Validation → 400`, `Conflict → 409`,
  `Failure → 500`). Controllers call this mapping, not hand-picked status codes.
- `Result` can hold **multiple** `Error`s, so validation (many messages at once)
  fits the same type without a later refactor.
- A central, nested `Errors` catalog (e.g. `Errors.Machine.NotFound`) provides
  reusable, typo-proof error instances.
- `Result`/`Error`/`Errors` live in `Application/Common`, keeping the Domain
  layer free of application-level result concepts.

## Alternatives considered

- **Exceptions for everything** — simplest, but conflates expected and
  exceptional cases, pollutes logs with "not found" noise, and hurts performance
  on hot paths.
- **A third-party result library** (ErrorOr, Ardalis.Result, FluentResults) —
  equivalent functionality off the shelf. We hand-roll a minimal version to
  demonstrate the pattern and avoid a dependency; ErrorOr/Ardalis.Result are the
  drop-in equivalents if maintenance ever becomes a burden.

## Consequences

### Positive

- Method signatures honestly advertise their expected outcomes.
- Logs stay signal-rich: only genuine exceptions are logged (pairs with the
  logging strategy — controllers silent, services log business events,
  middleware logs exceptions).
- Uniform mapping from error category to HTTP status.

### Negative / trade-offs

- More ceremony than throwing: every service method returns `Result<T>` and
  callers must handle both branches.
- A hand-rolled `Result` is code we own and maintain (kept intentionally small).

### Follow-ups

- Implement `Result` / `Result<T>`, `Error` (+ `ErrorType`), and the `Errors`
  catalog as the first Application-foundation issue.
- Add exception-handling middleware in the Quality phase.
