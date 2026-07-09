# ADR-0011: Authentication via ASP.NET Core Identity and JWT bearer tokens

- **Status:** Accepted
- **Date:** 2026-07-09
- **Deciders:** Project owner

## Context

The API needs authentication and authorization. Requirements: secure password
handling, stateless auth suitable for an API, role-based access control, and a
recognizable, standard approach for a portfolio. The API is a single monolith.

## Decision

- **ASP.NET Core Identity** for the user store and password hashing, backed by EF
  Core in the existing `FactoryPulseDb` (`FactoryPulseDbContext` inherits
  `IdentityDbContext<ApplicationUser>`).
- **JWT bearer tokens** for stateless authentication, signed with a **symmetric
  HMAC-SHA256** key held in user-secrets. Tokens are validated by
  `AddJwtBearer` (issuer, audience, lifetime, signing key).
- **Role-based authorization** with three roles — `Admin`, `Manager`, `Viewer` —
  applied via **named policies** (`CanWrite`, `AdminOnly`) rather than inline role
  strings, and **secure-by-default**: `[Authorize]` on the base controller, with
  explicit `[AllowAnonymous]` only on login.
- **Admin-only registration**; roles and a seeded admin user are created on
  startup so the API is immediately usable.
- **Refresh tokens** are deferred (access-token-only for now).

## Alternatives considered

- **Custom auth / hand-rolled password hashing** — rejected outright; never roll
  your own crypto. Identity is the standard, audited choice.
- **Cookie authentication** — simpler for server-rendered apps, but stateful and
  awkward for an API/SPA/mobile client. JWT is the right fit; `AddIdentityCore`
  is used (not `AddIdentity`) precisely to avoid the cookie stack.
- **Asymmetric (RSA) token signing** — appropriate when multiple independent
  services must validate tokens without sharing a secret. Overkill for a single
  monolith; symmetric HMAC is simpler and sufficient.
- **Inline `[Authorize(Roles = "...")]` everywhere** — works, but scatters role
  strings; **policies** centralize the rules in one place.

## Consequences

### Positive

- Secure, standard, widely-recognized authentication.
- Stateless: no server-side session; scales horizontally.
- Secure-by-default authorization; role rules centralized as policies.
- Immediately usable via the seeded admin.

### Negative / trade-offs

- Symmetric signing means the key must be shared/protected; fine for a monolith,
  revisit if the system is ever split into independently-validating services.
- Access-token-only means no revocation before expiry and users re-login when the
  token expires — acceptable now; refresh tokens are the planned follow-up.
- The signing key must be managed per environment (dev user-secrets today; Azure
  Key Vault / App Service config in production — never reuse the dev key).

### Follow-ups

- Refresh tokens with rotation (deferred issue).
- Production key management via Azure Key Vault at the deployment milestone.
