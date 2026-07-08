# ADR-0010: Testing strategy and library choices

- **Status:** Accepted
- **Date:** 2026-07-08
- **Deciders:** Project owner

## Context

The project needs an automated test suite that both protects the code and signals
engineering quality to reviewers. Two things must be decided: **what to test at
which level**, and **which libraries** to use. Library choice matters here because
two popular options in the .NET testing space carry licensing or reputation risks.

## Decision

### Levels

- **Unit tests (primary)** target pure logic with no infrastructure:
  - **Domain** — `ProductionOrder` lifecycle: `Create`, `Start`, `Complete`,
    `Cancel`, and the illegal-transition guards.
  - **Result pattern** — success/failure, multi-error, `Value` access, implicit
    conversions, `Match`.
  - **Services** — business rules (not found, conflict, validation, happy path)
    with repositories **mocked**, verifying both outcomes and persistence calls
    (`Received` / `DidNotReceive`).
- **Integration tests (planned, few)** — a handful using **Testcontainers**
  (real SQL Server in a container) + `WebApplicationFactory`, added alongside
  CI/CD so the pipeline runs `unit → integration`.

### Libraries

| Purpose | Choice |
|---------|--------|
| Test framework | **xUnit** |
| Mocking | **NSubstitute** |
| Assertions | **Shouldly** |
| Integration (planned) | **Testcontainers**, `Microsoft.AspNetCore.Mvc.Testing` |

Test naming: `Method_Scenario_ExpectedBehaviour`. Structure: Arrange-Act-Assert.

## Alternatives considered

- **Moq** (mocking) — the most common choice, but the 2023 *SponsorLink* incident
  (it scanned developers' email addresses at build time) damaged trust. Avoided in
  favour of **NSubstitute** (MIT, clean syntax, no such history).
- **FluentAssertions** (assertions) — excellent, but **v8+ moved to a commercial
  license** (paid for commercial use). Avoided to keep the project free of paid
  dependencies; **Shouldly** (free, readable, good failure messages) chosen
  instead. (`AwesomeAssertions`, a free fork of FluentAssertions v7, is an
  equivalent fallback.)
- **In-memory EF provider** for integration — rejected: it does not behave like
  real SQL Server (no relational constraints, different translation), which
  undermines the point of an integration test. Testcontainers uses the real
  engine.

## Consequences

### Positive

- Fast, dependency-free unit tests that cover the domain rules and service logic
  (the highest-value behaviour).
- No licensing footnotes and no reputation baggage in the test toolchain — itself
  a considered decision worth discussing.
- The mocked-repository service tests validate that the interface/DI architecture
  pays off: business logic is testable without a database.

### Negative / trade-offs

- Two assertion/mocking ecosystems to learn if contributors are used to
  Moq/FluentAssertions.
- Integration tests (Testcontainers) require Docker on the test machine / CI
  runner — acceptable, since the project already uses Docker.

### Follow-ups

- Add the handful of integration tests with the CI/CD milestone.
- Extend service tests to remaining operations (update, cancel) as coverage grows.
