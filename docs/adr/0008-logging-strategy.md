# ADR-0008: Structured logging with a layer-based logging strategy

- **Status:** Accepted
- **Date:** 2026-07-06
- **Deciders:** Project owner

## Context

The application needs logging that is useful rather than noisy. Two decisions
are entangled: *what* logging library to use, and *where* (which layers) should
log. Undisciplined logging — every layer logging everything — produces volume
without signal and buries real problems.

## Decision

**Library:** Use **Serilog** (`Serilog.AspNetCore`) as the logging provider, with
**Console** and rolling **File** sinks, configured from `appsettings.json`.
Framework categories (`Microsoft.AspNetCore`, `Microsoft.EntityFrameworkCore`)
are overridden to `Warning` to cut noise; application logs default to
`Information`. HTTP requests are summarised via `UseSerilogRequestLogging`.

**Strategy — who logs what:**

| Layer | Logging responsibility |
|-------|------------------------|
| **Controllers** | Nothing. They only translate `Result` → HTTP. |
| **Services** | Important **business events** (machine created / updated / deleted), using structured properties (e.g. `{MachineId}`). |
| **Middleware** | **Exceptions** — the `GlobalExceptionHandler` logs unhandled exceptions as errors. |

Expected outcomes (validation failures, not-found) flow through `Result` and are
**not** logged — they are normal control flow, not problems.

## Alternatives considered

- **Default `Microsoft.Extensions.Logging` only** — works, but weaker structured
  logging and sink ecosystem than Serilog; less compelling for the portfolio.
- **Log in every layer (incl. controllers)** — rejected: duplicates entries and
  drowns signal. The layer-based split keeps each log line meaningful.
- **Log expected `Result` failures** — rejected: a client requesting a missing
  machine is normal, not an error; logging it would create noise (see
  [ADR-0006](0006-result-for-expected-outcomes.md)).

## Consequences

### Positive

- Structured, queryable logs (properties, not just text) to console and file.
- Low-noise logs: framework chatter suppressed, expected outcomes excluded, only
  genuine failures and meaningful business events recorded.
- One clean summary line per HTTP request.
- Because Serilog plugs into `ILogger`, existing `ILogger<T>` usage (e.g. the
  exception handler) flows through it with no code change.

### Negative / trade-offs

- A small amount of discipline required to keep to the strategy as new features
  are added.
- Log files are environment output and must be git-ignored.

### Follow-ups

- Add correlation/trace ids and richer enrichment if/when needed.
- In cloud hosting, add an appropriate sink (e.g. Azure/Seq) alongside Console.
