# ADR-0012: Keep authentication in Infrastructure and surface it through `Result`

- **Status:** Accepted
- **Date:** 2026-07-09
- **Deciders:** Project owner

## Context

ASP.NET Core Identity types (`ApplicationUser : IdentityUser`,
`UserManager<>`, `RoleManager<>`) depend on `Microsoft.AspNetCore.Identity`.
Introducing authentication must not compromise two existing principles:

- the **Domain stays framework-free** (Clean Architecture), and
- expected outcomes flow through **`Result`**, not exceptions
  ([ADR-0006](0006-result-for-expected-outcomes.md)).

A decision is needed on *where* auth lives and *how* its outcomes are exposed.

## Decision

- **Identity lives in Infrastructure.** `ApplicationUser` and the Identity
  configuration are Infrastructure concerns. The Domain has **no** dependency on
  Identity and defines no user entity.
- **Auth is surfaced through `IAuthService`** (defined in Application, implemented
  in Infrastructure). Controllers depend on the interface, not on `UserManager`.
  `AuthService` returns `Result<AuthResponse>` — consistent with every other
  service.
- **Token generation is behind `IJwtTokenGenerator`** (Application interface,
  Infrastructure implementation) — the same interface/implementation split as the
  repositories, so token creation is unit-testable without a web host.
- A new **`Unauthorized` `ErrorType`** is added, mapping to **HTTP 401** in
  `ApiController.HandleFailure`. Invalid credentials return a deliberately vague
  `Auth.InvalidCredentials` (never reveal whether the email or the password was
  wrong).

## Alternatives considered

- **Auth logic in Application, referencing Identity** — would pull
  `Microsoft.AspNetCore.Identity` into the Application layer and couple business
  logic to the Identity framework. Rejected; the interface/implementation split
  keeps Application clean.
- **A user entity in the Domain** — rejected; the authenticated user is an
  infrastructure/identity concern, not a business aggregate. The Domain stays
  pure.
- **Throwing for invalid credentials** — inconsistent with ADR-0006; invalid
  login is an expected outcome, so it is a `Result` failure (401), not an
  exception.

## Consequences

### Positive

- Domain remains dependency-free; Application depends only on auth *abstractions*.
- Auth is consistent with the rest of the app (`Result`, `Match` → HTTP) and
  unit-testable (mocked `UserManager`/`RoleManager`, decoded tokens).
- Clear 401 (unauthenticated) vs 403 (authenticated, wrong role) semantics.

### Negative / trade-offs

- One more interface/implementation pair (`IAuthService`/`IJwtTokenGenerator`) —
  the standard cost of the abstraction, and it pays off in testability.
- `ToValidationErrors`-style mapping of Identity's own errors into `Error`s adds a
  small translation step (weak-password errors surface as 400).

### Follow-ups

- Extend with refresh-token operations on `IAuthService` when added.
